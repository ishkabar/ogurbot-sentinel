# Ogur.Sentinel.Desktop (Archived)

[![wakatime](https://wakatime.com/badge/github/ishkabar/ogurbot-sentinel.svg?style=flat-square)](https://wakatime.com/badge/github/ishkabar/ogurbot-sentinel)
![.NET](https://img.shields.io/badge/.NET-9.0--windows-512BD4?style=flat-square&logo=dotnet)
![WPF](https://img.shields.io/badge/WPF-Windows-512BD4?style=flat-square)
![Status](https://img.shields.io/badge/status-Archived-orange?style=flat-square)

Original WPF desktop client for boss timer display. Replaced by DevExpress version.

## Status
**Archived** - This project has been superseded by `Ogur.Sentinel.Devexpress` which provides:
- Professional DevExpress UI components
- Better performance and styling
- MSI installer with WiX Toolset
- Enhanced color theming

For current desktop client, see [Ogur.Sentinel.Devexpress](../Ogur.Sentinel.Devexpress).

## Legacy Features
- Pure WPF implementation (no third-party UI frameworks)
- Single-file self-contained executable
- Basic timer display with manual refresh
- Lightweight footprint

## Build (Legacy)
```bash
dotnet publish -c Release -r win-x64 --self-contained
```

Output: Single `.exe` file with embedded .NET runtime (self-contained).

## Why Archived?
Migrated to DevExpress for:
- Professional UI components and themes
- Better MVVM support with code generators
- Smoother animations and transitions
- Office2019 and VS2019Dark themes
- Improved maintainability

## Dependencies
- `System.Net.Http.Json` (8.0.1)
- WPF (built-in with .NET Windows SDK)

## Migration Notes
If you need to reference legacy code:
- Authentication logic moved to `Ogur.Sentinel.Core`
- UI patterns migrated to MVVM with DevExpress
- Settings persistence now uses `%APPDATA%` directory
