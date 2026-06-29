#!/usr/bin/env bash
# Build the plugin and install it as a Rain World Remix mod.
#
# IMPORTANT: Rain World's MultiFolderLoader quarantines anything dropped in
# BepInEx/plugins (it moves non-whitelisted files to BepInEx/backup on every launch).
# Plugins MUST live in a Remix mod folder: StreamingAssets/mods/<id>/plugins/<dll>.
# With no enabledMods.txt present, every mod folder loads, so this "just works"
# without touching the enabled-mods list (which would otherwise disable your DLC).
#
# Usage:
#   scripts/deploy.sh
#   RAINWORLD_DIR="/path" scripts/deploy.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RAINWORLD_DIR="${RAINWORLD_DIR:-/mnt/hdd/SteamLibrary/steamapps/common/Rain World}"
PROJECT="$REPO_ROOT/src/Ultrawide219/Ultrawide219.csproj"
ASSEMBLY="Ultrawide219.dll"
DLL="$REPO_ROOT/src/Ultrawide219/bin/Release/$ASSEMBLY"
MOD_ID="ultrawide219"

if [[ ! -d "$RAINWORLD_DIR" ]]; then
  echo "error: Rain World dir not found: $RAINWORLD_DIR" >&2
  echo "       set RAINWORLD_DIR to override." >&2
  exit 1
fi

echo "==> building (Release)"
dotnet build "$PROJECT" -c Release -v minimal -p:RainWorldDir="$RAINWORLD_DIR"

# Clean up any earlier (wrong) BepInEx/plugins install + its quarantined copy.
for stale in \
  "$RAINWORLD_DIR/BepInEx/plugins/$ASSEMBLY" \
  "$RAINWORLD_DIR/BepInEx/backup/$ASSEMBLY"; do
  [[ -f "$stale" ]] && { echo "==> removing stale $stale"; rm -f "$stale"; }
done

MOD_DIR="$RAINWORLD_DIR/RainWorld_Data/StreamingAssets/mods/$MOD_ID"
echo "==> installing Remix mod at $MOD_DIR"
mkdir -p "$MOD_DIR/plugins"
cp -v "$REPO_ROOT/mod/modinfo.json" "$MOD_DIR/modinfo.json"
cp -v "$DLL" "$MOD_DIR/plugins/$ASSEMBLY"

echo "==> done. Launch Rain World; check BepInEx/LogOutput.log for 'Ultrawide 21:9'."
echo "    If you later toggle anything in the in-game Remix menu, make sure"
echo "    'Ultrawide 21:9' stays enabled there (that writes enabledMods.txt)."
