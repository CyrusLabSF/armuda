[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$OutputDirectory = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'Releases\Armuda-0.1.0')
)

$ErrorActionPreference = 'Stop'
$version = '0.1.0'
$source = Join-Path $ProjectRoot "Releases\Armuda-$version\Windows"
$archivePath = Join-Path $OutputDirectory "Armuda-Windows-$version.zip"
$stageName = "Armuda-Windows-$version-" + [guid]::NewGuid().ToString('N')
$stageRoot = Join-Path ([System.IO.Path]::GetTempPath()) $stageName

if (-not (Test-Path -LiteralPath (Join-Path $source 'Armuda.exe'))) {
    throw "Armuda.exe was not found in the Windows build folder: $source"
}

New-Item -ItemType Directory -Path $stageRoot -Force | Out-Null
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

robocopy $source $stageRoot /E `
    /XD (Join-Path $source 'Armuda_BurstDebugInformation_DoNotShip') `
    /NFL /NDL /NJH /NJS /NP | Out-Null

if ($LASTEXITCODE -ge 8) {
    throw "Robocopy failed with exit code $LASTEXITCODE"
}

Compress-Archive -Path (Join-Path $stageRoot '*') -DestinationPath $archivePath -CompressionLevel Optimal -Force

$resolvedStageRoot = (Resolve-Path -LiteralPath $stageRoot).Path
$resolvedTempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
if (-not $resolvedStageRoot.StartsWith($resolvedTempRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
    -not (Split-Path -Leaf $resolvedStageRoot).StartsWith("Armuda-Windows-$version-", [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to remove unexpected staging path: $resolvedStageRoot"
}

Remove-Item -LiteralPath $resolvedStageRoot -Recurse -Force

$archive = Get-Item -LiteralPath $archivePath
$hash = Get-FileHash -LiteralPath $archivePath -Algorithm SHA256
[PSCustomObject]@{
    Path = $archive.FullName
    SizeBytes = $archive.Length
    SHA256 = $hash.Hash
}
