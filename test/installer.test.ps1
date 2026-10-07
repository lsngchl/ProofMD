[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$installerDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\desktop\ProofMD\Installer'))
. (Join-Path $installerDirectory 'ProofMD.Installation.ps1')

function Assert([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Assert-Throws([scriptblock]$Action, [string]$Message) {
    $threw = $false
    try { & $Action } catch { $threw = $true }
    Assert $threw $Message
}

$testId = [Guid]::NewGuid().ToString('N')
$testRoot = Join-Path ([IO.Path]::GetTempPath()) "ProofMD-installer-tests-$testId"
$registryParent = 'Software\ProofMD.InstallerTests'
$registryPath = "$registryParent\$testId"
$registry = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($registryPath)

try {
    foreach ($script in Get-ChildItem -LiteralPath $installerDirectory -Filter '*.ps1') {
        $tokens = $null
        $parseErrors = $null
        [void][Management.Automation.Language.Parser]::ParseFile($script.FullName, [ref]$tokens, [ref]$parseErrors)
        Assert ($parseErrors.Count -eq 0) "Invalid installer syntax in $($script.Name): $parseErrors"
    }

    # A release folder whose executable carries real version information.
    $source = Join-Path $testRoot 'release'
    New-Item -ItemType Directory -Path (Join-Path $source 'Viewer') -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSHOME 'powershell.exe') -Destination (Join-Path $source 'ProofMD.exe')
    foreach ($file in @('ProofMD.dll', 'Viewer\index.html')) {
        [IO.File]::WriteAllText((Join-Path $source $file), 'installer test fixture')
    }
    Get-ChildItem -LiteralPath $installerDirectory -File | Copy-Item -Destination $source
    $localAppData = Join-Path $testRoot 'Local'
    $programs = Join-Path $testRoot 'Start Menu'
    $paths = Get-ProofMDPaths $localAppData
    $executable = Join-Path $paths.Install 'ProofMD.exe'
    $command = Get-ProofMDOpenCommand $executable
    $uninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\ProofMD'

    Install-ProofMD $source $localAppData $programs $registry
    Assert (Test-Path -LiteralPath $executable) 'Installing should copy ProofMD.exe.'
    foreach ($class in @('ProofMD.Markdown', 'Applications\ProofMD.exe')) {
        Assert ((Get-ProofMDRegistryValue $registry "Software\Classes\$class\shell\open\command") -eq $command) "$class should open ProofMD."
    }
    foreach ($extension in @('.md', '.markdown')) {
        $key = $registry.OpenSubKey("Software\Classes\$extension\OpenWithProgids")
        try { Assert ($key.GetValueNames() -contains 'ProofMD.Markdown') "ProofMD should be offered in Open with for $extension." }
        finally { $key.Close() }
    }
    Assert ((Get-ProofMDRegistryValue $registry 'Software\RegisteredApplications' 'ProofMD') -eq 'Software\ProofMD\Capabilities') 'ProofMD should appear in Default Apps.'
    Assert ((Get-ProofMDRegistryValue $registry $uninstallKey 'DisplayVersion') -eq (Get-Item -LiteralPath $executable).VersionInfo.ProductVersion) 'Installed apps should show the executable version.'
    Assert ((Get-ProofMDRegistryValue $registry $uninstallKey 'UninstallString') -eq ('"{0}"' -f (Join-Path $paths.Install 'Uninstall-ProofMD.cmd'))) 'Settings should run the pausing uninstall wrapper.'
    $shell = New-Object -ComObject WScript.Shell
    Assert ($shell.CreateShortcut((Join-Path $programs 'ProofMD.lnk')).TargetPath -eq $executable) 'The Start Menu shortcut should open ProofMD.'

    [IO.File]::WriteAllText((Join-Path $paths.Install 'stale-from-old-version.txt'), 'stale')
    New-Item -ItemType Directory -Path $paths.Profile -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $paths.Profile 'window-state.json'), '{}')
    Install-ProofMD $source $localAppData $programs $registry
    Assert (-not (Test-Path -LiteralPath (Join-Path $paths.Install 'stale-from-old-version.txt'))) 'Upgrading should not keep files from the previous version.'
    Assert (-not (Test-Path -LiteralPath "$($paths.Install).new") -and -not (Test-Path -LiteralPath "$($paths.Install).old")) 'Upgrading should not leave staging folders.'
    Assert (Test-Path -LiteralPath (Join-Path $paths.Profile 'window-state.json')) 'Upgrading should keep the user profile.'

    Set-ProofMDRegistryValues $registry 'Software\Classes\md_auto_file\shell\open\command' @{ '' = $command }
    Set-ProofMDRegistryValues $registry 'Software\Classes\markdown_auto_file\shell\open\command' @{ '' = '"C:\Other\Editor.exe" "%1"' }
    $key = $registry.CreateSubKey('Software\Classes\.md\OpenWithProgids')
    try { $key.SetValue('Other.Markdown', [byte[]]@(), [Microsoft.Win32.RegistryValueKind]::None) }
    finally { $key.Close() }
    Remove-ProofMDRegistration $registry $paths.Install
    foreach ($class in @('ProofMD.Markdown', 'Applications\ProofMD.exe', 'md_auto_file')) {
        Assert ($null -eq $registry.OpenSubKey("Software\Classes\$class")) "Uninstalling should remove $class."
    }
    Assert ($null -ne $registry.OpenSubKey('Software\Classes\markdown_auto_file')) 'Uninstalling must keep classes that open another app.'
    $key = $registry.OpenSubKey('Software\Classes\.md\OpenWithProgids')
    try {
        Assert ($key.GetValueNames() -notcontains 'ProofMD.Markdown') 'Uninstalling should remove ProofMD from Open with.'
        Assert ($key.GetValueNames() -contains 'Other.Markdown') 'Uninstalling must keep other apps in Open with.'
    }
    finally { $key.Close() }
    Assert ($null -eq $registry.OpenSubKey($uninstallKey)) 'Uninstalling should remove the Installed apps entry.'
    Assert ($null -eq (Get-ProofMDRegistryValue $registry 'Software\RegisteredApplications' 'ProofMD')) 'Uninstalling should remove ProofMD from Default Apps.'

    $incomplete = Join-Path $testRoot 'incomplete'
    New-Item -ItemType Directory -Path $incomplete | Out-Null
    Assert-Throws { Install-ProofMD $incomplete $localAppData $programs $registry } 'An incomplete release should be rejected.'
    Assert ($null -eq $registry.OpenSubKey('Software\Classes\ProofMD.Markdown')) 'A rejected release should not register anything.'

    $linkTarget = Join-Path $testRoot 'link-target'
    $linkedLocalAppData = Join-Path $testRoot 'linked'
    New-Item -ItemType Directory -Path $linkTarget | Out-Null
    New-Item -ItemType Junction -Path $linkedLocalAppData -Target $linkTarget | Out-Null
    Assert-Throws { Install-ProofMD $source $linkedLocalAppData $programs $registry } 'Installing through a directory link should be rejected.'
    (Get-Item -LiteralPath $linkedLocalAppData).Delete()

    Write-Host 'ProofMD installer tests passed.'
}
finally {
    $registry.Close()
    $parent = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($registryParent, $true)
    if ($null -ne $parent) {
        try {
            $parent.DeleteSubKeyTree($testId, $false)
            $empty = $parent.SubKeyCount -eq 0 -and $parent.ValueCount -eq 0
        }
        finally { $parent.Close() }
        if ($empty) { [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKey($registryParent, $false) }
    }
    if (Test-Path -LiteralPath $testRoot) { Remove-ProofMDDirectory $testRoot }
}
