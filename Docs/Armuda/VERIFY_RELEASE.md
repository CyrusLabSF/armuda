# Verify an Armuda release

Armuda's Windows package is unsigned during the community-preview phase. Download it only from the official release page:

https://github.com/CyrusLabSF/armuda/releases

## Windows PowerShell

Place the downloaded package and `SHA256SUMS.txt` in the same folder, then calculate the package hash:

```powershell
Get-FileHash -Algorithm SHA256 .\Armuda-Windows-0.1.1.zip
```

Compare the result with the corresponding entry in `SHA256SUMS.txt`. For release `0.1.1`, the expected Windows archive hash is:

```text
78217E7286EC796EB69037AC9F98DE001ABAA3A5E61619DA5E01DE15601FEC3D
```

The expected Android APK hash is:

```text
182D784F62EDD9E0CE6B9FB498BA1ADE5D8558A6D61B9278BF524DFC9C52FFFD
```

If a hash differs, do not run the file. Delete it and download it again from the official release page.

## Important limitations

A matching hash confirms that a download matches the checksum published with the release. It does not provide an Authenticode publisher identity and cannot protect against compromise of both a package and its checksum. Review the public source and build locally when stronger assurance is needed.

Do not disable Microsoft Defender, SmartScreen, Smart App Control, or another security product globally to run Armuda. Community testers should report unexpected security detections privately according to [SECURITY.md](../../SECURITY.md).
