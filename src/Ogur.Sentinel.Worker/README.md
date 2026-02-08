# Ogur.Sentinel.Worker

[![wakatime](https://wakatime.com/badge/github/ishkabar/ogurbot-sentinel.svg?style=flat-square)](https://wakatime.com/badge/github/ishkabar/ogurbot-sentinel)
![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?style=flat-square&logo=dotnet)
![Discord](https://img.shields.io/badge/Discord-NetCord-5865F2?style=flat-square&logo=discord)

Discord bot that joins voice channels and plays audio alerts when boss respawns occur.

## Features
- **Voice Channel Integration**: Automatic connection to Discord voice channels
- **Audio Alerts**: Plays WAV files at respawn milestones (2h warning, 10m final alert)
- **Slash Commands**: `/respawn`, `/leave`, `/break` modules
- **NetCord Gateway v10**: Modern Discord library
- **Opus Encoding**: Real-time voice streaming with Concentus
- **Background Workers**: RespawnWorker for timer scheduling

## Structure
```
Ogur.Sentinel.Worker/
├── Discord/
│   ├── DiscordBotHostedService.cs    # Bot lifecycle
│   ├── CommandRegistrationService.cs # Slash command setup
│   ├── Modules/
│   │   ├── RespawnModule.cs          # /respawn commands
│   │   ├── LeaveModule.cs            # /leave commands
│   │   └── AdminBreakModule.cs       # /break commands
│   └── Handlers/
│       └── VoiceServerUpdateHandler.cs
├── Services/
│   ├── DiscordVoiceClient.cs         # Voice connection
│   ├── RespawnSchedulerService.cs    # Timer scheduling
│   ├── LeaveService.cs               # Leave tracking
│   └── WikiSyncService.cs            # External wiki integration
├── RespawnWorker.cs                  # Background timer worker
└── assets/
    ├── respawn_2h.wav                # 2 hour warning audio
    └── respawn_10m.wav               # 10 minute warning audio
```

## Dependencies
- `NetCord` (1.0.0-alpha.439) - Discord Gateway client
- `NAudio` (2.2.1) - Audio playback
- `Concentus` (2.2.2) - Opus encoding
- `Sodium.Core` (1.4.0) - Voice encryption
