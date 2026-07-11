[CmdletBinding()]
param(
    [string]$UmaViewerDir = $env:UMAVIEWER_DIR,
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$Version = $env:VERSION,
    [switch]$Install
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src/UmaPetForge/UmaPetForge.csproj"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet SDK 8 or newer is required."
}
if ([string]::IsNullOrWhiteSpace($UmaViewerDir)) {
    throw "Set UMAVIEWER_DIR or pass -UmaViewerDir with the folder containing UmaViewer.exe."
}

$UmaViewerDir = (Resolve-Path $UmaViewerDir).Path
$requiredFiles = @(
    (Join-Path $UmaViewerDir "UmaViewer.exe"),
    (Join-Path $UmaViewerDir "UmaViewer_Data/Managed/umamusume.dll"),
    (Join-Path $UmaViewerDir "BepInEx/core/BepInEx.dll")
)
foreach ($requiredFile in $requiredFiles) {
    if (-not (Test-Path $requiredFile -PathType Leaf)) {
        throw "Required file not found: $requiredFile"
    }
}

$buildArgs = @(
    "build",
    $project,
    "--configuration", $Configuration,
    "-p:UmaViewerDir=$UmaViewerDir"
)
if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $buildArgs += "-p:Version=$Version"
}

& dotnet @buildArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE."
}

$buildDir = Join-Path $repoRoot "artifacts/bin/$Configuration/net472"
$pluginDll = Join-Path $buildDir "UmaPetForge.dll"
if (-not (Test-Path $pluginDll -PathType Leaf)) {
    throw "Expected build output was not produced: $pluginDll"
}

$packageRoot = Join-Path $repoRoot "artifacts/package"
$packageDir = Join-Path $packageRoot "BepInEx/plugins/UmaPetForge"
if (Test-Path $packageRoot) {
    Remove-Item $packageRoot -Recurse -Force
}
New-Item $packageDir -ItemType Directory -Force | Out-Null
Copy-Item $pluginDll $packageDir
$pdb = Join-Path $buildDir "UmaPetForge.pdb"
if (Test-Path $pdb -PathType Leaf) {
    Copy-Item $pdb $packageDir
}

# Keep release contents on an explicit allowlist. In particular, never copy
# exporter output or game assets into the distributable archive.
$releaseFiles = @(
    "README.md",
    "CHANGELOG.md",
    "LICENSE",
    "THIRD_PARTY.md"
)
foreach ($releaseFile in $releaseFiles) {
    $source = Join-Path $repoRoot $releaseFile
    if (-not (Test-Path $source -PathType Leaf)) {
        throw "Required release file is missing: $releaseFile"
    }
    Copy-Item $source $packageRoot
}

$packageConfigDir = Join-Path $packageRoot "config"
New-Item $packageConfigDir -ItemType Directory -Force | Out-Null
$configExamples = @(
    "dev.pqqqqq.umapetforge.example.cfg",
    "UmaPetForge_Overrides.example.csv"
)
foreach ($configExample in $configExamples) {
    $source = Join-Path $repoRoot "config/$configExample"
    if (-not (Test-Path $source -PathType Leaf)) {
        throw "Required config example is missing: config/$configExample"
    }
    Copy-Item $source $packageConfigDir
}

if ($Install) {
    $installDir = Join-Path $UmaViewerDir "BepInEx/plugins/UmaPetForge"
    New-Item $installDir -ItemType Directory -Force | Out-Null
    Copy-Item $pluginDll (Join-Path $installDir "UmaPetForge.dll") -Force
    Write-Host "Installed $(Join-Path $installDir 'UmaPetForge.dll')"
}

Write-Host "Built $pluginDll"
Write-Host "Packaged $packageRoot"
