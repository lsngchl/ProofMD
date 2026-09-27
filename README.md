# ProofMD Viewer

**Latest release: 2.0.2**

[Download the Windows release](https://github.com/lsngchl/ProofMD/releases/latest)

A small, local-first Markdown viewer that renders LaTeX written with either
`\(...\)` and `\[...\]` or `$...$` and `$$...$$`.

## Workspace layout

- Viewer source: `index.html` and `src/`
- Windows desktop wrapper and installer scripts: `desktop/ProofMD/`
- ProofFold format: `DocumentFormats/ProofFold/`
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
- In-app navigation for relative Markdown links and an exploration map
- In-place, recursively nested ProofFold disclosures from an adjacent `prooffold.json`
- Recursive map layout that keeps sibling subtrees ordered as branches grow
- Map branches ordered by their source links rather than discovery order
- Drag-to-pan map navigation with slider, button, fit, and wheel zoom controls
- Collapsible `+N` branches for temporarily revealing nearby structure
- Shared DAG branches unfolded into duplicate, state-synchronized tree nodes
- Card-preserving semantic overview below 40% zoom and from the Overview button
- Per-document unresolved toggles with composable map badges
- External-link icons for web references that open in the system browser
- Source-anchored reading-position restoration when navigating back
- Code spans and fenced code blocks are excluded from math rendering
- Markdown footnotes with linked references and backreferences
- Raw HTML in Markdown is disabled

An adjacent `<document>.unresolved` marker records that a document is not yet
understood. The desktop viewer creates or removes this marker from the document
toolbar and reflects it on every visible occurrence of the node in the map.

For ProofFold documents, the app checks only for `prooffold.json` beside the
Markdown file being opened. ProofFold mode is enabled when the manifest's
`entry` resolves to that file. Links with the `"fold"` title then expand their
configured `folds/` targets in place, including nested folds, instead of
navigating away from the entry document. Opening a fold or reference file
directly continues to use ordinary Markdown mode. In ProofFold mode, the app
brand changes to ProofFold and the map shows every reachable fold immediately
as a top-down tree rooted at the entry document; it does not use exploration
state. An omitted `foldsDirectory` uses `folds/`, which can be absent until the
first fold is created. The viewer does not require notation or reference files,
directories, or their manifest fields. Unreadable folds are skipped while the
entry and other folds remain available. If the manifest is invalid or unreadable,
the document opens as ordinary Markdown. Editing the manifest updates the mode
automatically; adding or removing folds updates the document and its map.

Ordinary Markdown exploration maps stay in memory for the current window
session. Relative Markdown links navigate in the current window.

Run the app feature test suite with:

```sh
npm test
```

Run the desktop navigation, unresolved-state, and ProofFold tests on Windows with:

```powershell
dotnet run --project test/ProofMD.DesktopTests/ProofMD.DesktopTests.csproj
```

Run the Windows installer migration tests with:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File test/installer.test.ps1
```

These tests use temporary folders and an isolated registry key, which they remove
afterward.

Verify document rendering through the installed Windows file associations with:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File test/desktop-rendering.test.ps1 -ApplicationPath "$env:LOCALAPPDATA\Programs\ProofMD\ProofMD.exe" -UseFileAssociation
```

This test opens and closes temporary documents in the actual desktop app and
checks that rendering completes, including after a manifest is damaged and
repaired. It covers both `.md` and `.markdown` file associations. To test an
uninstalled build, pass its executable path and omit `-UseFileAssociation`.

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
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Build-Release.ps1
```

The script checks that all tracked version numbers match, builds the web assets,
and publishes the Windows app to `release/ProofMD-<version>/`. It replaces only
that version's output folder and stops immediately if a build step fails. Add
`-Archive` to create `release/ProofMD-<version>-win-x64.zip` for GitHub Releases.

The app requires the .NET 10 Desktop Runtime for Windows x64 and Microsoft Edge
WebView2 Runtime. Run `Install-ProofMD.cmd` from the release folder to install the
app for the current user.
The installer registers ProofMD as an available handler for `.md` and `.markdown`,
but does not open Windows Default Apps settings or change the existing default app.
Administrator privileges are not required.

When upgrading from LeanMD, close the existing app before running the installer.
ProofMD installs into `%LOCALAPPDATA%\Programs\ProofMD` and replaces the registered
LeanMD installation and Start Menu shortcut. ProofMD appears in Open With and
Default Apps with its own icon. The installer removes obsolete LeanMD history
and retains compatibility registrations only for extensions whose Windows
default choice still references LeanMD. It preserves Windows-owned default
choices and their hashes, including `UserChoiceLatest`.

To complete the rename of an existing LeanMD default choice, select ProofMD in
the file's **Open with** dialog and choose **Always**. Reinstalling then retires
the unused compatibility registrations. Verify an Explorer double-click and the
file icon after changing the default; a successful direct executable launch
alone does not verify the Explorer association.

On first launch, ProofMD copies the saved window position and moves the WebView2
profile from `%LOCALAPPDATA%\LeanMD` to `%LOCALAPPDATA%\ProofMD`. Existing ProofMD
settings take precedence. The internal viewer origin stays `leanmd.local` so the
saved light/dark preference remains accessible; the preference is then saved
under the new `proofmd-theme` key. A locked legacy profile produces a retry
message, preserving the original data. Uninstalling ProofMD removes its current
profile and registrations; any remaining legacy settings backup is retained.

On first launch, the desktop window uses most of the primary monitor's working
area. On subsequent launches it restores the last normal size and position,
including whether the window was maximized. If the saved monitor is no longer
available, ProofMD falls back to a large centered window on the primary display.
The window remains transparent while WebView2 prepares the viewer shell. It is
revealed only after the shell has painted, and Markdown rendering starts after
the visible loading state has painted once.

## Publish a tagged release

Starting with 2.0.0, official releases use an annotated `v<version>` Git tag and
a GitHub Release containing the Windows x64 ZIP. Keep published tags fixed so
each download corresponds to one source revision.

1. Update the stable version in `package.json`, `ProofMD.csproj`, `app.manifest`,
   the installer display version, and the latest-release line above. Write
   `release-notes/<version>.md` describing changes and installation requirements.
2. Run the web, desktop, and installer tests documented above. Stage all changes
   with `git add -A`, then run `git commit` separately to create the release commit.
3. From that clean checkout, run the release build with `-Archive`. Check the
   published executable's version and the ZIP contents.
4. Create the annotated tag, push the source commit and tag together, then upload
   the ZIP to a draft GitHub Release. After checking the draft, publish it.

The following commands run in Windows PowerShell from the repository root after
the source commit and release ZIP are ready. GitHub CLI must be authenticated:

```powershell
$version = (Get-Content package.json -Raw | ConvertFrom-Json).version
$tag = "v$version"
git tag -a $tag -m "ProofMD $version"
git push --atomic origin HEAD:main $tag
gh release create $tag "release/ProofMD-$version-win-x64.zip" --verify-tag --draft --title "ProofMD $version" --notes-file "release-notes/$version.md"
gh release edit $tag --draft=false --latest
```

For this version, the tag is `v2.0.2` and the asset is
`ProofMD-2.0.2-win-x64.zip`. Local release folders remain generated output;
GitHub Releases stores the downloadable ZIP alongside the tagged source.
