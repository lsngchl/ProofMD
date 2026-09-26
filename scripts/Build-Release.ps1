[CmdletBinding()]
param([switch]$Archive)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$package = Get-Content -LiteralPath (Join-Path $repoRoot 'package.json') -Raw | ConvertFrom-Json
$version = $package.version
if ($version -notmatch '^\d+\.\d+\.\d+$') {
    throw "A stable release version is required: $version"
}

[xml]$project = Get-Content -LiteralPath (Join-Path $repoRoot 'desktop\ProofMD\ProofMD.csproj')
$properties = $project.Project.PropertyGroup
if ($properties.Version -ne $version -or
    $properties.InformationalVersion -ne $version -or
    $properties.AssemblyVersion -ne "$version.0" -or
    $properties.FileVersion -ne "$version.0") {
    throw 'Package and executable versions must match before releasing.'
}
[xml]$manifest = Get-Content -LiteralPath (Join-Path $repoRoot 'desktop\ProofMD\app.manifest')
if ($manifest.assembly.assemblyIdentity.version -ne "$version.0") {
    throw 'The application manifest version must match the release.'
}
$installer = Get-Content -LiteralPath (Join-Path $repoRoot 'desktop\ProofMD\Installer\ProofMD.Installation.ps1') -Raw
if ($installer -notmatch ("DisplayVersion\s*=\s*'" + [regex]::Escape($version) + "'")) {
    throw 'The installer display version must match the release.'
}
$readme = Get-Content -LiteralPath (Join-Path $repoRoot 'README.md') -Raw
if (-not $readme.Contains("**Latest release: $version**")) {
    throw 'The README latest stable release must match the release.'
}

$dotnetCommand = Get-Command dotnet.exe -ErrorAction SilentlyContinue
$dotnet = if ($dotnetCommand) { $dotnetCommand.Source } else { 'C:\Program Files\dotnet\dotnet.exe' }
$pnpm = (Get-Command pnpm.cmd -ErrorAction Stop).Source
$releaseRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'release'))
$outputDirectory = [IO.Path]::GetFullPath((Join-Path $releaseRoot "ProofMD-$version"))

Push-Location $repoRoot
try {
    & $pnpm build
    if ($LASTEXITCODE -ne 0) { throw 'Web asset build failed; publishing was stopped.' }

    # Delete only this version's exact output, after validating the resolved path.
    if ([IO.Path]::GetDirectoryName($outputDirectory) -ne $releaseRoot -or
        [IO.Path]::GetFileName($outputDirectory) -ne "ProofMD-$version") {
        throw "Unexpected release output path: $outputDirectory"
    }
    foreach ($directory in @($releaseRoot, $outputDirectory)) {
        if ((Test-Path -LiteralPath $directory) -and
            ((Get-Item -LiteralPath $directory -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Release output must not use directory links: $directory"
        }
    }
    if (Test-Path -LiteralPath $outputDirectory) {
        Remove-Item -LiteralPath $outputDirectory -Recurse -Force
    }

    & $dotnet publish desktop/ProofMD/ProofMD.csproj -c Release -r win-x64 --self-contained false -o $outputDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Windows publishing failed.' }

    $executable = Get-Item -LiteralPath (Join-Path $outputDirectory 'ProofMD.exe')
    if ($executable.VersionInfo.ProductName -ne 'ProofMD' -or
        $executable.VersionInfo.FileVersion -ne "$version.0") {
        throw 'Published executable metadata does not match the release.'
    }
    foreach ($required in @('ProofMD.dll', 'ProofMD.runtimeconfig.json', 'Viewer\index.html',
        'Install-ProofMD.cmd', 'Install-ProofMD.ps1', 'Uninstall-ProofMD.cmd',
        'Uninstall-ProofMD.ps1', 'ProofMD.Installation.ps1')) {
        if (-not (Test-Path -LiteralPath (Join-Path $outputDirectory $required) -PathType Leaf)) {
            throw "Published release is missing $required"
        }
    }

    Write-Host "Release folder: $outputDirectory"
    if ($Archive) {
        $archivePath = Join-Path $releaseRoot "ProofMD-$version-win-x64.zip"
        Compress-Archive -LiteralPath $outputDirectory -DestinationPath $archivePath -Force
        Write-Host "Release asset: $archivePath"
    }
}
finally {
    Pop-Location
}
