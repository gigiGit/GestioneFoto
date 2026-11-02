
# Database Upgrade Script for GestioneFoto
# Usage: .\upgrade-database.ps1 -DbPath "path\to\database.db"

param(
    [Parameter(Mandatory=$false)]
    [string]$DbPath = "$PSScriptRoot\..\test-archives\test-scan.db",
    
    [switch]$BuildTool
)

Write-Host "=== GestioneFoto Database Upgrade ===" -ForegroundColor Cyan
Write-Host ""

# Build tool if requested or if it doesn't exist
$toolPath = "$PSScriptRoot\..\DatabaseUpgradeTool\bin\Release\net8.0-windows\DatabaseUpgradeTool.exe"
$toolSrcPath = "$PSScriptRoot\..\DatabaseUpgradeTool"

if ($BuildTool -or -not (Test-Path $toolPath)) {
    Write-Host "Building DatabaseUpgradeTool..."
    Push-Location $toolSrcPath
    dotnet build -c Release | Out-Null
    Pop-Location
    
    if (-not (Test-Path $toolPath)) {
        Write-Host "ERROR: Failed to build tool" -ForegroundColor Red
        exit 1
    }
    Write-Host "✓ Tool built successfully`n" -ForegroundColor Green
}

# Resolve database path
if (-not ([System.IO.Path]::IsPathRooted($DbPath))) {
    $DbPath = Join-Path $PSScriptRoot $DbPath
}

Write-Host "Database: $DbPath"

if (-not (Test-Path $DbPath)) {
    Write-Host "ERROR: Database not found at $DbPath" -ForegroundColor Red
    exit 1
}

$dbSize = (Get-Item $DbPath).Length
Write-Host "Size: $([Math]::Round($dbSize / 1MB, 2)) MB`n" -ForegroundColor Gray

# Run upgrade tool
Write-Host "Running upgrade..." -ForegroundColor Cyan
& $toolPath $DbPath

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n✓ Database successfully upgraded!" -ForegroundColor Green
    Write-Host "`nYou can now use this database with the latest version of GestioneFoto.`n" -ForegroundColor Green
} else {
    Write-Host "`n✗ Upgrade failed" -ForegroundColor Red
    exit 1
}
