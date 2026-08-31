# Armuda 0.1.0 preview

This packaging preview establishes the first repeatable Windows, Magic Leap 2 APK, and community-source release workflow.

## Interaction updates

- The cursor remains visible and free; no Tab/Alt cursor mode is required.
- Normal UI controls respond to a standard click or tap.
- Middle-mouse drag provides unrestricted desktop camera look.
- Left-click selects configured glyphs and nodes.
- Right-click opens a configured glyph or node HUD.
- Touch supports tap selection, drag look outside UI, and long-press HUD access.

## Display and packaging updates

- Screen-space UI scales from a 1920 x 1080 reference layout as the window changes size.
- Windows builds are resizable and run in a standard window.
- The Android build retains Armuda's Magic Leap 2 OpenXR profile and x86-64 ABI.
- Only the production `Assets/ArTus_2026.unity` scene is enabled for player builds.
- Packaging validation rejects missing scene files and missing scripts in enabled scenes.
- Community archives omit caches, generated builds, recovery content, legacy scenes, sample scenes, secrets, and signing files.

## Release status

The Windows player and Magic Leap 2 APK are testing previews. The APK uses a test signature; a production keystore is required before public store distribution. The included rights notice is not an open-source license, so a license must be approved before the GitHub repository is described as open source.
