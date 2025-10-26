@echo off
setlocal

set PROJECT=%1

if "%PROJECT%"=="" (
    echo Usage: run.cmd [api^|worker^|desktop]
    exit /b 1
)

if "%PROJECT%"=="api" (
    echo 🚀 Running API...
    dotnet run --project src\Ogur.Sentinel.Api
    goto :end
)

if "%PROJECT%"=="worker" (
    echo 🚀 Running Worker...
    dotnet run --project src\Ogur.Sentinel.Worker
    goto :end
)

if "%PROJECT%"=="desktop" (
    echo 🚀 Running Desktop...
    dotnet run --project src\Ogur.Sentinel.Desktop
    goto :end
)

echo Unknown project: %PROJECT%
echo Available: api, worker, desktop
exit /b 1

:end