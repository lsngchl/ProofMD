[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$appName = 'LeanMD'
$progId = 'LeanMD.Markdown'
$sourceDirectory = [IO.Path]::GetFullPath($PSScriptRoot)
$localAppData = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
$installDirectory = [IO.Path]::GetFullPath((Join-Path $localAppData 'Programs\LeanMD'))
$executablePath = Join-Path $installDirectory 'LeanMD.exe'

if ($sourceDirectory.TrimEnd('\') -ne $installDirectory.TrimEnd('\')) {
    New-Item -ItemType Directory -Path $installDirectory -Force | Out-Null
    Get-ChildItem -LiteralPath $sourceDirectory -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $installDirectory -Recurse -Force
    }
}

if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
    throw "LeanMD.exe was not found in $installDirectory"
}

$currentUser = [Microsoft.Win32.Registry]::CurrentUser
$classes = $currentUser.CreateSubKey('Software\Classes')

try {
    $documentType = $classes.CreateSubKey($progId)
    $documentType.SetValue('', 'Markdown Document')
    $documentType.SetValue('FriendlyTypeName', 'Markdown Document')
    $documentType.CreateSubKey('DefaultIcon').SetValue('', "`"$executablePath`",0")
    $documentType.CreateSubKey('shell\open\command').SetValue('', "`"$executablePath`" `"%1`"")

    $application = $classes.CreateSubKey('Applications\LeanMD.exe')
    $application.SetValue('FriendlyAppName', $appName)
    $application.CreateSubKey('shell\open\command').SetValue('', "`"$executablePath`" `"%1`"")
    $supportedTypes = $application.CreateSubKey('SupportedTypes')
    $supportedTypes.SetValue('.md', '', [Microsoft.Win32.RegistryValueKind]::String)
    $supportedTypes.SetValue('.markdown', '', [Microsoft.Win32.RegistryValueKind]::String)

    foreach ($extension in @('.md', '.markdown')) {
        $openWith = $classes.CreateSubKey("$extension\OpenWithProgids")
        $openWith.SetValue($progId, [byte[]]@(), [Microsoft.Win32.RegistryValueKind]::None)
        $openWith.Close()
    }
}
finally {
    $classes.Close()
}

$capabilities = $currentUser.CreateSubKey('Software\LeanMD\Capabilities')
$capabilities.SetValue('ApplicationName', $appName)
$capabilities.SetValue('ApplicationDescription', 'A lightweight local Markdown and LaTeX viewer.')
$fileAssociations = $capabilities.CreateSubKey('FileAssociations')
$fileAssociations.SetValue('.md', $progId)
$fileAssociations.SetValue('.markdown', $progId)
$fileAssociations.Close()
$capabilities.Close()

$registeredApplications = $currentUser.CreateSubKey('Software\RegisteredApplications')
$registeredApplications.SetValue($appName, 'Software\LeanMD\Capabilities')
$registeredApplications.Close()

$uninstall = $currentUser.CreateSubKey('Software\Microsoft\Windows\CurrentVersion\Uninstall\LeanMD')
$uninstall.SetValue('DisplayName', $appName)
$uninstall.SetValue('DisplayVersion', '1.5.1')
$uninstall.SetValue('Publisher', 'LeanMD')
$uninstall.SetValue('InstallLocation', $installDirectory)
$uninstall.SetValue('DisplayIcon', $executablePath)
$uninstall.SetValue(
    'UninstallString',
    "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$(Join-Path $installDirectory 'Uninstall-LeanMD.ps1')`"")
$uninstall.SetValue('NoModify', 1, [Microsoft.Win32.RegistryValueKind]::DWord)
$uninstall.SetValue('NoRepair', 1, [Microsoft.Win32.RegistryValueKind]::DWord)
$uninstall.Close()

$programs = [Environment]::GetFolderPath([Environment+SpecialFolder]::Programs)
$shortcutPath = Join-Path $programs 'LeanMD.lnk'
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $executablePath
$shortcut.WorkingDirectory = $installDirectory
$shortcut.Description = 'LeanMD Markdown Viewer'
$shortcut.Save()

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class LeanMDShellNotify {
    [DllImport("shell32.dll")]
    public static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
}
'@
[LeanMDShellNotify]::SHChangeNotify(0x08000000, 0, [IntPtr]::Zero, [IntPtr]::Zero)

Write-Host 'LeanMD was installed successfully.'
