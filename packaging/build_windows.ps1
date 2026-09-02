param(
    [string]$Version = "2026.09.02-preview"
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$releaseRoot = Join-Path $repoRoot "release\windows"
$workRoot = Join-Path $repoRoot "build\pyinstaller"
$specPath = Join-Path $PSScriptRoot "windows\armuda.spec"

function Assert-ArmudaSourceTree {
    $requiredFiles = @(
        "Armuda World Directory Map\Armuda\run_forever.py",
        "Armuda World Directory Map\Armuda\Core\ocean_brain.py",
        "Armuda World Directory Map\Armuda\Core\armuda_interaction_system.py",
        "packaging\windows\armuda.spec"
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

New-Item -ItemType Directory -Force -Path $releaseRoot, $workRoot | Out-Null

python -m PyInstaller --noconfirm --clean --distpath $releaseRoot --workpath $workRoot $specPath
if ($LASTEXITCODE -ne 0) {
    throw "PyInstaller failed with exit code $LASTEXITCODE"
}

$packageDir = Join-Path $releaseRoot "Armuda"
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "windows\README-WINDOWS.md") -Destination (Join-Path $packageDir "README.md") -Force
foreach ($item in @("LICENSE.md", "LICENSE-CONTENT.md", "CODE_LICENSE_SCOPE.md", "TRADEMARKS.md", "NOTICE.md")) {
    Copy-Item -LiteralPath (Join-Path $repoRoot $item) -Destination $packageDir -Force
}
Copy-Item -LiteralPath (Join-Path $repoRoot "LICENSES") -Destination (Join-Path $packageDir "LICENSES") -Recurse -Force

$archivePath = Join-Path $releaseRoot "Armuda-Windows-x64-$Version.zip"
if (Test-Path -LiteralPath $archivePath) {
    Remove-Item -LiteralPath $archivePath -Force
}
Compress-Archive -LiteralPath $packageDir -DestinationPath $archivePath -CompressionLevel Optimal

$hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
$hashPath = "$archivePath.sha256"
Set-Content -LiteralPath $hashPath -Value "$hash  $([IO.Path]::GetFileName($archivePath))" -Encoding ascii

Write-Host "Windows package: $archivePath"
Write-Host "SHA256: $hash"
