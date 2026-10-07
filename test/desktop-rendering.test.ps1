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
    # Two windows cover what only the real app can show: the viewer starting in WebView2,
    # ProofFold rendering and live reload (.md), and ordinary Markdown (.markdown).
    # Manifest variations are covered by the desktop tests without opening windows.
    $proofFoldDirectory = Join-Path $testRoot 'prooffold'
    New-Item -ItemType Directory -Path (Join-Path $proofFoldDirectory 'folds') -Force | Out-Null
    $proofFoldDocument = Join-Path $proofFoldDirectory 'main.md'
    $manifestPath = Join-Path $proofFoldDirectory 'prooffold.json'
    $validManifest = '{"formatVersion":1,"entry":"main.md"}'
    [IO.File]::WriteAllText($proofFoldDocument, "# Rendering check`n`nThe body is \(x^2\).`n`n[Fold: step](folds/step.md `"fold`")`n", $utf8)
    [IO.File]::WriteAllText((Join-Path $proofFoldDirectory 'folds\step.md'), 'Fold body.', $utf8)
    [IO.File]::WriteAllText($manifestPath, $validManifest, $utf8)
    Test-RenderedDocument $proofFoldDocument 'ProofFold' {
        param($process)
        [IO.File]::WriteAllText($manifestPath, '{', $utf8)
        Wait-RenderedDocument $process $proofFoldDocument 'ProofMD'
        [IO.File]::WriteAllText($manifestPath, $validManifest, $utf8)
        Wait-RenderedDocument $process $proofFoldDocument 'ProofFold'
        Write-Host 'Reloaded after the manifest was damaged and repaired.'
    }

    $ordinaryDocument = Join-Path $testRoot 'ordinary.markdown'
    [IO.File]::WriteAllText($ordinaryDocument, "# Ordinary`n`nPlain Markdown with `$e^{i\pi}+1=0`$.`n", $utf8)
    Test-RenderedDocument $ordinaryDocument 'ProofMD'

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
