#!/usr/bin/env bash
# Builds the iOS app and collects the IPA as a release asset.
#
# Environment (set by the workflow):
#   SEMVER              release semver, e.g. 2026.910.0-g0v0
#   FILE_VERSION        assembly file version
#   ASSEMBLY_VERSION    assembly version
#   INFO_VERSION        informational version
#   SENTRY_DSN          Sentry DSN injected into the build
set -euo pipefail

dotnet build osu.iOS/osu.iOS.csproj \
  --configuration Release \
  --runtime ios-arm64 \
  -p:BuildIpa=true \
  -p:CodesignKey="-" \
  --no-restore \
  -p:Version="$SEMVER" \
  -p:FileVersion="$FILE_VERSION" \
  -p:AssemblyVersion="$ASSEMBLY_VERSION" \
  -p:SentryDsn="$SENTRY_DSN" \
  -p:InformationalVersion="$INFO_VERSION"

mkdir -p Releases

ipa_file=$(find osu.iOS/bin/ -name "*.ipa" | head -1)
if [[ -z "$ipa_file" ]]; then
  echo "ERROR: No IPA file found after build!"
  exit 1
fi

echo "Found IPA: $ipa_file"
cp "$ipa_file" Releases/g0v0-ios.ipa
echo "Final artifact: Releases/g0v0-ios.ipa"
