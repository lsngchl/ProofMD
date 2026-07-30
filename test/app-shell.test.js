import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const html = readFileSync(new URL("../index.html", import.meta.url), "utf8");
const appSource = readFileSync(new URL("../src/app.js", import.meta.url), "utf8");
const rendererSource = readFileSync(
  new URL("../src/renderer.js", import.meta.url),
  "utf8",
);
const stylesSource = readFileSync(
  new URL("../src/styles.css", import.meta.url),
  "utf8",
);
const desktopHost = readFileSync(
  new URL("../desktop/LeanMD/MainForm.cs", import.meta.url),
  "utf8",
);
const desktopProgram = readFileSync(
  new URL("../desktop/LeanMD/Program.cs", import.meta.url),
  "utf8",
);
const desktopApplicationContext = readFileSync(
  new URL("../desktop/LeanMD/LeanMDApplicationContext.cs", import.meta.url),
  "utf8",
);
const explorationMapStore = readFileSync(
  new URL("../desktop/LeanMD/ExplorationMapStore.cs", import.meta.url),
  "utf8",
);
const leanMdStructure = readFileSync(
  new URL("../desktop/LeanMD/LeanMdStructure.cs", import.meta.url),
  "utf8",
);
const unresolvedStateStore = readFileSync(
  new URL("../desktop/LeanMD/UnresolvedStateStore.cs", import.meta.url),
  "utf8",
);
const gitignore = readFileSync(new URL("../.gitignore", import.meta.url), "utf8");

test("starts with a Markdown drop target", () => {
  assert.match(html, /id="emptyState"/);
  assert.match(html, /Drop a Markdown file here/);
  assert.match(appSource, /showEmptyState\(\);/);
  assert.doesNotMatch(appSource, /else\s*{\s*window\.LeanMD\.openSample\(\)/);
  assert.match(desktopHost, /type = "show-empty-state"/);
});

test("does not calculate or display document statistics", () => {
  assert.doesNotMatch(html, /documentMeta|document-meta/);
  assert.doesNotMatch(
    appSource,
    /countWords|wordCount|equationCount|\.math-inline, \.math-display|rendered with/,
  );
});

test("keeps display mathematics close to the surrounding text", () => {
  assert.match(
    stylesSource,
    /\.markdown-body \.math-display\s*{[^}]*margin-top: 0\.55em;[^}]*margin-bottom: 0\.55em;/s,
  );
  assert.match(
    stylesSource,
    /\.math-display\s*{[^}]*padding: 0\.15rem 0\.25rem;/s,
  );
  assert.match(
    stylesSource,
    /\.markdown-body \.math-display > \.katex-display\s*{[^}]*margin-block: 0;/s,
  );
});

test("does not bundle a Markdown sample into the app", () => {
  assert.doesNotMatch(html, /sampleButton|Reload sample/);
  assert.doesNotMatch(
    appSource,
    /sampleMarkdown|SAMPLE_NAME|\?raw|openSample|open-sample|bundled-sample-opened/,
  );
  assert.doesNotMatch(desktopHost, /open-sample|bundled-sample-opened/);
});

test("distinguishes internal Markdown links from external web links", () => {
  assert.match(appSource, /elements\.preview\.addEventListener\("click"/);
  assert.match(appSource, /link\.getAttribute\("title"\)/);
  assert.match(appSource, /type: "open-markdown-link",[\s\S]*href,[\s\S]*role,/);
  assert.match(desktopHost, /case "open-markdown-link":/);
  assert.match(desktopHost, /OpenLinkedMarkdownAsync\([\s\S]*hrefElement\.GetString\(\),[\s\S]*role,[\s\S]*position,[\s\S]*linkOrder\)/);
  assert.match(
    desktopHost,
    /ResolveLinkedMarkdownPath\(sourcePath, href\)/,
  );
  assert.match(appSource, /function isExternalWebHref\(href\)/);
  assert.match(appSource, /const href = link\.getAttribute\("href"\)/);
  assert.match(appSource, /appendExternalLinkIndicator\(link\)/);
  assert.match(stylesSource, /\.external-link-icon\s*{/);
  assert.match(appSource, /opens in an external browser/);
});

test("keeps undiscovered links in the current structure in the same window", () => {
  assert.match(desktopHost, /role\?\.Equals\(\s*"recall"/);
  assert.match(desktopHost, /bool targetWasDiscovered = _mapNodes\.Contains/);
  assert.match(desktopHost, /bool targetIsInCurrentStructure/);
  assert.match(
    desktopHost,
    /if \(isRecall && !targetWasDiscovered && !targetIsInCurrentStructure\)/,
  );
  assert.match(desktopHost, /_openRecallWindow\(linkedPath, this\)/);
  assert.match(desktopHost, /ApplyRecallWindowBounds\(recallSource\)/);
  assert.match(desktopProgram, /new LeanMDApplicationContext\(markdownPath\)/);
  assert.match(desktopApplicationContext, /new MainForm\(markdownPath, OpenRecallWindow/);
  assert.match(desktopApplicationContext, /if \(_openWindowCount == 0\)/);
});

test("desktop drag and drop keeps the source file path in the host", () => {
  assert.match(desktopHost, /AllowExternalDrop = true/);
  assert.match(appSource, /postMessageWithAdditionalObjects/);
  assert.match(appSource, /type: "open-dropped-file"/);
  assert.match(desktopHost, /eventArgs\.AdditionalObjects/);
  assert.match(desktopHost, /CoreWebView2File droppedFile/);
  assert.match(desktopHost, /droppedFile\.Path/);
});

test("reloads the current Markdown file after external changes", () => {
  assert.match(desktopHost, /new FileSystemWatcher\(directory\)/);
  assert.match(desktopHost, /MarkdownReloadDebounceMilliseconds = 250/);
  assert.match(desktopHost, /FileShare\.ReadWrite \| FileShare\.Delete/);
  assert.match(desktopHost, /type = "reload-markdown"/);
  assert.match(desktopHost, /source == _lastRenderedSource/);
  assert.match(desktopHost, /DisposeMarkdownWatcher\(\)/);
  assert.match(appSource, /message\?\.type === "reload-markdown"/);
  assert.match(appSource, /preserveScroll: true, showLoading: false/);
  assert.match(appSource, /top: Math\.min\(previousScrollY, maximumScroll\)/);
});

test("polls WSL Markdown files when Windows change notifications are unavailable", () => {
  assert.match(desktopHost, /WslMarkdownPollIntervalMilliseconds = 750/);
  assert.match(
    desktopHost,
    /if \(IsWslMarkdownPath\(markdownPath\)\)\s*{\s*_wslMarkdownPollTimer\.Start\(\);\s*return;/,
  );
  assert.match(
    desktopHost,
    /OnWslMarkdownPollTimerTick[\s\S]*await ReloadCurrentMarkdownAsync\(\)/,
  );
  assert.equal(
    desktopHost.includes(
      String.raw`fullPath.StartsWith(
                    @"\\wsl$\",
                    StringComparison.OrdinalIgnoreCase)`,
    ),
    true,
  );
  assert.equal(
    desktopHost.includes(
      String.raw`fullPath.StartsWith(
                    @"\\wsl.localhost\",
                    StringComparison.OrdinalIgnoreCase)`,
    ),
    true,
  );
  assert.match(
    desktopHost,
    /DisposeMarkdownWatcher\(\)[\s\S]*_wslMarkdownPollTimer\.Stop\(\)/,
  );
});

test("records live context only for structured LeanMD documents", () => {
  assert.match(desktopHost, /FindLeanMdMetadataDirectory\(markdownPath\)/);
  assert.match(desktopHost, /Path\.Combine\(metadataDirectory, "dependencies\.json"\)/);
  assert.match(desktopHost, /Path\.Combine\(metadataDirectory, "current-context\.json"\)/);
  assert.match(desktopHost, /type = "viewer-context"|case "viewer-context":/);
  assert.match(desktopHost, /document = relativeDocumentPath/);
  assert.match(desktopHost, /viewport,/);
  assert.match(desktopHost, /focus,/);
  assert.match(appSource, /data-source-start-line/);
  assert.match(appSource, /VIEWER_CONTEXT_DEBOUNCE_MILLISECONDS = 300/);
  assert.match(appSource, /type: "viewer-context"/);
  assert.match(appSource, /currentSelectionFocus\(\)/);
});

test("projects the complete LeanMD structure into the exploration map", () => {
  assert.match(html, /id="mapButton"/);
  assert.match(html, /id="mapOverlay"/);
  assert.match(html, /\+N expands hidden branches/);
  assert.match(desktopHost, /UpdateStructuredMapAfterOpen\(markdownPath, previousPath\)/);
  assert.match(leanMdStructure, /IReadOnlyCollection<string> Documents => _documents/);
  assert.match(desktopHost, /_mapNodes\.AddRange\(structure\.Documents/);
  assert.match(desktopHost, /_mapEdges\.AddRange\(structure\.Edges\)/);
  assert.doesNotMatch(desktopHost, /RevealStructuredPath/);
  assert.match(desktopHost, /type = "map-state"/);
  assert.match(desktopHost, /unexplored = isStructuredMap && !visited\.Contains\(path\)/);
  assert.match(appSource, /function renderMap\(\)/);
  assert.match(appSource, /unfoldExplorationMap\(\s*mapState\.nodes/);
  assert.match(appSource, /focusExplorationMap\(\s*unfoldedMap/);
  assert.match(appSource, /expandedMapOccurrenceKeys/);
  assert.match(appSource, /node\.hiddenChildCount/);
  assert.match(appSource, /className = "map-node-expand-button"/);
  assert.match(appSource, /const count = mapState\.nodes\.length/);
  assert.match(appSource, /const isCurrent = documentId === mapState\.current/);
  assert.match(appSource, /const isPrevious = documentId === mapState\.previous/);
  assert.match(appSource, /button\.classList\.toggle\("is-unexplored", isUnexplored\)/);
  assert.match(stylesSource, /\.map-node\.is-unexplored\s*{/);
  assert.match(appSource, /const documentId = node\.documentId/);
  assert.match(appSource, /type: "open-map-node",[\s\S]*id: documentId,[\s\S]*position:/);
  assert.match(appSource, /layoutExplorationMap\(\s*displayedMap\.nodes/);
  assert.match(stylesSource, /\.map-node-shell\s*{/);
  assert.doesNotMatch(appSource, /is-secondary/);
  assert.doesNotMatch(stylesSource, /stroke-dasharray/);
  assert.doesNotMatch(appSource, /isMapPositionFree|mapPositions\.has/);
});

test("toggles persistent unresolved sidecars from the document toolbar", () => {
  assert.match(html, /id="unresolvedButton"/);
  assert.match(html, /aria-pressed="false"/);
  assert.match(appSource, /type: "set-document-unresolved"/);
  assert.match(appSource, /unresolved: !currentDocumentUnresolved/);
  assert.match(desktopHost, /case "set-document-unresolved":/);
  assert.match(desktopHost, /contextId != _documentContextId/);
  assert.match(desktopHost, /UnresolvedStateStore\.SetUnresolved/);
  assert.match(unresolvedStateStore, /Path\.ChangeExtension\(Path\.GetFullPath\(markdownPath\)/);
  assert.match(unresolvedStateStore, /File\.Move\(temporaryPath, sidecarPath, overwrite: false\)/);
  assert.match(unresolvedStateStore, /File\.Delete\(sidecarPath\)/);
  assert.match(desktopHost, /new FileSystemWatcher\(workspaceRoot, "\*\.unresolved"\)/);
});

test("persists structured-document exploration maps across primary-window sessions", () => {
  assert.match(desktopHost, /RestoreStructuredMap\(structure\)/);
  assert.match(desktopHost, /ExplorationMapStore\.Load/);
  assert.match(desktopHost, /ExplorationMapStore\.Save/);
  assert.match(desktopHost, /_persistExplorationMap = recallSource is null/);
  assert.match(explorationMapStore, /CurrentSchemaVersion = 2/);
  assert.match(explorationMapStore, /DependenciesFingerprint/);
  assert.match(explorationMapStore, /StateFileName = "exploration-map\.json"/);
  assert.match(leanMdStructure, /Path\.GetRelativePath\(workspaceRoot, fullPath\)/);
  assert.match(explorationMapStore, /File\.Move\(temporaryPath, statePath, overwrite: true\)/);
  assert.match(gitignore, /\*\*\/\.leanmd\/exploration-map\.json/);
  assert.match(gitignore, /\*\*\/\.leanmd\/\.exploration-map\.\*\.tmp/);
});

test("watches and reconciles dependency manifest changes", () => {
  assert.match(desktopHost, /new FileSystemWatcher\(metadataDirectory, "dependencies\.json"\)/);
  assert.match(desktopHost, /LeanMdStructureReloadDebounceMilliseconds = 250/);
  assert.match(desktopHost, /ReconcileStructuredMap\(structure\)/);
  assert.match(desktopHost, /_mapNodes\.AddRange\(structure\.Documents/);
  assert.match(desktopHost, /_mapEdges\.AddRange\(structure\.Edges\)/);
  assert.match(desktopHost, /_mapDependenciesFingerprint = structure\.Fingerprint/);
});

test("orders map branches by source links instead of discovery", () => {
  assert.match(appSource, /type: "document-links"/);
  assert.match(appSource, /leanmdLinkOrder/);
  assert.match(desktopHost, /UpdateUnstructuredLinkOrders/);
  assert.match(leanMdStructure, /GetEdgeOrder/);
  assert.match(desktopHost, /_leanMdStructure\.GetEdgeOrder\(edge\.From, edge\.To\)/);
});

test("does not draw a new edge when revisiting a discovered node", () => {
  assert.match(desktopHost, /bool targetWasAlreadyDiscovered = _mapNodes\.Contains/);
  assert.match(desktopHost, /if \(!targetWasAlreadyDiscovered\)\s*{\s*_mapNodes\.Add\(targetPath\)/s);
  assert.match(
    desktopHost,
    /if \(!targetWasAlreadyDiscovered\)[\s\S]*?_mapEdges\.Add\(new ExplorationMapEdge\(/,
  );
});

test("renders and styles Markdown footnotes", () => {
  assert.match(rendererSource, /from "markdown-it-footnote"/);
  assert.match(rendererSource, /\.use\(footnotePlugin\)/);
  assert.match(stylesSource, /\.markdown-body \.footnotes\s*{/);
  assert.match(stylesSource, /\.markdown-body \.footnote-ref/);
  assert.match(stylesSource, /\.markdown-body \.footnote-backref/);
});

test("marks the immediately previous document only on the detailed map", () => {
  assert.match(desktopHost, /private string\? _previousMapPath/);
  assert.match(desktopHost, /previous = _previousMapPath/);
  assert.match(appSource, /previous: typeof nextState\.previous === "string"/);
  assert.match(
    appSource,
    /button\.classList\.toggle\("is-previous", isPrevious && !mapOverviewMode\)/,
  );
  assert.match(stylesSource, /\.map-node\.is-previous::after\s*{/);
  assert.match(stylesSource, /content: "Previous"/);
  assert.match(
    stylesSource,
    /\.map-node\.is-previous\s*{[^}]*border-color: color-mix\([^;]*28%[^;]*\);[^}]*box-shadow: 0 8px 22px/s,
  );
  assert.match(
    stylesSource,
    /\.map-node\.is-current\s*{[^}]*border-width: 2px;[^}]*0 0 0 4px[^}]*32%/s,
  );
});

test("composes unresolved map badges with previous and unexplored states", () => {
  assert.match(desktopHost, /unresolved = UnresolvedStateStore\.IsUnresolved\(path\)/);
  assert.match(appSource, /button\.classList\.toggle\("is-unresolved", isUnresolved\)/);
  assert.match(stylesSource, /\.map-node\.is-unresolved::before\s*{/);
  assert.match(stylesSource, /\.map-node\.is-previous::after\s*{/);
  assert.match(stylesSource, /content: "Unresolved"/);
  assert.match(appSource, /if \(isUnresolved\) stateDescriptions\.push\("Unresolved document\."\)/);
});

test("toggles the map with M and closes it with Escape", () => {
  assert.match(html, /aria-keyshortcuts="M"/);
  assert.match(appSource, /function toggleMap\(\)/);
  assert.match(appSource, /mapButton\.addEventListener\("click", toggleMap\)/);
  assert.match(appSource, /event\.key\.toLowerCase\(\) === "m"/);
  assert.match(appSource, /event\.key === "Escape" && !elements\.mapOverlay\.hidden/);
  assert.doesNotMatch(appSource, /map(?:Button|CloseButton)\.focus\(\)/);
});

test("separates map reset from close and confirms before clearing the map", () => {
  const closePosition = html.indexOf('id="mapCloseButton"');
  const footerPosition = html.indexOf('<footer class="map-footer">');
  const resetPosition = html.indexOf('id="mapResetButton"');

  assert.ok(closePosition >= 0 && closePosition < footerPosition);
  assert.ok(resetPosition > footerPosition);
  assert.match(html, /id="mapResetDialog"/);
  assert.match(html, /The full\s+structure will remain visible/);
  assert.match(appSource, /mapResetDialog\.showModal\(\)/);
  assert.match(
    appSource,
    /mapResetConfirmButton\.addEventListener\("click",[\s\S]*type: "reset-map"/,
  );
});

test("pans the map by dragging and zooms without scrollbars", () => {
  assert.match(html, /id="mapZoomSlider"/);
  assert.match(html, /id="mapZoomOutButton"/);
  assert.match(html, /id="mapZoomInButton"/);
  assert.match(html, /id="mapZoomFitButton"/);
  assert.match(html, />\s*Overview\s*</);
  assert.match(appSource, /setPointerCapture\(event\.pointerId\)/);
  assert.match(appSource, /addEventListener\("pointermove", moveMapPan\)/);
  assert.match(appSource, /function setMapZoom\(/);
  assert.match(appSource, /MAP_OVERVIEW_ENTER_ZOOM = 0\.4/);
  assert.match(appSource, /MAP_OVERVIEW_EXIT_ZOOM = 0\.48/);
  assert.match(appSource, /mapOverviewForced = true/);
  assert.match(appSource, /classList\.toggle\("is-overview", mapOverviewMode\)/);
  assert.doesNotMatch(appSource, /MAP_OVERVIEW_GEOMETRY/);
  assert.match(
    appSource,
    /layoutExplorationMap\(\s*displayedMap\.nodes,[\s\S]*MAP_NODE_GEOMETRY/,
  );
  assert.match(
    appSource,
    /button\.classList\.toggle\("is-previous", isPrevious && !mapOverviewMode\)/,
  );
  assert.match(
    appSource,
    /glyph\.textContent = isUnresolved[\s\S]*isUnexplored[\s\S]*isRoot/,
  );
  assert.match(stylesSource, /\.map-surface\.is-overview \.map-node\s*{/);
  assert.doesNotMatch(
    stylesSource,
    /\.map-surface\.is-overview \.map-node-shell\s*{/,
  );
  assert.doesNotMatch(
    stylesSource,
    /\.map-surface\.is-overview \.map-node\s*{[^}]*border-radius:\s*50%/s,
  );
  assert.match(
    stylesSource,
    /\.map-surface\.is-overview \.map-node-glyph\s*{[^}]*font-size:\s*3rem/s,
  );
  assert.doesNotMatch(
    stylesSource,
    /\.map-surface\.is-overview \.map-edges path\s*{/,
  );
  assert.match(
    stylesSource,
    /\.map-surface\.is-overview \.map-node\.is-unresolved \.map-node-glyph\s*{/,
  );
  assert.match(
    stylesSource,
    /\.map-surface\.is-overview \.map-node\.is-unresolved::before\s*{?[^}]*content:\s*none/s,
  );
  assert.match(appSource, /addEventListener\(\s*"wheel"/);
  assert.match(stylesSource, /\.map-viewport\s*{[^}]*overflow: hidden;/s);
  assert.match(stylesSource, /\.map-viewport\.is-panning\s*{[^}]*cursor: grabbing;/s);
});

test("returns to the previous document with Backspace or comma", () => {
  assert.match(appSource, /event\.key === "Backspace"/);
  assert.match(appSource, /event\.key === ","/);
  assert.match(appSource, /type: "go-back"/);
  assert.match(desktopHost, /Stack<DocumentHistoryEntry> _documentHistory/);
  assert.match(desktopHost, /case "go-back":/);
  assert.match(desktopHost, /await GoBackAsync\(\)/);
  assert.match(desktopHost, /OpenReason\.History/);
  assert.match(appSource, /position: currentDocumentPosition\(\)/);
  assert.match(appSource, /function restoreDocumentPosition\(position\)/);
  assert.match(appSource, /sourceLine: anchor\?\.range\.startLine/);
  assert.match(desktopHost, /restorePosition: previous\.Position/);
  assert.match(desktopHost, /restorePosition = serializedRestorePosition/);
});

test("shows a keyboard shortcut guide from the top bar", () => {
  assert.match(html, /id="keyGuideButton"/);
  assert.match(html, /id="keyGuidePanel"/);
  assert.match(html, /<kbd>M<\/kbd>/);
  assert.match(html, /<kbd>Backspace<\/kbd>/);
  assert.match(html, /<kbd>,<\/kbd>/);
  assert.match(html, /<kbd>Esc<\/kbd>/);
  assert.match(appSource, /function toggleKeyGuide\(\)/);
});
