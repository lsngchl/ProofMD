using System.Text.Json;
using ProofMD;

// Runs every test and reports all failures; each test gets its own temporary folder.
string testRoot = Path.Combine(Path.GetTempPath(), $"ProofMD-desktop-tests-{Guid.NewGuid():N}");
var tests = new (string Name, Func<string, Task> Run)[]
{
    ("markdown link resolution", TestMarkdownPaths),
    ("navigation policy", TestNavigationPolicy),
    ("viewer messages", TestViewerMessages),
    ("unresolved markers", TestUnresolvedMarkers),
    ("ProofFold structure", TestProofFoldStructure),
    ("ProofFold recovery", TestProofFoldRecovery),
    ("session opening", TestSessionOpening),
    ("session navigation and history", TestSessionNavigation),
    ("session reload", TestSessionReload),
    ("session unresolved state", TestSessionUnresolved),
};

int failures = 0;
try
{
    foreach ((string name, Func<string, Task> run) in tests)
    {
        string folder = Path.Combine(testRoot, name.Replace(' ', '-'));
        Directory.CreateDirectory(folder);
        try
        {
            await run(folder);
        }
        catch (Exception exception)
        {
            failures++;
            Console.Error.WriteLine($"FAILED {name}: {exception.Message}");
        }
    }
}
finally
{
    if (Directory.Exists(testRoot)) Directory.Delete(testRoot, recursive: true);
}

Console.WriteLine(failures == 0
    ? $"All {tests.Length} desktop test groups passed."
    : $"{failures} of {tests.Length} desktop test groups failed.");
return failures == 0 ? 0 : 1;

static Task TestMarkdownPaths(string root)
{
    string source = WriteDocument(root, "notes/source.md");
    string target = WriteDocument(root, "notes/Target File.md");
    WriteDocument(root, "other/Deep.markdown");

    Assert(MarkdownPaths.ResolveLink(source, "Target%20File.md#part") == target,
        "Links should be decoded and lose their fragment.");
    Assert(MarkdownPaths.ResolveLink(source, "target file.MD?x=1") == target,
        "Links that differ only in case should resolve to the file's on-disk name.");
    Assert(MarkdownPaths.ResolveLink(source, "../other/deep.markdown") == Path.Combine(root, "other", "Deep.markdown"),
        "Parent-relative links should resolve.");
    Assert(MarkdownPaths.ResolveLink(source, "missing.md") == Path.Combine(root, "notes", "missing.md"),
        "Links to missing files still resolve, so opening them can report the problem.");
    foreach (string href in new[] { "paper.pdf", "/abs.md", "C:/abs.md", "", "#section" })
    {
        Assert(MarkdownPaths.ResolveLink(source, href) is null, $"'{href}' is not a relative Markdown link.");
    }
    Assert(MarkdownPaths.IsWithin(root, target) && !MarkdownPaths.IsWithin(Path.Combine(root, "notes"), Path.Combine(root, "other")),
        "IsWithin should accept descendants only.");
    return Task.CompletedTask;
}

static Task TestNavigationPolicy(string root)
{
    const string entry = "https://proofmd.example/index.html";
    Assert(NavigationPolicy.Decide(entry, entry, isReloadOrHistory: false) == NavigationDecision.Allow,
        "The viewer page should load.");
    Assert(NavigationPolicy.Decide(entry, entry, isReloadOrHistory: true) == NavigationDecision.Block,
        "Reloading the viewer would lose the open document.");
    foreach (string uri in new[] { "https://example.org/a", "http://example.org", "mailto:someone@example.org" })
    {
        Assert(NavigationPolicy.Decide(uri, entry, false) == NavigationDecision.OpenExternally, $"{uri} should open externally.");
    }
    foreach (string uri in new[] { "https://proofmd.example/paper.pdf", "file:///C:/x.md", "ms-settings:display", "javascript:alert(1)", "about:blank" })
    {
        Assert(NavigationPolicy.Decide(uri, entry, false) == NavigationDecision.Block, $"{uri} should be blocked.");
    }
    return Task.CompletedTask;
}

static Task TestViewerMessages(string root)
{
    ViewerMessage? link = ViewerMessage.Parse(
        """{"type":"open-markdown-link","href":"a.md","order":2,"position":{"sourceLine":17,"offset":-12.5,"scrollY":640.25}}""");
    Assert(link is { Type: "open-markdown-link", Href: "a.md", Order: 2 } &&
        link.ValidPosition == new DocumentPosition(17, -12.5, 640.25),
        "Link messages should carry their position.");
    Assert(ViewerMessage.Parse("""{"type":"go-back","position":{"sourceLine":0,"scrollY":1}}""")!.ValidPosition is null,
        "Invalid positions should be ignored.");
    Assert(ViewerMessage.Parse("""{"type":"go-back","position":{"scrollY":120}}""")!.ValidPosition == new DocumentPosition(null, 0, 120),
        "A position without a source line keeps its pixel fallback.");
    Assert(ViewerMessage.Parse("not json") is null && ViewerMessage.Parse("""{"order":"x"}""") is null,
        "Malformed messages should be ignored.");
    using JsonDocument serialized = JsonDocument.Parse(ViewerMessage.Serialize(new { restorePosition = new DocumentPosition(3, 1, 2) }));
    JsonElement position = serialized.RootElement.GetProperty("restorePosition");
    Assert(position.GetProperty("sourceLine").GetInt32() == 3 && !position.TryGetProperty("isValid", out _),
        "Positions should serialize with the viewer's field names only.");
    return Task.CompletedTask;
}

static Task TestUnresolvedMarkers(string root)
{
    string document = WriteDocument(root, "a.md");
    string marker = UnresolvedStateStore.SidecarPath(document);
    Assert(marker == document + ".unresolved", "The marker should extend the full file name.");
    Assert(UnresolvedStateStore.SidecarPath(Path.ChangeExtension(document, ".markdown")) != marker,
        "Documents that differ only in extension should keep separate markers.");
    Assert(!UnresolvedStateStore.IsUnresolved(document), "Documents start resolved.");
    UnresolvedStateStore.SetUnresolved(document, true);
    UnresolvedStateStore.SetUnresolved(document, true);
    Assert(UnresolvedStateStore.IsUnresolved(document) && File.ReadAllText(marker) == "status: unresolved\n",
        "Marking unresolved should create a readable marker idempotently.");
    UnresolvedStateStore.SetUnresolved(document, false);
    Assert(!File.Exists(marker), "Clearing the state should remove the marker.");
    return Task.CompletedTask;
}

static Task TestProofFoldStructure(string root)
{
    string entry = WriteDocument(root, "main.md");
    string instructions = WriteDocument(root, "AGENTS.md");
    string firstFold = WriteDocument(root, "folds/First.md");
    WriteDocument(root, "folds/nested/second.md");
    WriteDocument(root, "folds/notes.txt");
    WriteManifest(root, """{"formatVersion":1,"entry":"main.md","foldsDirectory":"folds","notationRegistry":"notation.yaml"}""");

    ProofFoldStructure? structure = ProofFoldStructure.LoadForEntry(entry);
    Assert(structure is { EntryId: "main.md" } &&
        structure.Folds.Select(fold => fold.Id).SequenceEqual(["folds/First.md", "folds/nested/second.md"]),
        "Every Markdown file under the folds folder should load, with on-disk names as ids.");
    Assert(structure!.TryResolveDocument("FOLDS/first.md", out string resolved) && resolved == firstFold,
        "Fold ids should resolve regardless of case.");
    Assert(ProofFoldStructure.LoadForEntry(instructions) is null && ProofFoldStructure.LoadForEntry(firstFold) is null,
        "Only the configured entry should open in ProofFold mode.");
    Assert(ProofFoldStructure.LoadForEntry(entry)!.HasSameContent(structure), "Unchanged sources should compare equal.");
    File.AppendAllText(firstFold, "changed\n");
    Assert(!ProofFoldStructure.LoadForEntry(entry)!.HasSameContent(structure), "A changed fold should be detected.");

    WriteManifest(root, """{"formatVersion":1,"entry":"main.md"}""");
    Assert(ProofFoldStructure.LoadForEntry(entry) is { Folds.Count: 2 }, "foldsDirectory should default to folds.");
    Directory.Delete(Path.Combine(root, "folds"), recursive: true);
    Assert(ProofFoldStructure.LoadForEntry(entry) is { Folds.Count: 0 }, "A missing folds folder means no folds yet.");
    return Task.CompletedTask;
}

static Task TestProofFoldRecovery(string root)
{
    string entry = WriteDocument(root, "main.md");
    string manifest = Path.Combine(root, ProofFoldStructure.ManifestFileName);
    foreach (string invalid in new[]
    {
        "{", "null", "[]", "{}",
        """{"formatVersion":"1","entry":"main.md"}""",
        """{"formatVersion":2,"entry":"main.md"}""",
        """{"formatVersion":1,"entry":null}""",
        """{"formatVersion":1,"entry":"main.md","foldsDirectory":"../outside"}""",
        """{"formatVersion":1,"entry":"main.md","foldsDirectory":"a\\b"}""",
    })
    {
        File.WriteAllText(manifest, invalid);
        Assert(ProofFoldStructure.TryLoadForEntry(entry) is null, $"Invalid manifest {invalid} should mean ordinary Markdown.");
    }

    WriteManifest(root, """{"formatVersion":1,"entry":"main.md"}""");
    using (new FileStream(manifest, FileMode.Open, FileAccess.Read, FileShare.None))
    {
        Assert(ProofFoldStructure.TryLoadForEntry(entry) is null, "An unreadable manifest should mean ordinary Markdown.");
    }
    string fold = WriteDocument(root, "folds/a.md");
    WriteDocument(root, "folds/b.md");
    using (new FileStream(fold, FileMode.Open, FileAccess.Read, FileShare.None))
    {
        Assert(ProofFoldStructure.TryLoadForEntry(entry) is { Folds.Count: 1 }, "An unreadable fold should be skipped.");
    }
    Assert(ProofFoldStructure.TryLoadForEntry(entry) is { Folds.Count: 2 }, "The fold should return once readable.");
    return Task.CompletedTask;
}

static async Task TestSessionOpening(string root)
{
    var empty = new SessionProbe(null);
    await empty.Session.ViewerReadyAsync();
    Assert(empty.Types().SequenceEqual(["show-empty-state"]), "Without a document the viewer should show its empty state.");

    var missing = new SessionProbe(Path.Combine(root, "missing.md"));
    await missing.Session.ViewerReadyAsync();
    Assert(missing.Errors.Count == 1 && missing.Types().SequenceEqual(["show-empty-state"]) && missing.Session.CurrentPath is null,
        "A missing start document should be reported and leave the viewer empty.");

    string document = WriteDocument(root, "start.md");
    var probe = new SessionProbe(document.ToUpperInvariant());
    await probe.Session.ViewerReadyAsync();
    Assert(probe.Session.CurrentPath == document, "The open document should use its on-disk name.");
    Assert(probe.Types().SequenceEqual(["open-markdown", "map-state"]), "Opening should send the document and its map.");
    Assert(probe.Last("map-state").GetProperty("root").GetString() == document, "The map should start at the document.");

    probe.Messages.Clear();
    await probe.Session.ViewerReadyAsync();
    Assert(probe.Types().SequenceEqual(["open-markdown", "map-state"]),
        "If the page loads again, the current document should be sent again.");
}

static async Task TestSessionNavigation(string root)
{
    string start = WriteDocument(root, "start.md");
    string chapter = WriteDocument(root, "Chapter.md");
    string appendix = WriteDocument(root, "sub/appendix.md");
    var probe = new SessionProbe(start);
    await probe.Session.ViewerReadyAsync();

    var position = new DocumentPosition(12, 4, 300);
    await probe.Session.OpenLinkAsync("chapter.md", sourceDocument: null, order: 1, position);
    Assert(probe.Session.CurrentPath == chapter && probe.Session.HistoryCount == 1,
        "Following a link should open its target and remember where the reader was.");
    JsonElement map = probe.Last("map-state");
    Assert(map.GetProperty("current").GetString() == chapter &&
        map.GetProperty("previous").GetString() == start &&
        map.GetProperty("nodes").GetArrayLength() == 2 &&
        map.GetProperty("edges")[0].GetProperty("to").GetString() == chapter &&
        map.GetProperty("edges")[0].GetProperty("order").GetInt32() == 1,
        "The map should record the link with its on-disk target name and order.");

    await probe.Session.OpenLinkAsync("paper.pdf", null, null, position);
    await probe.Session.OpenLinkAsync("missing.md", null, null, position);
    Assert(probe.Session.CurrentPath == chapter && probe.Errors.Count == 1 && probe.Session.HistoryCount == 1,
        "Failed links should leave the open document and history unchanged.");

    await probe.Session.OpenLinkAsync("sub/appendix.md", null, 0, null);
    await probe.Session.UpdateLinkOrdersAsync(contextId: 999, [new LinkOrder("x.md", 0)]);
    await probe.Session.OpenMapNodeAsync(start, null);
    Assert(probe.Session.CurrentPath == start && probe.Session.HistoryCount == 3, "Map nodes should open with history.");
    await probe.Session.OpenMapNodeAsync(start, null);
    await probe.Session.OpenMapNodeAsync(Path.Combine(root, "unknown.md"), null);
    Assert(probe.Session.HistoryCount == 3, "Opening the current or an unknown node should do nothing.");

    probe.Messages.Clear();
    await probe.Session.GoBackAsync();
    Assert(probe.Session.CurrentPath == appendix && probe.Session.HistoryCount == 2, "Back should return to the previous document.");
    await probe.Session.GoBackAsync();
    JsonElement restored = probe.Last("open-markdown").GetProperty("restorePosition");
    Assert(probe.Session.CurrentPath == chapter && restored.ValueKind == JsonValueKind.Null,
        "Back should restore the stored position, here none.");
    await probe.Session.GoBackAsync();
    restored = probe.Last("open-markdown").GetProperty("restorePosition");
    Assert(probe.Session.CurrentPath == start && restored.GetProperty("sourceLine").GetInt32() == 12,
        "Back should restore the reading position recorded when leaving.");

    await probe.Session.ResetMapAsync();
    Assert(probe.Session.HistoryCount == 0 && probe.Last("map-state").GetProperty("nodes").GetArrayLength() == 1,
        "Resetting the map should restart it at the current document and clear history.");

    string foldRoot = Path.Combine(root, "proof");
    string entry = WriteDocument(foldRoot, "main.md");
    WriteDocument(foldRoot, "folds/inner/step.md");
    string sibling = WriteDocument(foldRoot, "folds/inner/related.md");
    WriteManifest(foldRoot, """{"formatVersion":1,"entry":"main.md"}""");
    await probe.Session.OpenAsync(entry);
    Assert(probe.Last("open-markdown").GetProperty("proofFold").GetProperty("folds").GetArrayLength() == 2,
        "A ProofFold entry should send its fold sources.");
    await probe.Session.OpenLinkAsync("related.md", sourceDocument: "folds/inner/step.md", order: null, position: null);
    Assert(probe.Session.CurrentPath == sibling, "Links inside a fold should resolve relative to the fold file.");
}

static async Task TestSessionReload(string root)
{
    string entry = WriteDocument(root, "main.md");
    string fold = WriteDocument(root, "folds/a.md");
    WriteManifest(root, """{"formatVersion":1,"entry":"main.md"}""");
    string other = WriteDocument(root, "other.md");
    var probe = new SessionProbe(entry);
    await probe.Session.ViewerReadyAsync();
    await probe.Session.OpenLinkAsync("other.md", null, null, null);
    await probe.Session.GoBackAsync();
    await probe.Session.OpenLinkAsync("other.md", null, null, null);
    int history = probe.Session.HistoryCount;

    probe.Messages.Clear();
    await probe.Session.ReloadAsync();
    Assert(probe.Messages.Count == 0, "An unchanged document should not be sent again.");

    File.AppendAllText(other, "more\n");
    await probe.Session.ReloadAsync();
    Assert(probe.Types().SequenceEqual(["reload-markdown"]) && probe.Session.HistoryCount == history,
        "A changed document should be re-sent without touching history or the map.");

    await probe.Session.GoBackAsync();
    probe.Messages.Clear();
    File.WriteAllText(Path.Combine(root, ProofFoldStructure.ManifestFileName), "{");
    await probe.Session.ReloadAsync();
    Assert(probe.Last("reload-markdown").GetProperty("proofFold").ValueKind == JsonValueKind.Null &&
        probe.Session.HistoryCount == history - 1,
        "A broken manifest should re-render the entry as ordinary Markdown and keep history.");
    WriteManifest(root, """{"formatVersion":1,"entry":"main.md"}""");
    File.AppendAllText(fold, "changed\n");
    await probe.Session.ReloadAsync();
    Assert(probe.Last("reload-markdown").GetProperty("proofFold").GetProperty("folds").GetArrayLength() == 1,
        "A repaired manifest should restore ProofFold mode.");

    File.Delete(entry);
    probe.Messages.Clear();
    await probe.Session.ReloadAsync();
    Assert(probe.Messages.Count == 0 && probe.Errors.Count == 0, "A deleted document should keep showing its last version.");
}

static async Task TestSessionUnresolved(string root)
{
    string document = WriteDocument(root, "a.md");
    var probe = new SessionProbe(document);
    await probe.Session.ViewerReadyAsync();
    int contextId = probe.Last("open-markdown").GetProperty("contextId").GetInt32();

    await probe.Session.SetUnresolvedAsync(contextId, true);
    Assert(probe.Last("document-unresolved-state").GetProperty("unresolved").GetBoolean() &&
        UnresolvedStateStore.IsUnresolved(document) &&
        probe.Last("map-state").GetProperty("nodes")[0].GetProperty("unresolved").GetBoolean(),
        "Marking unresolved should update the marker, the toolbar, and the map.");

    probe.Messages.Clear();
    await probe.Session.SetUnresolvedAsync(contextId + 1, false);
    Assert(probe.Last("document-unresolved-state").GetProperty("unresolved").GetBoolean() &&
        UnresolvedStateStore.IsUnresolved(document),
        "A request for another document should be answered with the unchanged state.");
}

static void WriteManifest(string root, string json)
{
    Directory.CreateDirectory(root);
    File.WriteAllText(Path.Combine(root, ProofFoldStructure.ManifestFileName), json);
}

static string WriteDocument(string root, string relativePath)
{
    string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, $"# {Path.GetFileNameWithoutExtension(path)}\n");
    return path;
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

/// <summary>Runs a session and records what it posts and reports.</summary>
internal sealed class SessionProbe
{
    public SessionProbe(string? initialPath)
    {
        Session = new ViewerSession(
            initialPath,
            message => Messages.Add(JsonDocument.Parse(ViewerMessage.Serialize(message)).RootElement.Clone()),
            Errors.Add);
    }

    public ViewerSession Session { get; }
    public List<JsonElement> Messages { get; } = [];
    public List<string> Errors { get; } = [];

    public IEnumerable<string?> Types() => Messages.Select(message => message.GetProperty("type").GetString());

    public JsonElement Last(string type) =>
        Messages.Last(message => message.GetProperty("type").GetString() == type);
}
