# Repository agent instructions

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
