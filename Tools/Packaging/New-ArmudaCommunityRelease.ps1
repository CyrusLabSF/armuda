[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$OutputDirectory = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'Releases'),
    [bool]$KeepLatestOnly = $true
)

$ErrorActionPreference = 'Stop'
$version = '0.1.1'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$releaseName = "Armuda-Community-$version-$stamp"
$stageRoot = Join-Path ([System.IO.Path]::GetTempPath()) $releaseName
$archivePath = Join-Path $OutputDirectory "$releaseName.zip"

if (-not (Test-Path -LiteralPath (Join-Path $ProjectRoot 'Assets\ArTus_2026.unity'))) {
    throw "The selected project root is not the Armuda Unity project: $ProjectRoot"
}

New-Item -ItemType Directory -Path $stageRoot -Force | Out-Null
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$topLevelFiles = @(
    '.gitignore',
    'README.md',
    'CONTRIBUTING.md',
    'CODE_OF_CONDUCT.md',
    'SECURITY.md',
    'LICENSE.md',
    'NOTICE.md'
)

foreach ($file in $topLevelFiles) {
    Copy-Item -LiteralPath (Join-Path $ProjectRoot $file) -Destination $stageRoot
}

$copyRoots = @('Assets', 'Packages', 'ProjectSettings', 'Docs\Armuda', 'Tools\Packaging')
foreach ($relativeRoot in $copyRoots) {
    $source = Join-Path $ProjectRoot $relativeRoot
    $destination = Join-Path $stageRoot $relativeRoot
    New-Item -ItemType Directory -Path $destination -Force | Out-Null

    robocopy $source $destination /E /XD `
        (Join-Path $ProjectRoot 'Assets\Library') `
        (Join-Path $ProjectRoot 'Assets\_Recovery') `
        (Join-Path $ProjectRoot 'Assets\Logs') `
        (Join-Path $ProjectRoot 'Assets\Scenes\SampleScene') `
        (Join-Path $ProjectRoot 'Assets\TextMesh Pro\Examples & Extras') `
        (Join-Path $ProjectRoot 'Assets\XR\Temp') `
        /XF '*.csproj' '*.sln' '*.user' '*.keystore' '*.jks' '*.p12' '.env' '.env.*' `
        'Library.meta' '_Recovery.meta' 'Logs.meta' 'Temp.meta' `
        'ArTus.unity' 'ArTus.unity.meta' `
        'ArTus2025.unity' 'ArTus2025.unity.meta' `
        'ArTus2025(Baseline).unity' 'ArTus2025(Baseline).unity.meta' `
        'ArTusBroke.unity' 'ArTusBroke.unity.meta' `
        'Baseline.unity' 'Baseline.unity.meta' `
        'SampleScene.unity' 'SampleScene.unity.meta' `
        /NFL /NDL /NJH /NJS /NP | Out-Null

    if ($LASTEXITCODE -ge 8) {
        throw "Robocopy failed for $relativeRoot with exit code $LASTEXITCODE"
    }
}

Compress-Archive -Path (Join-Path $stageRoot '*') -DestinationPath $archivePath -CompressionLevel Optimal

$resolvedStageRoot = (Resolve-Path -LiteralPath $stageRoot).Path
$resolvedTempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
if (-not $resolvedStageRoot.StartsWith($resolvedTempRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
    -not (Split-Path -Leaf $resolvedStageRoot).StartsWith('Armuda-Community-', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to remove unexpected staging path: $resolvedStageRoot"
}

Remove-Item -LiteralPath $resolvedStageRoot -Recurse -Force

if ($KeepLatestOnly) {
    $resolvedOutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path
    Get-ChildItem -LiteralPath $resolvedOutputDirectory -Filter "Armuda-Community-$version-*.zip" -File |
        Where-Object { $_.FullName -ne $archivePath } |
        ForEach-Object {
            if ($_.DirectoryName -ne $resolvedOutputDirectory -or
                -not $_.Name.StartsWith("Armuda-Community-$version-", [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Refusing to remove unexpected archive path: $($_.FullName)"
            }

            Remove-Item -LiteralPath $_.FullName -Force
        }
}

$archive = Get-Item -LiteralPath $archivePath
$hash = Get-FileHash -LiteralPath $archivePath -Algorithm SHA256
[PSCustomObject]@{
    Path = $archive.FullName
    SizeBytes = $archive.Length
    SHA256 = $hash.Hash
}
