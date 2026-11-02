
# Simple test: launch app, wait for UI, check database
# This script opens the app, gives it time to load the profile, 
# then checks if the database exists and contains thumbnail data

param(
    [string]$AppExe = "G:\GestioneFoto\bin\GestioneFoto.App.exe",
    [string]$ProfilePath = "G:\GestioneFoto\test-archives\test.profile",
    [string]$DbPath = "G:\GestioneFoto\test-archives\test-scan.db"
)

Write-Host "=== Manual Test Instructions ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "The app will now open with the test profile loaded."
Write-Host "To complete the test:"
Write-Host "  1. Click 'Scansione' button to start the scan"
Write-Host "  2. Wait for the scan to complete"
Write-Host "  3. Close the app (it will save the database)"
Write-Host "  4. This script will then verify the results"
Write-Host ""

# Delete old database
if (Test-Path $DbPath) {
    Remove-Item $DbPath
    Write-Host "Deleted old database"
}

# Launch app
Write-Host "Launching app with profile: $ProfilePath"
Write-Host ""

$process = Start-Process -FilePath $AppExe -ArgumentList "--profile=$ProfilePath" -PassThru

# Wait for app to close
$process.WaitForExit()

# Brief pause to ensure database is flushed
Start-Sleep -Seconds 1

# Verify database
Write-Host ""
Write-Host "=== Verification ===" -ForegroundColor Cyan

if (Test-Path $DbPath) {
    Write-Host "Database created: $DbPath"
    $dbSize = (Get-Item $DbPath).Length
    Write-Host "Database size: $dbSize bytes"
    Write-Host ""
    
    # Read and display database contents using sqlite3 if available
    $sqlite3 = "$env:ProgramFiles\Git\usr\bin\sqlite3.exe"
    if (Test-Path $sqlite3) {
        Write-Host "Querying database..."
        Write-Host ""
        
        # Get record count
        $count = & $sqlite3 $DbPath "SELECT COUNT(*) FROM SRC"
        Write-Host "Total records: $count"
        
        # Get thumbnail count
        $thumbCount = & $sqlite3 $DbPath "SELECT COUNT(*) FROM SRC WHERE THUMBNAIL IS NOT NULL"
        Write-Host "Records with thumbnail: $thumbCount"
        
        Write-Host ""
        Write-Host "File listing:"
        & $sqlite3 $DbPath "SELECT FILE, LENGTH(HASH) as HashLen FROM SRC LIMIT 10" | ForEach-Object {
            Write-Host "  $_"
        }
    } else {
        Write-Host "Note: sqlite3.exe not found. Install Git for Windows to use sqlite3 CLI."
        Write-Host "You can also open the database manually with SQLite Browser:"
        Write-Host "  Path: $DbPath"
    }
} else {
    Write-Host "ERROR: Database not created!" -ForegroundColor Red
    Write-Host "Check that:"
    Write-Host "  1. You clicked 'Scansione' in the app"
    Write-Host "  2. The scan completed without errors"
    Write-Host "  3. The app was closed properly"
}
