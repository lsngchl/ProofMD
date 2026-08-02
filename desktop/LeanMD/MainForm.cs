using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace LeanMD;

internal sealed class MainForm : Form
{
    private static MainForm? s_lastActivatedWindow;
    private const string ViewerHostName = "leanmd.local";
    private const int MarkdownReloadDebounceMilliseconds = 250;
    private const int WslFileStatePollIntervalMilliseconds = 750;
    private const int MarkdownReadRetryCount = 4;
    private const int MarkdownReadRetryDelayMilliseconds = 100;
    private const int LeanMdContextWriteDebounceMilliseconds = 100;
    private const int LeanMdStructureReloadDebounceMilliseconds = 250;
    private const int UnresolvedStateReloadDebounceMilliseconds = 150;
    private string? _markdownPath;
    private string? _lastMarkdownDirectory;
    private readonly Action<string, MainForm> _openRecallWindow;
    private readonly bool _persistWindowState;
    private readonly bool _persistExplorationMap;
    private readonly WebView2 _webView;
    private readonly System.Windows.Forms.Timer _markdownReloadTimer;
    private readonly System.Windows.Forms.Timer _wslFileStatePollTimer;
    private readonly System.Windows.Forms.Timer _leanMdContextWriteTimer;
    private readonly System.Windows.Forms.Timer _leanMdStructureReloadTimer;
    private readonly System.Windows.Forms.Timer _unresolvedStateReloadTimer;
    private readonly string _contextWindowId = Guid.NewGuid().ToString("N");
    private readonly List<string> _mapNodes = [];
    private readonly List<ExplorationMapEdge> _mapEdges = [];
    private readonly List<string> _mapVisitedNodes = [];
    private readonly Stack<DocumentHistoryEntry> _documentHistory = new();
    private string? _mapRootPath;
    private string? _previousMapPath;
    private string? _mapMetadataDirectory;
    private string? _mapDependenciesFingerprint;
    private int _mapSessionId;
    private Task<string>? _initialMarkdownReadTask;
    private FileSystemWatcher? _markdownWatcher;
    private FileSystemWatcher? _leanMdStructureWatcher;
    private FileSystemWatcher? _unresolvedStateWatcher;
    private LeanMdStructure? _leanMdStructure;
    private string? _lastRenderedSource;
    private string? _leanMdMetadataDirectory;
    private string? _unresolvedStateFingerprint;
    private int _documentContextId;
    private ViewerViewport? _viewerViewport;
    private ViewerFocus? _viewerFocus;
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

    private readonly record struct ViewerViewport(
        int StartLine,
        int EndLine,
        int CenterLine);

    private readonly record struct ViewerFocus(
        int StartLine,
        int EndLine,
        string SelectedText);

    public MainForm(
        string? markdownPath,
        Action<string, MainForm> openRecallWindow,
        MainForm? recallSource = null)
    {
        _markdownPath = markdownPath;
        _lastMarkdownDirectory = markdownPath is null
            ? null
            : Path.GetDirectoryName(markdownPath);
        _openRecallWindow = openRecallWindow;
        _persistWindowState = recallSource is null;
        _persistExplorationMap = recallSource is null;
        Text = "LeanMD";
        MinimumSize = new Size(720, 540);
        BackColor = Color.FromArgb(243, 241, 236);
        ApplyAppIcon();
        Opacity = 0;
        ApplyInitialWindowState();
        if (recallSource is not null)
        {
            ApplyRecallWindowBounds(recallSource);
        }

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
        _leanMdContextWriteTimer = new System.Windows.Forms.Timer
        {
            Interval = LeanMdContextWriteDebounceMilliseconds,
        };
        _leanMdContextWriteTimer.Tick += OnLeanMdContextWriteTimerTick;
        _leanMdStructureReloadTimer = new System.Windows.Forms.Timer
        {
            Interval = LeanMdStructureReloadDebounceMilliseconds,
        };
        _leanMdStructureReloadTimer.Tick += OnLeanMdStructureReloadTimerTick;
        _unresolvedStateReloadTimer = new System.Windows.Forms.Timer
        {
            Interval = UnresolvedStateReloadDebounceMilliseconds,
        };
        _unresolvedStateReloadTimer.Tick += OnUnresolvedStateReloadTimerTick;

        Controls.Add(_webView);
        Shown += OnShown;
        FormClosing += OnFormClosing;
        Activated += OnFormActivated;
        Deactivate += OnFormDeactivated;
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
                "Microsoft Edge WebView2 Runtime is required. Install it from Microsoft, then reopen LeanMD.");
        }
        catch (Exception exception)
        {
            ShowStartupError($"LeanMD could not start.\n\n{exception.Message}");
        }
    }

    private async Task InitializeViewerAsync()
    {
        string userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LeanMD",
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
                ShowStartupError("The LeanMD viewer could not be loaded.");
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
            throw new InvalidOperationException("The installed LeanMD viewer files were not found.");
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
                        string? role =
                            message.RootElement.TryGetProperty("role", out JsonElement roleElement) &&
                            roleElement.ValueKind == JsonValueKind.String
                                ? roleElement.GetString()
                                : null;
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
                        await OpenLinkedMarkdownAsync(
                            hrefElement.GetString(),
                            role,
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
                case "viewer-context":
                    UpdateViewerContext(message.RootElement);
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
                "LeanMD",
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
            _markdownPath = markdownPath;
            _lastMarkdownDirectory = Path.GetDirectoryName(markdownPath);
            _initialMarkdownReadTask = null;
            _lastRenderedSource = source;
            ConfigureMarkdownWatcher(markdownPath);
            ConfigureLeanMdContext(markdownPath);
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
            });
            UpdateMapAfterOpen(markdownPath, reason, previousPath, linkSourcePath, linkOrder);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                $"The Markdown file could not be opened.\n\n{exception.Message}",
                "LeanMD",
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
        string? role,
        DocumentPosition? historyPosition,
        int? linkOrder)
    {
        if (_markdownPath is null || string.IsNullOrWhiteSpace(href)) return;

        string sourcePath = _markdownPath;

        try
        {
            string? linkedPath = ResolveLinkedMarkdownPath(sourcePath, href);
            if (linkedPath is null) return;
            bool isRecall = role?.Equals(
                "recall",
                StringComparison.OrdinalIgnoreCase) == true;
            bool targetWasDiscovered = _mapNodes.Contains(
                linkedPath,
                StringComparer.OrdinalIgnoreCase);
            bool targetIsInCurrentStructure =
                _leanMdStructure?.ContainsDocument(linkedPath) == true;
            if (isRecall && !targetWasDiscovered && !targetIsInCurrentStructure)
            {
                if (!File.Exists(linkedPath))
                {
                    MessageBox.Show(
                        this,
                        $"The Markdown file was not found:\n{linkedPath}",
                        "LeanMD",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                _openRecallWindow(linkedPath, this);
                return;
            }

            await OpenMarkdownPathAsync(
                linkedPath,
                reason: OpenReason.Link,
                linkSourcePath: sourcePath,
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
        if (_leanMdStructure?.ContainsDocument(markdownPath) == true)
        {
            UpdateStructuredMapAfterOpen(markdownPath, previousPath);
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

    private void UpdateStructuredMapAfterOpen(
        string markdownPath,
        string? previousPath)
    {
        LeanMdStructure structure = _leanMdStructure!;
        bool sameMap = _mapMetadataDirectory is not null &&
            PathsEqual(_mapMetadataDirectory, structure.MetadataDirectory) &&
            _mapRootPath is not null &&
            PathsEqual(_mapRootPath, structure.RootPath) &&
            _mapNodes.Count > 0;

        if (!sameMap)
        {
            RestoreStructuredMap(structure);
        }

        ReconcileStructuredMap(structure);
        MarkMapNodeVisited(markdownPath);
        SetPreviousMapNode(previousPath, markdownPath);
        PersistMapState();
        PublishMapState();
    }

    private void RestoreStructuredMap(LeanMdStructure structure)
    {
        _mapSessionId++;
        _mapRootPath = structure.RootPath;
        _mapMetadataDirectory = structure.MetadataDirectory;
        _mapDependenciesFingerprint = structure.Fingerprint;
        _previousMapPath = null;
        _documentHistory.Clear();
        _mapNodes.Clear();
        _mapEdges.Clear();
        _mapVisitedNodes.Clear();

        ExplorationMapState? restored = _persistExplorationMap
            ? ExplorationMapStore.Load(structure.MetadataDirectory)
            : null;
        if (restored is not null && PathsEqual(restored.RootPath, structure.RootPath))
        {
            _mapVisitedNodes.AddRange(restored.VisitedNodes);
            _mapDependenciesFingerprint = restored.DependenciesFingerprint;
        }
    }

    private void ReconcileStructuredMap(LeanMdStructure structure)
    {
        List<string> visited = _mapVisitedNodes
            .Where(structure.ContainsDocument)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (_mapRootPath is null || !PathsEqual(_mapRootPath, structure.RootPath))
        {
            _mapSessionId++;
        }

        _mapRootPath = structure.RootPath;
        _mapEdges.Clear();
        _mapEdges.AddRange(structure.Edges);
        _mapNodes.Clear();
        _mapNodes.AddRange(structure.Documents
            .OrderBy(structure.GetDocumentOrder)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase));
        _mapVisitedNodes.Clear();
        _mapVisitedNodes.AddRange(visited);

        _mapMetadataDirectory = structure.MetadataDirectory;
        _mapDependenciesFingerprint = structure.Fingerprint;
    }

    private void MarkMapNodeVisited(string path)
    {
        if (!_mapVisitedNodes.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            _mapVisitedNodes.Add(path);
        }
    }

    private void ResetMap(string currentPath)
    {
        if (_leanMdStructure?.ContainsDocument(currentPath) == true)
        {
            LeanMdStructure structure = _leanMdStructure;
            _mapSessionId++;
            _mapRootPath = structure.RootPath;
            _mapMetadataDirectory = structure.MetadataDirectory;
            _mapDependenciesFingerprint = structure.Fingerprint;
            _previousMapPath = null;
            _documentHistory.Clear();
            _mapNodes.Clear();
            _mapEdges.Clear();
            _mapVisitedNodes.Clear();
            ReconcileStructuredMap(structure);
            MarkMapNodeVisited(currentPath);
            PersistMapState();
            PublishMapState();
            return;
        }

        StartUnstructuredMap(currentPath);
    }

    private void StartUnstructuredMap(string rootPath)
    {
        _mapSessionId++;
        _mapRootPath = rootPath;
        _mapMetadataDirectory = null;
        _mapDependenciesFingerprint = null;
        _previousMapPath = null;
        _documentHistory.Clear();
        _mapNodes.Clear();
        _mapNodes.Add(rootPath);
        _mapEdges.Clear();
        _mapVisitedNodes.Clear();
        _mapVisitedNodes.Add(rootPath);
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
            _mapRootPath = sourcePath;
            _mapMetadataDirectory = null;
            _mapDependenciesFingerprint = null;
            _previousMapPath = null;
            _mapNodes.Clear();
            _mapEdges.Clear();
            _mapVisitedNodes.Clear();
            _mapNodes.Add(sourcePath);
            _mapVisitedNodes.Add(sourcePath);
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

        MarkMapNodeVisited(targetPath);
        PublishMapState();
    }

    private void UpdateUnstructuredLinkOrders(JsonElement message)
    {
        if (_mapMetadataDirectory is not null ||
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

    private void PersistMapState()
    {
        if (!_persistExplorationMap ||
            _mapMetadataDirectory is null ||
            _mapRootPath is null ||
            _mapNodes.Count == 0)
        {
            return;
        }

        ExplorationMapStore.Save(
            _mapMetadataDirectory,
            new ExplorationMapState(
                _mapRootPath,
                _mapNodes.ToArray(),
                _mapEdges.ToArray(),
                _mapVisitedNodes.ToArray(),
                _mapDependenciesFingerprint));
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
        string? rootDirectory = _mapMetadataDirectory is null
            ? _mapRootPath is null
                ? null
                : Path.GetDirectoryName(_mapRootPath)
            : Directory.GetParent(_mapMetadataDirectory)?.FullName;
        bool isStructuredMap = _mapMetadataDirectory is not null;
        var visited = new HashSet<string>(
            _mapVisitedNodes,
            StringComparer.OrdinalIgnoreCase);

        PostViewerMessage(new
        {
            type = "map-state",
            sessionId = _mapSessionId,
            root = _mapRootPath,
            current = _markdownPath,
            previous = _previousMapPath,
            nodes = _mapNodes.Select((path, discoveryOrder) => new
            {
                id = path,
                label = isStructuredMap && !visited.Contains(path)
                    ? "?"
                    : Path.GetFileNameWithoutExtension(path).Replace('_', ' '),
                detail = isStructuredMap && !visited.Contains(path)
                    ? "Unexplored structure node"
                    : path.Equals(_mapRootPath, StringComparison.OrdinalIgnoreCase)
                    ? isStructuredMap ? "Structure root" : "Starting document"
                    : rootDirectory is null
                        ? Path.GetFileName(path)
                        : Path.GetRelativePath(rootDirectory, path).Replace('\\', '/'),
                unexplored = isStructuredMap && !visited.Contains(path),
                unresolved = UnresolvedStateStore.IsUnresolved(path),
                order = isStructuredMap && _leanMdStructure is not null
                    ? _leanMdStructure.GetDocumentOrder(path)
                    : discoveryOrder,
            }),
            edges = _mapEdges.Select(edge => new
            {
                from = edge.From,
                to = edge.To,
                order = isStructuredMap && _leanMdStructure is not null
                    ? _leanMdStructure.GetEdgeOrder(edge.From, edge.To)
                    : edge.Order,
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

    private void OnWatchedDirectoryChanged(object sender, FileSystemEventArgs eventArgs)
    {
        string? markdownPath = _markdownPath;
        if (markdownPath is null) return;

        bool affectsCurrentFile = PathsEqual(eventArgs.FullPath, markdownPath);
        string unresolvedPath = UnresolvedStateStore.SidecarPath(markdownPath);
        bool affectsUnresolvedState = PathsEqual(eventArgs.FullPath, unresolvedPath);
        if (eventArgs is RenamedEventArgs renamedEventArgs)
        {
            affectsCurrentFile |= PathsEqual(renamedEventArgs.OldFullPath, markdownPath);
            affectsUnresolvedState |= PathsEqual(
                renamedEventArgs.OldFullPath,
                unresolvedPath);
        }

        if (affectsCurrentFile)
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
            await PollWslLeanMdStructureAsync();
            await PollWslUnresolvedStatesAsync();
        }
        finally
        {
            _wslFileStatePollInProgress = false;
        }
    }

    private async Task PollWslLeanMdStructureAsync()
    {
        string? metadataDirectory = _leanMdMetadataDirectory;
        if (metadataDirectory is null || !IsWslPath(metadataDirectory)) return;

        LeanMdStructure? structure = await Task.Run(
            () => LeanMdStructure.Load(metadataDirectory));
        if (_formIsClosing ||
            structure is null ||
            !PathsEqual(_leanMdMetadataDirectory, metadataDirectory) ||
            structure.Fingerprint == _leanMdStructure?.Fingerprint)
        {
            return;
        }

        ApplyLeanMdStructure(structure);
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
                if (!PathsEqual(_markdownPath, markdownPath) || source == _lastRenderedSource)
                {
                    return;
                }

                _lastRenderedSource = source;
                PostViewerMessage(new
                {
                    type = "reload-markdown",
                    source,
                    name = Path.GetFileName(markdownPath),
                    contextId = _documentContextId,
                });
                return;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
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

    private void ConfigureLeanMdContext(string markdownPath)
    {
        string? previousMetadataDirectory = _leanMdMetadataDirectory;
        string? nextMetadataDirectory = FindLeanMdMetadataDirectory(markdownPath);
        bool metadataDirectoryChanged =
            previousMetadataDirectory is null != nextMetadataDirectory is null ||
            previousMetadataDirectory is not null &&
            nextMetadataDirectory is not null &&
            !PathsEqual(previousMetadataDirectory, nextMetadataDirectory);

        _leanMdContextWriteTimer.Stop();
        _documentContextId += 1;
        _viewerViewport = null;
        _viewerFocus = null;
        _leanMdMetadataDirectory = nextMetadataDirectory;
        if (metadataDirectoryChanged || _leanMdStructureWatcher is null)
        {
            ConfigureLeanMdStructure(nextMetadataDirectory);
        }

        if (previousMetadataDirectory is not null &&
            !PathsEqual(previousMetadataDirectory, nextMetadataDirectory))
        {
            DeleteOwnedLeanMdContext(previousMetadataDirectory);
        }

        ScheduleLeanMdContextWrite();
    }

    private void ConfigureLeanMdStructure(string? metadataDirectory)
    {
        DisposeLeanMdStructureWatcher();
        DisposeUnresolvedStateWatcher();
        _leanMdStructure = metadataDirectory is null
            ? null
            : LeanMdStructure.Load(metadataDirectory);
        if (metadataDirectory is null || !Directory.Exists(metadataDirectory)) return;

        _leanMdStructureWatcher = new FileSystemWatcher(metadataDirectory, "dependencies.json")
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName |
                NotifyFilters.LastWrite |
                NotifyFilters.CreationTime |
                NotifyFilters.Size,
            EnableRaisingEvents = true,
        };
        _leanMdStructureWatcher.Changed += OnLeanMdStructureChanged;
        _leanMdStructureWatcher.Created += OnLeanMdStructureChanged;
        _leanMdStructureWatcher.Deleted += OnLeanMdStructureChanged;
        _leanMdStructureWatcher.Renamed += OnLeanMdStructureChanged;

        string? workspaceRoot = Directory.GetParent(metadataDirectory)?.FullName;
        if (workspaceRoot is not null && Directory.Exists(workspaceRoot))
        {
            _unresolvedStateWatcher = new FileSystemWatcher(workspaceRoot, "*.unresolved")
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName |
                    NotifyFilters.LastWrite |
                    NotifyFilters.CreationTime |
                    NotifyFilters.Size,
                EnableRaisingEvents = true,
            };
            _unresolvedStateWatcher.Changed += OnUnresolvedStateChanged;
            _unresolvedStateWatcher.Created += OnUnresolvedStateChanged;
            _unresolvedStateWatcher.Deleted += OnUnresolvedStateChanged;
            _unresolvedStateWatcher.Renamed += OnUnresolvedStateChanged;
            _unresolvedStateWatcher.Error += OnUnresolvedStateWatcherError;
        }
    }

    private void OnUnresolvedStateChanged(object sender, FileSystemEventArgs eventArgs)
    {
        ScheduleUnresolvedStateRefresh();
    }

    private void OnUnresolvedStateWatcherError(object sender, ErrorEventArgs eventArgs)
    {
        ScheduleUnresolvedStateRefresh();
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

    private void OnLeanMdStructureChanged(object sender, FileSystemEventArgs eventArgs)
    {
        try
        {
            BeginInvoke(new Action(() =>
            {
                if (IsDisposed || Disposing) return;
                _leanMdStructureReloadTimer.Stop();
                _leanMdStructureReloadTimer.Start();
            }));
        }
        catch (InvalidOperationException)
        {
            // The window closed while the file-system event was being delivered.
        }
    }

    private void OnLeanMdStructureReloadTimerTick(object? sender, EventArgs eventArgs)
    {
        _leanMdStructureReloadTimer.Stop();
        string? metadataDirectory = _leanMdMetadataDirectory;
        if (metadataDirectory is null) return;

        LeanMdStructure? structure = LeanMdStructure.Load(metadataDirectory);
        if (structure is null) return;

        ApplyLeanMdStructure(structure);
    }

    private void ApplyLeanMdStructure(LeanMdStructure structure)
    {
        _leanMdStructure = structure;
        if (_mapMetadataDirectory is null ||
            !PathsEqual(_mapMetadataDirectory, structure.MetadataDirectory))
        {
            return;
        }

        ReconcileStructuredMap(structure);
        if (_markdownPath is not null && structure.ContainsDocument(_markdownPath))
        {
            MarkMapNodeVisited(_markdownPath);
        }
        PersistMapState();
        PublishMapState();
    }

    private void DisposeLeanMdStructureWatcher()
    {
        _leanMdStructureReloadTimer.Stop();
        if (_leanMdStructureWatcher is null) return;

        _leanMdStructureWatcher.EnableRaisingEvents = false;
        _leanMdStructureWatcher.Changed -= OnLeanMdStructureChanged;
        _leanMdStructureWatcher.Created -= OnLeanMdStructureChanged;
        _leanMdStructureWatcher.Deleted -= OnLeanMdStructureChanged;
        _leanMdStructureWatcher.Renamed -= OnLeanMdStructureChanged;
        _leanMdStructureWatcher.Dispose();
        _leanMdStructureWatcher = null;
    }

    private void DisposeUnresolvedStateWatcher()
    {
        _unresolvedStateReloadTimer.Stop();
        if (_unresolvedStateWatcher is null) return;

        _unresolvedStateWatcher.EnableRaisingEvents = false;
        _unresolvedStateWatcher.Changed -= OnUnresolvedStateChanged;
        _unresolvedStateWatcher.Created -= OnUnresolvedStateChanged;
        _unresolvedStateWatcher.Deleted -= OnUnresolvedStateChanged;
        _unresolvedStateWatcher.Renamed -= OnUnresolvedStateChanged;
        _unresolvedStateWatcher.Error -= OnUnresolvedStateWatcherError;
        _unresolvedStateWatcher.Dispose();
        _unresolvedStateWatcher = null;
    }

    private static string? FindLeanMdMetadataDirectory(string markdownPath)
    {
        string? documentDirectory = Path.GetDirectoryName(Path.GetFullPath(markdownPath));
        if (documentDirectory is null) return null;

        for (var directory = new DirectoryInfo(documentDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            string metadataDirectory = Path.Combine(directory.FullName, ".leanmd");
            string dependenciesPath = Path.Combine(metadataDirectory, "dependencies.json");
            if (File.Exists(dependenciesPath))
            {
                return metadataDirectory;
            }
        }

        return null;
    }

    private void UpdateViewerContext(JsonElement message)
    {
        if (_leanMdMetadataDirectory is null ||
            !message.TryGetProperty("contextId", out JsonElement contextIdElement) ||
            !contextIdElement.TryGetInt32(out int contextId) ||
            contextId != _documentContextId)
        {
            return;
        }

        _viewerViewport = TryReadViewerViewport(message, out ViewerViewport viewport)
            ? viewport
            : null;
        _viewerFocus = TryReadViewerFocus(message, out ViewerFocus focus)
            ? focus
            : null;
        ScheduleLeanMdContextWrite();
    }

    private static bool TryReadViewerViewport(
        JsonElement message,
        out ViewerViewport viewport)
    {
        viewport = default;
        if (!message.TryGetProperty("viewport", out JsonElement element) ||
            element.ValueKind != JsonValueKind.Object ||
            !TryReadPositiveInt(element, "startLine", out int startLine) ||
            !TryReadPositiveInt(element, "endLine", out int endLine) ||
            !TryReadPositiveInt(element, "centerLine", out int centerLine) ||
            endLine < startLine ||
            centerLine < startLine ||
            centerLine > endLine)
        {
            return false;
        }

        viewport = new ViewerViewport(startLine, endLine, centerLine);
        return true;
    }

    private static bool TryReadViewerFocus(JsonElement message, out ViewerFocus focus)
    {
        focus = default;
        if (!message.TryGetProperty("focus", out JsonElement element) ||
            element.ValueKind != JsonValueKind.Object ||
            !TryReadPositiveInt(element, "startLine", out int startLine) ||
            !TryReadPositiveInt(element, "endLine", out int endLine) ||
            endLine < startLine ||
            !element.TryGetProperty("selectedText", out JsonElement selectedTextElement) ||
            selectedTextElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        string? selectedText = selectedTextElement.GetString()?.Trim();
        if (string.IsNullOrEmpty(selectedText)) return false;

        focus = new ViewerFocus(
            startLine,
            endLine,
            selectedText.Length <= 2000 ? selectedText : selectedText[..2000]);
        return true;
    }

    private static bool TryReadPositiveInt(
        JsonElement element,
        string propertyName,
        out int value)
    {
        value = 0;
        return element.TryGetProperty(propertyName, out JsonElement property) &&
            property.TryGetInt32(out value) &&
            value > 0;
    }

    private void ScheduleLeanMdContextWrite()
    {
        if (_formIsClosing ||
            !ReferenceEquals(s_lastActivatedWindow, this) ||
            _leanMdMetadataDirectory is null ||
            _markdownPath is null)
        {
            return;
        }

        _leanMdContextWriteTimer.Stop();
        _leanMdContextWriteTimer.Start();
    }

    private async void OnLeanMdContextWriteTimerTick(object? sender, EventArgs eventArgs)
    {
        _leanMdContextWriteTimer.Stop();
        await WriteLeanMdContextAsync();
    }

    private async Task WriteLeanMdContextAsync()
    {
        if (_formIsClosing || !ReferenceEquals(s_lastActivatedWindow, this)) return;

        string? metadataDirectory = _leanMdMetadataDirectory;
        string? markdownPath = _markdownPath;
        if (metadataDirectory is null || markdownPath is null) return;

        string? workspaceRoot = Directory.GetParent(metadataDirectory)?.FullName;
        if (workspaceRoot is null) return;

        int contextId = _documentContextId;
        ViewerViewport? viewportSnapshot = _viewerViewport;
        ViewerFocus? focusSnapshot = _viewerFocus;
        string relativeDocumentPath = Path.GetRelativePath(workspaceRoot, markdownPath)
            .Replace(Path.DirectorySeparatorChar, '/');

        object? viewport = viewportSnapshot is ViewerViewport visible
            ? new
            {
                startLine = visible.StartLine,
                endLine = visible.EndLine,
                centerLine = visible.CenterLine,
            }
            : null;
        object? focus = focusSnapshot is ViewerFocus selected
            ? new
            {
                startLine = selected.StartLine,
                endLine = selected.EndLine,
                selectedText = selected.SelectedText,
            }
            : null;
        string json = JsonSerializer.Serialize(
            new
            {
                schemaVersion = 1,
                windowId = _contextWindowId,
                document = relativeDocumentPath,
                viewport,
                focus,
                updatedAt = DateTimeOffset.Now.ToString("O"),
            },
            new JsonSerializerOptions { WriteIndented = true });

        string contextPath = Path.Combine(metadataDirectory, "current-context.json");
        string temporaryPath = Path.Combine(
            metadataDirectory,
            $".current-context.{_contextWindowId}.tmp");

        try
        {
            await File.WriteAllTextAsync(temporaryPath, json, new UTF8Encoding(false));
            if (contextId != _documentContextId ||
                !PathsEqual(metadataDirectory, _leanMdMetadataDirectory))
            {
                return;
            }

            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    File.Move(temporaryPath, contextPath, overwrite: true);
                    return;
                }
                catch (IOException) when (attempt < 2)
                {
                    await Task.Delay(50 * (attempt + 1));
                }
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // Live context is optional; document viewing must continue if it cannot be written.
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
            catch
            {
                // A later context update can replace an abandoned temporary file.
            }
        }
    }

    private void DeleteOwnedLeanMdContext(string metadataDirectory)
    {
        string contextPath = Path.Combine(metadataDirectory, "current-context.json");
        if (!File.Exists(contextPath)) return;

        try
        {
            using JsonDocument context = JsonDocument.Parse(File.ReadAllText(contextPath));
            if (context.RootElement.TryGetProperty("windowId", out JsonElement windowId) &&
                windowId.ValueKind == JsonValueKind.String &&
                windowId.GetString() == _contextWindowId)
            {
                File.Delete(contextPath);
            }
        }
        catch
        {
            // Another window may be replacing the shared context file.
        }
    }

    private void OnFormActivated(object? sender, EventArgs eventArgs)
    {
        s_lastActivatedWindow = this;
        ScheduleLeanMdContextWrite();
    }

    private async void OnFormDeactivated(object? sender, EventArgs eventArgs)
    {
        if (_formIsClosing ||
            !ReferenceEquals(s_lastActivatedWindow, this) ||
            !_leanMdContextWriteTimer.Enabled)
        {
            return;
        }

        _leanMdContextWriteTimer.Stop();
        await WriteLeanMdContextAsync();
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
        using Stream? stream = typeof(MainForm).Assembly.GetManifestResourceStream("LeanMD.AppIcon.ico");
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

    private void ApplyRecallWindowBounds(MainForm sourceWindow)
    {
        Rectangle sourceBounds = sourceWindow.WindowState == FormWindowState.Normal
            ? sourceWindow.Bounds
            : sourceWindow.RestoreBounds;
        Rectangle workingArea = Screen.FromRectangle(sourceBounds).WorkingArea;
        int width = Math.Clamp(sourceBounds.Width, MinimumSize.Width, workingArea.Width);
        int height = Math.Clamp(sourceBounds.Height, MinimumSize.Height, workingArea.Height);
        const int offset = 36;
        int x = sourceBounds.Left + offset;
        int y = sourceBounds.Top + offset;

        if (x + width > workingArea.Right)
        {
            x = Math.Max(workingArea.Left, sourceBounds.Left - offset);
        }
        if (y + height > workingArea.Bottom)
        {
            y = Math.Max(workingArea.Top, sourceBounds.Top - offset);
        }

        WindowState = FormWindowState.Normal;
        StartPosition = FormStartPosition.Manual;
        Bounds = new Rectangle(
            Math.Clamp(x, workingArea.Left, workingArea.Right - width),
            Math.Clamp(y, workingArea.Top, workingArea.Bottom - height),
            width,
            height);
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs eventArgs)
    {
        _formIsClosing = true;
        DisposeMarkdownWatcher();
        _markdownReloadTimer.Dispose();
        _wslFileStatePollTimer.Dispose();
        DisposeLeanMdStructureWatcher();
        _leanMdStructureReloadTimer.Dispose();
        DisposeUnresolvedStateWatcher();
        _unresolvedStateReloadTimer.Dispose();
        _leanMdContextWriteTimer.Stop();
        _leanMdContextWriteTimer.Dispose();
        if (_leanMdMetadataDirectory is not null)
        {
            DeleteOwnedLeanMdContext(_leanMdMetadataDirectory);
        }
        if (ReferenceEquals(s_lastActivatedWindow, this))
        {
            s_lastActivatedWindow = null;
        }

        if (!_persistWindowState) return;

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
            "LeanMD",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
        Close();
    }
}
