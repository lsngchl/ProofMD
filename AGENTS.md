# Repository agent instructions

## Windows-first repository tooling

Treat the Windows environment as authoritative for this repository. It builds,
tests, packages, and releases a Windows desktop application from a working tree
on the Windows filesystem.

- Run dependency installation, Node package scripts, automated tests, Vite
  asset builds, .NET restore/build/publish commands, installer operations, and
  release commands with Windows executables and Windows working-tree paths.
- Do not try the WSL version of a project tool first. WSL utilities may be used
  for read-only inspection, searching, and file editing, but not as the runtime
  used to test or build the application.
- A Windows process launched from WSL may inherit WSL's process `PATH` instead
  of the user `PATH` assembled by Windows. If a Windows tool does not resolve by
  name, use its full Windows installation path rather than falling back to the
  WSL tool. In particular, use `C:\Program Files\nodejs\npm.cmd` for npm and
  `C:\Program Files\dotnet\dotnet.exe` for .NET when necessary.

## Windows Git

Use Windows Git for every Git operation in this repository. The working tree
is stored on the Windows filesystem and is used to develop and release a
Windows desktop application. When operating from WSL, invoke
`/mnt/c/Program Files/Git/cmd/git.exe` with the Windows working-tree path (for
this checkout, `-C C:/GitRepos/LeanMD`) instead of invoking WSL's
`/usr/bin/git` against this working tree.

## Latest release in README

Keep the latest stable release version near the top of the root `README.md` in the `**Latest release: <version>**` line.
Whenever the stable release version changes, update that line as part of the same change and keep it consistent with the tracked package, executable, manifest, and installer versions.
Record only stable release versions in this line; do not record test, development, preview, nightly, release-candidate, or other non-production build versions.

## Local release requests

In this repository, a request to "release" the current version means rebuilding
the gitignored local `release/LeanMD-<version>/` folder from the currently
checked-out source. The release folder is generated locally because it is not
restored when source changes are fetched from Git. If the user also asks to
update this machine, install the application from that regenerated local release
folder.

Follow the release commands documented in `README.md` in order. Treat every
step as fail-fast: if the web asset build fails, stop and do not run
`dotnet publish`. Before publishing, remove only the exact target-version output
folder so stale files cannot survive into the regenerated release.

Do not create or push Git tags, create GitHub Releases, upload artifacts, or
create release archives or checksums unless the user explicitly requests those
remote or packaging actions.
