# Ogur.Sentinel.Core

[![wakatime](https://wakatime.com/badge/github/ishkabar/ogurbot-sentinel.svg?style=flat-square)](https://wakatime.com/badge/github/ishkabar/ogurbot-sentinel)
![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?style=flat-square&logo=dotnet)

Shared business logic and utilities for Ogur.Sentinel.

## Components
- **Auth**: `UserStore`, `InMemoryTokenStore`, `RedisTokenStore`
- **Respawn**: `RespawnState`, `SchedulingMath` (timer calculations)
- **Time**: `PolishDateParser`, `TimeUtil` (timezone handling)
- **VersionHelper**: Build time and version metadata

## Dependencies
None - pure business logic with abstractions from `Ogur.Sentinel.Abstractions`.
