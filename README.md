# Armuda Community Preview

Armuda is a local-first 3D world-building environment rendered with GLFW and OpenGL. This preview keeps the cursor available for ordinary UI interaction while using explicit pointer gestures for world navigation.

## Controls

- Left click: select controls, objects, and glyph nodes.
- Right click: open a glyph node attachment/HUD or an object's action menu.
- Hold the mouse-wheel button and drag: look in any direction.
- Mouse wheel: move between first- and third-person distance.
- `W`, `A`, `S`, `D`: move; `Space` / `Shift`: change depth.
- `Ctrl+S`: save; `Ctrl+Z` / `Ctrl+Y`: undo / redo.

## Run from source

Use Python 3.14 on Windows:

```powershell
python -m pip install -r requirements-desktop.txt
python "Armuda World Directory Map/Armuda/run_forever.py"
```

Armuda stores source-run data inside the source package. Packaged builds store profiles, worlds, logs, settings, and uploads under `%LOCALAPPDATA%\Armuda`, keeping application files clean and writable.

Friend discovery, requests, notifications, and direct messages are persistent and fully usable between profiles on the same installation. Cross-device community communication requires a hosted identity and message relay; the preview labels these channels as on-device rather than implying internet delivery.

## Build targets

- Windows desktop: `powershell -ExecutionPolicy Bypass -File packaging/build_windows.ps1`
- GitHub source archive: `powershell -ExecutionPolicy Bypass -File packaging/build_github_release.ps1`
- Android: see `packaging/android/README.md`. The desktop renderer requires a mobile rendering/input port before a real APK can be produced.

## AI image configuration

The desktop client never stores provider secrets. The bundled image service reads `OPENAI_API_KEY` or another configured provider token from the process environment. Preview mode works without a provider key.

## Release status

This is a community preview. CyFi-authored code and project documentation identified in `CODE_LICENSE_SCOPE.md` are licensed under MPL-2.0. Armuda creative content and branding remain protected under `LICENSE-CONTENT.md` and `TRADEMARKS.md`. See `LICENSE.md` and `NOTICE.md` before redistributing or contributing.
