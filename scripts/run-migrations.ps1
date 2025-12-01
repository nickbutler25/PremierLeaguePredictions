# Database Migration Script for Premier League Predictions
# This script should be run as part of the deployment process BEFORE starting the application

$ErrorActionPreference = "Stop"

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Running Database Migrations" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

# Get the script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = Join-Path $ScriptDir "..\backend\PremierLeaguePredictions.API"

# Check if connection string is provided
if (-not $env:ConnectionStrings__DefaultConnection) {
    Write-Host "Error: ConnectionStrings__DefaultConnection environment variable is not set" -ForegroundColor Red
    Write-Host "Please set the database connection string before running migrations" -ForegroundColor Yellow
    exit 1
}

Write-Host "Project directory: $ProjectDir"
Write-Host "Running EF Core migrations..."

Push-Location $ProjectDir

try {
    # Apply migrations using dotnet ef
    dotnet ef database update --no-build --verbose

    if ($LASTEXITCODE -eq 0) {
        Write-Host "=========================================" -ForegroundColor Green
        Write-Host "✓ Migrations completed successfully" -ForegroundColor Green
        Write-Host "=========================================" -ForegroundColor Green
        exit 0
    } else {
        throw "Migration command failed with exit code $LASTEXITCODE"
    }
}
catch {
    Write-Host "=========================================" -ForegroundColor Red
    Write-Host "✗ Migration failed: $_" -ForegroundColor Red
    Write-Host "=========================================" -ForegroundColor Red
    exit 1
}
finally {
    Pop-Location
}
