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
    TestProofFoldMissingDirectory(testRoot);
    TestProofFoldOptionalComponents(testRoot);
    TestProofFoldRecovery(testRoot);
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

static void TestProofFoldMissingDirectory(string testRoot)
{
    string root = Path.Combine(testRoot, "proof-fold-missing-directory");
    string entry = WriteDocument(root, "main.md");
    string foldsDirectory = Path.Combine(root, "folds");
    Directory.CreateDirectory(Path.Combine(root, "references"));
    File.WriteAllText(Path.Combine(root, "notation.yaml"), "version: 1\nsymbols: []\n");
    WriteProofFoldManifest(root, "folds");

    ProofFoldStructure? empty = ProofFoldStructure.LoadForEntry(entry);
    Assert(empty is not null && empty.Folds.Count == 0 &&
        empty.Documents.SequenceEqual([entry]) && empty.Edges.Count == 0 &&
        empty.TryResolveSourceDocument("main.md", out string resolvedEntry) &&
        PathsEqual(resolvedEntry, entry) && !Directory.Exists(foldsDirectory),
        "An entry without a folds directory should open with an empty fold collection.");
    Assert(empty!.AffectsRenderedDocument(foldsDirectory),
        "Creating or removing the folds directory should trigger a ProofFold reload.");

    File.AppendAllText(entry, "\n[First](folds/first.md \"fold\")\n");
    ProofFoldStructure? pending = ProofFoldStructure.LoadForEntry(entry);
    Assert(pending is not null && pending.Folds.Count == 0 &&
        pending.Documents.SequenceEqual([entry]) && pending.Edges.Count == 0,
        "A missing fold target should leave the entry available to read.");

    string firstFold = WriteDocument(root, "folds/first.md");
    ProofFoldStructure? populated = ProofFoldStructure.LoadForEntry(entry);
    Assert(populated is not null && populated.Folds.Count == 1 &&
        populated.Documents.SequenceEqual([entry, firstFold]) &&
        populated.Edges.SequenceEqual([new ExplorationMapEdge(entry, firstFold, 0)]) &&
        populated.Fingerprint != empty.Fingerprint,
        "Adding the first fold should update the render fingerprint and fold map.");

    File.Delete(firstFold);
    Directory.Delete(foldsDirectory);
    ProofFoldStructure? removed = ProofFoldStructure.LoadForEntry(entry);
    Assert(removed is not null && removed.Folds.Count == 0 &&
        removed.Documents.SequenceEqual([entry]) && removed.Edges.Count == 0 &&
        removed.Fingerprint == empty.Fingerprint,
        "Removing the folds directory should restore an entry-only ProofFold document.");
}

static void TestProofFoldOptionalComponents(string testRoot)
{
    for (int components = 0; components < 8; components++)
    {
        string root = Path.Combine(testRoot, $"proof-fold-components-{components}");
        string entry = WriteDocument(root, "main.md");
        File.AppendAllText(entry, "\n[First](folds/first.md \"fold\")\n");
        if ((components & 1) != 0)
        {
            File.WriteAllText(Path.Combine(root, "notation.yaml"), "version: 1\nsymbols: []\n");
        }
        if ((components & 2) != 0) Directory.CreateDirectory(Path.Combine(root, "references"));
        if ((components & 4) != 0) WriteDocument(root, "folds/first.md");
        WriteProofFoldManifest(root, "folds");

        ProofFoldStructure? structure = ProofFoldStructure.LoadForEntry(entry);
        int expectedFolds = (components & 4) != 0 ? 1 : 0;
        Assert(structure is not null && structure.Folds.Count == expectedFolds &&
            structure.Documents.Count == expectedFolds + 1,
            $"The entry should load with every combination of optional components ({components}).");
    }

    string minimalRoot = Path.Combine(testRoot, "proof-fold-minimal-manifest");
    string minimalEntry = WriteDocument(minimalRoot, "main.md");
    File.WriteAllText(Path.Combine(minimalRoot, "prooffold.json"),
        """{"formatVersion":1,"entry":"main.md","foldsDirectory":"folds"}""");
    Assert(ProofFoldStructure.LoadForEntry(minimalEntry) is not null,
        "Authoring metadata that is not used for rendering should be optional.");
    File.WriteAllText(Path.Combine(minimalRoot, "prooffold.json"),
        """{"formatVersion":1,"entry":"main.md"}""");
    Assert(ProofFoldStructure.LoadForEntry(minimalEntry) is { Folds.Count: 0 },
        "The foldsDirectory field should be optional before any folds are created.");
    WriteDocument(minimalRoot, "folds/first.md");
    Assert(ProofFoldStructure.LoadForEntry(minimalEntry) is { Folds.Count: 1 },
        "An omitted foldsDirectory should use the conventional folds directory.");
}

static void TestProofFoldRecovery(string testRoot)
{
    string root = Path.Combine(testRoot, "proof-fold-recovery");
    string entry = WriteDocument(root, "main.md");
    string ordinaryDocument = WriteDocument(root, "notes.md");
    string manifestPath = Path.Combine(root, "prooffold.json");
    string[] invalidManifests =
    [
        "{", "null", "[]", "1", "{}",
        """{"formatVersion":"1","entry":"main.md","foldsDirectory":"folds"}""",
        """{"formatVersion":2,"entry":"main.md","foldsDirectory":"folds"}""",
        """{"formatVersion":1,"entry":null,"foldsDirectory":"folds"}""",
        """{"formatVersion":1,"entry":"main.md","foldsDirectory":null}""",
        """{"formatVersion":1,"entry":"main.md","foldsDirectory":"../outside"}""",
        """{"formatVersion":1,"entry":"main.md","foldsDirectory":"bad\u0000path"}""",
    ];
    foreach (string manifest in invalidManifests)
    {
        File.WriteAllText(manifestPath, manifest);
        Assert(ProofFoldStructure.TryLoadForEntry(entry) is null &&
            ProofFoldStructure.TryLoadForEntry(ordinaryDocument) is null,
            "Invalid ProofFold metadata should allow ordinary Markdown rendering.");
    }

    WriteProofFoldManifest(root, "folds");
    using (var lockedManifest = new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.None))
    {
        Assert(ProofFoldStructure.TryLoadForEntry(entry) is null,
            "An unreadable manifest should allow ordinary Markdown rendering.");
    }
    Assert(ProofFoldStructure.TryLoadForEntry(entry) is not null,
        "ProofFold should recover when its manifest becomes readable again.");

    string firstFold = WriteDocument(root, "folds/first.md");
    string secondFold = WriteDocument(root, "folds/second.md");
    File.AppendAllText(entry,
        "\n[First](folds/first.md \"fold\")\n[Second](folds/second.md \"fold\")\n");
    ProofFoldStructure? partial;
    using (var lockedFold = new FileStream(firstFold, FileMode.Open, FileAccess.Read, FileShare.None))
    {
        partial = ProofFoldStructure.TryLoadForEntry(entry);
        Assert(partial is not null && partial.Folds.Count == 1 &&
            partial.Documents.SequenceEqual([entry, secondFold]),
            "An unreadable fold should leave the entry and other folds available.");
    }
    ProofFoldStructure? recovered = ProofFoldStructure.TryLoadForEntry(entry);
    Assert(recovered is not null && recovered.Folds.Count == 2 &&
        recovered.Fingerprint != partial!.Fingerprint,
        "A recovered fold should update the rendered document and its map.");
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
