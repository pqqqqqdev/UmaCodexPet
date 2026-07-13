[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$Version,

    [string]$ChangelogPath,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ChangelogPath)) {
    $ChangelogPath = Join-Path (Split-Path -Parent $PSScriptRoot) "CHANGELOG.md"
}

if (-not (Test-Path -LiteralPath $ChangelogPath -PathType Leaf)) {
    throw "Changelog was not found: $ChangelogPath"
}

$lines = @(Get-Content -LiteralPath $ChangelogPath -Encoding UTF8)
$escapedVersion = [regex]::Escape($Version)
$headingPattern = '^##\s+(?:\[' + $escapedVersion + '\]|' + $escapedVersion + ')(?:\s+-\s+.+)?\s*$'
$startIndex = -1

for ($index = 0; $index -lt $lines.Count; $index++) {
    if ($lines[$index] -match $headingPattern) {
        $startIndex = $index
        break
    }
}

if ($startIndex -lt 0) {
    throw "CHANGELOG.md has no exact section for version $Version. Expected a heading such as '## $Version - YYYY-MM-DD'."
}

$body = [System.Collections.Generic.List[string]]::new()
for ($index = $startIndex + 1; $index -lt $lines.Count; $index++) {
    if ($lines[$index] -match '^##\s+') {
        break
    }

    $body.Add($lines[$index])
}

while ($body.Count -gt 0 -and [string]::IsNullOrWhiteSpace($body[0])) {
    $body.RemoveAt(0)
}
while ($body.Count -gt 0 -and [string]::IsNullOrWhiteSpace($body[$body.Count - 1])) {
    $body.RemoveAt($body.Count - 1)
}

if ($body.Count -eq 0) {
    throw "The CHANGELOG.md section for version $Version is empty."
}

$outputDirectory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

$body | Set-Content -LiteralPath $OutputPath -Encoding UTF8
Write-Host "Prepared curated release notes for $Version at $OutputPath"
