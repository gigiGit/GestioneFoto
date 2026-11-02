#!/usr/bin/env pwsh
<#
.SYNOPSIS
Script di test per la scansione degli archivi compressi.
#>

$ErrorActionPreference = 'Stop'

# Percorsi
$TestRoot = "$PSScriptRoot\test-archives"
$TestDb = "$TestRoot\test-scan.db"
$TestProfile = "$TestRoot\test.profile"
$ArchiveDir = "$TestRoot\archives"
$TempFiles = "$TestRoot\temp-files"
$AppExe = "$PSScriptRoot\bin\GestioneFoto.App.exe"

Write-Host "=== Test Scansione Archivi ===" -ForegroundColor Cyan

# Pulisci test precedenti
if (Test-Path $TestRoot) {
    Write-Host "Pulizia test precedenti..." -ForegroundColor Yellow
    Remove-Item $TestRoot -Recurse -Force -ErrorAction Ignore
}

# Crea struttura di test
Write-Host "Creazione struttura di test..." -ForegroundColor Yellow
New-Item -Path $ArchiveDir -ItemType Directory -Force | Out-Null
New-Item -Path $TempFiles -ItemType Directory -Force | Out-Null

# Crea file di test (JPG/PNG/MP4 minimalisti)
Write-Host "Creazione file di test..." -ForegroundColor Yellow

# Piccolo PNG (pixel rosso)
$pngBytes = @(
    137, 80, 78, 71, 13, 10, 26, 10,  # PNG signature
    0, 0, 0, 13,  # IHDR chunk size
    73, 72, 68, 82,  # "IHDR"
    0, 0, 0, 1, 0, 0, 0, 1,  # 1x1 image
    8, 2, 0, 0, 0,  # 8-bit RGB
    144, 119, 83, 222  # CRC
) + @(
    0, 0, 0, 12,  # IDAT chunk size
    73, 68, 65, 84,  # "IDAT"
    8, 29, 1, 3, 0, 252, 255, 255, 0, 0, 3, 0, 1
) + @(
    0, 0, 0, 0,  # IEND chunk size
    73, 69, 78, 68,  # "IEND"
    174, 66, 96, 130  # CRC
)

$jpgBytes = @(
    255, 216, 255, 224, 0, 16,  # JPG SOI + APP0
    74, 70, 73, 70, 0, 1, 1, 0,  # "JFIF"
    0, 1, 0, 1, 0, 0, 255, 219,
    0, 67, 0, 8, 6, 6, 7, 6, 5, 8, 7, 7, 7, 9, 9,
    8, 10, 12, 20, 13, 12, 11, 11, 12, 25, 18, 19, 15, 20, 29, 26,
    31, 30, 29, 26, 28, 28, 30, 31, 32, 34, 34, 34, 28, 30, 36, 35,
    32, 34, 30, 31, 34, 30, 31, 30, 255, 192, 0, 11, 8, 0, 1, 0, 1,
    1, 1, 17, 0, 255, 196, 0, 31, 0, 0, 1, 5, 1, 1, 1, 1, 1, 1, 0,
    0, 0, 0, 0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 255, 218,
    0, 8, 1, 1, 0, 0, 63, 0, 251, 255, 217
)

$test1Png = "$TempFiles\test1.png"
$test2Jpg = "$TempFiles\test2.jpg"
$test3Jpg = "$TempFiles\test3.jpg"

[IO.File]::WriteAllBytes($test1Png, $pngBytes)
[IO.File]::WriteAllBytes($test2Jpg, $jpgBytes)
[IO.File]::WriteAllBytes($test3Jpg, $jpgBytes)

Write-Host "- $test1Png ($(Get-Item $test1Png).Length bytes)"
Write-Host "- $test2Jpg ($(Get-Item $test2Jpg).Length bytes)"
Write-Host "- $test3Jpg ($(Get-Item $test3Jpg).Length bytes)"

# Crea archivi ZIP
Write-Host "Creazione archivi ZIP..." -ForegroundColor Yellow
$zip1 = "$ArchiveDir\archive1.zip"
$zip2 = "$ArchiveDir\archive2.zip"

Add-Type -AssemblyName System.IO.Compression.FileSystem

$zip = [System.IO.Compression.ZipFile]::Open($zip1, 'Create')
[System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $test1Png, "cartella1/test1.png") | Out-Null
[System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $test2Jpg, "cartella1/test2.jpg") | Out-Null
$zip.Dispose()

$zip = [System.IO.Compression.ZipFile]::Open($zip2, 'Create')
[System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $test3Jpg, "cartella2/test3.jpg") | Out-Null
$zip.Dispose()

Write-Host "- $zip1"
Write-Host "- $zip2"

# Crea profilo di test
Write-Host "Creazione profilo di test..." -ForegroundColor Yellow

@"
ProfileName=Test-Archives
MinSize=100
UserName=test
IncludePath=$ArchiveDir
ExcludePaths=
Extensions=jpg,jpeg,png,gif,bmp,tif,tiff,webp,mp4,avi,mov,mkv,wmv,flv,webm,m4v,3gp,mpg,mpeg,nef,m2ts
DestinationRoot=$TestRoot\output
DatabasePath=$TestDb
DestinationTemplate=YYYY/MM/DD
"@ | Out-File $TestProfile -Encoding UTF8

Write-Host "- $TestProfile"

# Esegui scansione
Write-Host ""
Write-Host "Esecuzione scansione..." -ForegroundColor Cyan
if (-not (Test-Path $AppExe)) {
    Write-Host "ERRORE: $AppExe non trovato" -ForegroundColor Red
    Write-Host "Compila il progetto con: dotnet build src\GestioneFoto.App" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "=== ISTRUZIONI ===" -ForegroundColor Cyan
Write-Host "1. L'app si aprirà tra 2 secondi..."
Write-Host "2. Nel profilo, seleziona: $TestProfile"
Write-Host "3. Clicca 'Scansione file'"
Write-Host "4. Attendi il completamento"
Write-Host "5. Chiudi l'app"
Write-Host ""

Start-Sleep -Seconds 2
& $AppExe "--profile=$TestProfile"

Write-Host ""
Write-Host "Test continuato..." -ForegroundColor Yellow

# Verifica risultati nel database
Write-Host ""
Write-Host "Verifica risultati nel database..." -ForegroundColor Cyan

if (-not (Test-Path $TestDb)) {
    Write-Host "ERRORE: Database non creato" -ForegroundColor Red
    exit 1
}

$DbPath = $TestDb
$ConnStr = "Data Source=$DbPath"

Add-Type -AssemblyName System.Data.SQLite

$conn = New-Object System.Data.SQLite.SQLiteConnection($ConnStr)
$conn.Open()

$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT COUNT(*) FROM SRC"
$count = $cmd.ExecuteScalar()

Write-Host "Total file nel database: $count" -ForegroundColor Green

$cmd.CommandText = "SELECT hash, file, size FROM SRC ORDER BY file"
$reader = $cmd.ExecuteReader()
Write-Host ""
Write-Host "File scansionati:" -ForegroundColor Green
while ($reader.Read()) {
    $file = $reader.GetString(1)
    $size = $reader.GetInt64(2)
    Write-Host "  $file ($size bytes)"
}
$reader.Dispose()

# Verifica file virtuali
$cmd.CommandText = "SELECT COUNT(*) FROM SRC WHERE file LIKE '%|%'"
$virtualCount = $cmd.ExecuteScalar()
Write-Host ""
Write-Host "File da archivi (virtuali): $virtualCount" -ForegroundColor Green

# Mostra dettagli EXIF
$cmd.CommandText = "SELECT file, dtExif FROM SRC WHERE dtExif IS NOT NULL"
$reader = $cmd.ExecuteReader()
if ($reader.HasRows) {
    Write-Host ""
    Write-Host "Date EXIF rilevate:" -ForegroundColor Green
    while ($reader.Read()) {
        Write-Host "  $($reader.GetString(0)) -> $($reader.GetString(1))"
    }
}
$reader.Dispose()

$conn.Dispose()

# Pulizia
Write-Host ""
Write-Host "Pulizia file temporanei..." -ForegroundColor Yellow
Remove-Item $TempFiles -Recurse -Force

Write-Host ""
Write-Host "=== Test Completato ===" -ForegroundColor Cyan
Write-Host "Database: $TestDb" -ForegroundColor Yellow
Write-Host "Cartella archivi: $ArchiveDir" -ForegroundColor Yellow
