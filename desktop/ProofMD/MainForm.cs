using System.Diagnostics;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ProofMD;

internal sealed class MainForm : Form
{
    private const string ViewerEntryUri = "https://proofmd.example/index.html";
    private const int ReloadDebounceMilliseconds = 250;
    private const int UnresolvedDebounceMilliseconds = 150;
    private const int ViewerStartTimeoutMilliseconds = 8000;
    private static readonly HashSet<string> ContextMenuItems = ["copy", "selectAll", "print"];
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    // Set by the end-to-end test: the window opens beyond every monitor, without a taskbar
    // button, without taking focus, and without saving its placement.
    private static readonly bool Offscreen = Environment.GetEnvironmentVariable("PROOFMD_OFFSCREEN") == "1";

    private readonly WebView2 _webView;
    private readonly ViewerSession _session;
    private readonly System.Windows.Forms.Timer _reloadTimer = new() { Interval = ReloadDebounceMilliseconds };
    private readonly System.Windows.Forms.Timer _unresolvedTimer = new() { Interval = UnresolvedDebounceMilliseconds };
    private readonly System.Windows.Forms.Timer _viewerStartTimer = new() { Interval = ViewerStartTimeoutMilliseconds };
    private DocumentWatcher? _watcher;
    private bool _viewerLoaded;
    private bool _windowRevealed;

    public MainForm(string? markdownPath)
    {
        Text = "ProofMD";
        MinimumSize = new Size(720, 540);
        BackColor = Color.FromArgb(243, 241, 236);
        ApplyAppIcon();
        // The window stays transparent until the viewer has painted its shell.
        Opacity = 0;
        if (Offscreen)
        {
            StartPosition = FormStartPosition.Manual;
            Bounds = new Rectangle(SystemInformation.VirtualScreen.Right + 200, SystemInformation.VirtualScreen.Top, 1200, 800);
        }
        else
        {
            ApplyInitialWindowState();
        }
        _webView = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = BackColor };
        Controls.Add(_webView);

        _session = new ViewerSession(markdownPath, PostToViewer, ShowError);
        _session.DocumentChanged += ConfigureWatcher;
        _reloadTimer.Tick += async (_, _) =>
        {
            _reloadTimer.Stop();
            await _session.ReloadAsync();
        };
        _unresolvedTimer.Tick += async (_, _) =>
        {
            _unresolvedTimer.Stop();
            await _session.RefreshUnresolvedAsync();
        };
        _viewerStartTimer.Tick += (_, _) =>
        {
            _viewerStartTimer.Stop();
            if (!_windowRevealed) ShowStartupError("The ProofMD viewer did not start.");
        };

        Shown += OnShown;
        FormClosing += OnFormClosing;
    }

    private async void OnShown(object? sender, EventArgs eventArgs)
    {
        try
        {
            await InitializeViewerAsync();
        }
        catch (WebView2RuntimeNotFoundException)
        {
            ShowStartupError("Microsoft Edge WebView2 Runtime is required. Install it from Microsoft, then reopen ProofMD.");
        }
        catch (Exception exception)
        {
            ShowStartupError($"ProofMD could not start.\n\n{exception.Message}");
        }
    }

    private async Task InitializeViewerAsync()
    {
        string viewerDirectory = Path.Combine(AppContext.BaseDirectory, "Viewer");
        if (!File.Exists(Path.Combine(viewerDirectory, "index.html")))
        {
            throw new InvalidOperationException("The installed ProofMD viewer files were not found.");
        }

        // Off screen, WebView2 would treat the window as hidden and stop painting. Its own
        // profile keeps tests away from the user's settings and from a running ProofMD,
        // which WebView2 would refuse to share a profile with under different options.
        var options = new CoreWebView2EnvironmentOptions(
            Offscreen ? "--disable-features=CalculateNativeWinOcclusion" : null);
        CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,
            userDataFolder: Offscreen
                ? Path.Combine(Path.GetTempPath(), "ProofMD-offscreen", "WebView2")
                : Path.Combine(UserProfile.DirectoryPath, "WebView2"),
            options);
        await _webView.EnsureCoreWebView2Async(environment);
        CoreWebView2 core = _webView.CoreWebView2;

        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreDefaultScriptDialogsEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.PermissionRequested += (_, eventArgs) => eventArgs.State = CoreWebView2PermissionState.Deny;
        core.NavigationStarting += OnNavigationStarting;
        core.NewWindowRequested += (_, eventArgs) =>
        {
            eventArgs.Handled = true;
            OpenExternally(eventArgs.Uri);
        };
        core.ContextMenuRequested += (_, eventArgs) =>
        {
            IList<CoreWebView2ContextMenuItem> items = eventArgs.MenuItems;
            for (int index = items.Count - 1; index >= 0; index--)
            {
                if (!ContextMenuItems.Contains(items[index].Name)) items.RemoveAt(index);
            }
        };
        core.NavigationCompleted += OnNavigationCompleted;
        core.DocumentTitleChanged += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(core.DocumentTitle)) Text = core.DocumentTitle;
        };
        core.WebMessageReceived += OnWebMessageReceived;

        core.SetVirtualHostNameToFolderMapping(
            new Uri(ViewerEntryUri).Host,
            viewerDirectory,
            CoreWebView2HostResourceAccessKind.DenyCors);
        core.Navigate(ViewerEntryUri);
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs eventArgs)
    {
        bool isReloadOrHistory = eventArgs.NavigationKind is
            CoreWebView2NavigationKind.Reload or CoreWebView2NavigationKind.BackOrForward;
        switch (NavigationPolicy.Decide(eventArgs.Uri, ViewerEntryUri, isReloadOrHistory))
        {
            case NavigationDecision.Allow:
                return;
            case NavigationDecision.OpenExternally:
                eventArgs.Cancel = true;
                OpenExternally(eventArgs.Uri);
                return;
            default:
                eventArgs.Cancel = true;
                return;
        }
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs eventArgs)
    {
        // Later navigations are only ever cancelled ones; the viewer page stays loaded.
        if (_viewerLoaded) return;
        _viewerLoaded = true;
        if (!eventArgs.IsSuccess)
        {
            ShowStartupError("The ProofMD viewer could not be loaded.");
            return;
        }
        _viewerStartTimer.Start();
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs eventArgs)
    {
        ViewerMessage? message = ViewerMessage.Parse(eventArgs.WebMessageAsJson);
        if (message is null) return;

        // Dropped files are only accessible while this event is being handled.
        string? droppedPath = message.Type == "open-dropped-file" &&
            eventArgs.AdditionalObjects is [CoreWebView2File file]
                ? file.Path
                : null;

        // Handle the message after WebView2's callback returns: dialogs and message boxes
        // must not run a nested message loop inside it.
        BeginInvoke(async () =>
        {
            try
            {
                await HandleMessageAsync(message, droppedPath);
            }
            catch (Exception exception)
            {
                Trace.TraceError("ProofMD could not handle {0}: {1}", message.Type, exception);
            }
        });
    }

    private async Task HandleMessageAsync(ViewerMessage message, string? droppedPath)
    {
        switch (message.Type)
        {
            case "viewer-shell-painted":
                RevealWindow();
                PostToViewer(new { type = "host-window-visible" });
                break;
            case "viewer-window-painted":
                await _session.ViewerReadyAsync();
                break;
            case "open-file-dialog":
                if (ChooseMarkdownFile() is string chosenPath) await _session.OpenAsync(chosenPath);
                break;
            case "open-dropped-file":
                if (droppedPath is not null && MarkdownPaths.IsMarkdown(droppedPath))
                {
                    await _session.OpenAsync(droppedPath);
                }
                break;
            case "open-markdown-link":
                await _session.OpenLinkAsync(message.Href, message.SourceDocument, message.Order, message.ValidPosition);
                break;
            case "open-external-link":
                if (message.Href is not null) OpenExternally(message.Href);
                break;
            case "document-links":
                await _session.UpdateLinkOrdersAsync(message.ContextId, message.Links);
                break;
            case "open-map-node":
                await _session.OpenMapNodeAsync(message.Id, message.ValidPosition);
                break;
            case "go-back":
                await _session.GoBackAsync();
                break;
            case "reset-map":
                await _session.ResetMapAsync();
                break;
            case "set-document-unresolved":
                await _session.SetUnresolvedAsync(message.ContextId, message.Unresolved);
                break;
        }
    }

    private string? ChooseMarkdownFile()
    {
        using var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Filter = "Markdown files (*.md;*.markdown)|*.md;*.markdown|All files (*.*)|*.*",
            Title = "Open Markdown",
        };
        string? currentDirectory = Path.GetDirectoryName(_session.CurrentPath);
        if (currentDirectory is not null && Directory.Exists(currentDirectory))
        {
            dialog.InitialDirectory = currentDirectory;
        }
        return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null;
    }

    private void PostToViewer(object message)
    {
        _webView.CoreWebView2.PostWebMessageAsJson(ViewerMessage.Serialize(message));
    }

    private void ShowError(string message)
    {
        BeginInvoke(() => MessageBox.Show(this, message, "ProofMD", MessageBoxButtons.OK, MessageBoxIcon.Warning));
    }

    private void ConfigureWatcher()
    {
        _watcher?.Dispose();
        _watcher = null;
        if (_session.CurrentPath is null) return;

        _watcher = new DocumentWatcher(_session.CurrentPath, _session.ProofFold?.FoldsDirectory);
        _watcher.ContentChanged += () => RestartOnUiThread(_reloadTimer);
        _watcher.UnresolvedChanged += () => RestartOnUiThread(_unresolvedTimer);
    }

    private void RestartOnUiThread(System.Windows.Forms.Timer timer)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try
        {
            BeginInvoke(() =>
            {
                timer.Stop();
                timer.Start();
            });
        }
        catch (InvalidOperationException)
        {
            // The window closed while the file-system event was being delivered.
        }
    }

    private void OpenExternally(string uri)
    {
        if (!NavigationPolicy.CanOpenExternally(uri, ViewerEntryUri)) return;
        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            Trace.TraceWarning("ProofMD could not open {0}: {1}", uri, exception.Message);
        }
    }

    private void RevealWindow()
    {
        if (_windowRevealed) return;
        _windowRevealed = true;
        _viewerStartTimer.Stop();
        Opacity = 1;
        if (!Offscreen) Activate();
    }

    private void ApplyAppIcon()
    {
        using Stream? stream = typeof(MainForm).Assembly.GetManifestResourceStream("ProofMD.AppIcon.ico");
        if (stream is null) return;
        using var embeddedIcon = new Icon(stream);
        Icon = (Icon)embeddedIcon.Clone();
    }

    private void ApplyInitialWindowState()
    {
        WindowStateData? saved = WindowStateStore.Load();
        if (saved is null || !TryRestoreSavedBounds(saved))
        {
            ApplyLargeDefaultBounds();
            return;
        }
        if (saved.Maximized) WindowState = FormWindowState.Maximized;
    }

    private bool TryRestoreSavedBounds(WindowStateData saved)
    {
        if (saved.Width < MinimumSize.Width || saved.Height < MinimumSize.Height) return false;

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
        StartPosition = FormStartPosition.Manual;
        Bounds = new Rectangle(
            Math.Clamp(saved.X, workingArea.Left, workingArea.Right - width),
            Math.Clamp(saved.Y, workingArea.Top, workingArea.Bottom - height),
            width,
            height);
        return true;
    }

    private void ApplyLargeDefaultBounds()
    {
        Rectangle workingArea = Screen.PrimaryScreen?.WorkingArea ?? Screen.FromPoint(Cursor.Position).WorkingArea;
        int width = Math.Min(Math.Max(MinimumSize.Width, workingArea.Width - 48), Math.Max(1120, (int)(workingArea.Width * 0.82)));
        int height = Math.Min(Math.Max(MinimumSize.Height, workingArea.Height - 48), Math.Max(760, (int)(workingArea.Height * 0.88)));
        StartPosition = FormStartPosition.Manual;
        Bounds = new Rectangle(
            workingArea.Left + (workingArea.Width - width) / 2,
            workingArea.Top + (workingArea.Height - height) / 2,
            width,
            height);
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs eventArgs)
    {
        _watcher?.Dispose();
        _reloadTimer.Dispose();
        _unresolvedTimer.Dispose();
        _viewerStartTimer.Dispose();
        if (Offscreen) return;
        WindowStateStore.Save(
            WindowState == FormWindowState.Normal ? Bounds : RestoreBounds,
            maximized: WindowState == FormWindowState.Maximized);
    }

    protected override bool ShowWithoutActivation => Offscreen;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            if (Offscreen) parameters.ExStyle |= WsExToolWindow | WsExNoActivate;
            return parameters;
        }
    }

    private void ShowStartupError(string message)
    {
        RevealWindow();
        MessageBox.Show(this, message, "ProofMD", MessageBoxButtons.OK, MessageBoxIcon.Error);
        Close();
    }
}
