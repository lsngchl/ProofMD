[CmdletBinding()]
param([switch]$Cleanup)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ProofMD.Installation.ps1')

$localAppData = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
$paths = Get-ProofMDPaths $localAppData
Assert-ProofMDPath $paths.Install (Join-Path $localAppData 'Programs\ProofMD')
Assert-ProofMDPath $paths.Profile (Join-Path $localAppData 'ProofMD')
Assert-ProofMDStopped @((Join-Path $paths.Install 'ProofMD.exe'))

if ($Cleanup) {
    Start-Sleep -Milliseconds 750
    foreach ($target in @($paths.Install, $paths.Profile)) {
        Assert-ProofMDPath $target $target
        if (Test-Path -LiteralPath $target) {
            Remove-Item -LiteralPath $target -Recurse -Force
        }
    }
    return
}

Remove-ProofMDRegistration ([Microsoft.Win32.Registry]::CurrentUser) $paths.Install
$programs = [Environment]::GetFolderPath([Environment+SpecialFolder]::Programs)
$shortcutPath = Join-Path $programs 'ProofMD.lnk'
if (Test-Path -LiteralPath $shortcutPath) {
    Remove-Item -LiteralPath $shortcutPath -Force
}
Send-ProofMDShellNotification
Start-Process powershell.exe -ArgumentList @(
    '-NoProfile', '-ExecutionPolicy', 'Bypass',
    '-File', ('"{0}"' -f $PSCommandPath), '-Cleanup') -WindowStyle Hidden
Write-Host 'ProofMD was unregistered and will be removed.'
