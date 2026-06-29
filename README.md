# Rain World — Ultrawide 21:9

A BepInEx mod that renders Rain World at a **true 21:9** internal resolution
(`1792x768` by default) instead of the stock 16:9 presets. You see **more of each
room horizontally**, with **no pixel stretching**. Width is configurable, so 32:9
super-ultrawide works too.

> Status: **v0.1 — working scaffold.** Builds and installs cleanly. The render-path
> hooks are implemented against the decompiled v1.11.8 engine; in-game visual tuning
> (menu backgrounds, room-edge bleed) is the next pass — see [Caveats](#caveats).

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
scripts/deploy.sh            # build + copy to BepInEx/plugins/
scripts/deploy.sh --remix    # also stage a toggleable Remix mod under StreamingAssets/mods/
```

Then launch the game and check `BepInEx/LogOutput.log` for the `Ultrawide 21:9` lines
(it logs the IL-patch hit count and the applied resolution).

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
