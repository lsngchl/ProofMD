using System.Text.Json;

namespace ProofMD;

internal readonly record struct ProofFoldDocument(string Id, string Source);

/// <summary>
/// The fold sources of a ProofFold document. The viewer renders folds and derives the fold
/// map from these sources with the same Markdown parser, so the two always agree.
/// </summary>
internal sealed class ProofFoldStructure
{
    public const string ManifestFileName = "prooffold.json";

    private readonly Dictionary<string, string> _pathsById;

    private ProofFoldStructure(
        string rootDirectory,
        string entryId,
        string foldsDirectory,
        IReadOnlyList<ProofFoldDocument> folds,
        Dictionary<string, string> pathsById)
    {
        RootDirectory = rootDirectory;
        EntryId = entryId;
        FoldsDirectory = foldsDirectory;
        Folds = folds;
        _pathsById = pathsById;
    }

    public string RootDirectory { get; }
    public string EntryId { get; }
    public string FoldsDirectory { get; }
    public IReadOnlyList<ProofFoldDocument> Folds { get; }

    /// <summary>
    /// Returns null when the document is ordinary Markdown, including when its manifest is
    /// unreadable or invalid: ProofFold only augments a document that can always be shown.
    /// </summary>
    public static ProofFoldStructure? TryLoadForEntry(string markdownPath)
    {
        try
        {
            return LoadForEntry(markdownPath);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or
                InvalidDataException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    public static ProofFoldStructure? LoadForEntry(string markdownPath)
    {
        string rootDirectory = Path.GetDirectoryName(markdownPath)
            ?? throw new ArgumentException("A document path is required.", nameof(markdownPath));
        string manifestPath = Path.Combine(rootDirectory, ManifestFileName);
        if (!File.Exists(manifestPath)) return null;

        JsonElement manifest;
        try
        {
            using JsonDocument document = JsonDocument.Parse(ReadSharedText(manifestPath));
            manifest = document.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{ManifestFileName} is not valid JSON.", exception);
        }

        if (manifest.ValueKind != JsonValueKind.Object ||
            !manifest.TryGetProperty("formatVersion", out JsonElement version) ||
            version.ValueKind != JsonValueKind.Number ||
            !version.TryGetInt32(out int formatVersion) ||
            formatVersion != 1)
        {
            throw new InvalidDataException($"{ManifestFileName} must declare formatVersion 1.");
        }

        string entryPath = ResolveConfiguredPath(rootDirectory, RequiredPath(manifest, "entry"), "entry");
        if (!MarkdownPaths.AreEqual(entryPath, Path.GetFullPath(markdownPath))) return null;
        if (!MarkdownPaths.IsMarkdown(entryPath) || !File.Exists(entryPath))
        {
            throw new InvalidDataException("The ProofFold entry must be an existing Markdown file.");
        }

        string foldsDirectory = ResolveConfiguredPath(
            rootDirectory,
            manifest.TryGetProperty("foldsDirectory", out _) ? RequiredPath(manifest, "foldsDirectory") : "folds",
            "foldsDirectory");

        string entryId = DocumentId(rootDirectory, MarkdownPaths.Canonicalize(entryPath));
        var pathsById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [entryId] = entryPath,
        };
        var folds = new List<ProofFoldDocument>();
        IEnumerable<string> foldPaths = Directory.Exists(foldsDirectory)
            ? Directory.EnumerateFiles(foldsDirectory, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
            })
            : [];
        foreach (string foldPath in foldPaths
            .Where(MarkdownPaths.IsMarkdown)
            .Order(StringComparer.OrdinalIgnoreCase))
        {
            string id = DocumentId(rootDirectory, foldPath);
            if (pathsById.ContainsKey(id)) continue;
            try
            {
                folds.Add(new ProofFoldDocument(id, ReadSharedText(foldPath)));
                pathsById.Add(id, foldPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // The viewer reports a missing fold; the other folds stay usable.
            }
        }

        return new ProofFoldStructure(rootDirectory, entryId, foldsDirectory, folds, pathsById);
    }

    public bool TryResolveDocument(string documentId, out string path)
    {
        return _pathsById.TryGetValue(documentId, out path!);
    }

    public bool HasSameContent(ProofFoldStructure? other)
    {
        return other is not null &&
            EntryId == other.EntryId &&
            FoldsDirectory == other.FoldsDirectory &&
            Folds.SequenceEqual(other.Folds);
    }

    public object ToMessage()
    {
        return new
        {
            entry = EntryId,
            folds = Folds.Select(fold => new { path = fold.Id, source = fold.Source }),
        };
    }

    private static string RequiredPath(JsonElement manifest, string propertyName)
    {
        if (!manifest.TryGetProperty(propertyName, out JsonElement element) ||
            element.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(element.GetString()))
        {
            throw new InvalidDataException($"{ManifestFileName} must declare a non-empty {propertyName} path.");
        }

        string value = element.GetString()!;
        if (value.Contains('\\'))
        {
            throw new InvalidDataException($"ProofFold {propertyName} must use forward slashes.");
        }
        return value;
    }

    private static string ResolveConfiguredPath(string rootDirectory, string relativePath, string propertyName)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException($"ProofFold {propertyName} must be relative to {ManifestFileName}.");
        }

        string path = Path.GetFullPath(Path.Combine(rootDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!MarkdownPaths.IsWithin(rootDirectory, path))
        {
            throw new InvalidDataException($"ProofFold {propertyName} escapes the document folder.");
        }
        return path;
    }

    private static string DocumentId(string rootDirectory, string path)
    {
        return Path.GetRelativePath(rootDirectory, path).Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string ReadSharedText(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
