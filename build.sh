#!/bin/bash
# Build OwTraitMod.dll and deploy the mod folder to the Old World Mods directory.
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"
MODS="$HOME/Library/Application Support/OldWorld/Mods"
DEST="$MODS/OwTraitMod"

echo "building..."
dotnet build "$HERE/OwTraitMod.csproj" -clp:NoSummary -nologo | grep -iE "error|warn|Build succeeded" || true

echo "deploying -> $DEST"
rm -rf "$DEST"; mkdir -p "$DEST/Infos"
cp "$HERE/mod/ModInfo.xml" "$DEST/"
cp "$HERE/mod/modpicture.png" "$DEST/"
cp "$HERE/mod/Infos/"*.xml "$DEST/Infos/"
cp /tmp/owtraitmod-build/OwTraitMod/bin/Debug/OwTraitMod.dll "$DEST/"
echo "done. Files:"; find "$DEST" -type f
echo
echo "Enable in-game: Mods -> 'Turn 2 Starting Trait' -> restart -> New Game with"
echo "Leader = Pick Later and Customize Leader OFF."
