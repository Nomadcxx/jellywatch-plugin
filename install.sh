#!/bin/bash
set -euo pipefail

PLUGIN_DIR="${1:-$HOME/.local/share/jellyfin/plugins/JellyWatch}"
JELLYFIN_SERVICE="${JELLYFIN_SERVICE:-jellyfin}"

echo "Installing JellyWatch Plugin..."
echo "Target directory: $PLUGIN_DIR"

# Create plugin directory
mkdir -p "$PLUGIN_DIR"

# Find the latest artifact
ARTIFACT=$(ls -t artifacts/jellywatch_*.zip 2>/dev/null | head -1)
if [ -z "$ARTIFACT" ]; then
    echo "Error: No artifact found. Run ./build.sh first."
    exit 1
fi

echo "Installing from: $ARTIFACT"

# Extract to plugin directory
unzip -o "$ARTIFACT" -d "$PLUGIN_DIR"

# Copy manifest if not in zip
if [ -f "JellyWatch.Plugin/manifest.json" ]; then
    cp "JellyWatch.Plugin/manifest.json" "$PLUGIN_DIR/"
fi

echo ""
echo "Installation complete!"
echo ""
echo "Next steps:"
echo "  1. Restart Jellyfin: sudo systemctl restart $JELLYFIN_SERVICE"
echo "  2. Go to Jellyfin Dashboard > Plugins > JellyWatch"
echo "  3. Configure JellyWatch URL and Shared Secret"
echo "  4. Enable event forwarding"
echo ""
echo "Plugin installed to: $PLUGIN_DIR"
