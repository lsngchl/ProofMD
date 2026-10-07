# Agent rules for ProofMD

## Scope

- ProofMD is a Windows-only app: a WinForms host (`desktop/ProofMD/`) runs the
  web viewer (`index.html`, `src/`) in WebView2. The viewer has no browser mode;
  it requires `window.chrome.webview`.
- Build, test, install, and release only on Windows. On other machines, limit
  work to reading and editing files.
- The viewer is served from the virtual host `proofmd.example`, mapped to the
  app's `Viewer` folder. Markdown never reaches the host page except as text
  rendered by markdown-it with raw HTML disabled.

## Layout

Host (`desktop/ProofMD/`):

- `MainForm.cs`: the window and WebView2 wiring only. It applies
  `NavigationPolicy` (the viewer page is the only page ever shown; web and mail
  links go to the system), filters the context menu, and hands every page
  message to the session after WebView2's callback returns.
- `ViewerSession.cs`: the open document, back history, and exploration map,
  with no window dependency. Operations run one at a time and change state only
  after all reads succeed. Test new host behavior here.
- `MarkdownPaths.cs`: link resolution and `Canonicalize`, which gives every
  document its on-disk casing so the viewer can compare ids exactly.
- `ProofFoldStructure.cs` loads the manifest and fold sources only.
  `DocumentWatcher.cs` reports file changes.

Viewer (`src/`):

- `main.js` (startup, keys, host messages), `document-view.js` (rendering,
  folds, link clicks, reading position), `map-view.js`, and `ui.js` (elements,
  host bridge).
- `renderer.js` builds the markdown-it pipeline from the plugins in `src/`.
  The ProofFold map is derived in the viewer from the same parse
  (`foldLinkTargets` and `buildProofFoldMap`), so the map and the rendered folds
  cannot disagree. Do not add a second fold-link parser in the host.
- Pure logic lives in modules without DOM access (`links.js`, `position.js`,
  `proof-fold.js`, `map-layout.js`, the plugins) and is covered by `test/*.test.js`.
- Messages are JSON objects with a `type`. A new type needs both
  `MainForm.HandleMessageAsync` and the handler in `main.js`.
- To check the UI without the host, serve `dist-desktop/` with a script that
  defines `window.chrome.webview` before the viewer module loads, records
  `postMessage` calls, and delivers host messages to the registered listener.

## Commands

Run from the repository root. pnpm is pinned in `package.json` and runs through
corepack, so it does not need to be on `PATH`.

| Purpose | Command |
|---|---|
| Install web dependencies | `corepack pnpm install --frozen-lockfile` |
| Web tests | `corepack pnpm test` |
| Build viewer assets into `dist-desktop/` | `corepack pnpm build` |
| Desktop tests | `dotnet run --project test/ProofMD.DesktopTests/ProofMD.DesktopTests.csproj` |
| Installer tests | `powershell -NoProfile -ExecutionPolicy Bypass -File test/installer.test.ps1` |
| End-to-end rendering | `powershell -NoProfile -ExecutionPolicy Bypass -File test/desktop-rendering.test.ps1 -ApplicationPath <ProofMD.exe>` |
| Release build | `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Build-Release.ps1 [-Archive \| -Local]` |

- `dotnet build` of the desktop project requires `dist-desktop/` from the web
  build.
- The end-to-end test opens real ProofMD windows for a few seconds. Pass
  `-ProofFoldDocuments <main.md>...` to also render real ProofFold documents.
- The installer tests write only under `HKCU\Software\ProofMD.InstallerTests`
  and remove it afterwards.
- Install the app on this machine only when the user asks.

## Versions and releases

- `<Version>` in `desktop/ProofMD/ProofMD.csproj` is the only version number.
  The installer reads it from the executable. `README.md` names the latest
  stable release in its `**Latest release: <version>**` line; update that line
  only when a stable release is published.
- A release request means: raise `<Version>` and the README line, write
  `release-notes/<version>.md` (changes and installation requirements), commit,
  run `scripts/Build-Release.ps1 -Archive` on the clean commit, then:

  ```powershell
  $version = '<version>'
  git tag -a "v$version" -m "ProofMD $version"
  git push --atomic origin HEAD:main "v$version"
  gh release create "v$version" "release/ProofMD-$version-win-x64.zip" --verify-tag --draft --title "ProofMD $version" --notes-file "release-notes/$version.md"
  gh release edit "v$version" --draft=false --latest
  ```

- Check the draft's tag and asset before publishing it. Never move a published
  tag.
- `-Local` builds the working tree as `<version>-local` (shown in Installed
  apps) for trying changes on this machine; it is never published.

## ProofFold

`DocumentFormats/ProofFold/AGENTS.md` holds the authoring rules for ProofFold
documents and the template folder. Viewer behavior for ProofFold is described in
`README.md`.
