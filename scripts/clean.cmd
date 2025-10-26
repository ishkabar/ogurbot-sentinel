@echo off
echo 🧹 Cleaning Ogur.Sentinel...

dotnet clean

echo Removing bin/obj folders...
for /d /r . %%d in (bin,obj) do @if exist "%%d" rd /s /q "%%d"

echo ✅ Clean complete!