# Armuda 0.1.0 public preview

Armuda `0.1.0` is the first public preview of CyFi Network Corporation's interactive Unity environment for exploring connected glyphs, knowledge nodes, and their attached interfaces.

## Downloads

- `Armuda-0.1.0.apk`: production-signed Magic Leap 2 / OpenXR APK for Android x86-64. This is not a generic Android phone build.
- `Armuda-Windows-0.1.0.zip`: Windows x64 desktop build. The executable is currently unsigned, so Windows SmartScreen may display a warning.
- `Armuda-Community-0.1.0.zip`: Unity source package for authorized evaluation and contribution review. The source remains all rights reserved and is not represented as open source.
- `SHA256SUMS.txt`: SHA-256 integrity hashes for all downloadable packages.

## Interaction updates

- The cursor remains free without a Tab/Alt mode switch.
- Normal controls respond to click or tap.
- Holding and dragging the middle mouse button looks in any direction.
- Left-click selects configured glyph nodes.
- Right-click opens a configured glyph's attached HUD.
- Responsive canvases adapt to window-size changes.

## Verification

- Windows runtime smoke test passed.
- Android APK Signature Scheme v2 verification passed.
- Android signer subject: `CN=Armuda, OU=Release Signing, O=CyFi Network, C=US`.
- Android signing certificate SHA-256: `b25f10b46f6445367d8e1bb513a47fcd5d5cbf0466a530f12b18565f7811eadc`.

## Known release limitations

- The Windows executable does not yet carry a trusted Authenticode signature.
- Final Magic Leap 2 device validation is still required for hardware-specific OpenXR interactions.
- The current rights notice permits testing, evaluation, and contribution review but does not grant an open-source license.
