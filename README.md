# Ultrawide 21:9 UI — Rain World

A companion mod that makes Rain World's **menus, HUD, and full-screen effects** behave
correctly at **21:9 and wider**. It pairs with
[**SBCameraScroll**](https://github.com/SchuhBaum/SBCameraScroll) — that mod widens the
*gameplay* camera (stitching the room's camera textures together); this mod fixes
everything *around* the gameplay so the whole experience is seamless on an ultrawide
display.

> **Status: v0.2.** Works as a companion to SBCameraScroll. Built and tested against
> Rain World **v1.11.x** (Downpour / Remix).

## What it does

Rain World's UI is authored for a **768px-tall** coordinate space and is already
width-aware almost everywhere (it reads `Options.ScreenSize`). Two things break it on a
real ultrawide:

1. Running at a **native tall resolution** (e.g. 1440) pushes the coordinate space off
   768, so everything that centres on `(…, 384)` ends up mis-placed.
2. A set of **uniform full-screen filters** (pause darken, fades, ghost/electric flashes,
   cutscene letterbox bars) are hard-coded to ~1366/1400px wide and don't reach the edges.

So this mod:

- **Renders at a 768-tall internal resolution at your chosen 21:9 width**, upscaled to your
  monitor — keeping the entire UI coordinate space valid (vanilla's own centering then just
  works) at authentic pixel-art scale.
- **Stretches the uniform full-screen filters** to span the full width (height left alone).
- **Fixes the few genuine wide-screen layout bugs** (`Custom.GetScreenOffsets` safe-area,
  `SlideShow` intro/outro/dream-scene centring).
- **Leaves hand-drawn art centred + pillarboxed** — black bars on fixed-size illustrations
  are correct and intentional.

It does **not** touch the gameplay camera — that's SBCameraScroll's job.

## Requirements

- **Rain World v1.11+** (Downpour / Remix — ships with BepInEx).
- **[SBCameraScroll](https://github.com/SchuhBaum/SBCameraScroll)** by SchuhBaum — provides
  the widened gameplay camera. Install it first
  ([Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=2928752589)).

## Installation

Following the usual Rain World (Remix) mod convention:

1. **Install SBCameraScroll** (Workshop or its
   [Releases](https://github.com/SchuhBaum/SBCameraScroll/releases)).
2. **Install this mod.** Download `ultrawide219-v*.zip` from the
   [latest Release](https://github.com/KaiErikNiermann/RainWorld-Ultrawide219/releases/latest)
   and extract the `ultrawide219/` folder into `RainWorld_Data/StreamingAssets/mods/`, so it
   looks like:
   ```
   Rain World/RainWorld_Data/StreamingAssets/mods/ultrawide219/
     ├─ modinfo.json
     └─ plugins/Ultrawide219.dll
   ```
   (Or build from source — see *Building* below; `scripts/deploy.sh` installs it for you.)
3. **Enable both mods** in-game: **Options → Remix**, tick *SBCameraScroll* and
   *Ultrawide 21:9 UI*, then restart when prompted.
4. **Configure** (see below).

### Linux / Steam Deck (Proton)

BepInEx loads through a `winhttp.dll` proxy that Proton ignores by default. Set the game's
Steam **Launch Options** to:

```
WINEDLLOVERRIDES="winhttp=n,b" %command%
```

Without it, no mods load at all (no `BepInEx/LogOutput.log` is produced).

## Configuration

**In SBCameraScroll's Remix options:** set **Resolution → `Default`** (let this mod own the
resolution) and turn **Dynamic Zoom → On** (or set Camera Zoom to taste) for the wide
stitched view.

**In this mod's config** (`BepInEx/config/kai.ultrawide219.cfg`, created on first launch):

| Setting | Values | Notes |
|---|---|---|
| `Preset` | `Off`, `TrueUltrawide_1792x768`, `Monitor_2560x1080`, `Monitor_3440x1440`, `SuperUltrawide_32x9`, `MatchDisplay` | Pick the one matching your monitor. Internally always 768px tall; only the width changes. |
| `StretchFilters` | `true`/`false` | Stretch full-screen darkens/fades/flashes to full width. |
| `MenuLayout` | `true`/`false` | Apply the menu safe-area / slideshow fixes. |

## Building

Requires the .NET SDK. Builds `net472` on Linux via NuGet reference assemblies.

```bash
# point at your install (default: /mnt/hdd/SteamLibrary/steamapps/common/Rain World)
RAINWORLD_DIR="/path/to/Rain World" dotnet build src/Ultrawide219/Ultrawide219.csproj -c Release
# or build + install in one step:
RAINWORLD_DIR="/path/to/Rain World" scripts/deploy.sh
```

The plugin is pure [Harmony](https://github.com/BepInEx/HarmonyX) + reflection (no
MonoMod/IL); it references the game and BepInEx assemblies directly from your install.

## How it works (engineering notes)

The interesting bit is *why* this is small: a scan of the decompiled engine showed the UI
is already centred via `Menu.Init()` + `Options.ScreenSize`, so the only real work is
keeping the render **768-tall** and stretching a handful of fixed-width fills. The
full-screen fixer is **magic-value-guarded** — it only rescales sprites stuck at the known
broken constants (`scaleX ≈ 87.5` for `Futile_White`, `≈1366–1500` for `pixel`), so it
never disturbs correctly-sized art. See the commit history for the investigation (including
why the room art's baked **1400×800** per-camera images mean true wide *gameplay* needs
SBCameraScroll's stitching, not just a wider viewport).

## Credits

- **[SBCameraScroll](https://github.com/SchuhBaum/SBCameraScroll)** by **SchuhBaum**
  (MIT-licensed) — the wide gameplay camera this mod is built to complement. This project
  does not include or modify SBCameraScroll's code; it is an independent companion.
- Rain World by Videocult / Akupara Games.

## License

[MIT](LICENSE) © 2026 Kai Niermann (KaiErikNiermann).
