# Ogur.Sentinel

[![wakatime](https://wakatime.com/badge/github/ishkabar/ogurbot-sentinel.svg?style=flat-square)](https://wakatime.com/badge/github/ishkabar/ogurbot-sentinel)
![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?style=flat-square&logo=dotnet)
![C#](https://img.shields.io/badge/C%23-Latest-239120?style=flat-square&logo=csharp)
![Discord](https://img.shields.io/badge/Discord-Bot-5865F2?style=flat-square&logo=discord)
![Docker](https://img.shields.io/badge/Docker-Ready-2496ED?style=flat-square&logo=docker)

Centralized boss respawn timer management system with Discord voice alerts, web configuration panel, and real-time desktop client.

## Overview

**Ogur.Sentinel** is a multi-component system for tracking and broadcasting game boss respawn timers with Discord integration:
- **Worker (Discord Bot)**: Joins voice channels and plays audio alerts when bosses respawn
- **API Backend**: REST API for timer management and real-time synchronization
- **Web Panel**: ASP.NET Razor Pages for timer configuration and leave management
- **Desktop Client**: DevExpress WPF app for real-time timer display with color-coded warnings

## Features

### Discord Integration (Worker)
- **Voice Channel Joining**: Automatic connection to Discord voice channels
- **Audio Alerts**: Plays WAV files at respawn milestones (2h warning, 10m warning)
- **Slash Commands**: `/respawn`, `/leave`, `/break` for timer management
- **NetCord**: Modern Discord library with Gateway v10 support
- **Opus Audio**: Real-time voice streaming with Concentus encoder

### Web Panel (API)
- **Timer Configuration**: Set and manage boss respawn schedules
- **Leave System**: Track player absences for coordination
- **Authentication**: JWT-based user login with role management
- **Razor Pages**: Server-side rendered UI with Bootstrap 5
- **Redis Caching**: Optional distributed caching for token storage

### Desktop Client (DevExpress)
- **Real-time Sync**: Automatic timer updates at configurable intervals (5-300s)
- **Color-coded Warnings**: 
  - Green border - Safe time remaining
  - Orange border - Warning threshold reached (0-120 min)
  - Red border - Critical time remaining (0-60 min)
- **Time Offset**: Adjust displayed time relative to server (-180 to +180s)
- **Always on Top**: Pin window above other applications
- **MSI Installer**: Professional WiX-based installer with shortcuts

### Legacy Desktop (Archived - WPF)
Original WPF implementation, replaced by DevExpress version. Single-file executable with self-contained runtime.

## Tech Stack

### Worker (Ogur.Sentinel.Worker)
- **.NET 9** - C# Latest
- **NetCord** - Discord Gateway v10 client
- **NAudio** - Audio playback
- **Concentus** - Opus audio encoding for Discord voice
- **NLog** - Structured logging
- **Sodium.Core** - Libsodium bindings for voice encryption

### API (Ogur.Sentinel.Api)
- **.NET 9** - ASP.NET Core Web API
- **Razor Pages** - Server-side rendering
- **Redis** - Distributed token storage (optional)
- **NLog** - Structured logging
- **JWT** - Authentication tokens

### Desktop (Ogur.Sentinel.Devexpress)
- **.NET 8-windows** - WPF with DevExpress
- **DevExpress 23.2.14** - Professional UI components
- **Office2019Colorful Theme** - Modern UI styling
- **WiX Toolset** - MSI installer generation

### Core Libraries
- **Ogur.Sentinel.Core** - Business logic, time calculations, authentication
- **Ogur.Sentinel.Abstractions** - Shared interfaces and DTOs

## Solution Structure
```
Ogur.Sentinel.sln
├── src/
│   ├── Ogur.Sentinel.Worker/         # Discord bot (voice alerts)
│   ├── Ogur.Sentinel.Api/            # REST API + web panel
│   ├── Ogur.Sentinel.Core/           # Business logic
│   ├── Ogur.Sentinel.Abstractions/   # Shared interfaces
│   ├── Ogur.Sentinel.Devexpress/     # Desktop client (current)
│   └── Ogur.Sentinel.Desktop/        # Desktop client (archived WPF)
├── installer/                         # WiX MSI installer
├── scripts/                           # Build/run scripts
└── deploy/                            # Deployment configs
```

## Getting Started

### Prerequisites
- .NET 9 SDK
- Docker & Docker Compose (for deployment)
- Discord Bot Token (for Worker)
- Redis (optional, for API token storage)

### Local Development

**Run Worker (Discord Bot):**
```bash
cd src/Ogur.Sentinel.Worker
dotnet run
```

**Run API + Web Panel:**
```bash
cd src/Ogur.Sentinel.Api
dotnet run
# Access web panel at http://localhost:5000
```

**Run Desktop Client:**
```bash
cd src/Ogur.Sentinel.Devexpress
dotnet run
```

### Configuration

**Worker (`appsettings.json`):**
```json
{
  "Discord": {
    "Token": "YOUR_BOT_TOKEN",
    "GuildId": "YOUR_GUILD_ID",
    "VoiceChannelId": "YOUR_VOICE_CHANNEL_ID"
  },
  "ApiBaseUrl": "http://localhost:5000",
  "RespawnSettings": {
    "BossName": "Red Dragon",
    "RespawnInterval": "02:00:00"
  }
}
```

**API (`appsettings.json`):**
```json
{
  "Authentication": {
    "JwtSecretKey": "your-secret-key",
    "TokenExpiration": "24:00:00"
  },
  "Redis": {
    "Enabled": false,
    "ConnectionString": "localhost:6379"
  }
}
```

**Desktop (`%APPDATA%\Ogur.Sentinel.Desktop\settings.json`):**
```json
{
  "SyncInterval": 30,
  "TimeOffset": 0,
  "RedWarningMinutes": 10,
  "OrangeWarningMinutes": 30
}
```

## Docker Deployment
```bash
# Build and run
docker-compose up -d

# View logs
docker-compose logs -f worker api

# Stop services
docker-compose down
```

**Docker Compose Structure:**
- `ogur-sentinel-worker` - Discord bot
- `ogur-sentinel-api` - Web API + panel
- `redis` - Optional token storage

## Desktop Client Installation

### MSI Installer (Recommended)
1. Download `OgurSentinelSetup.msi` from releases
2. Run installer
3. Launch from Start Menu or Desktop shortcut

**Installer Features:**
- Installs to Program Files
- Creates Start Menu shortcuts
- Registers for easy uninstallation

### Portable Version
**Framework-dependent** (requires .NET 8 Runtime):
```bash
# Download Ogur.Sentinel.Devexpress-win-x64-*.zip
# Extract and run Ogur.Sentinel.Devexpress.exe
```

**Self-contained** (no dependencies):
```bash
# Download Ogur.Sentinel.Devexpress-selfcontained-*.zip
# Extract and run Ogur.Sentinel.Devexpress.exe
```

## Build Scripts

**Windows:**
```bash
scripts\build.cmd        # Build solution
scripts\clean.cmd        # Clean outputs
scripts\restore.cmd      # Restore NuGet packages
scripts\run.cmd          # Run API
```

**Linux/macOS:**
```bash
./scripts/build.sh       # Build solution
./scripts/clean.sh       # Clean outputs
./scripts/restore.sh     # Restore NuGet packages
./scripts/run.sh         # Run API
```

## Discord Bot Commands

### Respawn Management
```
/respawn set <boss> <time>    # Set next respawn time
/respawn list                  # Show all active timers
/respawn cancel <boss>         # Cancel timer
```

### Leave System
```
/leave add <user> <reason>     # Mark player as absent
/leave remove <user>           # Remove leave record
/leave list                    # Show all active leaves
```

### Admin
```
/break enable                  # Pause all timers
/break disable                 # Resume timers
```

## Web Panel Features

### Timer Management
- Add/edit/delete boss timers
- Set respawn intervals
- View countdown in real-time

### Leave Tracking
- Record player absences
- Add reason and duration
- Track return dates

### Authentication
- User registration
- Role-based access (Admin/User)
- JWT token management

## Desktop Client Features

### Real-time Display
- Multiple boss timers in single window
- Countdown in MM:SS format
- Next respawn time display

### Settings
- **Sync Interval**: 5-300 seconds (default: 30s)
- **Time Offset**: -180 to +180 seconds
- **Red Warning**: 0-60 minutes (critical)
- **Orange Warning**: 0-120 minutes (warning)

### Window Controls
- Pin on top
- Drag to move
- Minimize to taskbar
- Automatic login (stored credentials)

## Project Details

### [Ogur.Sentinel.Worker](./src/Ogur.Sentinel.Worker)
Discord bot with voice channel integration and audio alerts.

### [Ogur.Sentinel.Api](./src/Ogur.Sentinel.Api)
REST API and Razor Pages web panel for configuration.

### [Ogur.Sentinel.Core](./src/Ogur.Sentinel.Core)
Shared business logic, authentication, and time calculations.

### [Ogur.Sentinel.Abstractions](./src/Ogur.Sentinel.Abstractions)
Interfaces, DTOs, and contracts shared across projects.

### [Ogur.Sentinel.Devexpress](./src/Ogur.Sentinel.Devexpress)
DevExpress WPF desktop client (current).

### [Ogur.Sentinel.Desktop](./src/Ogur.Sentinel.Desktop) (Archived)
Original WPF desktop client (legacy).

## Troubleshooting

### Worker Issues
**Bot won't connect:**
- Verify Discord token is valid
- Check bot has proper permissions in guild
- Ensure Voice Channel ID is correct

**Audio not playing:**
- Verify bot is connected to voice channel
- Check audio files exist in `assets/` folder
- Ensure Opus encoder is installed

### API Issues
**Cannot start:**
- Check port 5000 is not in use
- Verify Redis is running (if enabled)
- Check NLog configuration

### Desktop Issues
**Timers not updating:**
- Verify API URL is correct
- Check sync interval setting
- Ensure credentials are valid

**Display problems:**
- Recommended display scaling: 100%
- Minimum resolution: 1920x1080
- Restart after changing settings

## License
Proprietary - All rights reserved © 2025 Dominik Karczewski (ogur.dev)

## Author
**Dominik Karczewski**
- Website: [ogur.dev](https://ogur.dev)
- GitHub: [@ishkabar](https://github.com/ishkabar)
