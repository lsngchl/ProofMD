[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ProofMD.Installation.ps1')

Install-ProofMD `
    -SourceDirectory $PSScriptRoot `
    -LocalAppData ([Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)) `
    -ProgramsDirectory ([Environment]::GetFolderPath([Environment+SpecialFolder]::Programs)) `
    -RegistryRoot ([Microsoft.Win32.Registry]::CurrentUser)
Send-ProofMDShellNotification
Write-Host 'ProofMD was installed. To make it the default, choose it in Open with > Always for a .md file.'
