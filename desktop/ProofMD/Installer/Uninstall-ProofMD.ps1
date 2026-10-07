[CmdletBinding()]
param([switch]$Cleanup)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ProofMD.Installation.ps1')

$paths = Get-ProofMDPaths ([Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData))
Assert-ProofMDStopped (Join-Path $paths.Install 'ProofMD.exe')

if ($Cleanup) {
    # Runs hidden after the visible uninstaller exits, because files of a running script's
    # folder cannot all be removed by that script.
    try {
        foreach ($target in @($paths.Install, $paths.Profile)) { Remove-ProofMDDirectory $target }
    }
    catch {
        Add-Type -AssemblyName System.Windows.Forms
        [void][System.Windows.Forms.MessageBox]::Show(
            "ProofMD was unregistered, but some files could not be removed.`n`n$($_.Exception.Message)",
            'ProofMD')
    }
    return
}

Remove-ProofMDRegistration ([Microsoft.Win32.Registry]::CurrentUser) $paths.Install
$shortcut = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::Programs)) 'ProofMD.lnk'
if (Test-Path -LiteralPath $shortcut) { Remove-Item -LiteralPath $shortcut -Force }
Send-ProofMDShellNotification
Start-Process powershell.exe -WindowStyle Hidden -WorkingDirectory ([IO.Path]::GetTempPath()) -ArgumentList @(
    '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('"{0}"' -f $PSCommandPath), '-Cleanup')
Write-Host 'ProofMD was unregistered. Its files are being removed.'
