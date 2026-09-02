# Armuda Android packaging status

The installed Android SDK, build tools, NDK, and JDK are sufficient to build an Android project. The current Armuda application itself is not yet an Android project: it creates a desktop GLFW window, requests desktop OpenGL 3.3, uses mouse buttons and Windows launch targets, and imports CPython desktop extensions.

Creating an APK directly from that code would either fail at startup or produce a misleading shell that is not Armuda. A real mobile release requires these explicit porting steps:

1. Select the Android application ID and signing owner.
2. Move the reusable world/profile schema behind a platform-neutral interface.
3. Implement the renderer with OpenGL ES 3.x or a maintained mobile engine such as Unity.
4. Map input consistently: tap selects, long-press opens an attachment/action, one-finger drag looks, and pinch controls distance.
5. Replace desktop file launchers with Android intents and scoped storage.
6. Add lifecycle save/restore, permission handling, performance budgets, and device tests.
7. Produce a signed internal-test APK/AAB only after the full Armuda scene renders and saves correctly on-device.

No placeholder APK is emitted by the release scripts. This gate protects testers from receiving an installable file that cannot run the real application.
