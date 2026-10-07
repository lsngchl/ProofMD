Set-StrictMode -Version Latest

$ProofMDExtensions = @('.md', '.markdown')
$ProofMDProgId = 'ProofMD.Markdown'
$ProofMDPayload = @('ProofMD.exe', 'ProofMD.dll', 'Viewer\index.html',
    'ProofMD.Installation.ps1', 'Uninstall-ProofMD.ps1', 'Uninstall-ProofMD.cmd')

function Get-ProofMDPaths([string]$LocalAppData) {
    $root = [IO.Path]::GetFullPath($LocalAppData)
    [pscustomobject]@{
        Install = Join-Path $root 'Programs\ProofMD'
        Profile = Join-Path $root 'ProofMD'
    }
}

# Refuses paths that pass through junctions or symbolic links before anything is copied or deleted.
function Assert-ProofMDNoLinks([string]$Path) {
    for ($directory = [IO.Path]::GetFullPath($Path); $directory; $directory = [IO.Path]::GetDirectoryName($directory)) {
        if ((Test-Path -LiteralPath $directory) -and
            ((Get-Item -LiteralPath $directory -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "ProofMD paths must not pass through directory links: $directory"
        }
    }
}

function Assert-ProofMDStopped([string]$ExecutablePath) {
    foreach ($process in @(Get-Process -Name ProofMD -ErrorAction SilentlyContinue)) {
        if ($process.Path -eq $ExecutablePath) {
            throw 'Close ProofMD before installing or removing it.'
        }
    }
}

function Set-ProofMDRegistryValues($RegistryRoot, [string]$Path, [hashtable]$Values) {
    $key = $RegistryRoot.CreateSubKey($Path)
    try {
        foreach ($name in $Values.Keys) { $key.SetValue($name, $Values[$name]) }
    }
    finally { $key.Close() }
}

function Get-ProofMDRegistryValue($RegistryRoot, [string]$Path, [string]$Name = '') {
    $key = $RegistryRoot.OpenSubKey($Path)
    if ($null -eq $key) { return $null }
    try { return $key.GetValue($Name) }
    finally { $key.Close() }
}

function Remove-ProofMDRegistryValue($RegistryRoot, [string]$Path, [string]$Name) {
    $key = $RegistryRoot.OpenSubKey($Path, $true)
    if ($null -eq $key) { return }
    try { $key.DeleteValue($Name, $false) }
    finally { $key.Close() }
}

function Get-ProofMDOpenCommand([string]$ExecutablePath) {
    return '"{0}" "%1"' -f $ExecutablePath
}

function Set-ProofMDFileClass($RegistryRoot, [string]$Class, [string]$ExecutablePath) {
    $path = "Software\Classes\$Class"
    Set-ProofMDRegistryValues $RegistryRoot $path @{
        '' = 'Markdown Document'
        FriendlyTypeName = 'Markdown Document'
        FriendlyAppName = 'ProofMD'
    }
    Set-ProofMDRegistryValues $RegistryRoot "$path\DefaultIcon" @{ '' = ('"{0}",0' -f $ExecutablePath) }
    Set-ProofMDRegistryValues $RegistryRoot "$path\shell\open\command" @{ '' = (Get-ProofMDOpenCommand $ExecutablePath) }
}

# Registers ProofMD as an available handler without changing the user's default app.
function Register-ProofMD($RegistryRoot, [string]$InstallDirectory) {
    $executable = Join-Path $InstallDirectory 'ProofMD.exe'
    $version = [string](Get-Item -LiteralPath $executable).VersionInfo.ProductVersion

    Set-ProofMDFileClass $RegistryRoot $ProofMDProgId $executable
    Set-ProofMDFileClass $RegistryRoot 'Applications\ProofMD.exe' $executable
    $supportedTypes = @{}
    foreach ($extension in $ProofMDExtensions) {
        $supportedTypes[$extension] = ''
        $key = $RegistryRoot.CreateSubKey("Software\Classes\$extension\OpenWithProgids")
        try { $key.SetValue($ProofMDProgId, [byte[]]@(), [Microsoft.Win32.RegistryValueKind]::None) }
        finally { $key.Close() }
    }
    Set-ProofMDRegistryValues $RegistryRoot 'Software\Classes\Applications\ProofMD.exe\SupportedTypes' $supportedTypes

    Set-ProofMDRegistryValues $RegistryRoot 'Software\ProofMD\Capabilities' @{
        ApplicationName = 'ProofMD'
        ApplicationDescription = 'A local Markdown viewer with LaTeX mathematics.'
        ApplicationIcon = ('"{0}",0' -f $executable)
    }
    $associations = @{}
    foreach ($extension in $ProofMDExtensions) { $associations[$extension] = $ProofMDProgId }
    Set-ProofMDRegistryValues $RegistryRoot 'Software\ProofMD\Capabilities\FileAssociations' $associations
    Set-ProofMDRegistryValues $RegistryRoot 'Software\RegisteredApplications' @{
        ProofMD = 'Software\ProofMD\Capabilities'
    }

    Set-ProofMDRegistryValues $RegistryRoot 'Software\Microsoft\Windows\CurrentVersion\Uninstall\ProofMD' @{
        DisplayName = 'ProofMD'
        DisplayVersion = $version
        Publisher = 'ProofMD'
        InstallLocation = $InstallDirectory
        DisplayIcon = $executable
        UninstallString = ('"{0}"' -f (Join-Path $InstallDirectory 'Uninstall-ProofMD.cmd'))
        NoModify = 1
        NoRepair = 1
    }
}

function Remove-ProofMDRegistration($RegistryRoot, [string]$InstallDirectory) {
    $command = Get-ProofMDOpenCommand (Join-Path $InstallDirectory 'ProofMD.exe')
    # Windows creates the *_auto_file classes when ProofMD is picked through Open with > Browse.
    $classes = @($ProofMDProgId, 'Applications\ProofMD.exe') +
        @($ProofMDExtensions | ForEach-Object { '{0}_auto_file' -f $_.TrimStart('.') })
    foreach ($class in $classes) {
        if ((Get-ProofMDRegistryValue $RegistryRoot "Software\Classes\$class\shell\open\command") -eq $command) {
            $RegistryRoot.DeleteSubKeyTree("Software\Classes\$class", $false)
        }
    }
    foreach ($extension in $ProofMDExtensions) {
        Remove-ProofMDRegistryValue $RegistryRoot "Software\Classes\$extension\OpenWithProgids" $ProofMDProgId
    }
    Remove-ProofMDRegistryValue $RegistryRoot 'Software\RegisteredApplications' 'ProofMD'
    $RegistryRoot.DeleteSubKeyTree('Software\ProofMD', $false)
    $RegistryRoot.DeleteSubKeyTree('Software\Microsoft\Windows\CurrentVersion\Uninstall\ProofMD', $false)
}

# Copies the release into a sibling folder first, then swaps it in, so files from an
# older version never survive an upgrade and a failed copy leaves the old install intact.
function Install-ProofMD(
    [string]$SourceDirectory,
    [string]$LocalAppData,
    [string]$ProgramsDirectory,
    $RegistryRoot
) {
    $source = [IO.Path]::GetFullPath($SourceDirectory).TrimEnd('\')
    $install = (Get-ProofMDPaths $LocalAppData).Install
    $executable = Join-Path $install 'ProofMD.exe'
    Assert-ProofMDNoLinks $install
    Assert-ProofMDStopped $executable
    foreach ($required in $ProofMDPayload) {
        if (-not (Test-Path -LiteralPath (Join-Path $source $required) -PathType Leaf)) {
            throw "Incomplete ProofMD release: $required was not found in $source"
        }
    }

    if ($source -ne $install) {
        $staging = "$install.new"
        $previous = "$install.old"
        foreach ($leftover in @($staging, $previous)) {
            if (Test-Path -LiteralPath $leftover) { Remove-Item -LiteralPath $leftover -Recurse -Force }
        }
        New-Item -ItemType Directory -Path $staging -Force | Out-Null
        Get-ChildItem -LiteralPath $source -Force | ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination $staging -Recurse -Force
        }
        if (Test-Path -LiteralPath $install) { Move-Item -LiteralPath $install -Destination $previous }
        Move-Item -LiteralPath $staging -Destination $install
        if (Test-Path -LiteralPath $previous) { Remove-Item -LiteralPath $previous -Recurse -Force }
    }

    Register-ProofMD $RegistryRoot $install
    New-Item -ItemType Directory -Path $ProgramsDirectory -Force | Out-Null
    $shortcut = (New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path $ProgramsDirectory 'ProofMD.lnk'))
    $shortcut.TargetPath = $executable
    $shortcut.WorkingDirectory = $install
    $shortcut.IconLocation = ('{0},0' -f $executable)
    $shortcut.Description = 'ProofMD Markdown Viewer'
    $shortcut.Save()
}

# Removes a folder, retrying while WebView2 helper processes release their file handles.
function Remove-ProofMDDirectory([string]$Path) {
    Assert-ProofMDNoLinks $Path
    for ($attempt = 1; Test-Path -LiteralPath $Path; $attempt++) {
        try { Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop }
        catch {
            if ($attempt -ge 20) { throw }
            Start-Sleep -Milliseconds 500
        }
    }
}

function Send-ProofMDShellNotification {
    if (-not ('ProofMDShellNotify' -as [type])) {
        Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class ProofMDShellNotify {
    [DllImport("shell32.dll")]
    public static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
}
'@
    }
    [ProofMDShellNotify]::SHChangeNotify(0x08000000, 0, [IntPtr]::Zero, [IntPtr]::Zero)
}
