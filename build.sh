#!/usr/bin/env bash
set -euo pipefail

echo "Building JellyWatch Plugin..."

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$SCRIPT_DIR/JellyWatch.Plugin"
OUTPUT_DIR="$SCRIPT_DIR/artifacts"
PACKAGE_DIR="$OUTPUT_DIR/package"
TARGET_FRAMEWORK="net9.0"

pushd "$PROJECT_DIR" >/dev/null

dotnet restore --verbosity quiet
dotnet build -c Release --no-restore --verbosity quiet

PLUGIN_VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' JellyWatch.Plugin.csproj | head -n 1)"
if [[ -z "$PLUGIN_VERSION" ]]; then
    echo "Unable to determine plugin version from JellyWatch.Plugin.csproj" >&2
    exit 1
fi

PLUGIN_NAME="jellywatch_${PLUGIN_VERSION}.zip"
BUILD_DIR="$PROJECT_DIR/bin/Release/$TARGET_FRAMEWORK"

mkdir -p "$OUTPUT_DIR" "$PACKAGE_DIR"

# Ensure manifest is present in build output for packaging.
cp -f "$PROJECT_DIR/manifest.json" "$BUILD_DIR/manifest.json"

required_files=(
    "JellyWatch.Plugin.dll"
    "JellyWatch.Plugin.pdb"
    "manifest.json"
)

for file in "${required_files[@]}"; do
    if [[ ! -f "$BUILD_DIR/$file" ]]; then
        echo "Missing build artifact: $BUILD_DIR/$file" >&2
        exit 1
    fi
    cp -f "$BUILD_DIR/$file" "$PACKAGE_DIR/$file"
done

rm -f "$OUTPUT_DIR/$PLUGIN_NAME"
(
    cd "$PACKAGE_DIR"
    zip -r "$OUTPUT_DIR/$PLUGIN_NAME" \
        "JellyWatch.Plugin.dll" \
        "JellyWatch.Plugin.pdb" \
        "manifest.json"
)

popd >/dev/null

echo ""
echo "Build complete!"
echo "Package directory: $PACKAGE_DIR"
echo "Output: $OUTPUT_DIR/$PLUGIN_NAME"
echo ""
echo "Installation:"
echo "  1. Copy $PLUGIN_NAME to Jellyfin plugins directory:"
echo "     ~/.local/share/jellyfin/plugins/JellyWatch/"
echo "  2. Restart Jellyfin server"
echo "  3. Configure in Jellyfin dashboard > Plugins"
echo ""
echo "Or use the install script:"
echo "  ./install.sh ~/.local/share/jellyfin/plugins/"
