using System.Diagnostics;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ProofMD;

internal sealed class MainForm : Form
{
    // Keep the original origin so migrated WebView2 local storage remains readable.
    private const string ViewerHostName = "leanmd.local";
    private const int MarkdownReloadDebounceMilliseconds = 250;
    private const int WslFileStatePollIntervalMilliseconds = 750;
    private const int MarkdownReadRetryCount = 4;
    private const int MarkdownReadRetryDelayMilliseconds = 100;
    private const int UnresolvedStateReloadDebounceMilliseconds = 150;
    private string? _markdownPath;
    private string? _lastMarkdownDirectory;
    private readonly WebView2 _webView;
    private readonly System.Windows.Forms.Timer _markdownReloadTimer;
    private readonly System.Windows.Forms.Timer _wslFileStatePollTimer;
    private readonly System.Windows.Forms.Timer _unresolvedStateReloadTimer;
    private readonly List<string> _mapNodes = [];
    private readonly List<ExplorationMapEdge> _mapEdges = [];
    private readonly Stack<DocumentHistoryEntry> _documentHistory = new();
    private string? _mapRootPath;
    private MapFormat _mapFormat = MapFormat.Markdown;
    private string? _previousMapPath;
    private int _mapSessionId;
    private Task<string>? _initialMarkdownReadTask;
    private FileSystemWatcher? _markdownWatcher;
    private FileSystemWatcher? _proofFoldWatcher;
    private ProofFoldStructure? _proofFoldStructure;
    private string? _lastRenderedSource;
    private string? _unresolvedStateFingerprint;
    private int _documentContextId;
    private bool _formIsClosing;
    private bool _initialContentSent;
    private bool _windowRevealed;
    private bool _wslFileStatePollInProgress;

    private enum OpenReason
    {
        Direct,
        Link,
        Map,
        History,
    }

    private enum MapFormat
    {
        Markdown,
        ProofFold,
    }

    public MainForm(string? markdownPath)
    {
        _markdownPath = markdownPath;
        _lastMarkdownDirectory = markdownPath is null
            ? null
            : Path.GetDirectoryName(markdownPath);
        Text = "ProofMD";
        MinimumSize = new Size(720, 540);
        BackColor = Color.FromArgb(243, 241, 236);
        ApplyAppIcon();
        Opacity = 0;
        ApplyInitialWindowState();
        _webView = new WebView2
        {
            Dock = DockStyle.Fill,
            AllowExternalDrop = true,
            DefaultBackgroundColor = BackColor,
        };
        _markdownReloadTimer = new System.Windows.Forms.Timer
        {
            Interval = MarkdownReloadDebounceMilliseconds,
        };
        _markdownReloadTimer.Tick += OnMarkdownReloadTimerTick;
        _wslFileStatePollTimer = new System.Windows.Forms.Timer
        {
            Interval = WslFileStatePollIntervalMilliseconds,
        };
        _wslFileStatePollTimer.Tick += OnWslFileStatePollTimerTick;
        _unresolvedStateReloadTimer = new System.Windows.Forms.Timer
        {
            Interval = UnresolvedStateReloadDebounceMilliseconds,
        };
        _unresolvedStateReloadTimer.Tick += OnUnresolvedStateReloadTimerTick;

        Controls.Add(_webView);
        Shown += OnShown;
        FormClosing += OnFormClosing;
    }

    private async void OnShown(object? sender, EventArgs eventArgs)
    {
        Shown -= OnShown;
        _initialMarkdownReadTask = StartInitialMarkdownRead();

        try
        {
            await InitializeViewerAsync();
        }
        catch (WebView2RuntimeNotFoundException)
        {
            ShowStartupError(
                "Microsoft Edge WebView2 Runtime is required. Install it from Microsoft, then reopen ProofMD.");
        }
        catch (Exception exception)
        {
            ShowStartupError($"ProofMD could not start.\n\n{exception.Message}");
        }
    }

    private async Task InitializeViewerAsync()
    {
        string userDataFolder = Path.Combine(
            UserProfile.DirectoryPath,
            "WebView2");

        CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,
            userDataFolder: userDataFolder);

        await _webView.EnsureCoreWebView2Async(environment);
        ConfigureWebView(_webView.CoreWebView2);

        _webView.NavigationCompleted += (_, eventArgs) =>
        {
            if (!eventArgs.IsSuccess)
            {
                ShowStartupError("The ProofMD viewer could not be loaded.");
            }
        };
        _webView.CoreWebView2.DocumentTitleChanged += (_, _) =>
        {
            string title = _webView.CoreWebView2.DocumentTitle;
            if (!string.IsNullOrWhiteSpace(title)) Text = title;
        };

        string viewerDirectory = Path.Combine(AppContext.BaseDirectory, "Viewer");
        string viewerEntry = Path.Combine(viewerDirectory, "index.html");
        if (!File.Exists(viewerEntry))
        {
            throw new InvalidOperationException("The installed ProofMD viewer files were not found.");
        }

        _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            ViewerHostName,
            viewerDirectory,
            CoreWebView2HostResourceAccessKind.DenyCors);
        _webView.CoreWebView2.Navigate($"https://{ViewerHostName}/index.html");
    }

    private void ConfigureWebView(CoreWebView2 core)
    {
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreDefaultScriptDialogsEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = true;
        core.Settings.IsWebMessageEnabled = true;
        core.WebMessageReceived += OnWebMessageReceived;

        core.NewWindowRequested += (_, eventArgs) =>
        {
            eventArgs.Handled = true;
            OpenExternalUri(eventArgs.Uri);
        };

        core.NavigationStarting += (_, eventArgs) =>
        {
            if (eventArgs.Uri.Equals("about:blank", StringComparison.OrdinalIgnoreCase)) return;

            if (Uri.TryCreate(eventArgs.Uri, UriKind.Absolute, out Uri? uri) &&
                uri.Scheme == Uri.UriSchemeHttps &&
                uri.Host.Equals(ViewerHostName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (uri is not null &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                eventArgs.Cancel = true;
                OpenExternalUri(eventArgs.Uri);
            }
        };

        core.PermissionRequested += (_, eventArgs) =>
        {
            eventArgs.State = CoreWebView2PermissionState.Deny;
        };
    }

    private async void OnWebMessageReceived(
        object? sender,
        CoreWebView2WebMessageReceivedEventArgs eventArgs)
    {
        try
        {
            using JsonDocument message = JsonDocument.Parse(eventArgs.WebMessageAsJson);
            if (!message.RootElement.TryGetProperty("type", out JsonElement typeElement)) return;

            switch (typeElement.GetString())
            {
                case "viewer-shell-painted":
                    RevealWindow();
                    PostViewerMessage(new { type = "host-window-visible" });
                    break;
                case "viewer-window-painted":
                    await SendInitialContentAsync();
                    break;
                case "open-file-dialog":
                    await ShowOpenMarkdownDialogAsync();
                    break;
                case "open-markdown-link":
                    if (message.RootElement.TryGetProperty("href", out JsonElement hrefElement) &&
                        hrefElement.ValueKind == JsonValueKind.String)
                    {
                        DocumentPosition? position = DocumentPosition.TryRead(
                            message.RootElement,
                            "position",
                            out DocumentPosition parsedPosition)
                                ? parsedPosition
                                : null;
                        int? linkOrder =
                            message.RootElement.TryGetProperty("order", out JsonElement orderElement) &&
                            orderElement.ValueKind == JsonValueKind.Number &&
                            orderElement.TryGetInt32(out int parsedOrder) &&
                            parsedOrder >= 0
                                ? parsedOrder
                                : null;
                        string? sourceDocument =
                            message.RootElement.TryGetProperty(
                                "sourceDocument",
                                out JsonElement sourceDocumentElement) &&
                            sourceDocumentElement.ValueKind == JsonValueKind.String
                                ? sourceDocumentElement.GetString()
                                : null;
                        await OpenLinkedMarkdownAsync(
                            hrefElement.GetString(),
                            sourceDocument,
                            position,
                            linkOrder);
                    }
                    break;
                case "document-links":
                    UpdateUnstructuredLinkOrders(message.RootElement);
                    break;
                case "open-dropped-file":
                    await OpenDroppedMarkdownAsync(eventArgs.AdditionalObjects);
                    break;
                case "open-map-node":
                    if (message.RootElement.TryGetProperty("id", out JsonElement idElement) &&
                        idElement.ValueKind == JsonValueKind.String)
                    {
                        DocumentPosition? position = DocumentPosition.TryRead(
                            message.RootElement,
                            "position",
                            out DocumentPosition parsedPosition)
                                ? parsedPosition
                                : null;
                        await OpenMapNodeAsync(idElement.GetString(), position);
                    }
                    break;
                case "go-back":
                    await GoBackAsync();
                    break;
                case "reset-map":
                    if (_markdownPath is not null)
                    {
                        ResetMap(_markdownPath);
                    }
                    break;
                case "set-document-unresolved":
                    SetCurrentDocumentUnresolved(message.RootElement);
                    break;
            }
        }
        catch
        {
            // Ignore malformed messages from the embedded page.
        }
    }

    private async Task SendInitialContentAsync()
    {
        if (_initialContentSent) return;
        _initialContentSent = true;

        if (_markdownPath is null)
        {
            PostViewerMessage(new { type = "show-empty-state" });
            return;
        }

        await OpenMarkdownPathAsync(
            _markdownPath,
            _initialMarkdownReadTask,
            showEmptyStateOnFailure: true);
    }

    private async Task ShowOpenMarkdownDialogAsync()
    {
        using var dialog = new OpenFileDialog
        {
            AddExtension = true,
            CheckFileExists = true,
            CheckPathExists = true,
            DefaultExt = "md",
            Filter = "Markdown files (*.md;*.markdown)|*.md;*.markdown|All files (*.*)|*.*",
            Multiselect = false,
            RestoreDirectory = true,
            Title = "Open Markdown",
        };

        string? currentDirectory = _markdownPath is null
            ? _lastMarkdownDirectory
            : Path.GetDirectoryName(_markdownPath);
        if (currentDirectory is not null && Directory.Exists(currentDirectory))
        {
            dialog.InitialDirectory = currentDirectory;
        }

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        await OpenMarkdownPathAsync(Path.GetFullPath(dialog.FileName));
    }

    private async Task OpenMarkdownPathAsync(
        string markdownPath,
        Task<string>? sourceTask = null,
        bool showEmptyStateOnFailure = false,
        OpenReason reason = OpenReason.Direct,
        string? linkSourcePath = null,
        int? linkOrder = null,
        DocumentPosition? historyPosition = null,
        DocumentPosition? restorePosition = null)
    {
        string? previousPath = _markdownPath;

        if (sourceTask is null && !File.Exists(markdownPath))
        {
            MessageBox.Show(
                this,
                $"The Markdown file was not found:\n{markdownPath}",
                "ProofMD",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            if (showEmptyStateOnFailure)
            {
                PostViewerMessage(new { type = "show-empty-state" });
            }
            return;
        }

        try
        {
            string source = sourceTask is not null
                ? await sourceTask
                : await ReadMarkdownSourceAsync(markdownPath);
            markdownPath = Path.GetFullPath(markdownPath);
            ProofFoldStructure? proofFoldStructure =
                ProofFoldStructure.LoadForEntry(markdownPath);
            _markdownPath = markdownPath;
            _lastMarkdownDirectory = Path.GetDirectoryName(markdownPath);
            _initialMarkdownReadTask = null;
            _lastRenderedSource = source;
            ConfigureMarkdownWatcher(markdownPath);
            ConfigureProofFoldStructure(proofFoldStructure);
            _documentContextId++;
            UpdateHistoryAfterOpen(
                reason,
                previousPath,
                markdownPath,
                historyPosition);
            object? serializedRestorePosition = restorePosition is DocumentPosition position
                ? new
                {
                    sourceLine = position.SourceLine,
                    offset = position.Offset,
                    scrollY = position.ScrollY,
                }
                : null;
            PostViewerMessage(new
            {
                type = "open-markdown",
                source,
                name = Path.GetFileName(markdownPath),
                contextId = _documentContextId,
                unresolved = UnresolvedStateStore.IsUnresolved(markdownPath),
                restorePosition = serializedRestorePosition,
                proofFold = ProofFoldPayload(proofFoldStructure),
            });
            UpdateMapAfterOpen(markdownPath, reason, previousPath, linkSourcePath, linkOrder);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                $"The Markdown file could not be opened.\n\n{exception.Message}",
                "ProofMD",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            if (showEmptyStateOnFailure)
            {
                PostViewerMessage(new { type = "show-empty-state" });
            }
        }
    }

    private async Task OpenLinkedMarkdownAsync(
        string? href,
        string? sourceDocument,
        DocumentPosition? historyPosition,
        int? linkOrder)
    {
        if (_markdownPath is null || string.IsNullOrWhiteSpace(href)) return;

        string sourcePath = _markdownPath;
        if (sourceDocument is not null)
        {
            if (_proofFoldStructure is null ||
                !_proofFoldStructure.TryResolveSourceDocument(
                    sourceDocument,
                    out sourcePath))
            {
                return;
            }
        }

        try
        {
            string? linkedPath = ResolveLinkedMarkdownPath(sourcePath, href);
            if (linkedPath is null) return;
            await OpenMarkdownPathAsync(
                linkedPath,
                reason: OpenReason.Link,
                linkSourcePath: _markdownPath,
                linkOrder: linkOrder,
                historyPosition: historyPosition);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException or UriFormatException)
        {
            // Ignore malformed local links and keep the current document open.
        }
    }

    private async Task OpenMapNodeAsync(
        string? nodeId,
        DocumentPosition? historyPosition)
    {
        if (nodeId is null || !_mapNodes.Contains(nodeId, StringComparer.OrdinalIgnoreCase)) return;

        await OpenMarkdownPathAsync(
            nodeId,
            reason: OpenReason.Map,
            historyPosition: historyPosition);
    }

    private async Task GoBackAsync()
    {
        while (_documentHistory.Count > 0)
        {
            DocumentHistoryEntry previous = _documentHistory.Pop();
            if (!File.Exists(previous.Path)) continue;

            await OpenMarkdownPathAsync(
                previous.Path,
                reason: OpenReason.History,
                restorePosition: previous.Position);
            return;
        }
    }

    private void UpdateHistoryAfterOpen(
        OpenReason reason,
        string? previousPath,
        string markdownPath,
        DocumentPosition? historyPosition)
    {
        if (reason == OpenReason.Direct)
        {
            _documentHistory.Clear();
            return;
        }

        if ((reason == OpenReason.Link || reason == OpenReason.Map) &&
            previousPath is not null &&
            !previousPath.Equals(markdownPath, StringComparison.OrdinalIgnoreCase))
        {
            _documentHistory.Push(new DocumentHistoryEntry(
                previousPath,
                historyPosition));
        }
    }

    private void UpdateMapAfterOpen(
        string markdownPath,
        OpenReason reason,
        string? previousPath,
        string? linkSourcePath,
        int? linkOrder)
    {
        if (_proofFoldStructure is not null &&
            PathsEqual(markdownPath, _proofFoldStructure.EntryPath))
        {
            UpdateProofFoldMap(_proofFoldStructure);
            return;
        }

        switch (reason)
        {
            case OpenReason.Direct:
                StartUnstructuredMap(markdownPath);
                return;
            case OpenReason.Link when linkSourcePath is not null:
                DiscoverUnstructuredMapLink(linkSourcePath, markdownPath, linkOrder);
                return;
            case OpenReason.Map:
            case OpenReason.History:
                SetPreviousMapNode(previousPath, markdownPath);
                PublishMapState();
                return;
        }
    }

    private void UpdateProofFoldMap(ProofFoldStructure structure)
    {
        bool sameMap = _mapFormat == MapFormat.ProofFold &&
            _mapRootPath is not null &&
            PathsEqual(_mapRootPath, structure.EntryPath) &&
            _mapNodes.SequenceEqual(
                structure.Documents,
                StringComparer.OrdinalIgnoreCase) &&
            _mapEdges.SequenceEqual(structure.Edges);
        if (!sameMap)
        {
            _mapSessionId++;
        }

        _mapFormat = MapFormat.ProofFold;
        _mapRootPath = structure.EntryPath;
        _previousMapPath = null;
        _mapNodes.Clear();
        _mapNodes.AddRange(structure.Documents);
        _mapEdges.Clear();
        _mapEdges.AddRange(structure.Edges);
        PublishMapState();
    }

    private void ResetMap(string currentPath)
    {
        if (_proofFoldStructure is not null &&
            PathsEqual(currentPath, _proofFoldStructure.EntryPath))
        {
            UpdateProofFoldMap(_proofFoldStructure);
            return;
        }

        StartUnstructuredMap(currentPath);
    }

    private void StartUnstructuredMap(string rootPath)
    {
        _mapSessionId++;
        _mapFormat = MapFormat.Markdown;
        _mapRootPath = rootPath;
        _previousMapPath = null;
        _documentHistory.Clear();
        _mapNodes.Clear();
        _mapNodes.Add(rootPath);
        _mapEdges.Clear();
        PublishMapState();
    }

    private void DiscoverUnstructuredMapLink(
        string sourcePath,
        string targetPath,
        int? linkOrder)
    {
        if (_mapRootPath is null || _mapNodes.Count == 0)
        {
            _mapSessionId++;
            _mapFormat = MapFormat.Markdown;
            _mapRootPath = sourcePath;
            _previousMapPath = null;
            _mapNodes.Clear();
            _mapEdges.Clear();
            _mapNodes.Add(sourcePath);
        }
        else if (!_mapNodes.Contains(sourcePath, StringComparer.OrdinalIgnoreCase))
        {
            _previousMapPath = null;
            PublishMapState();
            return;
        }

        SetPreviousMapNode(sourcePath, targetPath);
        bool targetWasAlreadyDiscovered = _mapNodes.Contains(
            targetPath,
            StringComparer.OrdinalIgnoreCase);

        if (!targetWasAlreadyDiscovered)
        {
            _mapNodes.Add(targetPath);
            if (!sourcePath.Equals(targetPath, StringComparison.OrdinalIgnoreCase))
            {
                _mapEdges.Add(new ExplorationMapEdge(
                    sourcePath,
                    targetPath,
                    linkOrder ?? int.MaxValue));
            }
        }

        PublishMapState();
    }

    private void UpdateUnstructuredLinkOrders(JsonElement message)
    {
        if (_mapFormat != MapFormat.Markdown ||
            _markdownPath is null ||
            !message.TryGetProperty("contextId", out JsonElement contextIdElement) ||
            !contextIdElement.TryGetInt32(out int contextId) ||
            contextId != _documentContextId ||
            !message.TryGetProperty("links", out JsonElement linksElement) ||
            linksElement.ValueKind != JsonValueKind.Array ||
            linksElement.GetArrayLength() > 10_000)
        {
            return;
        }

        string sourcePath = _markdownPath;
        var orderByTarget = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement linkElement in linksElement.EnumerateArray())
        {
            if (linkElement.ValueKind != JsonValueKind.Object ||
                !linkElement.TryGetProperty("href", out JsonElement hrefElement) ||
                hrefElement.ValueKind != JsonValueKind.String ||
                !linkElement.TryGetProperty("order", out JsonElement orderElement) ||
                orderElement.ValueKind != JsonValueKind.Number ||
                !orderElement.TryGetInt32(out int order) ||
                order < 0)
            {
                continue;
            }

            string? targetPath = ResolveLinkedMarkdownPath(sourcePath, hrefElement.GetString());
            if (targetPath is not null)
            {
                orderByTarget.TryAdd(targetPath, order);
            }
        }

        bool changed = false;
        for (int index = 0; index < _mapEdges.Count; index++)
        {
            ExplorationMapEdge edge = _mapEdges[index];
            if (!PathsEqual(edge.From, sourcePath) ||
                !orderByTarget.TryGetValue(edge.To, out int order) ||
                edge.Order == order)
            {
                continue;
            }

            _mapEdges[index] = edge with { Order = order };
            changed = true;
        }

        if (changed) PublishMapState();
    }

    private static string? ResolveLinkedMarkdownPath(string sourcePath, string? href)
    {
        if (string.IsNullOrWhiteSpace(href)) return null;

        try
        {
            int suffixStart = href.IndexOfAny(['?', '#']);
            string encodedPath = suffixStart >= 0 ? href[..suffixStart] : href;
            if (string.IsNullOrWhiteSpace(encodedPath)) return null;

            string relativePath = Uri.UnescapeDataString(encodedPath)
                .Replace('/', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(relativePath)) return null;

            string extension = Path.GetExtension(relativePath);
            if (!extension.Equals(".md", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".markdown", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string? sourceDirectory = Path.GetDirectoryName(sourcePath);
            return sourceDirectory is null
                ? null
                : Path.GetFullPath(Path.Combine(sourceDirectory, relativePath));
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException or UriFormatException)
        {
            return null;
        }
    }

    private void SetPreviousMapNode(string? previousPath, string currentPath)
    {
        _previousMapPath = previousPath is not null &&
            !previousPath.Equals(currentPath, StringComparison.OrdinalIgnoreCase) &&
            _mapNodes.Contains(previousPath, StringComparer.OrdinalIgnoreCase)
                ? previousPath
                : null;
    }

    private void PublishMapState()
    {
        bool isProofFoldMap = _mapFormat == MapFormat.ProofFold;
        string? rootDirectory = isProofFoldMap
            ? _proofFoldStructure?.RootDirectory
            : _mapRootPath is null
                ? null
                : Path.GetDirectoryName(_mapRootPath);

        PostViewerMessage(new
        {
            type = "map-state",
            format = isProofFoldMap ? "prooffold" : "markdown",
            sessionId = _mapSessionId,
            root = _mapRootPath,
            current = _markdownPath,
            previous = _previousMapPath,
            nodes = _mapNodes.Select((path, discoveryOrder) => new
            {
                id = path,
                label = isProofFoldMap
                    ? Path.GetFileNameWithoutExtension(path)
                        .Replace('_', ' ')
                        .Replace('-', ' ')
                    : Path.GetFileNameWithoutExtension(path).Replace('_', ' '),
                detail = path.Equals(_mapRootPath, StringComparison.OrdinalIgnoreCase)
                    ? isProofFoldMap
                        ? "ProofFold entry"
                        : "Starting document"
                    : rootDirectory is null
                        ? Path.GetFileName(path)
                        : Path.GetRelativePath(rootDirectory, path).Replace('\\', '/'),
                unresolved = !isProofFoldMap && UnresolvedStateStore.IsUnresolved(path),
                order = isProofFoldMap && _proofFoldStructure is not null
                    ? _proofFoldStructure.GetDocumentOrder(path)
                    : discoveryOrder,
            }),
            edges = _mapEdges.Select(edge => new
            {
                from = edge.From,
                to = edge.To,
                order = edge.Order,
            }),
        });
    }

    private async Task OpenDroppedMarkdownAsync(IReadOnlyList<object> additionalObjects)
    {
        if (additionalObjects.Count != 1 ||
            additionalObjects[0] is not CoreWebView2File droppedFile)
        {
            return;
        }

        string markdownPath = droppedFile.Path;
        string extension = Path.GetExtension(markdownPath);
        if (!extension.Equals(".md", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".markdown", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await OpenMarkdownPathAsync(Path.GetFullPath(markdownPath));
    }

    private void PostViewerMessage(object message)
    {
        _webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(message));
    }

    private static object? ProofFoldPayload(ProofFoldStructure? structure)
    {
        return structure is null
            ? null
            : new
            {
                entry = structure.EntryId,
                folds = structure.Folds.Select(fold => new
                {
                    path = fold.Id,
                    source = fold.Source,
                }).ToArray(),
            };
    }

    private void SetCurrentDocumentUnresolved(JsonElement message)
    {
        string? markdownPath = _markdownPath;
        if (markdownPath is null ||
            !message.TryGetProperty("contextId", out JsonElement contextIdElement) ||
            !contextIdElement.TryGetInt32(out int contextId) ||
            contextId != _documentContextId ||
            !message.TryGetProperty("unresolved", out JsonElement unresolvedElement) ||
            unresolvedElement.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
        {
            return;
        }

        string? error = null;
        try
        {
            UnresolvedStateStore.SetUnresolved(
                markdownPath,
                unresolvedElement.GetBoolean());
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            error = $"The unresolved state could not be changed: {exception.Message}";
        }

        PublishCurrentDocumentUnresolvedState(error);
        PublishMapState();
    }

    private void PublishCurrentDocumentUnresolvedState(string? error = null)
    {
        string? markdownPath = _markdownPath;
        PostViewerMessage(new
        {
            type = "document-unresolved-state",
            contextId = _documentContextId,
            enabled = markdownPath is not null,
            unresolved = markdownPath is not null &&
                UnresolvedStateStore.IsUnresolved(markdownPath),
            error,
        });
    }

    private void ConfigureMarkdownWatcher(string markdownPath)
    {
        DisposeMarkdownWatcher();

        string? directory = Path.GetDirectoryName(markdownPath);
        if (directory is null || !Directory.Exists(directory)) return;

        if (IsWslPath(markdownPath))
        {
            _wslFileStatePollTimer.Start();
            return;
        }

        var watcher = new FileSystemWatcher(directory)
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName |
                NotifyFilters.LastWrite |
                NotifyFilters.Size |
                NotifyFilters.CreationTime,
        };
        watcher.Changed += OnWatchedDirectoryChanged;
        watcher.Created += OnWatchedDirectoryChanged;
        watcher.Deleted += OnWatchedDirectoryChanged;
        watcher.Renamed += OnWatchedDirectoryChanged;
        watcher.Error += OnMarkdownWatcherError;
        watcher.EnableRaisingEvents = true;
        _markdownWatcher = watcher;
    }

    private void ConfigureProofFoldStructure(ProofFoldStructure? structure)
    {
        DisposeProofFoldWatcher();
        _proofFoldStructure = structure;
        if (structure is null ||
            !Directory.Exists(structure.RootDirectory) ||
            IsWslPath(structure.RootDirectory))
        {
            return;
        }

        var watcher = new FileSystemWatcher(structure.RootDirectory)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName |
                NotifyFilters.DirectoryName |
                NotifyFilters.LastWrite |
                NotifyFilters.Size |
                NotifyFilters.CreationTime,
        };
        watcher.Changed += OnProofFoldChanged;
        watcher.Created += OnProofFoldChanged;
        watcher.Deleted += OnProofFoldChanged;
        watcher.Renamed += OnProofFoldChanged;
        watcher.Error += OnProofFoldWatcherError;
        watcher.EnableRaisingEvents = true;
        _proofFoldWatcher = watcher;
    }

    private void OnProofFoldChanged(object sender, FileSystemEventArgs eventArgs)
    {
        ProofFoldStructure? structure = _proofFoldStructure;
        if (structure is null) return;

        bool affectsRenderedDocument = structure.AffectsRenderedDocument(eventArgs.FullPath);
        if (eventArgs is RenamedEventArgs renamedEventArgs)
        {
            affectsRenderedDocument |= structure.AffectsRenderedDocument(
                renamedEventArgs.OldFullPath);
        }
        if (affectsRenderedDocument) ScheduleMarkdownReload();
    }

    private void OnProofFoldWatcherError(object sender, ErrorEventArgs eventArgs)
    {
        ScheduleMarkdownReload();
    }

    private void OnWatchedDirectoryChanged(object sender, FileSystemEventArgs eventArgs)
    {
        string? markdownPath = _markdownPath;
        if (markdownPath is null) return;

        bool affectsCurrentFile = PathsEqual(eventArgs.FullPath, markdownPath);
        string? markdownDirectory = Path.GetDirectoryName(markdownPath);
        string? proofFoldManifestPath = markdownDirectory is null
            ? null
            : Path.Combine(markdownDirectory, ProofFoldStructure.ManifestFileName);
        bool affectsProofFoldManifest = PathsEqual(
            eventArgs.FullPath,
            proofFoldManifestPath);
        string unresolvedPath = UnresolvedStateStore.SidecarPath(markdownPath);
        bool affectsUnresolvedState = PathsEqual(eventArgs.FullPath, unresolvedPath);
        if (eventArgs is RenamedEventArgs renamedEventArgs)
        {
            affectsCurrentFile |= PathsEqual(renamedEventArgs.OldFullPath, markdownPath);
            affectsProofFoldManifest |= PathsEqual(
                renamedEventArgs.OldFullPath,
                proofFoldManifestPath);
            affectsUnresolvedState |= PathsEqual(
                renamedEventArgs.OldFullPath,
                unresolvedPath);
        }

        if (affectsCurrentFile || affectsProofFoldManifest)
        {
            ScheduleMarkdownReload();
        }
        if (affectsUnresolvedState)
        {
            ScheduleUnresolvedStateRefresh();
        }
    }

    private void OnMarkdownWatcherError(object sender, ErrorEventArgs eventArgs)
    {
        ScheduleMarkdownReload();
    }

    private void ScheduleMarkdownReload()
    {
        if (IsDisposed || Disposing || !IsHandleCreated) return;

        try
        {
            BeginInvoke(new Action(() =>
            {
                if (IsDisposed || Disposing) return;
                _markdownReloadTimer.Stop();
                _markdownReloadTimer.Start();
            }));
        }
        catch (InvalidOperationException)
        {
            // The window closed while the file-system event was being delivered.
        }
    }

    private async void OnMarkdownReloadTimerTick(object? sender, EventArgs eventArgs)
    {
        _markdownReloadTimer.Stop();
        await ReloadCurrentMarkdownAsync();
    }

    private async void OnWslFileStatePollTimerTick(object? sender, EventArgs eventArgs)
    {
        if (_formIsClosing || _wslFileStatePollInProgress) return;

        _wslFileStatePollInProgress = true;
        try
        {
            await ReloadCurrentMarkdownAsync();
            await PollWslUnresolvedStatesAsync();
        }
        finally
        {
            _wslFileStatePollInProgress = false;
        }
    }

    private async Task PollWslUnresolvedStatesAsync()
    {
        int contextId = _documentContextId;
        var pathsToCheck = new List<string>(_mapNodes);
        if (_markdownPath is not null)
        {
            pathsToCheck.Add(_markdownPath);
        }
        string[] documentPaths = pathsToCheck
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string fingerprint = await Task.Run(() => string.Join(
            "\n",
            documentPaths.Where(UnresolvedStateStore.IsUnresolved)));
        if (_formIsClosing ||
            contextId != _documentContextId ||
            fingerprint == _unresolvedStateFingerprint)
        {
            return;
        }

        _unresolvedStateFingerprint = fingerprint;
        PublishCurrentDocumentUnresolvedState();
        PublishMapState();
    }

    private async Task ReloadCurrentMarkdownAsync()
    {
        string? markdownPath = _markdownPath;
        if (markdownPath is null) return;

        for (int attempt = 0; attempt < MarkdownReadRetryCount; attempt++)
        {
            try
            {
                if (!File.Exists(markdownPath))
                {
                    throw new FileNotFoundException(null, markdownPath);
                }

                string source = await ReadMarkdownSourceAsync(markdownPath);
                ProofFoldStructure? proofFoldStructure =
                    ProofFoldStructure.LoadForEntry(markdownPath);
                bool proofFoldUnchanged = string.Equals(
                    proofFoldStructure?.Fingerprint,
                    _proofFoldStructure?.Fingerprint,
                    StringComparison.Ordinal);
                if (!PathsEqual(_markdownPath, markdownPath) ||
                    source == _lastRenderedSource && proofFoldUnchanged)
                {
                    return;
                }

                _lastRenderedSource = source;
                ConfigureProofFoldStructure(proofFoldStructure);
                PostViewerMessage(new
                {
                    type = "reload-markdown",
                    source,
                    name = Path.GetFileName(markdownPath),
                    contextId = _documentContextId,
                    proofFold = ProofFoldPayload(proofFoldStructure),
                });
                if (proofFoldStructure is not null)
                {
                    UpdateProofFoldMap(proofFoldStructure);
                }
                else if (_mapFormat == MapFormat.ProofFold)
                {
                    StartUnstructuredMap(markdownPath);
                }
                return;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                if (attempt == MarkdownReadRetryCount - 1) return;
                await Task.Delay(MarkdownReadRetryDelayMilliseconds * (attempt + 1));
            }
        }
    }

    private static async Task<string> ReadMarkdownSourceAsync(string markdownPath)
    {
        await using var stream = new FileStream(
            markdownPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 4096,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync();
    }

    private static bool PathsEqual(string? firstPath, string? secondPath)
    {
        if (firstPath is null || secondPath is null) return false;

        try
        {
            return Path.GetFullPath(firstPath)
                .Equals(Path.GetFullPath(secondPath), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsWslPath(string path)
    {
        try
        {
            string fullPath = Path.GetFullPath(path);
            return fullPath.StartsWith(
                    @"\\wsl$\",
                    StringComparison.OrdinalIgnoreCase) ||
                fullPath.StartsWith(
                    @"\\wsl.localhost\",
                    StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private void DisposeMarkdownWatcher()
    {
        _markdownReloadTimer.Stop();
        _wslFileStatePollTimer.Stop();
        _markdownWatcher?.Dispose();
        _markdownWatcher = null;
    }

    private void DisposeProofFoldWatcher()
    {
        if (_proofFoldWatcher is null) return;

        _proofFoldWatcher.EnableRaisingEvents = false;
        _proofFoldWatcher.Changed -= OnProofFoldChanged;
        _proofFoldWatcher.Created -= OnProofFoldChanged;
        _proofFoldWatcher.Deleted -= OnProofFoldChanged;
        _proofFoldWatcher.Renamed -= OnProofFoldChanged;
        _proofFoldWatcher.Error -= OnProofFoldWatcherError;
        _proofFoldWatcher.Dispose();
        _proofFoldWatcher = null;
    }

    private void ScheduleUnresolvedStateRefresh()
    {
        if (IsDisposed || Disposing || !IsHandleCreated) return;

        try
        {
            BeginInvoke(new Action(() =>
            {
                if (IsDisposed || Disposing) return;
                _unresolvedStateReloadTimer.Stop();
                _unresolvedStateReloadTimer.Start();
            }));
        }
        catch (InvalidOperationException)
        {
            // The window closed while the file-system event was being delivered.
        }
    }

    private void OnUnresolvedStateReloadTimerTick(object? sender, EventArgs eventArgs)
    {
        _unresolvedStateReloadTimer.Stop();
        PublishCurrentDocumentUnresolvedState();
        PublishMapState();
    }

    private void RevealWindow()
    {
        if (_windowRevealed) return;

        _windowRevealed = true;
        Opacity = 1;
        Activate();
    }

    private void ApplyAppIcon()
    {
        using Stream? stream = typeof(MainForm).Assembly.GetManifestResourceStream("ProofMD.AppIcon.ico");
        if (stream is null) return;

        using var embeddedIcon = new Icon(stream);
        Icon = (Icon)embeddedIcon.Clone();
    }

    private Task<string>? StartInitialMarkdownRead()
    {
        return _markdownPath is not null && File.Exists(_markdownPath)
            ? ReadMarkdownSourceAsync(_markdownPath)
            : null;
    }

    private static void OpenExternalUri(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out Uri? parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch
        {
            // External links are optional; keep the local viewer running.
        }
    }

    private void ApplyInitialWindowState()
    {
        WindowStateData? saved = WindowStateStore.Load();
        if (saved is null || !TryRestoreSavedBounds(saved))
        {
            ApplyLargeDefaultBounds();
            return;
        }

        if (saved.Maximized)
        {
            WindowState = FormWindowState.Maximized;
        }
    }

    private bool TryRestoreSavedBounds(WindowStateData saved)
    {
        if (saved.Width < MinimumSize.Width || saved.Height < MinimumSize.Height)
        {
            return false;
        }

        var savedBounds = new Rectangle(saved.X, saved.Y, saved.Width, saved.Height);
        Screen? screen = Screen.AllScreens.FirstOrDefault(candidate =>
        {
            Rectangle visible = Rectangle.Intersect(candidate.WorkingArea, savedBounds);
            return visible.Width >= 120 && visible.Height >= 120;
        });

        if (screen is null) return false;

        Rectangle workingArea = screen.WorkingArea;
        int width = Math.Clamp(saved.Width, MinimumSize.Width, workingArea.Width);
        int height = Math.Clamp(saved.Height, MinimumSize.Height, workingArea.Height);
        int x = Math.Clamp(saved.X, workingArea.Left, workingArea.Right - width);
        int y = Math.Clamp(saved.Y, workingArea.Top, workingArea.Bottom - height);

        StartPosition = FormStartPosition.Manual;
        Bounds = new Rectangle(x, y, width, height);
        return true;
    }

    private void ApplyLargeDefaultBounds()
    {
        Rectangle workingArea = Screen.PrimaryScreen?.WorkingArea
            ?? Screen.FromPoint(Cursor.Position).WorkingArea;

        int maximumWidth = Math.Max(MinimumSize.Width, workingArea.Width - 48);
        int maximumHeight = Math.Max(MinimumSize.Height, workingArea.Height - 48);
        int width = Math.Min(maximumWidth, Math.Max(1120, (int)(workingArea.Width * 0.82)));
        int height = Math.Min(maximumHeight, Math.Max(760, (int)(workingArea.Height * 0.88)));
        int x = workingArea.Left + (workingArea.Width - width) / 2;
        int y = workingArea.Top + (workingArea.Height - height) / 2;

        StartPosition = FormStartPosition.Manual;
        Bounds = new Rectangle(x, y, width, height);
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs eventArgs)
    {
        _formIsClosing = true;
        DisposeMarkdownWatcher();
        DisposeProofFoldWatcher();
        _markdownReloadTimer.Dispose();
        _wslFileStatePollTimer.Dispose();
        _unresolvedStateReloadTimer.Stop();
        _unresolvedStateReloadTimer.Dispose();

        Rectangle boundsToSave = WindowState == FormWindowState.Normal
            ? Bounds
            : RestoreBounds;

        WindowStateStore.Save(
            boundsToSave,
            maximized: WindowState == FormWindowState.Maximized);
    }

    private void ShowStartupError(string message)
    {
        RevealWindow();
        MessageBox.Show(
            this,
            message,
            "ProofMD",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
        Close();
    }
}
