# Armuda controls

## Desktop

| Action | Control |
| --- | --- |
| Activate a UI control | Left click |
| Select a configured glyph/node | Left click |
| Open a glyph/node HUD | Right click |
| Look around | Hold and drag middle mouse button |

The pointer remains visible and unlocked. Armuda does not require a Tab or Alt cursor mode.

## Touch

| Action | Control |
| --- | --- |
| Activate a UI control | Tap |
| Select a configured glyph/node | Tap |
| Open a glyph/node HUD | Long press |
| Look around | Drag outside the UI |

## Configuring a glyph

Add `ArmudaGlyphInteraction` to a world object with a collider. Assign the optional HUD root and UnityEvents in the Inspector. Normal `IPointerClickHandler` world interactions remain supported.
