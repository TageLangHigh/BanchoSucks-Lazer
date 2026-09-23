#!/usr/bin/env bash
# Converts the Windows .ico shipped in the repository into a macOS .icns used for
# packaging. Kept non-fatal on purpose: packaging falls back to the .ico.
set -euo pipefail

if [ ! -f "osu.Desktop/lazer.ico" ]; then
  echo "Warning: lazer.ico not found, creating placeholder icns"
  touch osu.Desktop/lazer.icns
  exit 0
fi

echo "Converting lazer.ico to lazer.icns for macOS..."

if ! sips -s format png osu.Desktop/lazer.ico --out temp_icon.png; then
  echo "Warning: Failed to convert ico to png, using .ico as fallback"
  cp osu.Desktop/lazer.ico osu.Desktop/lazer.icns
  exit 0
fi

mkdir -p lazer.iconset
sips -z 16 16     temp_icon.png --out lazer.iconset/icon_16x16.png
sips -z 32 32     temp_icon.png --out lazer.iconset/icon_16x16@2x.png
sips -z 32 32     temp_icon.png --out lazer.iconset/icon_32x32.png
sips -z 64 64     temp_icon.png --out lazer.iconset/icon_32x32@2x.png
sips -z 128 128   temp_icon.png --out lazer.iconset/icon_128x128.png
sips -z 256 256   temp_icon.png --out lazer.iconset/icon_128x128@2x.png
sips -z 256 256   temp_icon.png --out lazer.iconset/icon_256x256.png
sips -z 512 512   temp_icon.png --out lazer.iconset/icon_256x256@2x.png
sips -z 512 512   temp_icon.png --out lazer.iconset/icon_512x512.png
cp lazer.iconset/icon_512x512.png lazer.iconset/icon_512x512@2x.png

if ! iconutil -c icns lazer.iconset; then
  echo "Warning: iconutil failed, using .ico as fallback"
  cp osu.Desktop/lazer.ico osu.Desktop/lazer.icns
  rm -f temp_icon.png
  rm -rf lazer.iconset
  exit 0
fi

cp lazer.icns osu.Desktop/lazer.icns
rm -f temp_icon.png
rm -rf lazer.iconset
echo "Successfully created lazer.icns"
