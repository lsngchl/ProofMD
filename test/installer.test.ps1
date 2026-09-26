[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$installerDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\desktop\ProofMD\Installer'))
. (Join-Path $installerDirectory 'ProofMD.Installation.ps1')

function Assert([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

$testId = [Guid]::NewGuid().ToString('N')
$testRoot = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) "ProofMD-installer-tests-$testId"))
$registryPath = "Software\ProofMD.InstallerTests\$testId"
$registry = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($registryPath)

try {
    foreach ($script in Get-ChildItem -LiteralPath $installerDirectory -Filter '*.ps1') {
        $tokens = $null
        $parseErrors = $null
        [void][Management.Automation.Language.Parser]::ParseFile($script.FullName, [ref]$tokens, [ref]$parseErrors)
        Assert ($parseErrors.Count -eq 0) "Invalid installer syntax: $($script.Name): $parseErrors"
    }

    $source = Join-Path $testRoot 'release'
    $localAppData = Join-Path $testRoot 'Local'
    $programs = Join-Path $testRoot 'Start Menu'
    New-Item -ItemType Directory -Path (Join-Path $source 'Viewer') -Force | Out-Null
    foreach ($file in @('ProofMD.exe', 'ProofMD.dll', 'Viewer\index.html')) {
        [IO.File]::WriteAllText((Join-Path $source $file), 'installer test fixture')
    }
    Get-ChildItem -LiteralPath $installerDirectory -File | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $source
    }
    $paths = Get-ProofMDPaths $localAppData
    $executable = Join-Path $paths.Install 'ProofMD.exe'
    $legacyExecutable = Join-Path $paths.LegacyInstall 'LeanMD.exe'

    Install-ProofMD $source $localAppData $programs $registry
    Assert (Test-Path -LiteralPath $executable) 'Fresh installation should copy ProofMD.exe.'
    Assert (Test-ProofMDCommand $registry 'ProofMD.Markdown' $executable) 'The Markdown class should launch ProofMD.'
    Assert ((Get-ProofMDRegistryValue $registry 'Software\RegisteredApplications' 'ProofMD') -eq
        'Software\ProofMD\Capabilities') 'ProofMD should be registered in Default Apps.'
    Assert ($null -eq (Get-ProofMDRegistryValue $registry 'Software\Classes\LeanMD.Markdown')) 'Fresh installs should not create legacy aliases.'
    $shell = New-Object -ComObject WScript.Shell
    Assert ($shell.CreateShortcut((Join-Path $programs 'ProofMD.lnk')).TargetPath -eq $executable) 'The Start Menu shortcut should target ProofMD.exe.'

    New-Item -ItemType Directory -Path $paths.LegacyInstall -Force | Out-Null
    [IO.File]::WriteAllText($legacyExecutable, 'legacy executable fixture')
    Set-ProofMDRegistryValues $registry 'Software\Microsoft\Windows\CurrentVersion\Uninstall\LeanMD' @{
        InstallLocation = $paths.LegacyInstall
    }
    Set-ProofMDRegistryValues $registry 'Software\RegisteredApplications' @{ LeanMD = 'Software\LeanMD\Capabilities' }
    Set-ProofMDFileClass $registry 'LeanMD.Markdown' $legacyExecutable
    Set-ProofMDFileClass $registry 'Applications\LeanMD.exe' $legacyExecutable
    $legacyShortcut = $shell.CreateShortcut((Join-Path $programs 'LeanMD.lnk'))
    $legacyShortcut.TargetPath = $legacyExecutable
    $legacyShortcut.Save()
    $legacyProfile = Join-Path $localAppData 'LeanMD'
    New-Item -ItemType Directory -Path (Join-Path $legacyProfile 'WebView2') -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $legacyProfile 'window-state.json'), 'legacy window state')
    foreach ($extension in @('.md', '.markdown')) {
        Set-ProofMDRegistryValues $registry "Software\Classes\$extension\OpenWithProgids" @{ 'LeanMD.Markdown' = '' }
        $defaultClass = if ($extension -eq '.md') { 'LeanMD.Markdown' } else { 'Applications\LeanMD.exe' }
        Set-ProofMDRegistryValues $registry "Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\$extension\UserChoice" @{
            ProgId = $defaultClass
            Hash = 'preserve-this-Windows-owned-value'
        }
    }

    Install-ProofMD $source $localAppData $programs $registry
    Assert (-not (Test-Path -LiteralPath $paths.LegacyInstall)) 'Upgrade should remove the registered legacy installation.'
    Assert (-not (Test-Path -LiteralPath (Join-Path $programs 'LeanMD.lnk'))) 'Upgrade should remove the old shortcut.'
    Assert (Test-Path -LiteralPath (Join-Path $legacyProfile 'window-state.json')) 'Installer should retain legacy settings for first-launch migration.'
    Assert ($null -eq (Get-ProofMDRegistryValue $registry 'Software\Microsoft\Windows\CurrentVersion\Uninstall\LeanMD' 'InstallLocation')) 'Upgrade should retire the old uninstall entry.'
    Assert ($null -eq (Get-ProofMDRegistryValue $registry 'Software\RegisteredApplications' 'LeanMD')) 'Upgrade should retire the old app listing.'
    foreach ($class in @('LeanMD.Markdown', 'Applications\LeanMD.exe')) {
        Assert (Test-ProofMDCommand $registry $class $executable) "Existing defaults should launch ProofMD through $class."
    }
    foreach ($extension in @('.md', '.markdown')) {
        $choice = "Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\$extension\UserChoice"
        $expectedClass = if ($extension -eq '.md') { 'LeanMD.Markdown' } else { 'Applications\LeanMD.exe' }
        Assert ((Get-ProofMDRegistryValue $registry $choice 'ProgId') -eq $expectedClass) 'Upgrade should preserve the default app ProgID.'
        Assert ((Get-ProofMDRegistryValue $registry $choice 'Hash') -eq 'preserve-this-Windows-owned-value') 'Upgrade should preserve the default app hash.'
        Assert ($null -eq (Get-ProofMDRegistryValue $registry "Software\Classes\$extension\OpenWithProgids" 'LeanMD.Markdown')) 'Open With should list the new app registration.'
    }

    New-Item -ItemType Directory -Path $paths.Profile -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $paths.Profile 'window-state.json'), 'new ProofMD preference')
    Install-ProofMD $source $localAppData $programs $registry
    Assert ([IO.File]::ReadAllText((Join-Path $paths.Profile 'window-state.json')) -eq 'new ProofMD preference') 'Reinstall should preserve ProofMD settings.'

    $otherExecutable = Join-Path $testRoot 'separate-install\LeanMD.exe'
    Set-ProofMDFileClass $registry 'LeanMD.Markdown' $otherExecutable
    Remove-ProofMDRegistration $registry $paths.Install
    Assert (-not (Test-ProofMDCommand $registry 'ProofMD.Markdown' $executable)) 'Uninstall should remove ProofMD registration.'
    Assert (-not (Test-ProofMDCommand $registry 'Applications\LeanMD.exe' $executable)) 'Uninstall should remove its compatibility registration.'
    Assert (Test-ProofMDCommand $registry 'LeanMD.Markdown' $otherExecutable) 'Uninstall should preserve a legacy registration now owned by another installation.'

    $incomplete = Join-Path $testRoot 'incomplete'
    New-Item -ItemType Directory -Path $incomplete | Out-Null
    $rejected = $false
    try { Install-ProofMD $incomplete $localAppData $programs $registry }
    catch { $rejected = $true }
    Assert $rejected 'An incomplete release should fail before changing app registration.'
    Assert (-not (Test-ProofMDCommand $registry 'ProofMD.Markdown' $executable)) 'Failed installation should not register missing files.'
    $rejected = $false
    try { Assert-ProofMDPath $localAppData $paths.Install }
    catch { $rejected = $true }
    Assert $rejected 'Cleanup validation should reject paths outside the exact app directory.'

    Write-Host 'ProofMD installer tests passed: fresh install, upgrade, default-app compatibility, settings, reinstall, uninstall, and path validation.'
}
finally {
    $registry.Close()
    [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($registryPath, $false)
    $expectedRoot = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) "ProofMD-installer-tests-$testId"))
    Assert-ProofMDPath $testRoot $expectedRoot
    if (Test-Path -LiteralPath $testRoot) { Remove-Item -LiteralPath $testRoot -Recurse -Force }
}
