@echo off
echo 📦 Restoring packages...
dotnet restore

if %errorlevel% neq 0 exit /b %errorlevel%

echo ✅ Restore complete!