param(
    [string]$Version = "2026.09.02-preview"
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$releaseRoot = Join-Path $repoRoot "release\github"
$stageName = "Armuda-Community-Preview-$Version"
$stageRoot = Join-Path $releaseRoot $stageName

function Assert-ArmudaSourceTree {
    $requiredFiles = @(
        "Armuda World Directory Map\Armuda\run_forever.py",
        "Armuda World Directory Map\Armuda\Core\ocean_brain.py",
        "Armuda World Directory Map\Armuda\Core\armuda_interaction_system.py",
        "README.md"
    )
    foreach ($relativePath in $requiredFiles) {
        if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $relativePath) -PathType Leaf)) {
            throw "Armuda packaging refused: required runtime file is missing: $relativePath"
        }
    }

    $forbiddenPaths = @(
        "Assets\ArTus_2026.unity",
        "ProjectSettings\EditorBuildSettings.asset"
    )
    foreach ($relativePath in $forbiddenPaths) {
        if (Test-Path -LiteralPath (Join-Path $repoRoot $relativePath)) {
            throw "Armuda packaging refused: this is an ArTus/Unity source tree ($relativePath was found)."
        }
    }

    $entrypoint = Get-Content -LiteralPath (Join-Path $repoRoot "Armuda World Directory Map\Armuda\run_forever.py") -Raw
    if ($entrypoint -notmatch "OceanBrain" -or $entrypoint -notmatch "Armuda") {
        throw "Armuda packaging refused: the desktop entrypoint failed its product identity check."
    }
}

Assert-ArmudaSourceTree

New-Item -ItemType Directory -Force -Path $releaseRoot | Out-Null
if (Test-Path -LiteralPath $stageRoot) {
    $resolvedStage = (Resolve-Path -LiteralPath $stageRoot).Path
    if (-not $resolvedStage.StartsWith((Resolve-Path -LiteralPath $releaseRoot).Path, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clear a staging path outside the release directory."
    }
    Remove-Item -LiteralPath $resolvedStage -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $stageRoot | Out-Null

function Invoke-RobocopySafe {
    param([string]$Source, [string]$Destination, [string[]]$ExtraArgs)
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    & robocopy $Source $Destination /E /NFL /NDL /NJH /NJS /NP @ExtraArgs | Out-Null
    if ($LASTEXITCODE -gt 7) {
        throw "Robocopy failed for $Source with exit code $LASTEXITCODE"
    }
}

$sourceApp = Join-Path $repoRoot "Armuda World Directory Map\Armuda"
$targetApp = Join-Path $stageRoot "Armuda World Directory Map\Armuda"
Invoke-RobocopySafe $sourceApp $targetApp @(
    "/XD", "Data", "assets", "__pycache__",
    "/XF", "*.pyc", "*.pyo", "armuda_runtime.log", "armuda_visual_test.log"
)

$targetData = Join-Path $targetApp "Data"
New-Item -ItemType Directory -Force -Path $targetData | Out-Null
foreach ($filename in @(
    "__init__.py", "state_sync.py", "diagnostics_bridge.py", "health_receiver.py",
    "artus_connection.json", "hud_configs.json", "image_backend.json", "image_generation_connection.json"
)) {
    $source = Join-Path $sourceApp "Data\$filename"
    if (Test-Path -LiteralPath $source) {
        Copy-Item -LiteralPath $source -Destination $targetData -Force
    }
}

# Ship the runtime-owned metadata and UI icons, but never local user uploads.
$targetRuntimeAssets = Join-Path $targetApp "assets"
Invoke-RobocopySafe (Join-Path $sourceApp "assets") $targetRuntimeAssets @(
    "/XD", "UserImages", "UserMeshes", "__pycache__",
    "/XF", "*.pyc", "*.pyo"
)

$targetAssets = Join-Path $stageRoot "assets"
Invoke-RobocopySafe (Join-Path $repoRoot "assets") $targetAssets @(
    "/XD", "Uploads", "__pycache__",
    "/XF", "*.pyc", "*.pyo"
)

foreach ($item in @(
    "README.md", "CONTRIBUTING.md", "CODE_OF_CONDUCT.md", "SECURITY.md",
    "LICENSE.md", "LICENSE-CONTENT.md", "CODE_LICENSE_SCOPE.md", "TRADEMARKS.md",
    "NOTICE.md", "CONTRIBUTOR_LICENSE_AGREEMENT.md", "requirements-desktop.txt",
    ".gitignore", ".gitattributes"
)) {
    Copy-Item -LiteralPath (Join-Path $repoRoot $item) -Destination $stageRoot -Force
}
Invoke-RobocopySafe (Join-Path $repoRoot "LICENSES") (Join-Path $stageRoot "LICENSES") @()
Invoke-RobocopySafe (Join-Path $repoRoot "Documentation") (Join-Path $stageRoot "Documentation") @("/XD", "__pycache__")
Invoke-RobocopySafe (Join-Path $repoRoot "packaging") (Join-Path $stageRoot "packaging") @("/XD", "__pycache__")
if (Test-Path -LiteralPath (Join-Path $repoRoot ".github")) {
    Invoke-RobocopySafe (Join-Path $repoRoot ".github") (Join-Path $stageRoot ".github") @("/XD", "__pycache__")
}

Set-Content -LiteralPath (Join-Path $stageRoot "VERSION.txt") -Value $Version -Encoding ascii

$archivePath = Join-Path $releaseRoot "$stageName.zip"
if (Test-Path -LiteralPath $archivePath) {
    Remove-Item -LiteralPath $archivePath -Force
}
Compress-Archive -LiteralPath $stageRoot -DestinationPath $archivePath -CompressionLevel Optimal

$hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath "$archivePath.sha256" -Value "$hash  $([IO.Path]::GetFileName($archivePath))" -Encoding ascii

Write-Host "GitHub source package: $archivePath"
Write-Host "SHA256: $hash"
