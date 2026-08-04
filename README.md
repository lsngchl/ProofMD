# LeanMD Viewer

**Latest release: 1.5.0**

A small, local-first Markdown viewer that renders LaTeX written with either
`\(...\)` and `\[...\]` or `$...$` and `$$...$$`.

## Workspace layout

- Viewer source: `index.html` and `src/`
- Windows desktop wrapper and installer scripts: `desktop/LeanMD/`
- Document formats: `DocumentFormats/LeanMD/` and `DocumentFormats/ProofFold/`
- Application asset scripts: `scripts/`
- Automated tests: `test/`

The tracked package metadata, executable metadata, application manifest, and
installer display version are kept in sync with the latest release shown above.

## Features

- Standard Markdown through `markdown-it`
- Inline mathematics with `\(...\)`
- Display mathematics with `\[...\]`
- Inline mathematics with `$...$`
- Display mathematics with `$$...$$`
- KaTeX rendering with no remote font or script requests
- File picker, drag and drop, light/dark theme, and print styles
- In-app navigation for relative Markdown links and a structure-aware exploration map
- In-place, recursively nested ProofFold disclosures from an adjacent `prooffold.json`
- Undiscovered links inside the current LeanMD structure reuse the current viewer window
- Recursive map layout that keeps sibling subtrees ordered as branches grow
- Map branches ordered by their source links rather than discovery order
- Drag-to-pan map navigation with slider, button, fit, and wheel zoom controls
- Focused structured maps with the root branches, active path, and current children
- Collapsible `+N` branches for temporarily revealing nearby structure
- Shared DAG branches unfolded into duplicate, state-synchronized tree nodes
- Card-preserving semantic overview below 40% zoom and from the Overview button
- Persistent exploration maps for structured LeanMD document sets
- Per-document unresolved toggles with composable map badges
- External-link icons for web references that open in the system browser
- Source-anchored reading-position restoration when navigating back
- Code spans and fenced code blocks are excluded from math rendering
- Markdown footnotes with linked references and backreferences
- Raw HTML in Markdown is disabled

Use a standard Markdown link title to express the question that following the
link answers. A `"why"` link answers “Why does this hold?” by opening a more
detailed argument. A `"recall"` link answers “What was this again?” by returning
to a definition, notation, or earlier context. Link roles do not determine map
topology: any target in the current `.leanmd/dependencies.json` structure opens
in the same window and is placed by the why DAG. An undiscovered `"recall"`
target outside that structure still opens an independent window.

```md
[Why this holds](./details.md "why")
[What this meant](./definition.md "recall")
```

The LeanMD validator reads the `"why"` links in `root.md` and `nodes/*.md`
directly and writes the complete DAG to the generated
`.leanmd/dependencies.json` manifest. `"recall"` links remain navigation
metadata in Markdown and are ignored by why-DAG validation.

When the desktop viewer finds `.leanmd/dependencies.json` in the current
document's directory or one of its ancestors, it treats that directory as a
structured LeanMD document set. The active document, visible source-line range,
and selected passage are written to the ignored
`.leanmd/current-context.json` file for local Codex context sharing. Ordinary
Markdown files outside a structured document set are still viewed and
automatically reloaded without creating this context file.

The primary viewer window saves exploration progress in the ignored
`.leanmd/exploration-map.json` file. Its detailed map shows the root, every direct
child of the root, the active path to the current document, and the current
document's direct children. A `+N` control reveals hidden children on demand.
Unexplored documents appear as `?` nodes and reveal their names and paths after
they are opened. When multiple parents share a child, the map unfolds that child
and its descendants under every displayed parent. Those visual occurrences still
represent one document, so opening any occurrence updates all of them together.

At 40% zoom or below, the map switches to a semantic overview of the full
unfolded structure. It returns to the detailed map above 48% zoom. Overview keeps
the same rectangular cards and tree spacing, replacing unreadable text with large
centered state icons. Unresolved documents use the center icon rather than an
attached badge, and the previous-document marker is omitted. The Overview button
enters this mode directly and fits the full structure to the viewport. The viewer
watches `dependencies.json` and refreshes the map when dependencies change.
Resetting clears exploration progress except for the current document. Independent
recall windows keep temporary maps.

An adjacent `<document>.unresolved` marker records that a document is not yet
understood. The desktop viewer creates or removes this marker from the document
toolbar and reflects it on every visible occurrence of the node in the map.
Unresolved state is independent of exploration state: resetting the map may hide
the node, but the marker remains and is shown again when the node is revealed.

The document-set entry point is `root.md`. Every other authored document lives
directly under `nodes/`, so logical proof depth does not increase filesystem
path depth. Node filenames are unique lowercase ASCII slugs, while the first
level-one heading supplies the displayed title.

For ProofFold documents, the app checks only for `prooffold.json` beside the
Markdown file being opened. ProofFold mode is enabled when the manifest's
`entry` resolves to that file. Links with the `"fold"` title then expand their
configured `folds/` targets in place, including nested folds, instead of
navigating away from the entry document. Opening a fold or reference file
directly continues to use ordinary Markdown mode. In ProofFold mode, the app
brand changes to ProofFold and the map shows every reachable fold immediately
as a top-down tree rooted at the entry document; it does not use exploration
state.

Validate a document set, or regenerate its complete manifest after editing why
links, by passing its path:

```sh
node DocumentFormats/LeanMD/validate-why-dag.js path/to/document_set
node DocumentFormats/LeanMD/validate-why-dag.js path/to/document_set --write
```

Run the app feature test suite with:

```sh
npm test
```

## Run locally

```sh
pnpm install
pnpm dev
```

## Build the desktop viewer assets

```sh
pnpm build
```

The build writes the cacheable HTML, JavaScript, CSS, and WOFF2 math-font assets
directly to `dist-desktop/`. The Windows desktop project packages that directory
into its `Viewer/` output. A standalone single-HTML build is not produced.

## Windows app

The Windows desktop wrapper accepts a Markdown path as its first command-line
argument and loads cacheable viewer assets in WebView2.

```powershell
dotnet publish desktop/LeanMD/LeanMD.csproj -c Release -r win-x64 --self-contained false -o release/LeanMD-<version>
```

Official release folders use the `release/LeanMD-<version>/` naming convention.
Replace `<version>` in the command above with the latest release shown at the top
of this README. After publishing, run `Install-LeanMD.cmd` from that release
folder to install the app for the current user.
The installer registers LeanMD as an available handler for `.md` and `.markdown`,
but does not open Windows Default Apps settings or change the existing default app.
Administrator privileges are not required.

On first launch, the desktop window uses most of the primary monitor's working
area. On subsequent launches it restores the last normal size and position,
including whether the window was maximized. If the saved monitor is no longer
available, LeanMD falls back to a large centered window on the primary display.
The window remains transparent while WebView2 prepares the viewer shell. It is
revealed only after the shell has painted, and Markdown rendering starts after
the visible loading state has painted once.
