using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ProofMD;

internal readonly record struct ProofFoldDocument(string Id, string Source);

internal sealed class ProofFoldStructure
{
    public const string ManifestFileName = "prooffold.json";

    private static readonly Regex FoldLinkPattern = new(
        """(?<!!)\[[^\]\r\n]*\]\(\s*(?:<(?<angle>[^>\r\n]+)>|(?<plain>[^\s)\r\n]+))(?:\s+(?:"(?<double>[^"\r\n]*)"|'(?<single>[^'\r\n]*)'|\((?<parenthesized>[^)\r\n]*)\)))?\s*\)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly Dictionary<string, string> _sourcePathsById;
    private readonly Dictionary<string, int> _documentOrders;

    private ProofFoldStructure(
        string rootDirectory,
        string manifestPath,
        string entryPath,
        string entryId,
        string foldsDirectory,
        string fingerprint,
        IReadOnlyList<ProofFoldDocument> folds,
        Dictionary<string, string> sourcePathsById,
        IReadOnlyList<string> documents,
        IReadOnlyList<ExplorationMapEdge> edges,
        Dictionary<string, int> documentOrders)
    {
        RootDirectory = rootDirectory;
        ManifestPath = manifestPath;
        EntryPath = entryPath;
        EntryId = entryId;
        FoldsDirectory = foldsDirectory;
        Fingerprint = fingerprint;
        Folds = folds;
        _sourcePathsById = sourcePathsById;
        Documents = documents;
        Edges = edges;
        _documentOrders = documentOrders;
    }

    public string RootDirectory { get; }
    public string ManifestPath { get; }
    public string EntryPath { get; }
    public string EntryId { get; }
    public string FoldsDirectory { get; }
    public string Fingerprint { get; }
    public IReadOnlyList<ProofFoldDocument> Folds { get; }
    public IReadOnlyList<string> Documents { get; }
    public IReadOnlyList<ExplorationMapEdge> Edges { get; }

    public static ProofFoldStructure? LoadForEntry(string markdownPath)
    {
        string fullMarkdownPath = Path.GetFullPath(markdownPath);
        string? rootDirectory = Path.GetDirectoryName(fullMarkdownPath);
        if (rootDirectory is null) return null;

        string manifestPath = Path.Combine(rootDirectory, ManifestFileName);
        if (!File.Exists(manifestPath)) return null;

        string manifestSource = ReadSharedText(manifestPath);
        JsonDocument manifest;
        try
        {
            manifest = JsonDocument.Parse(manifestSource);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"{ManifestFileName} is not valid JSON.",
                exception);
        }

        using (manifest)
        {
            JsonElement root = manifest.RootElement;
            if (!root.TryGetProperty("formatVersion", out JsonElement versionElement) ||
                !versionElement.TryGetInt32(out int version) ||
                version != 1)
            {
                throw new InvalidDataException(
                    $"{ManifestFileName} must declare formatVersion 1.");
            }

            string entryId = RequiredPath(root, "entry");
            string entryPath = ResolveConfiguredPath(rootDirectory, entryId, "entry");
            if (!PathsEqual(entryPath, fullMarkdownPath)) return null;
            if (!IsMarkdownPath(entryPath) || !File.Exists(entryPath))
            {
                throw new InvalidDataException("The ProofFold entry must be an existing Markdown file.");
            }

            string foldsDirectory = ResolveConfiguredPath(
                rootDirectory,
                RequiredPath(root, "foldsDirectory"),
                "foldsDirectory");
            string notationRegistryPath = ResolveConfiguredPath(
                rootDirectory,
                RequiredPath(root, "notationRegistry"),
                "notationRegistry");
            string referencesDirectory = ResolveConfiguredPath(
                rootDirectory,
                RequiredPath(root, "referencesDirectory"),
                "referencesDirectory");

            if (!File.Exists(notationRegistryPath))
            {
                throw new InvalidDataException("The configured ProofFold notation registry does not exist.");
            }
            if (!Directory.Exists(referencesDirectory))
            {
                throw new InvalidDataException("The configured ProofFold references directory does not exist.");
            }

            var sourcePathsById = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                [CanonicalId(rootDirectory, entryPath)] = entryPath,
            };
            var sourcesByPath = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                [entryPath] = ReadSharedText(entryPath),
            };
            var folds = new List<ProofFoldDocument>();
            IEnumerable<string> foldPaths = Directory.Exists(foldsDirectory)
                ? Directory.EnumerateFiles(foldsDirectory, "*", SearchOption.AllDirectories)
                : [];
            foreach (string foldPath in foldPaths
                .Where(IsMarkdownPath)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                string id = CanonicalId(rootDirectory, foldPath);
                string source = ReadSharedText(foldPath);
                if (!sourcePathsById.TryAdd(id, foldPath))
                {
                    throw new InvalidDataException($"Duplicate ProofFold document path: {id}");
                }
                folds.Add(new ProofFoldDocument(id, source));
                sourcesByPath.Add(foldPath, source);
            }

            BuildMap(
                entryPath,
                foldsDirectory,
                sourcesByPath,
                out IReadOnlyList<string> documents,
                out IReadOnlyList<ExplorationMapEdge> edges,
                out Dictionary<string, int> documentOrders);

            var fingerprintSource = new StringBuilder(manifestSource);
            foreach (ProofFoldDocument fold in folds)
            {
                fingerprintSource
                    .Append('\0')
                    .Append(fold.Id)
                    .Append('\0')
                    .Append(fold.Source);
            }
            string fingerprint = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(fingerprintSource.ToString())));

            return new ProofFoldStructure(
                rootDirectory,
                manifestPath,
                entryPath,
                CanonicalId(rootDirectory, entryPath),
                foldsDirectory,
                fingerprint,
                folds,
                sourcePathsById,
                documents,
                edges,
                documentOrders);
        }
    }

    public bool ContainsDocument(string path)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch
        {
            return false;
        }

        return _documentOrders.ContainsKey(fullPath);
    }

    public int GetDocumentOrder(string path)
    {
        return _documentOrders.TryGetValue(Path.GetFullPath(path), out int order)
            ? order
            : int.MaxValue;
    }

    public bool TryResolveSourceDocument(string documentId, out string path)
    {
        return _sourcePathsById.TryGetValue(documentId, out path!);
    }

    public bool AffectsRenderedDocument(string path)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch
        {
            return false;
        }

        return PathsEqual(fullPath, ManifestPath) ||
            PathsEqual(fullPath, EntryPath) ||
            IsWithinDirectory(FoldsDirectory, fullPath);
    }

    private static void BuildMap(
        string entryPath,
        string foldsDirectory,
        IReadOnlyDictionary<string, string> sourcesByPath,
        out IReadOnlyList<string> documents,
        out IReadOnlyList<ExplorationMapEdge> edges,
        out Dictionary<string, int> documentOrders)
    {
        var discovered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<string>();
        var orderedDocuments = new List<string>();
        var orderedEdges = new List<ExplorationMapEdge>();

        void Discover(string path)
        {
            if (!discovered.Add(path)) return;
            orderedDocuments.Add(path);
            pending.Enqueue(path);
        }

        Discover(entryPath);
        while (pending.Count > 0)
        {
            string sourcePath = pending.Dequeue();
            if (!sourcesByPath.TryGetValue(sourcePath, out string? source)) continue;

            int linkOrder = 0;
            foreach (Match match in FoldLinkPattern.Matches(source))
            {
                string title = match.Groups["double"].Success
                    ? match.Groups["double"].Value
                    : match.Groups["single"].Success
                        ? match.Groups["single"].Value
                        : match.Groups["parenthesized"].Value;
                if (!title.Trim().Equals("fold", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string target = match.Groups["angle"].Success
                    ? match.Groups["angle"].Value
                    : match.Groups["plain"].Value;
                string? targetPath = ResolveFoldTarget(sourcePath, target);
                if (targetPath is null ||
                    !IsWithinDirectory(foldsDirectory, targetPath) ||
                    !sourcesByPath.ContainsKey(targetPath))
                {
                    continue;
                }

                orderedEdges.Add(new ExplorationMapEdge(
                    sourcePath,
                    targetPath,
                    linkOrder++));
                Discover(targetPath);
            }
        }

        documents = orderedDocuments;
        edges = orderedEdges;
        documentOrders = orderedDocuments
            .Select((path, order) => (path, order))
            .ToDictionary(
                item => item.path,
                item => item.order,
                StringComparer.OrdinalIgnoreCase);
    }

    private static string? ResolveFoldTarget(string sourcePath, string href)
    {
        try
        {
            int suffixStart = href.IndexOfAny(['?', '#']);
            string encodedPath = suffixStart >= 0 ? href[..suffixStart] : href;
            if (string.IsNullOrWhiteSpace(encodedPath)) return null;

            string relativePath = Uri.UnescapeDataString(encodedPath)
                .Replace('/', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(relativePath) || !IsMarkdownPath(relativePath))
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

    private static string RequiredPath(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement element) ||
            element.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(element.GetString()))
        {
            throw new InvalidDataException(
                $"{ManifestFileName} must declare a non-empty {propertyName} path.");
        }

        string value = element.GetString()!;
        if (value.Contains('\\'))
        {
            throw new InvalidDataException(
                $"ProofFold {propertyName} must use forward slashes.");
        }
        return value;
    }

    private static string ResolveConfiguredPath(
        string rootDirectory,
        string relativePath,
        string propertyName)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException(
                $"ProofFold {propertyName} must be relative to {ManifestFileName}.");
        }

        string path = Path.GetFullPath(Path.Combine(
            rootDirectory,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsWithinDirectory(rootDirectory, path))
        {
            throw new InvalidDataException(
                $"ProofFold {propertyName} escapes the document root.");
        }
        return path;
    }

    private static string CanonicalId(string rootDirectory, string path)
    {
        return Path.GetRelativePath(rootDirectory, path)
            .Replace(Path.DirectorySeparatorChar, '/');
    }

    private static bool IsWithinDirectory(string directory, string path)
    {
        string relative = Path.GetRelativePath(directory, path);
        return !Path.IsPathRooted(relative) &&
            !relative.Equals("..", StringComparison.Ordinal) &&
            !relative.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal);
    }

    private static bool IsMarkdownPath(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".markdown", StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathsEqual(string firstPath, string secondPath)
    {
        return Path.GetFullPath(firstPath).Equals(
            Path.GetFullPath(secondPath),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadSharedText(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
