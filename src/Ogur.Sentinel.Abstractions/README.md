# Ogur.Sentinel.Abstractions

[![wakatime](https://wakatime.com/badge/github/ishkabar/ogurbot-sentinel.svg?style=flat-square)](https://wakatime.com/badge/github/ishkabar/ogurbot-sentinel)
![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?style=flat-square&logo=dotnet)

Shared interfaces and DTOs for Ogur.Sentinel projects.

## Interfaces
- `ITokenStore` - Token persistence contract
- `IVersionHelper` - Build metadata access

## Models
- **Auth**: `LoginRequest`, `TokenData`, `User`, `Role`
- **Leaves**: `LeaveRecord`
- **Respawn**: `PersistedSettings`
- **Options**: `RespawnOptions`, `SettingsOptions`

## Dependencies
None - zero external dependencies by design.
