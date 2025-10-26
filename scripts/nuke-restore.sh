#!/bin/bash
set -e

echo "💣 NUCLEAR RESTORE - This will take a while..."

# Clear NuGet caches
echo "🧹 Clearing NuGet caches..."
dotnet nuget locals all --clear

# Remove all bin/obj
echo "🧹 Removing bin/obj folders..."
find . -type d \( -name "bin" -o -name "obj" \) -exec rm -rf {} + 2>/dev/null || true

# Clean
echo "🧹 Running dotnet clean..."
dotnet clean

# Restore
echo "📦 Restoring packages..."
dotnet restore --no-cache --force

echo "✅ Nuclear restore complete!"