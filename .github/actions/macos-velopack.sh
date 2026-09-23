#!/usr/bin/env bash
# Creates the macOS Velopack package for a single architecture.
#
# Environment (set by the workflow):
#   RUNTIME             RID that was published, e.g. osx-x64 / osx-arm64
#   VELOPACK_VERSION    three-part version required by Velopack
#   EXECUTABLE          published executable name
set -euo pipefail

icon_file="osu.Desktop/lazer.ico"
if [ -f "osu.Desktop/lazer.icns" ] && [ -s "osu.Desktop/lazer.icns" ]; then
  icon_file="osu.Desktop/lazer.icns"
  echo "Using lazer.icns for macOS packaging"
else
  echo "Using lazer.ico as fallback for macOS packaging"
fi

channel="osx-x64"
if [[ "$RUNTIME" == "osx-arm64" ]]; then
  channel="osx-arm64"
fi

vpk pack \
  -u "g0v0" \
  -v "$VELOPACK_VERSION" \
  -p "publish/$RUNTIME" \
  -e "$EXECUTABLE" \
  --packAuthors "g0v0! GooGuTeam" \
  --packTitle "g0v0!" \
  -i "$icon_file" \
  -c "$channel"
