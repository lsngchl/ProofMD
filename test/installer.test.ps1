[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$installerDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\desktop\ProofMD\Installer'))
. (Join-Path $installerDirectory 'ProofMD.Installation.ps1')

function Assert([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Test-RegistryValue($Registry, [string]$Path, [string]$Name) {
    $key = $Registry.OpenSubKey($Path)
    if ($null -eq $key) { return $false }
    try { return $key.GetValueNames() -contains $Name }
    finally { $key.Close() }
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
    Assert ($shell.CreateShortcut((Join-Path $programs 'ProofMD.lnk')).IconLocation -eq "$executable,0") 'The Start Menu shortcut should have an explicit ProofMD icon.'
    Assert ((Get-ProofMDRegistryValue $registry 'Software\ProofMD\Capabilities' 'ApplicationIcon') -eq
        ('"{0}",0' -f $executable)) 'Default Apps should have an explicit ProofMD icon.'

    New-Item -ItemType Directory -Path $paths.LegacyInstall -Force | Out-Null
    [IO.File]::WriteAllText($legacyExecutable, 'legacy executable fixture')
    Set-ProofMDRegistryValues $registry 'Software\Microsoft\Windows\CurrentVersion\Uninstall\LeanMD' @{
        InstallLocation = $paths.LegacyInstall
    }
    Set-ProofMDRegistryValues $registry 'Software\RegisteredApplications' @{ LeanMD = 'Software\LeanMD\Capabilities' }
    Set-ProofMDRegistryValues $registry 'Software\LeanMD\Capabilities' @{ ApplicationName = 'LeanMD' }
    Set-ProofMDRegistryValues $registry 'Software\LeanMD\Capabilities\FileAssociations' @{
        '.md' = 'LeanMD.Markdown'
        '.markdown' = 'LeanMD.Markdown'
    }
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
        Set-ProofMDRegistryValues $registry "Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\$extension\OpenWithProgids" @{
            'LeanMD.Markdown' = ''; 'Applications\LeanMD.exe' = ''; 'OtherEditor.Markdown' = ''
        }
        Set-ProofMDRegistryValues $registry "Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\$extension\OpenWithList" @{
            a = 'Code.exe'; b = 'LeanMD.exe'; c = 'ProofMD.exe'; MRUList = 'cba'
        }
        $defaultClass = if ($extension -eq '.md') { 'LeanMD.Markdown' } else { 'Applications\LeanMD.exe' }
        Set-ProofMDRegistryValues $registry "Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\$extension\UserChoice" @{
            ProgId = $defaultClass
            Hash = 'preserve-this-Windows-owned-value'
        }
        Set-ProofMDRegistryValues $registry "Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\$extension\UserChoiceLatest\ProgId" @{
            ProgId = $defaultClass
            Hash = 'preserve-the-current-Windows-choice'
        }
    }
    $muiPath = 'Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache'
    Set-ProofMDRegistryValues $registry $muiPath @{
        ($legacyExecutable + '.FriendlyAppName') = 'LeanMD'
        ($legacyExecutable + '.ApplicationCompany') = 'LeanMD'
        ('C:\another-install\LeanMD.exe.FriendlyAppName') = 'Keep this name'
    }

    Install-ProofMD $source $localAppData $programs $registry
    Assert (-not (Test-Path -LiteralPath $paths.LegacyInstall)) 'Upgrade should remove the registered legacy installation.'
    Assert (-not (Test-Path -LiteralPath (Join-Path $programs 'LeanMD.lnk'))) 'Upgrade should remove the old shortcut.'
    Assert (Test-Path -LiteralPath (Join-Path $legacyProfile 'window-state.json')) 'Installer should retain legacy settings for first-launch migration.'
    Assert ($null -eq (Get-ProofMDRegistryValue $registry 'Software\Microsoft\Windows\CurrentVersion\Uninstall\LeanMD' 'InstallLocation')) 'Upgrade should retire the old uninstall entry.'
    Assert ((Get-ProofMDRegistryValue $registry 'Software\RegisteredApplications' 'LeanMD') -eq
        'Software\LeanMD\Capabilities') 'Upgrade should preserve the registered identity used by the existing default app.'
    Assert ($null -eq (Get-ProofMDRegistryValue $registry 'Software\LeanMD\Capabilities' 'ApplicationName')) 'The compatibility identity should derive its display name from ProofMD.exe.'
    Assert ((Get-ProofMDRegistryValue $registry 'Software\LeanMD\Capabilities' 'Hidden') -eq 1) 'Only ProofMD should be advertised in Default Apps.'
    foreach ($class in @('LeanMD.Markdown', 'Applications\LeanMD.exe')) {
        Assert (Test-ProofMDCommand $registry $class $executable) "Existing defaults should launch ProofMD through $class."
        Assert ($null -eq (Get-ProofMDRegistryValue $registry "Software\Classes\$class" 'NoOpenWith')) 'The selected default must remain available to the Shell.'
        Assert ((Get-ProofMDRegistryValue $registry "Software\Classes\$class\DefaultIcon") -eq
            ('"{0}",0' -f $executable)) 'Existing default icons should resolve to ProofMD.exe.'
    }
    foreach ($extension in @('.md', '.markdown')) {
        $choice = "Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\$extension\UserChoice"
        $expectedClass = if ($extension -eq '.md') { 'LeanMD.Markdown' } else { 'Applications\LeanMD.exe' }
        Assert ((Get-ProofMDRegistryValue $registry $choice 'ProgId') -eq $expectedClass) 'Upgrade should preserve the default app ProgID.'
        Assert ((Get-ProofMDRegistryValue $registry $choice 'Hash') -eq 'preserve-this-Windows-owned-value') 'Upgrade should preserve the default app hash.'
        $latestChoice = "Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\$extension\UserChoiceLatest\ProgId"
        Assert ((Get-ProofMDRegistryValue $registry $latestChoice 'ProgId') -eq $expectedClass) 'Upgrade should preserve UserChoiceLatest.'
        Assert ((Get-ProofMDRegistryValue $registry $latestChoice 'Hash') -eq 'preserve-the-current-Windows-choice') 'Upgrade should preserve the current Windows choice hash.'
        Assert ((Get-ProofMDRegistryValue $registry 'Software\LeanMD\Capabilities\FileAssociations' $extension) -eq
            $expectedClass) 'The compatibility app should retain only the selected ProgID for each extension.'
        Assert ((Test-RegistryValue $registry 'Software\Classes\Applications\LeanMD.exe\SupportedTypes' $extension) -eq
            ($expectedClass -eq 'Applications\LeanMD.exe')) 'The compatibility executable should support only extensions that still select it.'
        $key = $registry.OpenSubKey("Software\Classes\$extension\OpenWithProgids")
        try {
            Assert ($key.GetValueNames() -contains $expectedClass) 'Keep the selected legacy ProgID available in Open With.'
            Assert ($key.GetValueNames() -contains 'ProofMD.Markdown') 'Open With should also include ProofMD.'
        }
        finally { $key.Close() }
        $history = "Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\$extension"
        Assert ($null -eq (Get-ProofMDRegistryValue $registry "$history\OpenWithList" 'b')) 'Remove the obsolete executable from Open With history.'
        Assert ((Get-ProofMDRegistryValue $registry "$history\OpenWithList" 'MRUList') -eq 'ca') 'Preserve other applications and their history order.'
        Assert (Test-RegistryValue $registry "$history\OpenWithProgids" 'OtherEditor.Markdown') 'Preserve other applications in Open With.'
    }
    Assert ($null -eq (Get-ProofMDRegistryValue $registry $muiPath ($legacyExecutable + '.FriendlyAppName'))) 'Remove the stale name cached for the replaced executable.'
    Assert ((Get-ProofMDRegistryValue $registry $muiPath 'C:\another-install\LeanMD.exe.FriendlyAppName') -eq 'Keep this name') 'Preserve caches belonging to a different installation.'

    # Repair the incomplete compatibility registration left by the first 2.0.0 installer,
    # after it has already removed the old installation and uninstall registration.
    $registry.DeleteSubKeyTree('Software\LeanMD', $false)
    $key = $registry.OpenSubKey('Software\RegisteredApplications', $true)
    try { $key.DeleteValue('LeanMD', $false) }
    finally { $key.Close() }
    foreach ($class in @('LeanMD.Markdown', 'Applications\LeanMD.exe')) {
        Set-ProofMDRegistryValues $registry "Software\Classes\$class" @{ NoOpenWith = '' }
    }
    Install-ProofMD $source $localAppData $programs $registry
    Assert ((Get-ProofMDRegistryValue $registry 'Software\RegisteredApplications' 'LeanMD') -eq
        'Software\LeanMD\Capabilities') 'Repair should restore the old registered identity even after its uninstall entry is gone.'
    Assert ((Get-ProofMDRegistryValue $registry 'Software\LeanMD\Capabilities\FileAssociations' '.md') -eq
        'LeanMD.Markdown') 'Repair should restore the capabilities needed for Shell execution.'
    Assert ($null -eq (Get-ProofMDRegistryValue $registry 'Software\Classes\LeanMD.Markdown' 'NoOpenWith')) 'Repair should remove the legacy default exclusion.'

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
    Assert ((Get-ProofMDRegistryValue $registry 'Software\RegisteredApplications' 'LeanMD') -eq
        'Software\LeanMD\Capabilities') 'Uninstall should preserve capabilities when another installation owns the legacy class.'

    Set-ProofMDFileClass $registry 'LeanMD.Markdown' $executable
    Set-ProofMDFileClass $registry 'Applications\LeanMD.exe' $executable
    Install-ProofMD $source $localAppData $programs $registry
    Remove-ProofMDRegistration $registry $paths.Install
    Assert ($null -eq (Get-ProofMDRegistryValue $registry 'Software\RegisteredApplications' 'LeanMD')) 'Uninstall should remove its own compatibility app registration.'
    Assert ($null -eq (Get-ProofMDRegistryValue $registry 'Software\LeanMD\Capabilities' 'ApplicationName')) 'Uninstall should remove its own compatibility capabilities.'

    # Windows has accepted a new .md choice while an old .markdown choice remains.
    # UserChoiceLatest takes precedence over stale UserChoice, and reinstall must
    # not advertise the unselected alias again.
    Set-ProofMDFileClass $registry 'LeanMD.Markdown' $executable
    Set-ProofMDFileClass $registry 'Applications\LeanMD.exe' $executable
    $mdChoice = 'Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.md\UserChoiceLatest\ProgId'
    Set-ProofMDRegistryValues $registry $mdChoice @{ ProgId = 'Applications\ProofMD.exe' }
    Install-ProofMD $source $localAppData $programs $registry
    Assert ((Get-ProofMDRegistryValue $registry $mdChoice 'ProgId') -eq 'Applications\ProofMD.exe') 'Preserve the user-selected ProofMD app.'
    Assert ((Get-ProofMDRegistryValue $registry $mdChoice 'Hash') -eq 'preserve-the-current-Windows-choice') 'Never rewrite the Windows choice hash.'
    Assert ($null -eq (Get-ProofMDRegistryValue $registry 'Software\Classes\LeanMD.Markdown')) 'Retire a legacy class after its last selected extension has migrated.'
    Assert (Test-ProofMDCommand $registry 'Applications\LeanMD.exe' $executable) 'Keep the class still selected for .markdown.'
    foreach ($path in @('Software\Classes\.md\OpenWithProgids',
        'Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.md\OpenWithProgids')) {
        Assert (-not (Test-RegistryValue $registry $path 'LeanMD.Markdown')) 'Remove the unselected legacy Markdown candidate.'
        Assert (-not (Test-RegistryValue $registry $path 'Applications\LeanMD.exe')) 'Remove the unselected legacy executable candidate.'
    }
    Assert ($null -eq (Get-ProofMDRegistryValue $registry 'Software\LeanMD\Capabilities\FileAssociations' '.md')) 'Stop advertising the legacy app for migrated extensions.'
    Install-ProofMD $source $localAppData $programs $registry
    Assert ($null -eq (Get-ProofMDRegistryValue $registry 'Software\Classes\LeanMD.Markdown')) 'Reinstall must not resurrect an unselected alias.'

    Set-ProofMDRegistryValues $registry 'Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.markdown\UserChoiceLatest\ProgId' @{
        ProgId = 'OtherEditor.Markdown'
    }
    Install-ProofMD $source $localAppData $programs $registry
    Assert ($null -eq (Get-ProofMDRegistryValue $registry 'Software\Classes\Applications\LeanMD.exe')) 'Retire the final unused legacy class.'
    Assert ($null -eq (Get-ProofMDRegistryValue $registry 'Software\RegisteredApplications' 'LeanMD')) 'Retire capabilities when the last legacy choice is replaced.'
    Assert ((Get-ProofMDUserChoice $registry '.markdown') -eq 'OtherEditor.Markdown') 'Preserve another application selected by the user.'
    Remove-ProofMDRegistration $registry $paths.Install

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
