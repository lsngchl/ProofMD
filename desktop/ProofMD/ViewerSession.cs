namespace ProofMD;

internal enum OpenReason
{
    Direct,
    Link,
    Map,
    History,
}

/// <summary>
/// The document state behind the viewer: the open document, back history, and exploration
/// map. It talks to the page only through posted messages, so it runs without a window.
/// Operations run one at a time, and state changes only after every read has succeeded.
/// </summary>
internal sealed class ViewerSession
{
    private const int ReloadReadAttempts = 4;

    private readonly Action<object> _post;
    private readonly Action<string> _showError;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Stack<DocumentHistoryEntry> _history = new();
    private readonly ExplorationMap _map = new();
    private string? _initialPath;
    private bool _viewerStarted;
    private string? _source;
    private int _contextId;

    public ViewerSession(string? initialPath, Action<object> post, Action<string> showError)
    {
        _initialPath = initialPath;
        _post = post;
        _showError = showError;
    }

    /// <summary>Raised after the open document or its ProofFold folders change.</summary>
    public event Action? DocumentChanged;

    public string? CurrentPath { get; private set; }
    public ProofFoldStructure? ProofFold { get; private set; }
    public ExplorationMap Map => _map;
    public int HistoryCount => _history.Count;

    /// <summary>
    /// Called whenever the page is ready: opens the initial document the first time, and
    /// shows the current state again if the page was ever reloaded.
    /// </summary>
    public Task ViewerReadyAsync() => RunAsync(async () =>
    {
        if (!_viewerStarted)
        {
            _viewerStarted = true;
            string? initialPath = _initialPath;
            _initialPath = null;
            if (initialPath is not null && await OpenCoreAsync(initialPath, OpenReason.Direct)) return;
        }

        if (CurrentPath is null || _source is null)
        {
            _post(new { type = "show-empty-state" });
            return;
        }

        PostOpenMarkdown(restorePosition: null);
        PublishMap();
    });

    public Task OpenAsync(string path) => RunAsync(() => OpenCoreAsync(path, OpenReason.Direct));

    public Task OpenLinkAsync(string? href, string? sourceDocument, int? order, DocumentPosition? position) =>
        RunAsync(async () =>
        {
            if (CurrentPath is null) return;

            string sourcePath = CurrentPath;
            if (sourceDocument is not null &&
                (ProofFold is null || !ProofFold.TryResolveDocument(sourceDocument, out sourcePath)))
            {
                return;
            }

            string? target = MarkdownPaths.ResolveLink(sourcePath, href);
            if (target is not null)
            {
                await OpenCoreAsync(target, OpenReason.Link, order is >= 0 ? order : null, position);
            }
        });

    public Task OpenMapNodeAsync(string? id, DocumentPosition? position) => RunAsync(async () =>
    {
        if (id is not null && _map.Contains(id) && !MarkdownPaths.AreEqual(id, CurrentPath))
        {
            await OpenCoreAsync(id, OpenReason.Map, historyPosition: position);
        }
    });

    public Task GoBackAsync() => RunAsync(async () =>
    {
        while (_history.TryPop(out DocumentHistoryEntry previous))
        {
            if (File.Exists(previous.Path) &&
                await OpenCoreAsync(previous.Path, OpenReason.History, restorePosition: previous.Position))
            {
                return;
            }
        }
    });

    public Task ResetMapAsync() => RunAsync(() =>
    {
        if (CurrentPath is null) return Task.CompletedTask;

        _history.Clear();
        _map.Start(CurrentPath);
        PublishMap();
        return Task.CompletedTask;
    });

    public Task UpdateLinkOrdersAsync(int? contextId, IReadOnlyList<LinkOrder>? links) => RunAsync(() =>
    {
        if (CurrentPath is null || contextId != _contextId || links is null || links.Count > 10_000)
        {
            return Task.CompletedTask;
        }

        var orderByTarget = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (LinkOrder link in links)
        {
            if (link.Order >= 0 && MarkdownPaths.ResolveLink(CurrentPath, link.Href) is string target)
            {
                orderByTarget.TryAdd(target, link.Order);
            }
        }

        if (_map.UpdateLinkOrders(CurrentPath, orderByTarget)) PublishMap();
        return Task.CompletedTask;
    });

    /// <summary>Always answers, so the page never waits on a request that was ignored.</summary>
    public Task SetUnresolvedAsync(int? contextId, bool? unresolved) => RunAsync(() =>
    {
        string? error = null;
        if (CurrentPath is not null && contextId == _contextId && unresolved is bool value)
        {
            try
            {
                UnresolvedStateStore.SetUnresolved(CurrentPath, value);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                error = $"The unresolved state could not be changed: {exception.Message}";
            }
        }

        PostUnresolvedState(error);
        PublishMap();
        return Task.CompletedTask;
    });

    public Task RefreshUnresolvedAsync() => RunAsync(() =>
    {
        PostUnresolvedState(error: null);
        PublishMap();
        return Task.CompletedTask;
    });

    /// <summary>Re-reads the open document after it or its folds changed on disk.</summary>
    public Task ReloadAsync() => RunAsync(async () =>
    {
        string? path = CurrentPath;
        if (path is null) return;

        for (int attempt = 1; ; attempt++)
        {
            try
            {
                string source = await ReadTextAsync(path);
                ProofFoldStructure? proofFold = await Task.Run(() => ProofFoldStructure.TryLoadForEntry(path));
                bool unchanged = source == _source &&
                    (proofFold is null ? ProofFold is null : proofFold.HasSameContent(ProofFold));
                if (unchanged) return;

                _source = source;
                ProofFold = proofFold;
                _post(new
                {
                    type = "reload-markdown",
                    source,
                    name = Path.GetFileName(path),
                    contextId = _contextId,
                    proofFold = proofFold?.ToMessage(),
                });
                DocumentChanged?.Invoke();
                return;
            }
            catch (Exception exception) when (
                exception is FileNotFoundException or DirectoryNotFoundException)
            {
                // An editor may delete and recreate the file; keep showing the last version.
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                if (attempt == ReloadReadAttempts) return;
                await Task.Delay(100 * attempt);
            }
        }
    });

    private async Task RunAsync(Func<Task> operation)
    {
        await _gate.WaitAsync();
        try
        {
            await operation();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<bool> OpenCoreAsync(
        string path,
        OpenReason reason,
        int? linkOrder = null,
        DocumentPosition? historyPosition = null,
        DocumentPosition? restorePosition = null)
    {
        string fullPath;
        string source;
        ProofFoldStructure? proofFold;
        try
        {
            fullPath = MarkdownPaths.Canonicalize(Path.GetFullPath(path));
            source = await ReadTextAsync(fullPath);
            proofFold = await Task.Run(() => ProofFoldStructure.TryLoadForEntry(fullPath));
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            _showError($"The Markdown file was not found:\n{path}");
            return false;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            _showError($"The Markdown file could not be opened.\n\n{exception.Message}");
            return false;
        }

        string? previous = CurrentPath;
        CurrentPath = fullPath;
        ProofFold = proofFold;
        _source = source;
        _contextId++;

        switch (reason)
        {
            case OpenReason.Direct:
                _history.Clear();
                _map.Start(fullPath);
                break;
            case OpenReason.Link:
                PushHistory(previous, fullPath, historyPosition);
                if (previous is not null) _map.Discover(previous, fullPath, linkOrder);
                _map.SetPrevious(previous, fullPath);
                break;
            case OpenReason.Map:
                PushHistory(previous, fullPath, historyPosition);
                _map.SetPrevious(previous, fullPath);
                break;
            case OpenReason.History:
                _map.SetPrevious(previous, fullPath);
                break;
        }

        PostOpenMarkdown(restorePosition);
        PublishMap();
        DocumentChanged?.Invoke();
        return true;
    }

    private void PushHistory(string? previous, string current, DocumentPosition? position)
    {
        if (previous is not null && !MarkdownPaths.AreEqual(previous, current))
        {
            _history.Push(new DocumentHistoryEntry(previous, position));
        }
    }

    private void PostOpenMarkdown(DocumentPosition? restorePosition)
    {
        _post(new
        {
            type = "open-markdown",
            source = _source,
            name = Path.GetFileName(CurrentPath),
            contextId = _contextId,
            unresolved = CurrentPath is not null && UnresolvedStateStore.IsUnresolved(CurrentPath),
            restorePosition,
            proofFold = ProofFold?.ToMessage(),
        });
    }

    private void PostUnresolvedState(string? error)
    {
        _post(new
        {
            type = "document-unresolved-state",
            contextId = _contextId,
            enabled = CurrentPath is not null,
            unresolved = CurrentPath is not null && UnresolvedStateStore.IsUnresolved(CurrentPath),
            error,
        });
    }

    private void PublishMap()
    {
        _post(_map.ToMessage(CurrentPath, UnresolvedStateStore.IsUnresolved));
    }

    private static async Task<string> ReadTextAsync(string path)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync();
    }
}
