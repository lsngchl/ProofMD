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
$legacyChoices = @('.md', '.markdown') | Where-Object {
    (Get-ProofMDUserChoice $installation.RegistryRoot $_) -in @('LeanMD.Markdown', 'Applications\LeanMD.exe')
}
if ($legacyChoices) {
    Write-Host ("Windows still records a LeanMD default for {0}. Select ProofMD in Open with and choose Always to complete the rename." -f
        ($legacyChoices -join ', '))
}
