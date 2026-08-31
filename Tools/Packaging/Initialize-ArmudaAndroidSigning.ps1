[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$SigningRoot = (Join-Path $env:USERPROFILE '.armuda\signing'),
    [string]$Alias = 'armuda-release',
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$projectVersionPath = Join-Path $ProjectRoot 'ProjectSettings\ProjectVersion.txt'
if (-not (Test-Path -LiteralPath $projectVersionPath)) {
    throw "Unity project version file was not found: $projectVersionPath"
}

$versionLine = Select-String -LiteralPath $projectVersionPath -Pattern '^m_EditorVersion:\s*(.+)$' | Select-Object -First 1
if (-not $versionLine) {
    throw "Unity editor version could not be read from: $projectVersionPath"
}

$unityVersion = $versionLine.Matches[0].Groups[1].Value.Trim()
$keytoolPath = Join-Path $env:ProgramFiles "Unity\Hub\Editor\$unityVersion\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe"
if (-not (Test-Path -LiteralPath $keytoolPath)) {
    throw "Unity Android keytool was not found: $keytoolPath"
}

$keystorePath = Join-Path $SigningRoot 'armuda-release.keystore'
$configPath = Join-Path $SigningRoot 'android-signing.env'
if (-not $Force -and ((Test-Path -LiteralPath $keystorePath) -or (Test-Path -LiteralPath $configPath))) {
    throw "Armuda signing material already exists at $SigningRoot. Nothing was overwritten."
}

New-Item -ItemType Directory -Path $SigningRoot -Force | Out-Null

function New-SigningSecret {
    $bytes = New-Object byte[] 32
    $random = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $random.GetBytes($bytes)
    }
    finally {
        $random.Dispose()
    }

    return [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

$signingPassword = New-SigningSecret
$distinguishedName = 'CN=Armuda, OU=Release Signing, O=CyFi Network, C=US'

& $keytoolPath -genkeypair -v `
    -keystore $keystorePath `
    -storetype PKCS12 `
    -alias $Alias `
    -keyalg RSA `
    -keysize 3072 `
    -validity 10000 `
    -dname $distinguishedName `
    -storepass $signingPassword `
    -keypass $signingPassword

if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $keystorePath)) {
    throw "Armuda keystore generation failed with exit code $LASTEXITCODE."
}

$configLines = @(
    '# Armuda Android release signing. Keep this file private and backed up securely.',
    "ARMUDA_ANDROID_KEYSTORE_PATH=$keystorePath",
    "ARMUDA_ANDROID_KEYSTORE_PASSWORD=$signingPassword",
    "ARMUDA_ANDROID_KEY_ALIAS=$Alias",
    "ARMUDA_ANDROID_KEY_PASSWORD=$signingPassword"
)
[System.IO.File]::WriteAllLines($configPath, $configLines, [System.Text.UTF8Encoding]::new($false))

$currentIdentity = [System.Security.Principal.WindowsIdentity]::GetCurrent().User
$inheritance = [System.Security.AccessControl.InheritanceFlags]'ContainerInherit, ObjectInherit'
$propagation = [System.Security.AccessControl.PropagationFlags]::None
$allow = [System.Security.AccessControl.AccessControlType]::Allow
$fullControl = [System.Security.AccessControl.FileSystemRights]::FullControl

$directoryAcl = New-Object System.Security.AccessControl.DirectorySecurity
$directoryAcl.SetOwner($currentIdentity)
$directoryAcl.SetAccessRuleProtection($true, $false)
$directoryAcl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule($currentIdentity, $fullControl, $inheritance, $propagation, $allow)))
Set-Acl -LiteralPath $SigningRoot -AclObject $directoryAcl

[PSCustomObject]@{
    KeystorePath = $keystorePath
    ConfigPath = $configPath
    Alias = $Alias
    CertificateSubject = $distinguishedName
    PasswordsPrinted = $false
}
