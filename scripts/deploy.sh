#!/usr/bin/env bash
# Build the plugin and install it into the Rain World BepInEx plugins folder.
#
# Usage:
#   scripts/deploy.sh                 # build Release + copy to BepInEx/plugins
#   RAINWORLD_DIR="/path" scripts/deploy.sh
#   scripts/deploy.sh --remix         # also stage a Remix mod folder under StreamingAssets/mods
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RAINWORLD_DIR="${RAINWORLD_DIR:-/mnt/hdd/SteamLibrary/steamapps/common/Rain World}"
PROJECT="$REPO_ROOT/src/Ultrawide219/Ultrawide219.csproj"
ASSEMBLY="Ultrawide219.dll"
DLL="$REPO_ROOT/src/Ultrawide219/bin/Release/$ASSEMBLY"

if [[ ! -d "$RAINWORLD_DIR" ]]; then
  echo "error: Rain World dir not found: $RAINWORLD_DIR" >&2
  echo "       set RAINWORLD_DIR to override." >&2
  exit 1
fi

echo "==> building (Release)"
dotnet build "$PROJECT" -c Release -v minimal -p:RainWorldDir="$RAINWORLD_DIR"

PLUGINS_DIR="$RAINWORLD_DIR/BepInEx/plugins"
echo "==> installing to $PLUGINS_DIR"
mkdir -p "$PLUGINS_DIR"
cp -v "$DLL" "$PLUGINS_DIR/$ASSEMBLY"

if [[ "${1:-}" == "--remix" ]]; then
  MOD_DIR="$RAINWORLD_DIR/RainWorld_Data/StreamingAssets/mods/ultrawide219"
  echo "==> staging Remix mod at $MOD_DIR"
  mkdir -p "$MOD_DIR/plugins"
  cp -v "$REPO_ROOT/mod/modinfo.json" "$MOD_DIR/modinfo.json"
  cp -v "$DLL" "$MOD_DIR/plugins/$ASSEMBLY"
  echo "    (enable 'Ultrawide 21:9' in the in-game Remix menu; remove the BepInEx/plugins copy to avoid double-load)"
fi

echo "==> done. Launch Rain World; check BepInEx/LogOutput.log for 'Ultrawide 21:9'."
