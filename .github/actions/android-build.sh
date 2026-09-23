#!/usr/bin/env bash
# Publishes the signed Android APK and collects it as a release asset.
#
# Environment (set by the workflow):
#   SEMVER                       release semver, e.g. 2026.910.0-g0v0
#   ANDROID_VERSION_CODE         numeric version code for the APK
#   ANDROID_KEYSTORE_PASSWORD    keystore password
#   ANDROID_KEY_ALIAS            key alias
#   ANDROID_KEY_PASSWORD         key password
#   SENTRY_DSN                   Sentry DSN injected into the build
#   ANDROID_SDK_ROOT             Android SDK location
set -euo pipefail

keystore_pass="${ANDROID_KEYSTORE_PASSWORD:-temp123}"
key_alias="${ANDROID_KEY_ALIAS:-osu}"
# PKCS12 keystores require key password == store password
key_pass="$keystore_pass"

echo "Using Java version: $(java -version 2>&1 | head -n 1)"
echo "Android SDK Root: $ANDROID_SDK_ROOT"
echo "Keystore file exists: $(test -f osu.keystore && echo 'Yes' || echo 'No')"
echo "Using key alias: $key_alias"

if [[ ! -f "osu.keystore" ]]; then
  echo "ERROR: Keystore file not found!"
  exit 1
fi

dotnet publish osu.Android/osu.Android.csproj \
  --configuration Release \
  --framework net10.0-android \
  --verbosity normal \
  --no-restore \
  --self-contained \
  -p:Version="$SEMVER" \
  -p:ApplicationVersion="$ANDROID_VERSION_CODE" \
  -p:ApplicationDisplayVersion="$SEMVER" \
  -p:SentryDsn="$SENTRY_DSN" \
  -p:AndroidSdkDirectory="$ANDROID_SDK_ROOT" \
  -p:AndroidKeyStore=true \
  -p:AndroidSigningKeyStore="$(pwd)/osu.keystore" \
  -p:AndroidSigningKeyAlias="$key_alias" \
  -p:AndroidSigningKeyPass="$key_pass" \
  -p:AndroidSigningStorePass="$keystore_pass" \
  -p:AndroidPackageFormat=apk

mkdir -p Releases

apk_file=$(find osu.Android/bin/Release/ -name "*-Signed.apk" | head -1)
if [[ -z "$apk_file" ]]; then
  echo "ERROR: No Signed APK file found after build!"
  exit 1
fi

echo "Found APK: $apk_file"
cp "$apk_file" Releases/g0v0.apk

if command -v aapt2 &> /dev/null; then
  echo "Verifying APK signature..."
  aapt2 dump badging Releases/g0v0.apk | grep -E "application-label|package" || true
fi
