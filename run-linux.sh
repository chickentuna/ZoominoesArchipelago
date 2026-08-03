#!/usr/bin/env bash
# Launch Zoominoes with BepInEx under Proton, outside of Steam.
#
# Three things are load-bearing and each one fails silently-ish if wrong:
#   - run-in-sniper: Proton Experimental needs the container runtime; invoking
#     ./proton directly just exits 1 with no error.
#   - absolute path to the .exe: a relative path inside the container gives
#     "err:steam:run_process Failed to create process: 2".
#   - WINEDLLOVERRIDES=winhttp=n,b: without it Wine loads its builtin winhttp
#     and doorstop never gets a chance to inject.
#
# Steam must be running (Steamworks.NET init).
set -euo pipefail

STEAM="$HOME/.steam/steam"
CLIENT="$HOME/.steam/debian-installation"
APPID=3282420
GAME="$STEAM/steamapps/common/Zoominos"
PROTON="$STEAM/steamapps/common/Proton - Experimental/proton"
SNIPER="$STEAM/steamapps/common/SteamLinuxRuntime_sniper/run-in-sniper"

cd "$GAME"

export STEAM_COMPAT_DATA_PATH="$STEAM/steamapps/compatdata/$APPID"
export STEAM_COMPAT_CLIENT_INSTALL_PATH="$CLIENT"
export SteamAppId=$APPID SteamGameId=$APPID
export WINEDLLOVERRIDES="winhttp=n,b"

# PROTON_LOG=1 PROTON_LOG_DIR=/tmp  # uncomment for a wine trace in /tmp/steam-$APPID.log
exec "$SNIPER" -- "$PROTON" waitforexitandrun "$GAME/Zoominoes.exe"
