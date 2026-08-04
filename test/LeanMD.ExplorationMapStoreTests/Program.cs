using System.Text.Json;
using LeanMD;

string testRoot = Path.Combine(
    Path.GetTempPath(),
    $"LeanMD-exploration-map-tests-{Guid.NewGuid():N}");

try
{
    string workspace = Path.Combine(testRoot, "workspace");
    string metadata = Path.Combine(workspace, ".leanmd");
    string rootDocument = WriteDocument(workspace, "root.md");
    string branchA = WriteDocument(workspace, "a/a.md");
    string branchB = WriteDocument(workspace, "b/b.md");
    string intermediate = WriteDocument(workspace, "a/x/x.md");
    string deepTarget = WriteDocument(workspace, "a/x/target.md");
    Directory.CreateDirectory(metadata);

    string unresolvedSidecar = UnresolvedStateStore.SidecarPath(branchA);
    Assert(unresolvedSidecar == Path.ChangeExtension(branchA, ".unresolved"),
        "The unresolved marker should replace the Markdown extension.");
    Assert(!UnresolvedStateStore.IsUnresolved(branchA),
        "A document without a marker should start resolved.");
    UnresolvedStateStore.SetUnresolved(branchA, unresolved: true);
    Assert(UnresolvedStateStore.IsUnresolved(branchA) && File.Exists(unresolvedSidecar),
        "Setting unresolved should create the adjacent marker.");
    Assert(File.ReadAllText(unresolvedSidecar) == "status: unresolved\n",
        "New markers should use the documented readable contents.");
    UnresolvedStateStore.SetUnresolved(branchA, unresolved: true);
    Assert(UnresolvedStateStore.IsUnresolved(branchA),
        "Setting an existing unresolved marker should be idempotent.");
    UnresolvedStateStore.SetUnresolved(branchA, unresolved: false);
    Assert(!File.Exists(unresolvedSidecar),
        "Clearing unresolved should remove the marker.");

    using (JsonDocument positionMessage = JsonDocument.Parse(
        """{"position":{"sourceLine":17,"offset":-12.5,"scrollY":640.25}}"""))
    {
        Assert(DocumentPosition.TryRead(
                positionMessage.RootElement,
                "position",
                out DocumentPosition position) &&
            position.SourceLine == 17 &&
            position.Offset == -12.5 &&
            position.ScrollY == 640.25,
            "Navigation positions should preserve their source anchor and pixel fallback.");
    }
    using (JsonDocument invalidPositionMessage = JsonDocument.Parse(
        """{"position":{"sourceLine":0,"offset":0,"scrollY":-1}}"""))
    {
        Assert(!DocumentPosition.TryRead(
                invalidPositionMessage.RootElement,
                "position",
                out _),
            "Invalid navigation positions should be rejected.");
    }

    WriteDependencies(
        metadata,
        "root.md",
        ("root.md", "a/a.md"),
        ("root.md", "b/b.md"),
        ("a/a.md", "a/x/x.md"),
        ("a/x/x.md", "a/x/target.md"));

    LeanMdStructure? structure = LeanMdStructure.Load(metadata);
    Assert(structure is not null, "A valid dependencies manifest should load.");
    Assert(structure!.Documents.Count == 5 &&
        structure.Documents.Contains(rootDocument) &&
        structure.Documents.Contains(deepTarget),
        "The structure should expose every document for the complete exploration map.");
    Assert(structure.Edges.Count == 4 &&
        structure.Edges.Any(edge =>
            PathsEqual(edge.From, rootDocument) && PathsEqual(edge.To, branchB)) &&
        structure.Edges.Any(edge =>
            PathsEqual(edge.From, intermediate) && PathsEqual(edge.To, deepTarget)),
        "The structure should expose every dependency edge for the complete exploration map.");
    Assert(structure.GetEdgeOrder(rootDocument, branchA) == 0 &&
        structure.GetEdgeOrder(rootDocument, branchB) == 1,
        "Structure edges should retain their source-link order.");

    WriteDependencies(
        metadata,
        "root.md",
        ("root.md", "b/b.md"),
        ("root.md", "a/a.md"),
        ("a/a.md", "a/x/x.md"),
        ("a/x/x.md", "a/x/target.md"));
    LeanMdStructure? reorderedStructure = LeanMdStructure.Load(metadata);
    Assert(reorderedStructure is not null &&
        reorderedStructure.GetEdgeOrder(rootDocument, branchB) == 0 &&
        reorderedStructure.GetEdgeOrder(rootDocument, branchA) == 1 &&
        reorderedStructure.GetDocumentOrder(branchB) <
            reorderedStructure.GetDocumentOrder(branchA),
        "Reordering source links should deterministically reorder map branches.");
    WriteDependencies(
        metadata,
        "root.md",
        ("root.md", "a/a.md"),
        ("root.md", "b/b.md"),
        ("a/a.md", "a/x/x.md"),
        ("a/x/x.md", "a/x/target.md"));

    string flatWorkspace = Path.Combine(testRoot, "flat-workspace");
    string flatMetadata = Path.Combine(flatWorkspace, ".leanmd");
    string flatRoot = WriteDocument(flatWorkspace, "root.md");
    string flatBranch = WriteDocument(flatWorkspace, "nodes/branch.md");
    string flatShared = WriteDocument(flatWorkspace, "nodes/shared.md");
    WriteFlatDependencies(
        flatMetadata,
        "root.md",
        ("root.md", "nodes/branch.md"),
        ("root.md", "nodes/shared.md"),
        ("nodes/branch.md", "nodes/shared.md"));

    LeanMdStructure? flatStructure = LeanMdStructure.Load(flatMetadata);
    Assert(flatStructure is not null &&
        flatStructure.ContainsDocument(flatRoot) &&
        flatStructure.ContainsDocument(flatBranch) &&
        flatStructure.ContainsDocument(flatShared) &&
        flatStructure.Edges.Count == 3 &&
        flatStructure.Edges.Any(edge =>
            PathsEqual(edge.From, flatRoot) && PathsEqual(edge.To, flatShared)),
        "The desktop structure loader should accept a versioned flat workspace.");

    var mapState = new ExplorationMapState(
        rootDocument,
        [branchA, branchB, deepTarget]);
    ExplorationMapStore.Save(metadata, mapState);

    string statePath = Path.Combine(metadata, ExplorationMapStore.StateFileName);
    Assert(File.Exists(statePath), "Save should create exploration-map.json.");
    string json = File.ReadAllText(statePath);
    Assert(!json.Contains(workspace, StringComparison.OrdinalIgnoreCase),
        "Persisted paths must be relative to the document set.");
    Assert(json.Contains("\"schemaVersion\": 3", StringComparison.Ordinal) &&
        !json.Contains("\"nodes\"", StringComparison.Ordinal) &&
        !json.Contains("\"edges\"", StringComparison.Ordinal) &&
        !json.Contains("dependenciesFingerprint", StringComparison.Ordinal),
        "The current state schema should persist only root and visited paths.");

    ExplorationMapState? loaded = ExplorationMapStore.Load(metadata);
    Assert(loaded is not null, "The document set should restore the saved map.");
    Assert(loaded!.VisitedNodes.Count == 3 &&
        loaded.VisitedNodes.Any(path => PathsEqual(path, branchB)) &&
        !loaded.VisitedNodes.Any(path => PathsEqual(path, intermediate)),
        "Visited and unexplored nodes must remain distinguishable after restore.");

    string relocatedWorkspace = Path.Combine(testRoot, "relocated-workspace");
    Directory.Move(workspace, relocatedWorkspace);
    string relocatedMetadata = Path.Combine(relocatedWorkspace, ".leanmd");
    loaded = ExplorationMapStore.Load(relocatedMetadata);
    Assert(loaded is not null &&
        PathsEqual(loaded.RootPath, Path.Combine(relocatedWorkspace, "root.md")),
        "Relative state should survive moving the document set.");

    string inserted = WriteDocument(relocatedWorkspace, "inserted/inserted.md");
    WriteDependencies(
        relocatedMetadata,
        "root.md",
        ("root.md", "a/a.md"),
        ("a/a.md", "inserted/inserted.md"),
        ("inserted/inserted.md", "b/b.md"),
        ("a/a.md", "a/x/x.md"),
        ("a/x/x.md", "a/x/target.md"));
    LeanMdStructure? changedStructure = LeanMdStructure.Load(relocatedMetadata);
    Assert(changedStructure is not null &&
        changedStructure.Fingerprint != structure.Fingerprint,
        "Changing dependencies should produce a new structure fingerprint.");
    Assert(changedStructure!.Documents.Contains(inserted) &&
        changedStructure.Edges.Any(edge =>
            PathsEqual(edge.From, Path.Combine(relocatedWorkspace, "a", "a.md")) &&
            PathsEqual(edge.To, inserted)) &&
        changedStructure.Edges.Any(edge =>
            PathsEqual(edge.From, inserted) &&
            PathsEqual(edge.To, Path.Combine(relocatedWorkspace, "b", "b.md"))),
        "An inserted dependency should appear immediately in the complete structure.");

    File.WriteAllText(
        statePath,
        JsonSerializer.Serialize(new
        {
            schemaVersion = 2,
            dependenciesFingerprint = "legacy",
            root = "root.md",
            nodes = new[] { "root.md", "a/a.md", "b/b.md" },
            edges = Array.Empty<object>(),
            visited = new[] { "a/a.md", "b/b.md" },
            updatedAt = DateTimeOffset.Now,
        }));
    loaded = ExplorationMapStore.Load(relocatedMetadata);
    Assert(loaded is not null && loaded.VisitedNodes.Count == 2,
        "Schema 2 exploration state should remain readable after simplification.");

    File.WriteAllText(statePath = Path.Combine(
        relocatedMetadata,
        ExplorationMapStore.StateFileName), "{ invalid");
    Assert(ExplorationMapStore.Load(relocatedMetadata) is null,
        "Malformed state should fall back to a new projection.");

    string outsideDocument = WriteDocument(testRoot, "outside.md");
    File.WriteAllText(
        statePath,
        JsonSerializer.Serialize(new
        {
            schemaVersion = 3,
            root = "../../outside.md",
            visited = Array.Empty<string>(),
        }));
    Assert(File.Exists(outsideDocument) &&
        ExplorationMapStore.Load(relocatedMetadata) is null,
        "Persisted paths must not escape the document set.");

    TestProofFoldStructure(testRoot);

    Console.WriteLine("LeanMD and ProofFold desktop structure tests passed.");
}
finally
{
    if (Directory.Exists(testRoot)) Directory.Delete(testRoot, recursive: true);
}

static void WriteDependencies(
    string metadataDirectory,
    string root,
    params (string From, string To)[] edges)
{
    Directory.CreateDirectory(metadataDirectory);
    var nextOrderBySource = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    object[] orderedEdges = edges.Select(edge =>
    {
        int order = nextOrderBySource.GetValueOrDefault(edge.From);
        nextOrderBySource[edge.From] = order + 1;
        return (object)new
        {
            from = edge.From,
            to = edge.To,
            kind = "why",
            order,
        };
    }).ToArray();
    File.WriteAllText(
        Path.Combine(metadataDirectory, "dependencies.json"),
        JsonSerializer.Serialize(new
        {
            root,
            edges = orderedEdges,
        }, new JsonSerializerOptions { WriteIndented = true }));
}

static void WriteFlatDependencies(
    string metadataDirectory,
    string root,
    params (string From, string To)[] edges)
{
    Directory.CreateDirectory(metadataDirectory);
    var nextOrderBySource = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    object[] orderedEdges = edges.Select(edge =>
    {
        int order = nextOrderBySource.GetValueOrDefault(edge.From);
        nextOrderBySource[edge.From] = order + 1;
        return (object)new
        {
            from = edge.From,
            to = edge.To,
            kind = "why",
            order,
        };
    }).ToArray();
    File.WriteAllText(
        Path.Combine(metadataDirectory, "dependencies.json"),
        JsonSerializer.Serialize(new
        {
            formatVersion = 2,
            layout = "flat",
            root,
            edges = orderedEdges,
        }, new JsonSerializerOptions { WriteIndented = true }));
}

static void TestProofFoldStructure(string testRoot)
{
    string root = Path.Combine(testRoot, "proof-fold");
    string entry = WriteDocument(root, "main.md");
    string instructions = WriteDocument(root, "AGENTS.md");
    string firstFold = WriteDocument(root, "folds/first.md");
    string nestedFold = WriteDocument(root, "folds/nested/second.md");
    Directory.CreateDirectory(Path.Combine(root, "references"));
    File.WriteAllText(Path.Combine(root, "notation.yaml"), "version: 1\nsymbols: []\n");
    WriteProofFoldManifest(root, "folds");

    ProofFoldStructure? structure = ProofFoldStructure.LoadForEntry(entry);
    Assert(structure is not null,
        "A valid adjacent prooffold.json should identify its configured entry.");
    Assert(structure!.EntryId == "main.md" &&
        structure.Folds.Count == 2 &&
        structure.Folds.Any(fold => fold.Id == "folds/first.md") &&
        structure.Folds.Any(fold => fold.Id == "folds/nested/second.md"),
        "ProofFold should load every Markdown fragment under its folds directory.");
    Assert(structure.TryResolveSourceDocument("folds/first.md", out string resolvedFold) &&
        PathsEqual(resolvedFold, firstFold),
        "ProofFold source ids should resolve to known entry or fold documents.");
    Assert(ProofFoldStructure.LoadForEntry(instructions) is null &&
        ProofFoldStructure.LoadForEntry(firstFold) is null,
        "Only the configured entry beside prooffold.json should enter ProofFold mode.");
    Assert(structure.AffectsRenderedDocument(nestedFold) &&
        !structure.AffectsRenderedDocument(instructions),
        "Only the manifest, entry, and folds should trigger a ProofFold rerender.");

    File.AppendAllText(firstFold, "Changed\n");
    ProofFoldStructure? changed = ProofFoldStructure.LoadForEntry(entry);
    Assert(changed is not null && changed.Fingerprint != structure.Fingerprint,
        "Changing a fold should change the ProofFold render fingerprint.");

    WriteProofFoldManifest(root, "../outside");
    bool rejectedEscape = false;
    try
    {
        ProofFoldStructure.LoadForEntry(entry);
    }
    catch (InvalidDataException)
    {
        rejectedEscape = true;
    }
    Assert(rejectedEscape,
        "ProofFold component paths must not escape the manifest directory.");
}

static void WriteProofFoldManifest(string root, string foldsDirectory)
{
    File.WriteAllText(
        Path.Combine(root, "prooffold.json"),
        JsonSerializer.Serialize(new
        {
            formatVersion = 1,
            entry = "main.md",
            foldsDirectory,
            notationRegistry = "notation.yaml",
            referencesDirectory = "references",
        }, new JsonSerializerOptions { WriteIndented = true }));
}

static string WriteDocument(string root, string relativePath)
{
    string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, $"# {Path.GetFileNameWithoutExtension(path)}\n");
    return path;
}

static bool PathsEqual(string left, string right)
{
    return Path.GetFullPath(left).Equals(
        Path.GetFullPath(right),
        StringComparison.OrdinalIgnoreCase);
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
