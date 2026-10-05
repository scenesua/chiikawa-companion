# Character rig verification

## Current source preview (2026-10-03)

Run `tools/check-source.ps1 -DotnetRoot <SDK directory> -Preview` from the project root.
This compiles current Windows source in memory, loads existing generated XAML stubs
and sprite resources, and renders PNGs without emitting an executable or launching
the desktop companion. Output is in `obj/rig-preview/`.

The preview checks all 17 profiles, premultiplied alpha, exact resting reconstruction
at 128 and 240 pixels, expression compatibility, expression transparency, cheek
input/release, and mouth/body isolation. An unfiltered run additionally compares
all 1,312 atlas frames byte-for-byte after splitting and recomposition, and checks
actual pulled portraits for lower-body isolation and valid output alpha.

`<character>-atlas-review.png` shows every ordinary source pose. The individual
`<character>-pulls.png` sheets show both cheeks in eight directions. `momonga-release.png`
shows rebound over time. `eating-drinking.png` and individual `*-meal-water.png` sheets
show white-rice chewing and water swallowing. These are sampled frames, not a full
native interaction recording or verification of every food/furniture combination.

## Visual fixes reviewed

- Sparse transparent regions no longer erase underlying ears, skin or clothing.
- Expression mixing retains the separated model instead of importing the flattened face again.
- Facial backing and expression decals obey premultiplied-alpha composition.
- Cheek deformation samples a continuous skin surface once; facial features remain anchored.
- Blush masks exclude dark fur outlines. Sanrio nose/mouth boundaries exclude old mouth strokes
  and nearby hands; hood backing prevents holes during inward pulls.
- Rilakkuma muzzle backing, Dekatsuyo white face, and Ode's single eye/beak have dedicated masks.
  Ode's upper beak corners are included with the beak rather than stretched with the shoulder.
- Chewing moves mouth/cheeks; water moves the mouth without moving the body or white belly.

## Limits of the evidence

All 17 characters use the shared model on Windows and macOS. Legacy atlases remain
import sources; this patch does not redraw official character artwork. Frontal
interaction masks are calibrated; byte-exact reconstruction does not prove semantic
part assignment for every directional, sleeping or furniture pose. Extreme pulls
can still show compressed outline strokes and a thin antialiased fringe.

Mac source type checking uses `-Platform Mac`; native macOS rendering and XAML
compilation are untested. No new executable, package, release or native desktop
smoke test was produced during this visual-fix pass. Existing executables are older
than these source changes.
