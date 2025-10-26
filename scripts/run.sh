#!/bin/bash
set -e

PROJECT=$1

if [ -z "$PROJECT" ]; then
    echo "Usage: ./run.sh [api|worker|desktop]"
    exit 1
fi

case $PROJECT in
    api)
        echo "🚀 Running API..."
        dotnet run --project src/Ogur.Sentinel.Api
        ;;
    worker)
        echo "🚀 Running Worker..."
        dotnet run --project src/Ogur.Sentinel.Worker
        ;;
    desktop)
        echo "🚀 Running Desktop..."
        dotnet run --project src/Ogur.Sentinel.Desktop
        ;;
    *)
        echo "Unknown project: $PROJECT"
        echo "Available: api, worker, desktop"
        exit 1
        ;;
esac