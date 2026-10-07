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
