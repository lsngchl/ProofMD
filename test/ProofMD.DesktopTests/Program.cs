using System.Text.Json;
using ProofMD;

string testRoot = Path.Combine(
    Path.GetTempPath(),
    $"ProofMD-desktop-tests-{Guid.NewGuid():N}");

try
{
    string workspace = Path.Combine(testRoot, "workspace");
    string rootDocument = WriteDocument(workspace, "root.md");
    string branchA = WriteDocument(workspace, "a/a.md");

    Assert(ProofFoldStructure.LoadForEntry(rootDocument) is null &&
        ProofFoldStructure.LoadForEntry(branchA) is null,
        "Ordinary Markdown documents should open without a format manifest.");

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
    using (JsonDocument fallbackPositionMessage = JsonDocument.Parse(
        """{"position":{"sourceLine":null,"scrollY":120}}"""))
    {
        Assert(DocumentPosition.TryRead(
                fallbackPositionMessage.RootElement,
                "position",
                out DocumentPosition position) &&
            position.SourceLine is null &&
            position.Offset == 0 &&
            position.ScrollY == 120,
            "Navigation should retain a pixel fallback when no source anchor is available.");
    }

    TestProofFoldStructure(testRoot);
    TestUserProfileMigration(testRoot);

    Console.WriteLine("Markdown navigation, unresolved state, ProofFold, and profile migration tests passed.");
}
finally
{
    if (Directory.Exists(testRoot)) Directory.Delete(testRoot, recursive: true);
}

static void TestUserProfileMigration(string testRoot)
{
    string localAppData = Path.Combine(testRoot, "profiles");
    string legacy = Path.Combine(localAppData, "LeanMD");
    string current = Path.Combine(localAppData, "ProofMD");
    UserProfile.Migrate(localAppData);
    Assert(!Directory.Exists(current), "A fresh launch should not fabricate legacy settings.");

    string legacyStorage = Path.Combine(legacy, "WebView2", "Default", "Local Storage");
    Directory.CreateDirectory(legacyStorage);
    File.WriteAllText(Path.Combine(legacy, "window-state.json"), """{"Width":1200,"Height":800}""");
    File.WriteAllText(Path.Combine(legacyStorage, "theme"), "dark");
    Assert(UserProfile.NeedsWebViewMigration(localAppData), "An existing WebView2 profile should be migrated.");
    UserProfile.Migrate(localAppData);
    Assert(File.ReadAllText(Path.Combine(current, "window-state.json")) ==
        File.ReadAllText(Path.Combine(legacy, "window-state.json")),
        "The first ProofMD launch should copy the saved window position.");
    Assert(File.ReadAllText(Path.Combine(current, "WebView2", "Default", "Local Storage", "theme")) == "dark" &&
        !Directory.Exists(Path.Combine(legacy, "WebView2")),
        "The complete WebView2 profile should move together, preserving origin storage.");

    File.WriteAllText(Path.Combine(current, "window-state.json"), "new-window-state");
    Directory.CreateDirectory(legacyStorage);
    File.WriteAllText(Path.Combine(legacyStorage, "theme"), "light");
    UserProfile.Migrate(localAppData);
    Assert(File.ReadAllText(Path.Combine(current, "window-state.json")) == "new-window-state" &&
        File.ReadAllText(Path.Combine(current, "WebView2", "Default", "Local Storage", "theme")) == "dark",
        "Repeated launches should retain existing ProofMD settings.");
    Assert(File.Exists(Path.Combine(legacyStorage, "theme")),
        "A newer ProofMD profile should leave any remaining legacy profile intact.");
}

static void TestProofFoldStructure(string testRoot)
{
    string root = Path.Combine(testRoot, "proof-fold");
    string entry = WriteDocument(root, "main.md");
    string instructions = WriteDocument(root, "AGENTS.md");
    string firstFold = WriteDocument(root, "folds/first.md");
    string nestedFold = WriteDocument(root, "folds/nested/second.md");
    string unlinkedFold = WriteDocument(root, "folds/unlinked.md");
    File.AppendAllText(entry,
        "\n[Second](folds/nested/second.md \"fold\")\n[First](folds/first.md \"fold\")\n");
    File.AppendAllText(firstFold, "\n[Shared second](nested/second.md \"fold\")\n");
    Directory.CreateDirectory(Path.Combine(root, "references"));
    File.WriteAllText(Path.Combine(root, "notation.yaml"), "version: 1\nsymbols: []\n");
    WriteProofFoldManifest(root, "folds");

    ProofFoldStructure? structure = ProofFoldStructure.LoadForEntry(entry);
    Assert(structure is not null,
        "A valid adjacent prooffold.json should identify its configured entry.");
    Assert(structure!.EntryId == "main.md" &&
        structure.Folds.Count == 3 &&
        structure.Folds.Any(fold => fold.Id == "folds/first.md") &&
        structure.Folds.Any(fold => fold.Id == "folds/nested/second.md"),
        "ProofFold should load every Markdown fragment under its folds directory.");
    Assert(structure.TryResolveSourceDocument("folds/first.md", out string resolvedFold) &&
        PathsEqual(resolvedFold, firstFold),
        "ProofFold source ids should resolve to known entry or fold documents.");
    Assert(structure.Documents.SequenceEqual([entry, nestedFold, firstFold]) &&
        !structure.ContainsDocument(unlinkedFold),
        "The ProofFold map should include reachable folds in source-link order.");
    Assert(structure.Edges.SequenceEqual([
            new ExplorationMapEdge(entry, nestedFold, 0),
            new ExplorationMapEdge(entry, firstFold, 1),
            new ExplorationMapEdge(firstFold, nestedFold, 0),
        ]) &&
        structure.GetDocumentOrder(nestedFold) < structure.GetDocumentOrder(firstFold),
        "ProofFold maps should preserve shared targets and each parent's link order.");
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

    File.WriteAllText(entry, "# Entry\n\n[First](folds/first.md \"fold\")\n");
    ProofFoldStructure? reordered = ProofFoldStructure.LoadForEntry(entry);
    Assert(reordered is not null &&
        reordered.Documents.SequenceEqual([entry, firstFold, nestedFold]) &&
        reordered.Edges.Count == 2,
        "Editing entry links should rebuild the reachable fold map.");

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
