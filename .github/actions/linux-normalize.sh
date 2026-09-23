#!/usr/bin/env bash
# Renames the Velopack output of the Linux build to stable release asset names.
#
# Environment (set by the workflow):
#   RUNTIME             RID that was packaged, e.g. linux-x64 / linux-arm64
set -euo pipefail

shopt -s nullglob

if [[ "$RUNTIME" == "linux-arm64" ]]; then
  for f in Releases/*.AppImage; do
    if [[ "$f" != "Releases/g0v0-arm64.AppImage" ]]; then
      mv -f "$f" Releases/g0v0-arm64.AppImage
    fi
  done
else
  for f in Releases/*.AppImage; do
    if [[ "$f" != "Releases/g0v0.AppImage" ]]; then
      mv -f "$f" Releases/g0v0.AppImage
    fi
  done
fi
