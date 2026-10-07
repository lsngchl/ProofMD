[CmdletBinding()]
param(
    # Creates release/ProofMD-<version>-win-x64.zip for a GitHub Release.
    [switch]$Archive,
    # Builds the working tree as <version>-local, which installs normally but must not be published.
    [switch]$Local
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectPath = Join-Path $repoRoot 'desktop\ProofMD\ProofMD.csproj'
$version = ([xml](Get-Content -LiteralPath $projectPath -Raw)).SelectSingleNode('/Project/PropertyGroup/Version').InnerText
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "ProofMD.csproj must declare a stable Version: $version" }
if ($Local -and $Archive) { throw 'A local build cannot be archived for publishing.' }
$readme = Get-Content -LiteralPath (Join-Path $repoRoot 'README.md') -Raw
if (-not $readme.Contains("**Latest release: $version**")) {
    throw "README.md must name $version as the latest release."
}

function Invoke-Step([string]$Description, [scriptblock]$Command) {
    Write-Host "== $Description"
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "$Description failed." }
}

Push-Location $repoRoot
try {
    $productVersion = $version
    if ($Local) {
        $productVersion = "$version-local"
    }
    else {
        if (git status --porcelain) {
            throw 'Commit or stash changes before building a release, or pass -Local.'
        }
        $tagCommit = git rev-parse --verify --quiet "refs/tags/v$version^{commit}"
        if ($tagCommit -and $tagCommit -ne (git rev-parse HEAD)) {
            throw "Tag v$version already points to another commit; raise the version in ProofMD.csproj."
        }
    }

    $env:COREPACK_ENABLE_DOWNLOAD_PROMPT = '0'
    Invoke-Step 'Install web dependencies' { corepack pnpm install --frozen-lockfile }
    Invoke-Step 'Web tests' { corepack pnpm test }
    Invoke-Step 'Desktop tests' { dotnet run --project test/ProofMD.DesktopTests/ProofMD.DesktopTests.csproj }
    Invoke-Step 'Installer tests' { powershell.exe -NoProfile -ExecutionPolicy Bypass -File test/installer.test.ps1 }
    Invoke-Step 'Web build' { corepack pnpm build }

    $releaseRoot = Join-Path $repoRoot 'release'
    $outputDirectory = Join-Path $releaseRoot "ProofMD-$version"
    foreach ($directory in @($releaseRoot, $outputDirectory)) {
        if ((Test-Path -LiteralPath $directory) -and
            ((Get-Item -LiteralPath $directory -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Release output must not use directory links: $directory"
        }
    }
    if (Test-Path -LiteralPath $outputDirectory) { Remove-Item -LiteralPath $outputDirectory -Recurse -Force }
    Invoke-Step 'Publish' {
        dotnet publish $projectPath -c Release -o $outputDirectory "-p:InformationalVersion=$productVersion"
    }

    $executable = Get-Item -LiteralPath (Join-Path $outputDirectory 'ProofMD.exe')
    if ($executable.VersionInfo.ProductVersion -ne $productVersion) {
        throw "Published ProofMD.exe reports $($executable.VersionInfo.ProductVersion), not $productVersion."
    }
    Write-Host "Release folder: $outputDirectory"

    if ($Archive) {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $archivePath = Join-Path $releaseRoot "ProofMD-$version-win-x64.zip"
        if (Test-Path -LiteralPath $archivePath) { Remove-Item -LiteralPath $archivePath -Force }
        [IO.Compression.ZipFile]::CreateFromDirectory(
            $outputDirectory, $archivePath, [IO.Compression.CompressionLevel]::Optimal, $true)
        Write-Host "Release asset: $archivePath"
    }
}
finally {
    Pop-Location
}
