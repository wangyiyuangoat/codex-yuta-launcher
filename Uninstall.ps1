[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Low')]
param(
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'CodexYutaLauncher'),
    [switch]$RemoveFiles
)

$ErrorActionPreference = 'Stop'
$projectName = 'codex-yuta-launcher'
$pathComparison = [System.StringComparison]::OrdinalIgnoreCase

function Get-NormalizedPath {
    param([Parameter(Mandatory = $true)][string]$Path)
    $absolute = [System.IO.Path]::GetFullPath($Path)
    return $absolute.TrimEnd([char[]]@('\', '/'))
}

function Test-SamePath {
    param([string]$First, [string]$Second)
    return [string]::Equals((Get-NormalizedPath $First), (Get-NormalizedPath $Second), $pathComparison)
}

function Assert-NoReparsePoint {
    param([Parameter(Mandatory = $true)][string]$Path)
    $cursor = [System.IO.Path]::GetFullPath($Path)
    while (-not [string]::IsNullOrWhiteSpace($cursor)) {
        if (Test-Path -LiteralPath $cursor) {
            $entry = Get-Item -LiteralPath $cursor -Force
            if (($entry.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Refusing to uninstall through a symbolic link or junction: $cursor"
            }
        }
        $parent = [System.IO.Directory]::GetParent($cursor)
        if ($null -eq $parent) { break }
        $cursor = $parent.FullName
    }
}

if (-not (Test-Path -LiteralPath $InstallDir)) {
    Write-Host "Installation directory is already absent: $InstallDir"
    return
}

$rootItem = Get-Item -LiteralPath $InstallDir -Force
if ($rootItem.PSProvider.Name -ne 'FileSystem' -or -not $rootItem.PSIsContainer) {
    throw 'InstallDir must be an existing filesystem directory.'
}
$installRoot = Get-NormalizedPath $rootItem.FullName
$rootPrefix = $installRoot + [System.IO.Path]::DirectorySeparatorChar
$volumeRoot = [System.IO.Path]::GetPathRoot($installRoot)
$desktopDirectory = [Environment]::GetFolderPath('DesktopDirectory')
$documentsDirectory = [Environment]::GetFolderPath('MyDocuments')
$criticalPaths = @($volumeRoot, $env:USERPROFILE, $env:LOCALAPPDATA, $desktopDirectory, $documentsDirectory)
foreach ($criticalPath in $criticalPaths) {
    if (-not [string]::IsNullOrWhiteSpace($criticalPath) -and (Test-SamePath $installRoot $criticalPath)) {
        throw "Refusing to use a system or personal root as the installation directory: $installRoot"
    }
}
Assert-NoReparsePoint $installRoot

$manifestPath = Join-Path $installRoot 'install-manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Installation manifest is missing. No files were removed: $manifestPath"
}
Assert-NoReparsePoint $manifestPath
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($manifest.Project -ne $projectName) {
    throw 'The manifest does not belong to codex-yuta-launcher. No files were removed.'
}
if ([string]::IsNullOrWhiteSpace([string]$manifest.InstallDirectory) -or
    -not [System.IO.Path]::IsPathRooted([string]$manifest.InstallDirectory) -or
    -not (Test-SamePath ([string]$manifest.InstallDirectory) $installRoot)) {
    throw 'The manifest installation directory does not match InstallDir. No files were removed.'
}

# Validate every deletion target before making any changes.
$listedFiles = New-Object 'System.Collections.Generic.List[string]'
if ($RemoveFiles) {
    if ($null -eq $manifest.Files -or $manifest.Files -is [string]) {
        throw 'The manifest Files field must be an array. No files were removed.'
    }
    foreach ($relativeFile in @($manifest.Files)) {
        if ($relativeFile -isnot [string] -or [string]::IsNullOrWhiteSpace($relativeFile) -or
            [System.IO.Path]::IsPathRooted($relativeFile) -or $relativeFile.Contains(':')) {
            throw 'The manifest contains an invalid relative file path. No files were removed.'
        }
        $segments = $relativeFile -split '[\\/]'
        if ($segments -contains '..' -or $segments -contains '.' -or $segments -contains '') {
            throw "The manifest contains a non-canonical relative path: $relativeFile"
        }
        $filePath = [System.IO.Path]::GetFullPath((Join-Path $installRoot $relativeFile))
        if (-not $filePath.StartsWith($rootPrefix, $pathComparison)) {
            throw "The manifest file is outside the installation directory: $relativeFile"
        }
        Assert-NoReparsePoint $filePath
        if (Test-Path -LiteralPath $filePath) {
            $fileItem = Get-Item -LiteralPath $filePath -Force
            if ($fileItem.PSIsContainer) {
                throw "The manifest lists a directory as a file: $relativeFile"
            }
        }
        if (-not $listedFiles.Contains($filePath)) { $listedFiles.Add($filePath) }
    }
}

$shortcutPath = [string]$manifest.ShortcutPath
if ([string]::IsNullOrWhiteSpace($shortcutPath)) {
    Write-Host 'This installation has no recorded desktop shortcut.'
} elseif (-not (Test-Path -LiteralPath $shortcutPath)) {
    Write-Host 'The recorded desktop shortcut is already absent.'
} else {
    $shortcutItem = Get-Item -LiteralPath $shortcutPath -Force
    $shortcutParent = [System.IO.Path]::GetDirectoryName($shortcutItem.FullName)
    $isDesktopShortcut = -not $shortcutItem.PSIsContainer -and
        [System.IO.Path]::GetExtension($shortcutItem.FullName) -ieq '.lnk' -and
        -not [string]::IsNullOrWhiteSpace($desktopDirectory) -and
        (Test-SamePath $shortcutParent $desktopDirectory) -and
        (($shortcutItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -eq 0)
    if (-not $isDesktopShortcut) {
        Write-Warning 'The recorded shortcut is not an ordinary shortcut on the current desktop; it was left untouched.'
    } else {
        $shell = $null
        $shortcut = $null
        $ownsShortcut = $false
        try {
            $shell = New-Object -ComObject WScript.Shell
            $shortcut = $shell.CreateShortcut($shortcutItem.FullName)
            $targetPath = [string]$shortcut.TargetPath
            if (-not [string]::IsNullOrWhiteSpace($targetPath) -and [System.IO.Path]::IsPathRooted($targetPath)) {
                $ownsShortcut = Test-SamePath $targetPath (Join-Path $installRoot 'YutaCodex.exe')
            }
        } finally {
            if ($null -ne $shortcut) { [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcut) }
            if ($null -ne $shell) { [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) }
        }
        if ($ownsShortcut) {
            if ($PSCmdlet.ShouldProcess($shortcutItem.FullName, 'Remove this launcher desktop shortcut')) {
                Remove-Item -LiteralPath $shortcutItem.FullName -Force
                Write-Host 'Removed the launcher desktop shortcut.'
            }
        } else {
            Write-Warning 'The shortcut now points to another target; it was left untouched.'
        }
    }
}

if (-not $RemoveFiles) {
    Write-Host 'Installed files were kept. Add -RemoveFiles to remove the files listed in the installation manifest.'
    return
}

# Keep the manifest until the other listed files have been processed.
$deletionOrder = @($listedFiles | Where-Object { -not (Test-SamePath $_ $manifestPath) })
$deletionOrder += @($listedFiles | Where-Object { Test-SamePath $_ $manifestPath })
foreach ($filePath in $deletionOrder) {
    if (Test-Path -LiteralPath $filePath -PathType Leaf) {
        Assert-NoReparsePoint $filePath
        if ($PSCmdlet.ShouldProcess($filePath, 'Remove an installed file listed in the manifest')) {
            Remove-Item -LiteralPath $filePath -Force
        }
    }
}

foreach ($directory in @((Join-Path $installRoot 'assets'), $installRoot)) {
    if (Test-Path -LiteralPath $directory -PathType Container) {
        Assert-NoReparsePoint $directory
        if (@(Get-ChildItem -LiteralPath $directory -Force).Count -eq 0) {
            if ($PSCmdlet.ShouldProcess($directory, 'Remove an empty launcher directory')) {
                Remove-Item -LiteralPath $directory -Force
            }
        }
    }
}
Write-Host 'Uninstall finished. Files not listed in the manifest were left untouched.'
