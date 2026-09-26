[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ProofMD.Installation.ps1')

$installation = @{
    SourceDirectory = $PSScriptRoot
    LocalAppData = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
    ProgramsDirectory = [Environment]::GetFolderPath([Environment+SpecialFolder]::Programs)
    RegistryRoot = [Microsoft.Win32.Registry]::CurrentUser
}
Install-ProofMD @installation
Send-ProofMDShellNotification
Write-Host 'ProofMD was installed successfully. Existing settings transfer on first launch.'
