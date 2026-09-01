# Armuda packaging

## Build identity

- Product: `Armuda`
- Company: `CyFi Network Corporation`
- Version: `0.1.1`
- Android application ID: `com.cyfinetwork.armuda`
- Android minimum SDK: 29
- Android target: Magic Leap 2 / OpenXR
- Android architecture: x86-64

## Unity menu

- `Armuda > Build > Validate Packaging`
- `Armuda > Build > Windows Desktop`
- `Armuda > Build > Android APK`

By default builds are written beneath `Releases/Armuda-0.1.1`. Batch builds can override the root with `-armudaOutputRoot <absolute-path>`.

## Required checks

1. Confirm the build contains only enabled scenes from Unity Build Settings.
2. Resize the desktop window through narrow, standard, ultrawide, and maximized layouts.
3. Verify the pointer stays free and UI controls work with a normal click.
4. Verify middle-mouse drag camera look.
5. Verify glyph selection and attached HUD behavior.
6. Install the APK on a Magic Leap 2 test device and verify its OpenXR interactions.

## Android release signing

Initialize a dedicated Armuda signing key once:

```powershell
.\Tools\Packaging\Initialize-ArmudaAndroidSigning.ps1
```

The initializer creates an RSA release key and an owner-only signing configuration beneath `%USERPROFILE%\.armuda\signing`. Passwords and keystore material stay outside the Unity project and are excluded from source and release archives. `ArmudaBuildPipeline.BuildAndroid` requires this configuration and refuses to fall back to a debug certificate.

Back up the entire private signing directory in a secure credential vault. Losing the signing key can prevent future APK upgrades. Set `ARMUDA_ANDROID_SIGNING_CONFIG` to an alternate configuration path when building on another trusted workstation or CI service.

## GitHub release

The source archive excludes Unity caches, generated builds, recovery scenes, IDE files, secrets, and signing material. The current rights notice is not an open-source license; approve a license before announcing an open-source release.
