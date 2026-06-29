# Rain World — Ultrawide 21:9

A BepInEx mod that renders Rain World at a **true 21:9** internal resolution
(`1792x768` by default) instead of the stock 16:9 presets. You see **more of each
room horizontally**, with **no pixel stretching**. Width is configurable, so 32:9
super-ultrawide works too.

> Status: **v0.1 — experiment, superseded.** Builds and installs cleanly and *does*
> widen the viewport — but investigation revealed a hard engine ceiling that makes a
> clean from-scratch 21:9 impractical. **Recommended path is now SBCameraScroll** (see
> below). This repo is kept as the engine write-up + a working resolution-override
> reference. The DLL is no longer installed into the game.

## ⚠️ The hard finding: rooms are baked at 1400×800 per camera position

Rain World does not render rooms live. Each **camera position** has its own pre-baked
**1400×800 PNG** (`levelTexture => persistentData.cameraTextures[…]`). Verified on disk:
**all 1,729 room images are exactly 1400×800, no exceptions.** The `1400` in
`hDisplace = (1400 - sSize.x)/2` *is* that width. Consequences:

- The stock 1366 viewport shows a 1366-window of the 1400 image (17px margin each side).
- **There is no baked art beyond 1400px at any single camera position.** Widening to a
  true 21:9 1792px needs 392px of art that does not exist in the files — so the extra
  columns show void / unintended texture edges.
- `CamPos` is the *bottom-left* corner of the intended view (center = `CamPos+(700,533)`),
  which is why naive widening (this mod, v0.1) only extends rightward.

Real extra horizontal content can only come from **stitching the neighbouring camera
positions' images** — which is a whole subsystem, and a mature mod already does it:

## ✅ Recommended: SBCameraScroll (+ its custom-resolution / dynamic-zoom)

[SBCameraScroll](https://github.com/SchuhBaum/SBCameraScroll) (SchuhBaum) stitches a
room's per-position 1400×800 images into one composite, supports an **arbitrary custom
resolution** (incl. non-768 heights, via its `FScreenMod`), and has a **Dynamic Zoom**
option whose own description is *"the camera zoom is adjusted dynamically per room. This
removes any black borders when using custom resolutions."* — i.e. the adaptive
aspect-ratio behaviour, already built and tested. It is a superset of this mod, so the
two must not run together (both overwrite `Options.screenResolutions`).

Setup (in-game **Options → Remix**, after enabling the mod): Resolution → `Custom`,
Custom Resolution → your monitor's native ultrawide (e.g. `3440x1440`), Dynamic Zoom → on.

The original design notes for *this* mod's hooks follow, kept for reference.

## Why this is possible (and not stretching)

Rain World is a Unity game built on the **Futile** 2D framework. Contrary to the
common belief that it's hard-locked to 16:9, its renderer is actually
**"fixed 768px height, arbitrary width"**:

- Every stock resolution (`Options.screenResolutions`) is `N x 768` — only the width
  varies (`1024, 1280, 1360, 1366`, and the Steam Deck's `1229`).
- `RainWorld.screenSize => Options.ScreenSize => screenResolutions[resolution]`.
- `Futile.UpdateScreenWidth(w)` reallocates the render target at `w x 768`; the
  orthographic size is derived from the fixed 768 height.

The devs simply never exposed a wide preset. The only genuinely fixed-size internals
are the **1400x800** effect buffers (`lightmap`, snow) and the camera's horizontal
centring (`hDisplace`) inside `RoomCamera`. True 21:9 at 768px tall is
`768 * 21/9 = 1792` wide, which exceeds 1400 — so the mod widens that internal space
to match.

Because the height stays 768 and the width grows to the exact monitor aspect, the GPU
scales the frame uniformly: **more field of view, zero stretch.**

## How it works

Three small, surgical hooks (`src/Ultrawide219/`):

| Hook | Target | Effect |
|---|---|---|
| Harmony getter postfix | `Options.ScreenSize` | Reports `1792x768` to the whole pipeline (window, Futile, camera `sSize`, shaders). Does **not** touch the saved resolution index, so uninstalling is safe. |
| MonoMod IL hook | `RoomCamera..ctor` | Rewrites the `1400` width literal of the three `new RenderTexture(1400, 800, …)` effect buffers to the wider internal width. |
| Harmony getter postfix | `RoomCamera.hDisplace` | Recomputes the viewport-centring offset for the widened buffer. |
| Plugin `Update()` one-shot | `Futile` / `Screen` | Pushes the wide width into Futile's render target + the OS window once the game is live. |

## Build

Requires the .NET SDK (tested with 10) and a local Rain World install with BepInEx
(ships with the game since the Downpour/Remix update).

```bash
dotnet build src/Ultrawide219/Ultrawide219.csproj -c Release
```

The install path defaults to `/mnt/hdd/SteamLibrary/steamapps/common/Rain World`.
Override with `RAINWORLD_DIR` (env) or `-p:RainWorldDir=...` (MSBuild).
.NET Framework reference assemblies are pulled from NuGet, so it builds on Linux.

## Install

```bash
scripts/deploy.sh    # build + install as a Remix mod under StreamingAssets/mods/ultrawide219/
```

Then launch the game and check `BepInEx/LogOutput.log` for the `Ultrawide 21:9` lines
(it logs the IL-patch hit count and the applied resolution).

### Two Rain World / Linux gotchas (both required)

1. **BepInEx injection under Proton.** Rain World is a Windows `.exe` run through
   Proton, and BepInEx hooks in via a `winhttp.dll` proxy that Wine ignores by default.
   Set the Steam **Launch Options** to:
   ```
   WINEDLLOVERRIDES="winhttp=n,b" %command%
   ```
   Without this, BepInEx never loads and there is no `BepInEx/LogOutput.log` at all.

2. **Plugins must be Remix mods, not `BepInEx/plugins/`.** Rain World's
   `MultiFolderLoader` patcher *quarantines* anything in `BepInEx/plugins` (it moves
   non-whitelisted files to `BepInEx/backup` on every launch) and only loads plugins
   from `StreamingAssets/mods/<id>/plugins/`. `deploy.sh` installs there. With no
   `enabledMods.txt` present, all mod folders load automatically; if you start managing
   mods in the in-game **Remix** menu, keep *Ultrawide 21:9* enabled there.

## Configure

After the first run, edit `BepInEx/config/kai.ultrawide219.cfg`:

- `Enabled` — master switch (off = stock behaviour).
- `Width` — internal render width at 768px tall. `1792` = 21:9, `2560` ≈ 32:9,
  `1366` = stock 16:9. Rule of thumb: `width = round(768 * your_aspect_ratio)`.

## Caveats

These are inherent to widening a screen-based game and are the focus of the next pass:

- **Room edges / small rooms** may reveal slightly past their intended bounds, since
  some rooms are barely wider than one screen.
- **Menu backgrounds** are authored at 16:9 and may show gaps at the sides
  (gameplay is unaffected).
- **Lightmap / snow coverage** at the new width is wired up but needs in-game
  verification across lit rooms.
- **Fullscreen**: on a native-3440-wide monitor the game runs `1792x768` scaled to
  fill — same aspect, so no stretch. Borderless/windowed also works.

## References

- `scratchpad/decomp/` (gitignored): decompiled `RoomCamera`, `FScreen`, `Futile`,
  `Options`, `RainWorld` that this mod was built against (ILSpy / `ilspycmd`).
- [Sharpener](https://github.com/PJB3005/RainWorldMods/tree/master/Sharpener) — the
  reference mod for decoupling Unity's resolution from the game's internal render.
