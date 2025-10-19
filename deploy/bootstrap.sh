#!/usr/bin/env bash
set -euo pipefail

# Simple first-deploy script for the VPS
# Usage: ./bootstrap.sh <github_owner>
OWNER="${1:-YOUR_GH_USERNAME}"

# Create networks if missing
docker network inspect proxy >/dev/null 2>&1 || docker network create proxy

# Replace OWNER placeholder in compose
sed -i "s|ghcr.io/OWNER/|ghcr.io/${OWNER}/|g" docker-compose.yml

# First run
cp -n .env.example .env || true
echo "Review and edit deploy/.env before continuing."