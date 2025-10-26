@echo off
cd /d "%~dp0\.."
echo 🔨 Building Ogur.Sentinel...
echo 📦 Restoring packages...
dotnet restore
if %errorlevel% neq 0 exit /b %errorlevel%
echo 🏗️ Building...
dotnet build --no-restore
if %errorlevel% neq 0 exit /b %errorlevel%
echo ✅ Build complete!