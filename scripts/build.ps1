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
$project = Join-Path $repoRoot "src/UmaCodexPet/UmaCodexPet.csproj"

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
$pluginDll = Join-Path $buildDir "UmaCodexPet.dll"
if (-not (Test-Path $pluginDll -PathType Leaf)) {
    throw "Expected build output was not produced: $pluginDll"
}

$packageRoot = Join-Path $repoRoot "artifacts/package"
$packageDir = Join-Path $packageRoot "BepInEx/plugins/UmaCodexPet"
if (Test-Path $packageRoot) {
    Remove-Item $packageRoot -Recurse -Force
}
New-Item $packageDir -ItemType Directory -Force | Out-Null
Copy-Item $pluginDll $packageDir
$pdb = Join-Path $buildDir "UmaCodexPet.pdb"
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
    "dev.pqqqqq.umacodexpet.example.cfg",
    "UmaCodexPet_Overrides.example.csv"
)
foreach ($configExample in $configExamples) {
    $source = Join-Path $repoRoot "config/$configExample"
    if (-not (Test-Path $source -PathType Leaf)) {
        throw "Required config example is missing: config/$configExample"
    }
    Copy-Item $source $packageConfigDir
}

if ($Install) {
    $pluginsRoot = Join-Path $UmaViewerDir "BepInEx/plugins"
    $legacyInstallDir = Join-Path $pluginsRoot "UmaPetForge"
    $legacyFiles = Get-ChildItem $pluginsRoot -File -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -in @("UmaPetForge.dll", "UmaPetForge.pdb") }
    foreach ($legacyFile in $legacyFiles) {
        Remove-Item $legacyFile.FullName -Force
    }
    if ((Test-Path $legacyInstallDir -PathType Container) -and
        -not (Get-ChildItem $legacyInstallDir -Force | Select-Object -First 1)) {
        Remove-Item $legacyInstallDir -Force
    }
    if ($legacyFiles.Count -gt 0) {
        Write-Host "Removed $($legacyFiles.Count) legacy UmaPetForge plugin file(s)"
    }
    $installDir = Join-Path $UmaViewerDir "BepInEx/plugins/UmaCodexPet"
    New-Item $installDir -ItemType Directory -Force | Out-Null
    Copy-Item $pluginDll (Join-Path $installDir "UmaCodexPet.dll") -Force
    Write-Host "Installed $(Join-Path $installDir 'UmaCodexPet.dll')"
}

Write-Host "Built $pluginDll"
Write-Host "Packaged $packageRoot"
