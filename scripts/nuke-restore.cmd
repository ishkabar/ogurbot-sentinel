@echo off
echo 💣 NUCLEAR RESTORE - This will take a while...

echo 🧹 Clearing NuGet caches...
dotnet nuget locals all --clear

echo 🧹 Removing bin/obj folders...
for /d /r . %%d in (bin,obj) do @if exist "%%d" rd /s /q "%%d"

echo 🧹 Running dotnet clean...
dotnet clean

echo 📦 Restoring packages...
dotnet restore --no-cache --force

if %errorlevel% neq 0 exit /b %errorlevel%

echo ✅ Nuclear restore complete!