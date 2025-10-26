#!/bin/bash
set -e

echo "🧹 Cleaning Ogur.Sentinel..."

# Clean build artifacts
dotnet clean

# Remove bin/obj folders
find . -type d \( -name "bin" -o -name "obj" \) -exec rm -rf {} + 2>/dev/null || true

echo "✅ Clean complete!"