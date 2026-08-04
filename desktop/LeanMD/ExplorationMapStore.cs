using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LeanMD;

internal sealed record ExplorationMapState(
    string RootPath,
    IReadOnlyList<string> VisitedNodes);

internal static class ExplorationMapStore
{
    internal const string StateFileName = "exploration-map.json";
    private const int CurrentSchemaVersion = 3;
    private const int MaximumStateFileBytes = 4 * 1024 * 1024;
    private const int MaximumVisitedCount = 10_000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    private sealed class PersistedMapState
    {
        public int SchemaVersion { get; set; }
        public string? Root { get; set; }
        public List<string>? Nodes { get; set; }
        public List<string>? Visited { get; set; }
    }

    public static ExplorationMapState? Load(string metadataDirectory)
    {
        try
        {
            string? workspaceRoot = Directory.GetParent(metadataDirectory)?.FullName;
            if (workspaceRoot is null) return null;

            string statePath = Path.Combine(metadataDirectory, StateFileName);
            var stateFile = new FileInfo(statePath);
            if (!stateFile.Exists || stateFile.Length > MaximumStateFileBytes) return null;

            PersistedMapState? persisted = JsonSerializer.Deserialize<PersistedMapState>(
                File.ReadAllText(statePath),
                JsonOptions);
            if (persisted is null ||
                persisted.SchemaVersion is < 1 or > CurrentSchemaVersion ||
                string.IsNullOrWhiteSpace(persisted.Root))
            {
                return null;
            }

            string? rootPath = LeanMdStructure.ResolveDocumentPath(
                workspaceRoot,
                persisted.Root);
            if (rootPath is null) return null;

            IReadOnlyList<string> visitedPaths;
            if (persisted.SchemaVersion == 1)
            {
                if (persisted.Nodes is null ||
                    persisted.Nodes.Count is 0 or > MaximumVisitedCount)
                {
                    return null;
                }

                List<string>? legacyNodes = ResolveUniquePaths(
                    workspaceRoot,
                    persisted.Nodes);
                if (legacyNodes is null ||
                    !legacyNodes.Contains(rootPath, StringComparer.OrdinalIgnoreCase))
                {
                    return null;
                }
                visitedPaths = legacyNodes;
            }
            else
            {
                if (persisted.Visited?.Count > MaximumVisitedCount)
                {
                    return null;
                }

                List<string>? visited = ResolveUniquePaths(
                    workspaceRoot,
                    persisted.Visited ?? []);
                if (visited is null) return null;
                visitedPaths = visited;
            }

            return new ExplorationMapState(
                rootPath,
                visitedPaths);
        }
        catch
        {
            // Persisted exploration state is optional and untrusted.
            return null;
        }
    }

    public static void Save(string metadataDirectory, ExplorationMapState state)
    {
        string? temporaryPath = null;
        try
        {
            string? workspaceRoot = Directory.GetParent(metadataDirectory)?.FullName;
            if (workspaceRoot is null ||
                state.VisitedNodes.Count > MaximumVisitedCount)
            {
                return;
            }

            string? root = LeanMdStructure.RelativeDocumentPath(
                workspaceRoot,
                state.RootPath);
            if (root is null) return;

            List<string>? visited = RelativeUniquePaths(workspaceRoot, state.VisitedNodes);
            if (visited is null) return;

            var persisted = new PersistedMapState
            {
                SchemaVersion = CurrentSchemaVersion,
                Root = root,
                Visited = visited,
            };

            string json = JsonSerializer.Serialize(persisted, JsonOptions) + "\n";
            string statePath = Path.Combine(metadataDirectory, StateFileName);
            temporaryPath = Path.Combine(
                metadataDirectory,
                $".exploration-map.{Guid.NewGuid():N}.tmp");
            File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));
            File.Move(temporaryPath, statePath, overwrite: true);
        }
        catch
        {
            // Map persistence must never prevent document navigation.
        }
        finally
        {
            try
            {
                if (temporaryPath is not null && File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch
            {
                // A later save can replace an abandoned temporary file.
            }
        }
    }

    private static List<string>? ResolveUniquePaths(
        string workspaceRoot,
        IEnumerable<string> paths)
    {
        var resolved = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in paths)
        {
            string? fullPath = LeanMdStructure.ResolveDocumentPath(workspaceRoot, path);
            if (fullPath is null || !seen.Add(fullPath)) return null;
            resolved.Add(fullPath);
        }
        return resolved;
    }

    private static List<string>? RelativeUniquePaths(
        string workspaceRoot,
        IEnumerable<string> paths)
    {
        var relative = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in paths)
        {
            string? relativePath = LeanMdStructure.RelativeDocumentPath(workspaceRoot, path);
            if (relativePath is null || !seen.Add(relativePath)) return null;
            relative.Add(relativePath);
        }
        return relative;
    }
}
