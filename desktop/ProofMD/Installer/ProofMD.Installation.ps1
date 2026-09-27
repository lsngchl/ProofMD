Set-StrictMode -Version Latest

function Get-ProofMDPaths([string]$LocalAppData) {
    $root = [IO.Path]::GetFullPath($LocalAppData)
    [pscustomobject]@{
        Install = [IO.Path]::GetFullPath((Join-Path $root 'Programs\ProofMD'))
        LegacyInstall = [IO.Path]::GetFullPath((Join-Path $root 'Programs\LeanMD'))
        Profile = [IO.Path]::GetFullPath((Join-Path $root 'ProofMD'))
    }
}

function Assert-ProofMDPath([string]$Path, [string]$ExpectedPath) {
    $fullPath = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    if ($fullPath -ne [IO.Path]::GetFullPath($ExpectedPath).TrimEnd('\')) {
        throw "Unexpected application path: $fullPath"
    }
    for ($directory = $fullPath; $directory; $directory = [IO.Path]::GetDirectoryName($directory)) {
        if (Test-Path -LiteralPath $directory) {
            $item = Get-Item -LiteralPath $directory -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Application paths must not traverse directory links: $directory"
            }
        }
    }
}

function Assert-ProofMDStopped([string[]]$ExecutablePaths) {
    foreach ($process in @(Get-Process -Name ProofMD, LeanMD -ErrorAction SilentlyContinue)) {
        if ($ExecutablePaths -contains $process.Path) {
            throw 'Close ProofMD and LeanMD before installing or removing the app.'
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

function Test-ProofMDCommand($RegistryRoot, [string]$Class, [string]$ExecutablePath) {
    $command = Get-ProofMDRegistryValue $RegistryRoot "Software\Classes\$Class\shell\open\command"
    return $command -eq ('"{0}" "%1"' -f $ExecutablePath)
}

function Set-ProofMDFileClass($RegistryRoot, [string]$Class, [string]$ExecutablePath) {
    $path = "Software\Classes\$Class"
    Set-ProofMDRegistryValues $RegistryRoot $path @{
        '' = 'Markdown Document'
        FriendlyTypeName = 'Markdown Document'
        FriendlyAppName = 'ProofMD'
    }
    $key = $RegistryRoot.OpenSubKey($path, $true)
    try { $key.DeleteValue('NoOpenWith', $false) }
    finally { $key.Close() }
    Set-ProofMDRegistryValues $RegistryRoot "$path\DefaultIcon" @{
        '' = ('"{0}",0' -f $ExecutablePath)
    }
    Set-ProofMDRegistryValues $RegistryRoot "$path\shell\open\command" @{
        '' = ('"{0}" "%1"' -f $ExecutablePath)
    }
}

function Get-ProofMDUserChoice($RegistryRoot, [string]$Extension) {
    $path = "Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\$Extension"
    $choice = Get-ProofMDRegistryValue $RegistryRoot "$path\UserChoiceLatest\ProgId" 'ProgId'
    if (-not $choice) { $choice = Get-ProofMDRegistryValue $RegistryRoot "$path\UserChoice" 'ProgId' }
    return $choice
}

function Test-ProofMDLegacyCapabilitiesOwner($RegistryRoot, [string[]]$OwnedClasses) {
    $registeredPath = Get-ProofMDRegistryValue $RegistryRoot 'Software\RegisteredApplications' 'LeanMD'
    if ($registeredPath -and $registeredPath -ne 'Software\LeanMD\Capabilities') { return $false }
    $classes = @(foreach ($extension in @('.md', '.markdown')) {
        $class = Get-ProofMDRegistryValue $RegistryRoot 'Software\LeanMD\Capabilities\FileAssociations' $extension
        if ($class) { $class }
    })
    if ($classes.Count -eq 0) { return $OwnedClasses -contains 'LeanMD.Markdown' }
    return @($classes | Where-Object { $OwnedClasses -notcontains $_ }).Count -eq 0
}

function Set-ProofMDCapabilities(
    $RegistryRoot, [string]$Application, [hashtable]$Associations, [string]$ExecutablePath
) {
    $path = "Software\$Application\Capabilities"
    Set-ProofMDRegistryValues $RegistryRoot $path @{
        ApplicationDescription = 'A lightweight local Markdown and LaTeX viewer.'
        ApplicationIcon = ('"{0}",0' -f $ExecutablePath)
    }
    $key = $RegistryRoot.OpenSubKey($path, $true)
    try {
        if ($Application -eq 'ProofMD') {
            $key.SetValue('ApplicationName', 'ProofMD')
            $key.DeleteValue('Hidden', $false)
        }
        else {
            # Keep only the identity still referenced by a Windows default choice.
            # Omit ApplicationName so Windows obtains ProofMD's name from its exe;
            # an explicit name must match the RegisteredApplications value name.
            $key.DeleteValue('ApplicationName', $false)
            $key.SetValue('Hidden', 1, [Microsoft.Win32.RegistryValueKind]::DWord)
        }
    }
    finally { $key.Close() }
    $key = $RegistryRoot.CreateSubKey("$path\FileAssociations")
    try {
        foreach ($extension in @('.md', '.markdown')) {
            if ($Associations.ContainsKey($extension)) { $key.SetValue($extension, $Associations[$extension]) }
            else { $key.DeleteValue($extension, $false) }
        }
    }
    finally { $key.Close() }
    Set-ProofMDRegistryValues $RegistryRoot 'Software\RegisteredApplications' @{
        $Application = "Software\$Application\Capabilities"
    }
}

function Remove-ProofMDLegacyHistory($RegistryRoot, [string]$LegacyExecutablePath) {
    foreach ($extension in @('.md', '.markdown')) {
        $path = "Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\$extension\OpenWithList"
        $key = $RegistryRoot.OpenSubKey($path, $true)
        if ($null -eq $key) { continue }
        try {
            $mru = [string]$key.GetValue('MRUList', '')
            foreach ($name in $key.GetValueNames()) {
                if ($name.Length -eq 1 -and $key.GetValue($name) -eq 'LeanMD.exe') {
                    $key.DeleteValue($name, $false)
                    $mru = $mru.Replace($name, '')
                }
            }
            if ($key.GetValueNames() -contains 'MRUList') { $key.SetValue('MRUList', $mru) }
        }
        finally { $key.Close() }
    }
    $key = $RegistryRoot.OpenSubKey('Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache', $true)
    if ($null -ne $key) {
        try {
            foreach ($suffix in @('.FriendlyAppName', '.ApplicationCompany')) {
                $key.DeleteValue($LegacyExecutablePath + $suffix, $false)
            }
        }
        finally { $key.Close() }
    }
}

function Register-ProofMD($RegistryRoot, [string]$InstallDirectory, [string]$LegacyInstallDirectory) {
    $executable = Join-Path $InstallDirectory 'ProofMD.exe'
    $legacyExecutable = Join-Path $LegacyInstallDirectory 'LeanMD.exe'
    Set-ProofMDFileClass $RegistryRoot 'ProofMD.Markdown' $executable
    Set-ProofMDFileClass $RegistryRoot 'Applications\ProofMD.exe' $executable
    Set-ProofMDRegistryValues $RegistryRoot 'Software\Classes\Applications\ProofMD.exe\SupportedTypes' @{
        '.md' = ''
        '.markdown' = ''
    }

    $choices = @{}
    foreach ($extension in @('.md', '.markdown')) {
        $choices[$extension] = Get-ProofMDUserChoice $RegistryRoot $extension
    }
    $ownedLegacyClasses = @()
    foreach ($legacyClass in @('LeanMD.Markdown', 'Applications\LeanMD.exe')) {
        if ((Test-ProofMDCommand $RegistryRoot $legacyClass $legacyExecutable) -or
            (Test-ProofMDCommand $RegistryRoot $legacyClass $executable)) {
            $ownedLegacyClasses += $legacyClass
            if ($choices.Values -contains $legacyClass) {
                Set-ProofMDFileClass $RegistryRoot $legacyClass $executable
                if ($legacyClass -eq 'Applications\LeanMD.exe') {
                    $key = $RegistryRoot.CreateSubKey("Software\Classes\$legacyClass\SupportedTypes")
                    try {
                        foreach ($extension in @('.md', '.markdown')) {
                            if ($choices[$extension] -eq $legacyClass) { $key.SetValue($extension, '') }
                            else { $key.DeleteValue($extension, $false) }
                        }
                    }
                    finally { $key.Close() }
                }
            }
            else { $RegistryRoot.DeleteSubKeyTree("Software\Classes\$legacyClass", $false) }
        }
    }

    foreach ($extension in @('.md', '.markdown')) {
        $classPath = "Software\Classes\$extension\OpenWithProgids"
        $historyPath = "Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\$extension\OpenWithProgids"
        foreach ($path in @($classPath, $historyPath)) {
            $key = if ($path -eq $classPath) { $RegistryRoot.CreateSubKey($path) } else { $RegistryRoot.OpenSubKey($path, $true) }
            if ($null -eq $key) { continue }
            try {
                foreach ($legacyClass in $ownedLegacyClasses) {
                    if ($choices[$extension] -eq $legacyClass) {
                        $key.SetValue($legacyClass, [byte[]]@(), [Microsoft.Win32.RegistryValueKind]::None)
                    }
                    else { $key.DeleteValue($legacyClass, $false) }
                }
                if ($path -eq $classPath) {
                    $key.SetValue('ProofMD.Markdown', [byte[]]@(), [Microsoft.Win32.RegistryValueKind]::None)
                }
            }
            finally { $key.Close() }
        }
    }

    Set-ProofMDCapabilities $RegistryRoot 'ProofMD' @{ '.md' = 'ProofMD.Markdown'; '.markdown' = 'ProofMD.Markdown' } $executable
    if (Test-ProofMDLegacyCapabilitiesOwner $RegistryRoot $ownedLegacyClasses) {
        $legacyAssociations = @{}
        foreach ($extension in @('.md', '.markdown')) {
            if ($ownedLegacyClasses -contains $choices[$extension]) { $legacyAssociations[$extension] = $choices[$extension] }
        }
        if ($legacyAssociations.Count -gt 0) {
            Set-ProofMDCapabilities $RegistryRoot 'LeanMD' $legacyAssociations $executable
        }
        else {
            $RegistryRoot.DeleteSubKeyTree('Software\LeanMD\Capabilities', $false)
            $key = $RegistryRoot.OpenSubKey('Software\RegisteredApplications', $true)
            if ($null -ne $key) {
                try { $key.DeleteValue('LeanMD', $false) }
                finally { $key.Close() }
            }
        }
    }
    if ($ownedLegacyClasses -contains 'Applications\LeanMD.exe') {
        Remove-ProofMDLegacyHistory $RegistryRoot $legacyExecutable
    }
    Set-ProofMDRegistryValues $RegistryRoot 'Software\Microsoft\Windows\CurrentVersion\Uninstall\ProofMD' @{
        DisplayName = 'ProofMD'
        DisplayVersion = '2.0.1'
        Publisher = 'ProofMD'
        InstallLocation = $InstallDirectory
        DisplayIcon = $executable
        UninstallString = ('powershell.exe -NoProfile -ExecutionPolicy Bypass -File "{0}"' -f
            (Join-Path $InstallDirectory 'Uninstall-ProofMD.ps1'))
        NoModify = 1
        NoRepair = 1
    }
}

function Remove-ProofMDRegistration($RegistryRoot, [string]$InstallDirectory) {
    $executable = Join-Path $InstallDirectory 'ProofMD.exe'
    $ownedLegacyClasses = @(foreach ($class in @('LeanMD.Markdown', 'Applications\LeanMD.exe')) {
        if (Test-ProofMDCommand $RegistryRoot $class $executable) { $class }
    })
    $ownsLegacyRegistration = Test-ProofMDLegacyCapabilitiesOwner $RegistryRoot $ownedLegacyClasses
    foreach ($class in @('ProofMD.Markdown', 'Applications\ProofMD.exe', 'LeanMD.Markdown', 'Applications\LeanMD.exe')) {
        if (Test-ProofMDCommand $RegistryRoot $class $executable) {
            $RegistryRoot.DeleteSubKeyTree("Software\Classes\$class", $false)
            foreach ($extension in @('.md', '.markdown')) {
                foreach ($path in @("Software\Classes\$extension\OpenWithProgids",
                    "Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\$extension\OpenWithProgids")) {
                    $key = $RegistryRoot.OpenSubKey($path, $true)
                    if ($null -ne $key) {
                        try { $key.DeleteValue($class, $false) }
                        finally { $key.Close() }
                    }
                }
            }
        }
    }
    $RegistryRoot.DeleteSubKeyTree('Software\ProofMD', $false)
    $RegistryRoot.DeleteSubKeyTree('Software\Microsoft\Windows\CurrentVersion\Uninstall\ProofMD', $false)
    if ($ownsLegacyRegistration) {
        $RegistryRoot.DeleteSubKeyTree('Software\LeanMD\Capabilities', $false)
    }
    $key = $RegistryRoot.OpenSubKey('Software\RegisteredApplications', $true)
    if ($null -ne $key) {
        try {
            $key.DeleteValue('ProofMD', $false)
            if ($ownsLegacyRegistration) { $key.DeleteValue('LeanMD', $false) }
        }
        finally { $key.Close() }
    }
}

function Install-ProofMD(
    [string]$SourceDirectory,
    [string]$LocalAppData,
    [string]$ProgramsDirectory,
    $RegistryRoot
) {
    $source = [IO.Path]::GetFullPath($SourceDirectory)
    $paths = Get-ProofMDPaths $LocalAppData
    Assert-ProofMDPath $paths.Install (Join-Path $LocalAppData 'Programs\ProofMD')
    Assert-ProofMDPath $paths.LegacyInstall (Join-Path $LocalAppData 'Programs\LeanMD')
    $executable = Join-Path $paths.Install 'ProofMD.exe'
    $legacyExecutable = Join-Path $paths.LegacyInstall 'LeanMD.exe'
    Assert-ProofMDStopped @($executable, $legacyExecutable)
    foreach ($required in @('ProofMD.exe', 'ProofMD.dll', 'Viewer\index.html',
        'Install-ProofMD.ps1', 'Uninstall-ProofMD.ps1', 'ProofMD.Installation.ps1')) {
        if (-not (Test-Path -LiteralPath (Join-Path $source $required) -PathType Leaf)) {
            throw "Incomplete ProofMD release: $required was not found in $source"
        }
    }

    if ($source.TrimEnd('\') -ne $paths.Install) {
        New-Item -ItemType Directory -Path $paths.Install -Force | Out-Null
        Get-ChildItem -LiteralPath $source -Force | ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination $paths.Install -Recurse -Force
        }
    }

    Register-ProofMD $RegistryRoot $paths.Install $paths.LegacyInstall
    New-Item -ItemType Directory -Path $ProgramsDirectory -Force | Out-Null
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut((Join-Path $ProgramsDirectory 'ProofMD.lnk'))
    $shortcut.TargetPath = $executable
    $shortcut.WorkingDirectory = $paths.Install
    $shortcut.IconLocation = ('{0},0' -f $executable)
    $shortcut.Description = 'ProofMD Markdown Viewer'
    $shortcut.Save()

    $legacyLocation = Get-ProofMDRegistryValue $RegistryRoot 'Software\Microsoft\Windows\CurrentVersion\Uninstall\LeanMD' 'InstallLocation'
    if ($legacyLocation -and [IO.Path]::GetFullPath($legacyLocation).TrimEnd('\') -eq $paths.LegacyInstall) {
        $RegistryRoot.DeleteSubKeyTree('Software\Microsoft\Windows\CurrentVersion\Uninstall\LeanMD', $false)
        $legacyShortcut = Join-Path $ProgramsDirectory 'LeanMD.lnk'
        if ((Test-Path -LiteralPath $legacyShortcut) -and
            $shell.CreateShortcut($legacyShortcut).TargetPath -eq $legacyExecutable) {
            Remove-Item -LiteralPath $legacyShortcut -Force
        }
        if ((Test-Path -LiteralPath $paths.LegacyInstall) -and
            $source -ne $paths.LegacyInstall -and
            -not $source.StartsWith($paths.LegacyInstall + '\', [StringComparison]::OrdinalIgnoreCase)) {
            Assert-ProofMDPath $paths.LegacyInstall (Join-Path $LocalAppData 'Programs\LeanMD')
            Remove-Item -LiteralPath $paths.LegacyInstall -Recurse -Force
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
