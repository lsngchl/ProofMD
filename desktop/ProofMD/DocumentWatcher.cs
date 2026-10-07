namespace ProofMD;

/// <summary>
/// Reports changes to the open document, its ProofFold manifest and folds, and its
/// unresolved marker. Events arrive on thread-pool threads.
/// </summary>
internal sealed class DocumentWatcher : IDisposable
{
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly string _documentPath;
    private readonly string _manifestPath;
    private readonly string _unresolvedPath;
    private readonly string? _foldsDirectory;

    public DocumentWatcher(string documentPath, string? foldsDirectory)
    {
        _documentPath = documentPath;
        _foldsDirectory = foldsDirectory;
        string directory = Path.GetDirectoryName(documentPath)!;
        _manifestPath = Path.Combine(directory, ProofFoldStructure.ManifestFileName);
        _unresolvedPath = UnresolvedStateStore.SidecarPath(documentPath);

        // The document folder also reveals a folds folder being created or removed.
        bool foldsInDocumentFolder = MarkdownPaths.AreEqual(foldsDirectory, directory);
        Watch(directory, includeSubdirectories: foldsInDocumentFolder);
        if (foldsDirectory is not null && !foldsInDocumentFolder && Directory.Exists(foldsDirectory))
        {
            Watch(foldsDirectory, includeSubdirectories: true);
        }
    }

    public event Action? ContentChanged;
    public event Action? UnresolvedChanged;

    public void Dispose()
    {
        foreach (FileSystemWatcher watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        _watchers.Clear();
    }

    private void Watch(string directory, bool includeSubdirectories)
    {
        try
        {
            var watcher = new FileSystemWatcher(directory)
            {
                IncludeSubdirectories = includeSubdirectories,
                NotifyFilter = NotifyFilters.FileName |
                    NotifyFilters.DirectoryName |
                    NotifyFilters.LastWrite |
                    NotifyFilters.Size |
                    NotifyFilters.CreationTime,
            };
            watcher.Changed += OnChanged;
            watcher.Created += OnChanged;
            watcher.Deleted += OnChanged;
            watcher.Renamed += OnChanged;
            // A buffer overflow loses events, so assume the document changed.
            watcher.Error += (_, _) => ContentChanged?.Invoke();
            watcher.EnableRaisingEvents = true;
            _watchers.Add(watcher);
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // Some locations cannot be watched; the document still opens without live reload.
        }
    }

    private void OnChanged(object sender, FileSystemEventArgs eventArgs)
    {
        string? oldPath = (eventArgs as RenamedEventArgs)?.OldFullPath;
        if (AffectsContent(eventArgs.FullPath) || (oldPath is not null && AffectsContent(oldPath)))
        {
            ContentChanged?.Invoke();
        }
        if (MarkdownPaths.AreEqual(eventArgs.FullPath, _unresolvedPath) ||
            MarkdownPaths.AreEqual(oldPath, _unresolvedPath))
        {
            UnresolvedChanged?.Invoke();
        }
    }

    private bool AffectsContent(string path)
    {
        return MarkdownPaths.AreEqual(path, _documentPath) ||
            MarkdownPaths.AreEqual(path, _manifestPath) ||
            (_foldsDirectory is not null &&
                (MarkdownPaths.AreEqual(path, _foldsDirectory) || MarkdownPaths.IsWithin(_foldsDirectory, path)));
    }
}
