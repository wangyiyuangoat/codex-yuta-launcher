[CmdletBinding()]
param(
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'CodexYutaLauncher'),
    [string]$AppId,
    [switch]$Preview,
    [switch]$NoShortcut
)
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'This launcher requires Windows.' }
$projectName = 'codex-yuta-launcher'
$version = '2.1.0'
function Assert-OrdinaryPath {
    param([string]$Path, [bool]$Directory)
    if (Test-Path -LiteralPath $Path) {
        $entry = Get-Item -LiteralPath $Path -Force
        if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -or $entry.LinkType) { throw ('Linked installation targets are not supported: ' + $Path) }
        if ([bool]$entry.PSIsContainer -ne $Directory) { throw ('Unexpected installation target type: ' + $Path) }
    }
}
$installRoot = [IO.Path]::GetFullPath($InstallDir).TrimEnd('\','/')
$repoRoot = [IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\','/')
$blockedPaths = @([IO.Path]::GetPathRoot($installRoot).TrimEnd('\','/'),
    $env:USERPROFILE, $env:LOCALAPPDATA, [Environment]::GetFolderPath('Desktop'),
    [Environment]::GetFolderPath('MyDocuments'), $repoRoot)
foreach ($blocked in $blockedPaths) {
    if ($blocked -and [String]::Equals($installRoot, [IO.Path]::GetFullPath($blocked).TrimEnd('\','/'), [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Choose a separate installation folder, not a system, profile, desktop, documents, or repository root.'
    }
}
$ancestorPath = $installRoot
while ($ancestorPath) {
    if (Test-Path -LiteralPath $ancestorPath) {
        if ((Get-Item -LiteralPath $ancestorPath -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw 'The installation path must not pass through a junction or symbolic link.'
        }
    }
    $ancestorPath = Split-Path -Parent $ancestorPath
}
$manifestPath = Join-Path $installRoot 'install-manifest.json'
Assert-OrdinaryPath -Path $manifestPath -Directory $false
$oldManifest = $null
if (Test-Path -LiteralPath $installRoot) {
    if (-not (Test-Path -LiteralPath $installRoot -PathType Container)) { throw 'InstallDir must be a directory.' }
    if (@(Get-ChildItem -LiteralPath $installRoot -Force).Count -gt 0) {
        if (-not (Test-Path -LiteralPath $manifestPath)) { throw 'The destination is not an empty folder or an existing installation of this project.' }
        $oldManifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        if ($oldManifest.Project -ne $projectName -or -not [String]::Equals([IO.Path]::GetFullPath($oldManifest.InstallDirectory).TrimEnd('\','/'), $installRoot, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'The existing installation manifest does not belong to this folder and project.'
        }
    }
}
if (-not $AppId) {
    if (Test-Path -LiteralPath (Join-Path $installRoot 'app-id.txt')) {
        $AppId = (Get-Content -LiteralPath (Join-Path $installRoot 'app-id.txt') -Raw).Trim()
    }
    elseif (Get-Command Get-StartApps -ErrorAction SilentlyContinue) {
        $installedApps = @(Get-StartApps | Where-Object { $_.AppID -like 'OpenAI.Codex_*' -or $_.Name -eq 'Codex' })
        if ($installedApps.Count -gt 0) { $AppId = ($installedApps | Sort-Object Name,AppID | Select-Object -First 1).AppID }
    }
    if (-not $AppId) { $AppId = 'OpenAI.Codex_2p2nqsd0c76g0!App' }
}
if ($AppId -notmatch '^[A-Za-z0-9_.!\-]+$') { throw 'AppId must be a Windows desktop application AUMID.' }
$shortcutPath = $null
$shell = $null
if (-not $NoShortcut) {
    $shortcutPath = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Codex - Yuta.lnk'
    $shell = New-Object -ComObject WScript.Shell
    if (Test-Path -LiteralPath $shortcutPath) {
        Assert-OrdinaryPath -Path $shortcutPath -Directory $false
        $existingShortcut = $shell.CreateShortcut($shortcutPath)
        if (-not [String]::Equals($existingShortcut.TargetPath, (Join-Path $installRoot 'YutaCodex.exe'), [StringComparison]::OrdinalIgnoreCase)) {
            throw 'A different Codex - Yuta shortcut already exists. Preserve it and use -NoShortcut, or choose a shortcut name manually after installation.'
        }
    }
}
$assetNames = @((Get-ChildItem -LiteralPath (Join-Path $repoRoot 'assets') -File).Name)
$ownedFiles = @('YutaCodex.exe','Preview.cmd','Uninstall.ps1','Cool-Blue-Theme.txt','app-id.txt','install-manifest.json')
$ownedFiles += @($assetNames | ForEach-Object { 'assets/' + $_ })
Assert-OrdinaryPath -Path (Join-Path $installRoot 'assets') -Directory $true
if ($oldManifest -and ($null -eq $oldManifest.Files -or $oldManifest.Files -is [string])) { throw 'The existing manifest has no valid file ownership list.' }
foreach ($relativeFile in $ownedFiles) {
    $targetFile = Join-Path $installRoot $relativeFile
    Assert-OrdinaryPath -Path $targetFile -Directory $false
    if ($oldManifest -and (Test-Path -LiteralPath $targetFile) -and $relativeFile -ne 'install-manifest.json' -and @($oldManifest.Files) -notcontains $relativeFile) {
        throw ('An unregistered user file would be overwritten; it was left untouched: ' + $targetFile)
    }
}
New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
if ($NoShortcut -and $oldManifest -and $oldManifest.ShortcutPath) { $shortcutPath = $oldManifest.ShortcutPath }
$manifest = [ordered]@{ Project=$projectName; Version=$version; InstallDirectory=$installRoot; ShortcutPath=$shortcutPath; Files=$ownedFiles }
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
try {
& (Join-Path $repoRoot 'source\build.ps1') -OutputDirectory $installRoot
Copy-Item -LiteralPath (Join-Path $repoRoot 'Uninstall.ps1') -Destination (Join-Path $installRoot 'Uninstall.ps1') -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs\Cool-Blue-Theme.txt') -Destination (Join-Path $installRoot 'Cool-Blue-Theme.txt') -Force
[IO.File]::WriteAllText((Join-Path $installRoot 'app-id.txt'), $AppId + [Environment]::NewLine, (New-Object Text.UTF8Encoding($false)))
[IO.File]::WriteAllText((Join-Path $installRoot 'Preview.cmd'), "@echo off`r`nstart `"`" `"%~dp0YutaCodex.exe`" --preview`r`n", [Text.Encoding]::ASCII)
if (-not $NoShortcut) {
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = Join-Path $installRoot 'YutaCodex.exe'
    $shortcut.WorkingDirectory = $installRoot
    $shortcut.Description = '9.2 second Yuta-themed Codex startup animation'
    $shortcut.IconLocation = Join-Path $installRoot 'assets\launcher.ico'
    $shortcut.Save()
}
} finally {
    # A failed build records only files that really exist, for scoped cleanup.
    $manifest.Files = @($ownedFiles | Where-Object { Test-Path -LiteralPath (Join-Path $installRoot $_) -PathType Leaf })
    $manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
}
Write-Output ('Installed: ' + $installRoot)
Write-Output ('Codex AppId: ' + $AppId)
if ($shortcutPath -and -not $NoShortcut) { Write-Output ('Desktop shortcut: ' + $shortcutPath) }
if ($Preview) {
    $previewRun = Start-Process -FilePath (Join-Path $installRoot 'YutaCodex.exe') -ArgumentList '--preview' -WindowStyle Hidden -PassThru -Wait
    if ($previewRun.ExitCode -ne 0) { throw ('Preview failed with exit code ' + $previewRun.ExitCode) }
}
