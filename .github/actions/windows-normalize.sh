#!/usr/bin/env bash
# Renames the Velopack output of the Windows build to stable release asset names.
#
# Environment (set by the workflow):
#   RUNTIME             RID that was packaged, e.g. win-x64 / win-arm64
set -euo pipefail

shopt -s nullglob

if [[ "$RUNTIME" == "win-arm64" ]]; then
  if [ -f "Releases/Setup.exe" ]; then
    mv -f Releases/Setup.exe Releases/g0v0-win-arm64-Setup.exe
  fi
else
  if [ -f "Releases/Setup.exe" ]; then
    mv -f Releases/Setup.exe Releases/g0v0-win-Setup.exe
  fi
fi
