# Armuda 3D Objects Library

This folder is the working library for custom 3D objects you want to place inside Armuda.

## Recommended structure

- `Static STL`
  Use for early Meshy exports and static shape testing.
- `Animated GLB`
  Use for final animated objects. `GLB` is the better target once movement/rigging is involved.
- `Textures`
  Use for supporting image maps or reference textures.
- `WIP`
  Use for in-progress exports that are not ready for placement.

## Scale guidance for Armuda

Armuda currently imports mesh coordinates almost exactly as-authored.
That means STL units are effectively treated as Armuda world units.

Best current rule:

- Treat `1 Armuda unit` as roughly `1 meter`
- Export or scale your object so its full size is already close to its intended in-world size

Examples:

- Small prop: `0.25` to `1.0` units tall
- Chair / console / sign: `1.0` to `2.5` units tall
- Person-sized statue: `1.6` to `2.2` units tall
- Small building / tower module: `4` to `20` units tall
- Skyline structure: `20+` units tall

## Important STL note

`STL` does not carry a reliable unit system or animation data.

So for Armuda:

- `STL` is good for static geometry blocking and early object tests
- `GLB` is the better final format for animated or richer objects

If Meshy gives you millimeter-based sizing, do **not** leave a human-scale object at `1800 mm` unless you convert that to about `1.8` world units before final placement.
Otherwise it may import far too large.

## Recommended workflow

1. Generate concept mesh in Meshy
2. Save early version in `Static STL`
3. Check placement scale in Armuda
4. Refine and animate externally
5. Export final animated version to `Animated GLB`

