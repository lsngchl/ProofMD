namespace ProofMD;

internal readonly record struct ExplorationMapEdge(string From, string To, int Order);

/// <summary>
/// The documents reached from a starting document by following links, in discovery order.
/// Paths are canonical (see <see cref="MarkdownPaths.Canonicalize"/>), so plain comparisons suffice.
/// </summary>
internal sealed class ExplorationMap
{
    private readonly List<string> _nodes = [];
    private readonly List<ExplorationMapEdge> _edges = [];

    public int SessionId { get; private set; }
    public string? Root { get; private set; }
    public string? Previous { get; private set; }
    public IReadOnlyList<string> Nodes => _nodes;
    public IReadOnlyList<ExplorationMapEdge> Edges => _edges;

    public bool Contains(string path) => _nodes.Contains(path, StringComparer.OrdinalIgnoreCase);

    public void Start(string root)
    {
        SessionId++;
        Root = root;
        Previous = null;
        _nodes.Clear();
        _nodes.Add(root);
        _edges.Clear();
    }

    /// <summary>Records that <paramref name="target"/> was reached from <paramref name="source"/>.</summary>
    public void Discover(string source, string target, int? linkOrder)
    {
        if (!Contains(source)) return;

        if (!Contains(target))
        {
            _nodes.Add(target);
            _edges.Add(new ExplorationMapEdge(source, target, linkOrder ?? int.MaxValue));
        }
    }

    public void SetPrevious(string? previous, string current)
    {
        Previous = previous is not null && !MarkdownPaths.AreEqual(previous, current) && Contains(previous)
            ? previous
            : null;
    }

    /// <summary>Orders a document's outgoing edges by where their links appear in it.</summary>
    public bool UpdateLinkOrders(string source, IReadOnlyDictionary<string, int> orderByTarget)
    {
        bool changed = false;
        for (int index = 0; index < _edges.Count; index++)
        {
            ExplorationMapEdge edge = _edges[index];
            if (MarkdownPaths.AreEqual(edge.From, source) &&
                orderByTarget.TryGetValue(edge.To, out int order) &&
                edge.Order != order)
            {
                _edges[index] = edge with { Order = order };
                changed = true;
            }
        }
        return changed;
    }

    public object ToMessage(string? current, Func<string, bool> isUnresolved)
    {
        string? rootDirectory = Root is null ? null : Path.GetDirectoryName(Root);
        return new
        {
            type = "map-state",
            sessionId = SessionId,
            root = Root,
            current,
            previous = Previous,
            nodes = _nodes.Select((path, order) => new
            {
                id = path,
                label = Path.GetFileNameWithoutExtension(path).Replace('_', ' '),
                detail = MarkdownPaths.AreEqual(path, Root)
                    ? "Starting document"
                    : rootDirectory is null
                        ? Path.GetFileName(path)
                        : Path.GetRelativePath(rootDirectory, path).Replace('\\', '/'),
                unresolved = isUnresolved(path),
                order,
            }),
            edges = _edges.Select(edge => new { from = edge.From, to = edge.To, order = edge.Order }),
        };
    }
}
