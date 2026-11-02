
# Test script for archive scanning functionality
# Usage: .\test-archives.ps1

param(
    [string]$AppPath = "G:\GestioneFoto\bin",
    [string]$TestDir = "G:\GestioneFoto\test-archives",
    [string]$ProfilePath = "$TestDir\test.profile",
    [string]$ArchiveDir = "$TestDir\archives",
    [string]$DbPath = "$TestDir\test-scan.db"
)

Write-Host "=== Archive Scan Test ===" -ForegroundColor Cyan
Write-Host ""

# Load assemblies
Add-Type -Path "$AppPath\GestioneFoto.App.dll"

# Verify test files exist
Write-Host "Checking prerequisites..."
$archives = @(
    "$ArchiveDir\archive1.zip",
    "$ArchiveDir\archive2.tar.gz",
    "$ArchiveDir\archive3.tgz"
)

foreach ($archive in $archives) {
    if (Test-Path $archive) {
        $size = (Get-Item $archive).Length
        Write-Host "  [OK] $(Split-Path $archive -Leaf) ($size bytes)"
    } else {
        Write-Host "  [FAIL] $(Split-Path $archive -Leaf) NOT FOUND" -ForegroundColor Red
    }
}

Write-Host ""

# Delete old database if exists
if (Test-Path $DbPath) {
    Remove-Item $DbPath
    Write-Host "Deleted old database"
}

# Load profile
Write-Host "Loading profile: $ProfilePath"
$profile = @{}
Get-Content $ProfilePath | ForEach-Object {
    $key, $value = $_ -split '='
    $profile[$key.Trim()] = $value.Trim()
}

Write-Host "  Profile: $($profile['ProfileName'])"
Write-Host "  Archives dir: $ArchiveDir"
Write-Host ""

# Perform scan
Write-Host "Starting scan..."
$writer = New-Object GestioneFoto.App.Data.HashDbWriter($DbPath, "test-app")
$scanner = New-Object GestioneFoto.App.Services.ScanCoordinator($writer)

$extensions = $profile['Extensions'].Split(',') | ForEach-Object { $_.Trim() }
$minSize = [long]$profile['MinSize']

$stats = $scanner.Scan(@($ArchiveDir), $extensions, $minSize)

Write-Host "Scan completed:"
Write-Host "  Files found: $($stats.FilesFound)"
Write-Host "  Files analyzed: $($stats.FilesAnalyzed)"
Write-Host "  Errors: $($stats.ErrorCount)"
Write-Host ""

# Verify database
Write-Host "Verifying database contents..."

if (Test-Path $DbPath) {
    $connStr = "Data Source=$DbPath"
    $conn = New-Object System.Data.SQLite.SQLiteConnection($connStr)
    $conn.Open()
    
    # Check columns
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "PRAGMA table_info(SRC)"
    $reader = $cmd.ExecuteReader()
    $columns = @()
    while ($reader.Read()) {
        $columns += $reader['name']
    }
    $reader.Close()
    
    Write-Host "  SRC table columns: $($columns -join ', ')"
    Write-Host "  Has THUMBNAIL column: $($columns -contains 'THUMBNAIL')"
    
    # Count records
    $cmd.CommandText = "SELECT COUNT(*) FROM SRC"
    $recordCount = $cmd.ExecuteScalar()
    Write-Host "  Total records: $recordCount"
    
    $cmd.CommandText = "SELECT COUNT(*) FROM SRC WHERE THUMBNAIL IS NOT NULL"
    $thumbnailCount = $cmd.ExecuteScalar()
    Write-Host "  Records with thumbnail: $thumbnailCount"
    
    # List files
    if ($recordCount -gt 0) {
        Write-Host ""
        Write-Host "  Files in database:"
        $cmd.CommandText = "SELECT FILE, LENGTH(HASH) as HashLen, CASE WHEN THUMBNAIL IS NOT NULL THEN 'Yes' ELSE 'No' END as HasThumbnail FROM SRC"
        $reader = $cmd.ExecuteReader()
        while ($reader.Read()) {
            $file = $reader['FILE']
            $hasThumbnail = $reader['HasThumbnail']
            Write-Host "    • $file (thumbnail: $hasThumbnail)"
        }
        $reader.Close()
    }
    
    $conn.Close()
} else {
    Write-Host "  ERROR: Database not created!" -ForegroundColor Red
}

Write-Host ""
Write-Host "Test completed" -ForegroundColor Green
