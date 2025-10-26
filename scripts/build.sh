#!/bin/bash
set -e

# CD do root projektu
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR/.."

echo "🔨 Building Ogur.Sentinel..."

# Restore
echo "📦 Restoring packages..."
dotnet restore

# Build all projects
echo "🏗️ Building..."
dotnet build --no-restore

echo "✅ Build complete!"