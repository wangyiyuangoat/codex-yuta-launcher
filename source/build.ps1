param(
    [string]$OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'dist')
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
$frameworkRoot = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path -LiteralPath (Join-Path $frameworkRoot 'csc.exe'))) {
    $frameworkRoot = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
}
$compilerPath = Join-Path $frameworkRoot 'csc.exe'
$wpfPath = Join-Path $frameworkRoot 'WPF'
if (-not (Test-Path -LiteralPath $compilerPath)) { throw 'The Windows .NET Framework C# compiler was not found.' }
$checkPaths = @($outputRoot, (Join-Path $outputRoot 'assets'), (Join-Path $outputRoot 'YutaCodex.exe'))
$checkPaths += @((Get-ChildItem -LiteralPath (Join-Path $repoRoot 'assets') -File) | ForEach-Object { Join-Path (Join-Path $outputRoot 'assets') $_.Name })
foreach ($checkPath in $checkPaths) {
    $cursor = $checkPath
    while ($cursor) {
        if (Test-Path -LiteralPath $cursor) {
            $entry = Get-Item -LiteralPath $cursor -Force
            if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -or $entry.LinkType) { throw ('Refusing to build through a linked path: ' + $cursor) }
        }
        $cursor = Split-Path -Parent $cursor
    }
}
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$compileArguments = @(
    '/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/debug-',
    ('/out:' + (Join-Path $outputRoot 'YutaCodex.exe')),
    '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Xaml.dll',
    ('/reference:' + (Join-Path $wpfPath 'WindowsBase.dll')),
    ('/reference:' + (Join-Path $wpfPath 'PresentationCore.dll')),
    ('/reference:' + (Join-Path $wpfPath 'PresentationFramework.dll')),
    ('/win32icon:' + (Join-Path $repoRoot 'assets\launcher.ico')),
    (Join-Path $PSScriptRoot 'Launcher.cs')
)
& $compilerPath @compileArguments
if ($LASTEXITCODE -ne 0) { throw "C# compilation failed with exit code $LASTEXITCODE." }
$targetAssets = Join-Path $outputRoot 'assets'
New-Item -ItemType Directory -Path $targetAssets -Force | Out-Null
foreach ($asset in Get-ChildItem -LiteralPath (Join-Path $repoRoot 'assets') -File) {
    $targetPath = Join-Path $targetAssets $asset.Name
    if (-not [String]::Equals($asset.FullName, [IO.Path]::GetFullPath($targetPath), [StringComparison]::OrdinalIgnoreCase)) {
        Copy-Item -LiteralPath $asset.FullName -Destination $targetPath -Force
    }
}
Write-Output ('Built ' + (Join-Path $outputRoot 'YutaCodex.exe'))
