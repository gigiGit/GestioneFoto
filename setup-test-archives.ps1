#!/usr/bin/env pwsh
<#
.SYNOPSIS
Setup test archivi: crea file di test, archivi ZIP e profilo.
Esegui questo script, poi apri manualmente l'app con il profilo generato.
#>

$ErrorActionPreference = 'Stop'

# Percorsi
$TestRoot = "$PSScriptRoot\test-archives"
$ArchiveDir = "$TestRoot\archives"
$TempFiles = "$TestRoot\temp-files"

Write-Host "=== Setup Test Scansione Archivi ===" -ForegroundColor Cyan

# Pulisci test precedenti
if (Test-Path $TestRoot) {
    Write-Host "Pulizia test precedenti..." -ForegroundColor Yellow
    Remove-Item $TestRoot -Recurse -Force -ErrorAction Ignore
}

# Crea struttura di test
Write-Host "Creazione struttura..." -ForegroundColor Yellow
New-Item -Path $ArchiveDir -ItemType Directory -Force | Out-Null
New-Item -Path $TempFiles -ItemType Directory -Force | Out-Null

# Piccolo PNG (pixel rosso)
$pngBytes = @(
    137, 80, 78, 71, 13, 10, 26, 10,
    0, 0, 0, 13,
    73, 72, 68, 82,
    0, 0, 0, 1, 0, 0, 0, 1,
    8, 2, 0, 0, 0,
    144, 119, 83, 222
) + @(
    0, 0, 0, 12,
    73, 68, 65, 84,
    8, 29, 1, 3, 0, 252, 255, 255, 0, 0, 3, 0, 1
) + @(
    0, 0, 0, 0,
    73, 69, 78, 68,
    174, 66, 96, 130
)

$jpgBytes = @(
    255, 216, 255, 224, 0, 16,
    74, 70, 73, 70, 0, 1, 1, 0,
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
$test4Jpg = "$TempFiles\test4.jpg"

[IO.File]::WriteAllBytes($test1Png, $pngBytes)
[IO.File]::WriteAllBytes($test2Jpg, $jpgBytes)
[IO.File]::WriteAllBytes($test3Jpg, $jpgBytes)
[IO.File]::WriteAllBytes($test4Jpg, $jpgBytes)

Write-Host "File di test creati (PNG e JPG)"

# Crea ZIP
Write-Host ""
Write-Host "Creazione archivi..." -ForegroundColor Yellow
$zip1 = "$ArchiveDir\archive1.zip"

Add-Type -AssemblyName System.IO.Compression.FileSystem

$zip = [System.IO.Compression.ZipFile]::Open($zip1, 'Create')
[System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $test1Png, "cartella1/test1.png") | Out-Null
[System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $test2Jpg, "cartella1/test2.jpg") | Out-Null
$zip.Dispose()

Write-Host "ZIP: $zip1"

# Crea TAR.GZ con tar.exe (disponibile in Windows 10+)
$targz1 = "$ArchiveDir\archive2.tar.gz"
$tar1 = "$ArchiveDir\archive2.tar"

try {
    Push-Location $TempFiles
    tar.exe -czf $targz1 test3.jpg
    Write-Host "TAR.GZ: $targz1"
    Pop-Location
} catch {
    Write-Host "Avviso: tar.exe non disponibile, usero PowerShell" -ForegroundColor Yellow
    Pop-Location
}

# Crea TGZ (uguale a .tar.gz, nome diverso) come copia
$tgz1 = "$ArchiveDir\archive3.tgz"
Copy-Item $targz1 $tgz1 -Force
Write-Host "TGZ: $tgz1"

# Crea profilo
Write-Host ""
Write-Host "Creazione profilo di test..." -ForegroundColor Yellow
$TestProfile = "$TestRoot\test.profile"
$TestDb = "$TestRoot\test-scan.db"

$profileContent = @"
ProfileName=Test-Archives
MinSize=100
UserName=test
IncludePath=$ArchiveDir
ExcludePaths=
Extensions=jpg,jpeg,png
DestinationRoot=$TestRoot\output
DatabasePath=$TestDb
DestinationTemplate=YYYY/MM/DD
"@

$profileContent | Out-File $TestProfile -Encoding UTF8

Write-Host "$TestProfile created"
Write-Host ""
Write-Host "Profilo di test: $TestProfile" -ForegroundColor Cyan
Write-Host "Database (dopo scansione): $TestDb" -ForegroundColor Cyan
Write-Host ""
Write-Host "Prossimi step:" -ForegroundColor Cyan
Write-Host "1. Compila: dotnet publish src\GestioneFoto.App -c Release -o bin"
Write-Host "2. Esegui: .\bin\GestioneFoto.App.exe --profile=`"$TestProfile`""
Write-Host "3. Nel profilo seleziona Test-Archives"
Write-Host "4. Clicca 'Scansione'"
Write-Host "5. Clicca 'Visualizza Database'"
Write-Host ""
Write-Host "Cosa dovresti vedere:" -ForegroundColor Cyan
Write-Host "- 4 file totali:"
Write-Host "  * 2 da archive1.zip (ZIP)"
Write-Host "  * 1 da archive2.tar.gz (TAR.GZ)"
Write-Host "  * 1 da archive3.tgz (TGZ)"
Write-Host "- Path con '|': es. 'archive1.zip|cartella1/test1.png'"
Write-Host ""

# Cleanup temp files ma mantieni gli archivi
Write-Host "Pulizia file temporanei..." -ForegroundColor Yellow
Remove-Item $TempFiles -Recurse -Force -ErrorAction Ignore
if (Test-Path $tar1) {
    Remove-Item $tar1 -Force -ErrorAction Ignore
}
