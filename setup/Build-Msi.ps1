[CmdletBinding()]
param(
    [string]$PublishDir = ".\src\GestioneFoto.App\bin\Release\net8.0-windows\win-x64\publish",
    [string]$OutputDir = ".\artifacts\installer",
    [string]$Version = "1.0.0",
    [string]$PackageName = "GestioneFoto"
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host "[MSI] $Message"
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$publishPath = Resolve-Path -LiteralPath (Join-Path $repoRoot $PublishDir)
$configPath = Join-Path $repoRoot "config"
$outputPath = Join-Path $repoRoot $OutputDir
$wxsPath = Join-Path $PSScriptRoot "msi\GestioneFoto.wxs"
$iconPath = Join-Path $PSScriptRoot "assets\camera.ico"
$exePath = Join-Path $publishPath "GestioneFoto.App.exe"

if (-not (Test-Path -LiteralPath $wxsPath)) {
    throw "File WiX non trovato: $wxsPath"
}

if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Publish non valido. File mancante: $exePath"
}

if (-not (Test-Path -LiteralPath $configPath)) {
    throw "Cartella config non trovata: $configPath"
}

if (-not (Test-Path -LiteralPath $iconPath)) {
    throw "Icona non trovata: $iconPath"
}

$wixCmd = Get-Command wix -ErrorAction SilentlyContinue
if (-not $wixCmd) {
    throw "Comando 'wix' non trovato. Installa WiX, ad esempio: dotnet tool install --global wix"
}

if (-not (Test-Path -LiteralPath $outputPath)) {
    New-Item -Path $outputPath -ItemType Directory | Out-Null
}

$msiPath = Join-Path $outputPath "$PackageName-$Version-x64.msi"

Write-Step "Genero MSI da: $publishPath con config da: $configPath"
# WiX v7 richiede accettazione OSMF EULA; questo comando è idempotente.
& wix eula accept wix7 | Out-Null

& wix build $wxsPath `
    -arch x64 `
    -d AppVersion=$Version `
    -d IconPath="$iconPath" `
    -bindpath PublishDir="$publishPath" `
    -bindpath ConfigDir="$configPath" `
    -o "$msiPath"

if ($LASTEXITCODE -ne 0) {
    throw "Build MSI fallita (exit code $LASTEXITCODE)."
}

Write-Step "MSI creato: $msiPath"
