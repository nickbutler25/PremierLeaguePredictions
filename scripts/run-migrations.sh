#!/bin/bash
# Database Migration Script for Premier League Predictions
# This script should be run as part of the deployment process BEFORE starting the application

set -e  # Exit on error

echo "========================================="
echo "Running Database Migrations"
echo "========================================="

# Get the script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_DIR="$SCRIPT_DIR/../backend/PremierLeaguePredictions.API"

# Check if connection string is provided
if [ -z "$ConnectionStrings__DefaultConnection" ]; then
    echo "Error: ConnectionStrings__DefaultConnection environment variable is not set"
    echo "Please set the database connection string before running migrations"
    exit 1
fi

echo "Project directory: $PROJECT_DIR"
echo "Running EF Core migrations..."

cd "$PROJECT_DIR"

# Apply migrations using dotnet ef
dotnet ef database update --no-build --verbose

if [ $? -eq 0 ]; then
    echo "========================================="
    echo "✓ Migrations completed successfully"
    echo "========================================="
    exit 0
else
    echo "========================================="
    echo "✗ Migration failed"
    echo "========================================="
    exit 1
fi
