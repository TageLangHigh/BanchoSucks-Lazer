#!/usr/bin/env bash
# Renames the Velopack output of the macOS build to stable release asset names.
#
# Environment (set by the workflow):
#   RUNTIME             RID that was packaged, e.g. osx-x64 / osx-arm64
set -euo pipefail

shopt -s nullglob

if [[ "$RUNTIME" == "osx-x64" ]]; then
  for f in Releases/*osx-x64*.zip; do mv -f "$f" Releases/g0v0.app.Intel.zip; done

  for f in Releases/*.nupkg; do
    if [[ "$f" == *osx*full.nupkg ]] && [[ "$f" != *arm64* ]]; then
      base=$(basename "$f")
      mv -f "$f" "Releases/${base/osx-full.nupkg/osx-x64-full.nupkg}"
    fi
  done
else
  for f in Releases/*osx-arm64*.zip; do mv -f "$f" Releases/g0v0.app.Apple.Silicon.zip; done

  for f in Releases/*.nupkg; do
    if [[ "$f" == *osx*full.nupkg ]]; then
      base=$(basename "$f")
      if [[ "$base" != *osx-arm64-full.nupkg ]]; then
        mv -f "$f" "Releases/${base/osx-full.nupkg/osx-arm64-full.nupkg}"
      fi
    fi
  done
fi
