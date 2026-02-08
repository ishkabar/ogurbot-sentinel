# Ogur.Sentinel.Api

[![wakatime](https://wakatime.com/badge/github/ishkabar/ogurbot-sentinel.svg?style=flat-square)](https://wakatime.com/badge/github/ishkabar/ogurbot-sentinel)
![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?style=flat-square&logo=dotnet)
![ASP.NET](https://img.shields.io/badge/ASP.NET-Core-512BD4?style=flat-square)

REST API and Razor Pages web panel for boss respawn timer configuration and leave management.

## Features
- **Razor Pages UI**: Bootstrap 5 web interface
- **JWT Authentication**: Token-based user authentication
- **Timer Management**: CRUD operations for respawn timers
- **Leave System**: Track player absences
- **Redis Caching**: Optional distributed token storage
- **File Downloads**: Serve desktop client installers

## Structure
```
Ogur.Sentinel.Api/
├── Pages/
│   ├── Index.cshtml              # Dashboard
│   ├── Respawn.cshtml            # Timer management
│   ├── Login.cshtml              # Authentication
│   └── Download.cshtml           # Client downloads
├── Http/
│   └── ProxyEndpoints.cs         # API endpoints
├── Middleware/
│   └── AuthMiddleware.cs         # JWT validation
└── wwwroot/
    ├── css/                      # Stylesheets
    ├── js/                       # Client scripts
    └── files/                    # Downloadable files
```

## Dependencies
- `Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation` (9.0.0)
- `Microsoft.Extensions.Caching.StackExchangeRedis` (10.0.0-rc)
- `NLog.Web` (6.0.5)
