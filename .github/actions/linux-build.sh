#!/usr/bin/env bash
# Builds and publishes the Linux desktop release for a single architecture.
#
# Environment (set by the workflow):
#   RUNTIME             RID to publish for, e.g. linux-x64 / linux-arm64
#   SEMVER              release semver, e.g. 2026.910.0-g0v0
#   FILE_VERSION        assembly file version
#   ASSEMBLY_VERSION    assembly version
#   INFO_VERSION        informational version (used by Velopack)
#   SENTRY_DSN          Sentry DSN injected into the build
set -euo pipefail

echo "Building for runtime: $RUNTIME"

dotnet publish osu.Desktop/osu.Desktop.csproj \
  --self-contained \
  --runtime "$RUNTIME" \
  --configuration Release \
  --output "publish/$RUNTIME" \
  --no-restore \
  --verbosity normal \
  -p:PublishSingleFile=false \
  -p:PublishTrimmed=false \
  -p:Version="$SEMVER" \
  -p:FileVersion="$FILE_VERSION" \
  -p:AssemblyVersion="$ASSEMBLY_VERSION" \
  -p:SentryDsn="$SENTRY_DSN" \
  -p:InformationalVersion="$INFO_VERSION"

chmod +x "publish/$RUNTIME/g0v0!"
