# ProofMD

**Latest release: 2.0.2**

[Download the Windows release](https://github.com/lsngchl/ProofMD/releases/latest)

ProofMD is a Windows viewer for Markdown documents that contain LaTeX
mathematics. It renders everything locally and reloads a document as soon as
the file changes on disk.

## Features

- Inline mathematics with `\(...\)` or `$...$`, display mathematics with
  `\[...\]` or `$$...$$`, rendered with KaTeX
- Footnotes, and empty HTML anchors such as `<a id="limits"></a>` as targets
  for links like `[Limits](#limits)`
- Relative links to other Markdown files open in the same window, and
  Backspace returns to the previous document at the same reading position
- An exploration map of the documents reached by following links
- ProofFold documents, whose folded passages expand in place (see below)
- Per-document "Unresolved" markers, shown on the map
- Light and dark themes, and print styles

## Keyboard

| Key | Action |
|---|---|
| `M` | Open or close the map |
| `Backspace` or `,` | Return to the previous document |
| `Esc` | Close the map or the keyboard guide |
| `Ctrl+O` | Open a Markdown file |

## ProofFold documents

A ProofFold document is a folder whose `prooffold.json` names an entry file:

```json
{ "formatVersion": 1, "entry": "main.md", "foldsDirectory": "folds" }
```

When the entry file is opened, links titled `"fold"`, such as
`[Fold: bound the error](folds/error.md "fold")`, expand the linked file in
place instead of navigating to it, and the map shows the complete fold
structure. `foldsDirectory` defaults to `folds`. If the manifest is missing or
invalid, the file opens as ordinary Markdown. `DocumentFormats/ProofFold/`
contains a template and the authoring rules.

## Unresolved markers

The **Unresolved** button marks the current document as not yet understood by
creating `<document file name>.unresolved` beside it, for example
`notes.md.unresolved`. Clicking it again removes the file.

## Install

ProofMD needs the Microsoft .NET 10 Desktop Runtime (x64) and the Microsoft Edge
WebView2 Runtime.

1. Download `ProofMD-<version>-win-x64.zip` from the latest release and extract
   the whole folder.
2. Close ProofMD if it is running, then run `Install-ProofMD.cmd` from the
   extracted folder. Installation is per user and needs no administrator rights.
3. To open Markdown files with ProofMD by default, right-click a `.md` file,
   choose **Open with**, select ProofMD, and choose **Always**.

ProofMD installs into `%LOCALAPPDATA%\Programs\ProofMD` and keeps its settings in
`%LOCALAPPDATA%\ProofMD`. Remove it from **Settings > Apps > Installed apps**.

## Build from source

The build runs on Windows with Node.js (for `corepack`) and the .NET 10 SDK:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Build-Release.ps1
```

The script runs every test, builds the viewer, and writes the app to
`release/ProofMD-<version>/`, where `Install-ProofMD.cmd` installs it.
