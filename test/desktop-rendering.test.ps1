[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ApplicationPath,
    [switch]$UseFileAssociation,
    [string[]]$ProofFoldDocuments = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$application = (Resolve-Path -LiteralPath $ApplicationPath).Path
$testId = [Guid]::NewGuid().ToString('N')
$testRoot = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) "ProofMD-rendering-tests-$testId"))
$utf8 = New-Object System.Text.UTF8Encoding($false)
# The app saves its window placement on close; keep the user's placement intact.
$windowStatePath = Join-Path $env:LOCALAPPDATA 'ProofMD\window-state.json'
$savedWindowState = if (Test-Path -LiteralPath $windowStatePath) { [IO.File]::ReadAllBytes($windowStatePath) } else { $null }

function Wait-RenderedDocument($Process, [string]$DocumentPath, [string]$Product) {
    # The viewer assigns this title only after rendering and enhancing the body.
    $expectedTitle = [IO.Path]::GetFileName($DocumentPath) + ' ' + [char]0x2014 + ' ' + $Product
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while ($timer.Elapsed.TotalSeconds -lt 20) {
        $Process.Refresh()
        if ($Process.HasExited) { throw "The app exited before rendering $DocumentPath" }
        if ($Process.MainWindowTitle -eq $expectedTitle) {
            if ($Process.Path -ne $application) { throw "Unexpected renderer executable: $($Process.Path)" }
            return
        }
        Start-Sleep -Milliseconds 100
    }
    throw "Rendering timed out for $DocumentPath. Window title: $($Process.MainWindowTitle)"
}

function Test-RenderedDocument([string]$DocumentPath, [string]$Product, [scriptblock]$AfterRender) {
    $process = $null
    try {
        if ($UseFileAssociation) {
            $process = Start-Process -FilePath $DocumentPath -PassThru
        }
        else {
            $process = Start-Process -FilePath $application -ArgumentList ('"{0}"' -f $DocumentPath) -PassThru
        }
        if ($null -eq $process) { throw "Windows did not return the process opening $DocumentPath" }
        Wait-RenderedDocument $process $DocumentPath $Product
        if ($AfterRender) { & $AfterRender $process }
        Write-Host "Rendered: $DocumentPath ($Product)"
    }
    finally {
        if ($null -ne $process) {
            if (-not $process.HasExited) {
                [void]$process.CloseMainWindow()
                if (-not $process.WaitForExit(5000)) { $process.Kill(); $process.WaitForExit() }
            }
            $process.Dispose()
        }
    }
}

try {
    $cases = @(
        @{ Name = 'missing-components.md'; Product = 'ProofFold'; Manifest = @{ formatVersion = 1; entry = 'missing-components.md'; foldsDirectory = 'folds'; notationRegistry = 'notation.yaml'; referencesDirectory = 'references' } },
        @{ Name = 'minimal.md'; Product = 'ProofFold'; Manifest = @{ formatVersion = 1; entry = 'minimal.md' } },
        @{ Name = 'minimal.markdown'; Product = 'ProofFold'; Manifest = @{ formatVersion = 1; entry = 'minimal.markdown' } },
        @{ Name = 'broken-manifest.md'; Product = 'ProofMD'; Manifest = '{' },
        @{ Name = 'ordinary.md'; Product = 'ProofMD'; Manifest = $null }
    )
    foreach ($case in $cases) {
        $directory = Join-Path $testRoot $case.Name
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
        $document = Join-Path $directory $case.Name
        [IO.File]::WriteAllText($document, "# Rendering check`n`nThe document body is readable.`n`n[Missing fold](folds/missing.md `"fold`")`n", $utf8)
        if ($null -ne $case.Manifest) {
            $manifest = if ($case.Manifest -is [string]) { $case.Manifest } else { $case.Manifest | ConvertTo-Json }
            [IO.File]::WriteAllText((Join-Path $directory 'prooffold.json'), $manifest, $utf8)
        }
        Test-RenderedDocument $document $case.Product
    }

    $reloadDirectory = Join-Path $testRoot 'reload'
    New-Item -ItemType Directory -Path $reloadDirectory -Force | Out-Null
    $reloadDocument = Join-Path $reloadDirectory 'reload.md'
    $reloadManifest = Join-Path $reloadDirectory 'prooffold.json'
    $validManifest = '{"formatVersion":1,"entry":"reload.md"}'
    [IO.File]::WriteAllText($reloadDocument, '# Reload check', $utf8)
    [IO.File]::WriteAllText($reloadManifest, $validManifest, $utf8)
    Test-RenderedDocument $reloadDocument 'ProofFold' {
        param($process)
        [IO.File]::WriteAllText($reloadManifest, '{', $utf8)
        Wait-RenderedDocument $process $reloadDocument 'ProofMD'
        [IO.File]::WriteAllText($reloadManifest, $validManifest, $utf8)
        Wait-RenderedDocument $process $reloadDocument 'ProofFold'
        Write-Host 'Rendered after manifest corruption and repair.'
    }

    foreach ($document in $ProofFoldDocuments) {
        Test-RenderedDocument (Resolve-Path -LiteralPath $document).Path 'ProofFold'
    }
    Write-Host 'Desktop rendering tests passed.'
}
finally {
    if ($null -ne $savedWindowState) { [IO.File]::WriteAllBytes($windowStatePath, $savedWindowState) }
    elseif (Test-Path -LiteralPath $windowStatePath) { Remove-Item -LiteralPath $windowStatePath -Force }
    $expectedRoot = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) "ProofMD-rendering-tests-$testId"))
    if ($testRoot -ne $expectedRoot -or [IO.Path]::GetDirectoryName($testRoot) -ne [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')) {
        throw "Unexpected rendering test cleanup path: $testRoot"
    }
    if (Test-Path -LiteralPath $testRoot) {
        if ((Get-Item -LiteralPath $testRoot -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw "Rendering test cleanup must not follow directory links: $testRoot"
        }
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
