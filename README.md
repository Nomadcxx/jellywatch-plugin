# JellyWatch Jellyfin Plugin

Companion plugin for JellyWatch media organizer that provides custom API endpoints and event forwarding for tighter integration.

## Features

- **Custom API Endpoints**: Query Jellyfin metadata directly from JellyWatch
- **Event Forwarding**: Rich webhooks for library changes and playback events
- **Scan Control**: Trigger library scans from JellyWatch
- **Playback Safety**: Check if files are being streamed before organizing
- **Unidentifiable Detection**: Find items Jellyfin couldn't identify

## Installation

### From Source

```bash
# Clone the repository
git clone https://github.com/Nomadcxx/jellywatch-plugin.git
cd jellywatch-plugin

# Build
./build.sh

# Install
./install.sh

# Restart Jellyfin
sudo systemctl restart jellyfin
```

### Manual Installation

1. Download the latest release from GitHub
2. Extract to Jellyfin plugins directory: `~/.local/share/jellyfin/plugins/JellyWatch/`
3. Restart Jellyfin

## Configuration

1. Go to Jellyfin Dashboard → Plugins → JellyWatch
2. Set JellyWatch URL (e.g., `http://localhost:3000`)
3. Set Shared Secret (must match JellyWatch config)
4. Enable desired event forwarding options

## API Endpoints

Phase 3 contract endpoints are exposed under `/jellywatch`:

- `GET /jellywatch/item-by-path?path=...` - Get item metadata by full file path
- `POST /jellywatch/refresh-path` - Trigger a targeted path refresh
- `POST /jellywatch/pause-scanning` - Pause plugin-triggered scans for a bounded duration
- `POST /jellywatch/resume-scanning` - Resume plugin-triggered scans immediately
- `GET /jellywatch/identification/{itemId}` - Fetch provider ID match details for a specific item
- `GET /jellywatch/status` - Plugin status and scan pause state

Additional diagnostics/operational endpoints currently exposed:

- `GET /jellywatch/health` - Plugin health check (anonymous)
- `GET /jellywatch/library-by-path?path=...` - Resolve library details for a path
- `GET /jellywatch/active-scans` - List active library scan tasks
- `POST /jellywatch/scan-library` - Trigger library scan by `libraryId` or `path`
- `GET /jellywatch/unidentifiable?libraryId=...` - List unidentified items (optional library filter)
- `GET /jellywatch/active-playback` - List active playback sessions

## Webhook Events

Events are forwarded to `POST /api/v1/webhooks/jellyfin`:

- `ItemAdded` - New item added to library
- `ItemRemoved` - Item removed from library
- `ItemUpdated` - Item metadata updated
- `PlaybackStart` - Playback started
- `PlaybackStopped` - Playback stopped
- `PlaybackProgress` - Playback progress (throttled)

## Development

```bash
# Build
dotnet build -c Release

# Test
dotnet test
```

## License

MIT License - See LICENSE file
