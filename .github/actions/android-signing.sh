#!/usr/bin/env bash
# Prepares the Android signing keystore.
#
# Falls back to a temporary throwaway keystore when no signing secrets are
# configured, so that forks without secrets can still produce a build.
#
# Environment (set by the workflow):
#   ANDROID_KEYSTORE_BASE64      base64 encoded PKCS12/JKS keystore
#   ANDROID_KEYSTORE_PASSWORD    keystore password
#   ANDROID_KEY_ALIAS            key alias
#   ANDROID_KEY_PASSWORD         key password
set -euo pipefail

echo "Setting up Android keystore for signing..."

if [[ -n "${ANDROID_KEYSTORE_BASE64:-}" ]]; then
  echo "Using keystore from GitHub Secrets"
  echo "$ANDROID_KEYSTORE_BASE64" | base64 -d > osu.keystore

  if [[ ! -f "osu.keystore" ]] || [[ ! -s "osu.keystore" ]]; then
    echo "ERROR: Failed to decode keystore from base64"
    exit 1
  fi

  if [[ -z "${ANDROID_KEYSTORE_PASSWORD:-}" ]] || [[ -z "${ANDROID_KEY_ALIAS:-}" ]] || [[ -z "${ANDROID_KEY_PASSWORD:-}" ]]; then
    echo "ERROR: Android signing secrets are incomplete!"
    echo "Required secrets: ANDROID_KEYSTORE_BASE64, ANDROID_KEYSTORE_PASSWORD, ANDROID_KEY_ALIAS, ANDROID_KEY_PASSWORD"
    exit 1
  fi

  echo "Verifying keystore integrity..."
  keytool -list -keystore osu.keystore -storepass "$ANDROID_KEYSTORE_PASSWORD" -alias "$ANDROID_KEY_ALIAS" || {
    echo "ERROR: Cannot access keystore with provided credentials"
    exit 1
  }

  echo "Keystore setup successful"
else
  echo "No keystore found in secrets, creating temporary one..."
  keytool -genkey -v \
    -keystore osu.keystore \
    -keyalg RSA \
    -keysize 2048 \
    -validity 10000 \
    -alias osu \
    -storepass temp123 \
    -keypass temp123 \
    -dname "CN=g0v0-temp,OU=g0v0,O=g0v0,L=Unknown,S=Unknown,C=Unknown"
  echo "WARNING: Using temporary keystore! Please configure ANDROID_KEYSTORE_BASE64 secret for production builds."
fi
