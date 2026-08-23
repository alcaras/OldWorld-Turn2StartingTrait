#!/bin/bash
# Build the mod and package a distributable zip in dist/.
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"
VERSION=$(grep -o "<modversion>[^<]*" "$HERE/mod/ModInfo.xml" | sed 's/<modversion>//')
STAGE="/tmp/owtraitmod-pkg"
ZIP="$HERE/dist/OwTraitMod-$VERSION.zip"

"$HERE/build.sh" > /dev/null
rm -rf "$STAGE"; mkdir -p "$STAGE/OwTraitMod/Infos" "$HERE/dist"
cp "$HERE/mod/ModInfo.xml" "$STAGE/OwTraitMod/"
cp "$HERE/mod/modpicture.png" "$STAGE/OwTraitMod/"
cp "$HERE/mod/Infos/"*.xml "$STAGE/OwTraitMod/Infos/"
cp /tmp/owtraitmod-build/OwTraitMod/bin/Debug/OwTraitMod.dll "$STAGE/OwTraitMod/"
cp "$HERE/INSTALL.txt" "$STAGE/"

rm -f "$ZIP"
(cd "$STAGE" && zip -qr "$ZIP" OwTraitMod INSTALL.txt -x '*.DS_Store')
echo "packaged -> $ZIP"
unzip -l "$ZIP"
