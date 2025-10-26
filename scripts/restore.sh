#!/bin/bash
set -e

echo "📦 Restoring packages..."
dotnet restore

echo "✅ Restore complete!"