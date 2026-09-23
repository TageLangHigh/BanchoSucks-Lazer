#!/usr/bin/env bash
# Creates the Windows Velopack installer for a single architecture.
#
# Environment (set by the workflow):
#   RUNTIME             RID that was published, e.g. win-x64 / win-arm64
#   VELOPACK_VERSION    three-part version required by Velopack
#   EXECUTABLE          published executable name
set -euo pipefail

channel="win"
if [[ "$RUNTIME" == "win-arm64" ]]; then
  channel="win-arm64"
fi

vpk pack \
  -u "g0v0" \
  -v "$VELOPACK_VERSION" \
  -p "publish/$RUNTIME" \
  -e "$EXECUTABLE" \
  --packAuthors "g0v0! GooGuTeam" \
  --packTitle "g0v0!" \
  -i "osu.Desktop/lazer.ico" \
  -c "$channel"
