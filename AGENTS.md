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
`/mnt/c/Program Files/Git/cmd/git.exe` with `-C` and the Windows form of the
working-tree path (`C:/...`, not `/mnt/c/...`) instead of invoking WSL's
`/usr/bin/git` against this working tree.

## Latest release in README

Keep the latest stable release version near the top of the root `README.md` in the `**Latest release: <version>**` line.
Whenever the stable release version changes, update that line as part of the same change and keep it consistent with the tracked package, executable, manifest, and installer versions.
Record only stable release versions in this line; do not record test, development, preview, nightly, release-candidate, or other non-production build versions.

## Tagged releases from 2.0.0

Starting with 2.0.0, a request to release a version means preparing its source
commit, an annotated `v<version>` Git tag, and a GitHub Release with the Windows
x64 ZIP attached. Follow the release procedure in `README.md`, including the
version checks, tests, and tracked `release-notes/<version>.md` file.

Build with `scripts/Build-Release.ps1 -Archive` on Windows. It regenerates the
gitignored `release/ProofMD-<version>/` folder and its ZIP. Every build step is
fail-fast: a failed web build must stop before `dotnet publish`. Remove only the
exact target-version output folder after validating its absolute path.

Commit the release source before creating its tag, and publish the branch and
tag together. Never move an already published release tag. Upload the ZIP to a
draft GitHub Release, verify its tag and asset, then publish the release.
Authenticated `gh` commands must run in the normal Windows environment.

An explicitly local-only build skips tags, pushes, and GitHub publishing. Update
the app installed on this machine only when the user requests installation.
Checksums and additional distribution formats require an explicit request.
