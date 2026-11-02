[CmdletBinding()]
param(
    [string]$ProjectPath = ".\src\GestioneFoto.App\GestioneFoto.App.csproj",
    [string]$ScannerProjectPath = ".\src\GestioneFoto.Scanner\GestioneFoto.Scanner.csproj",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained = $true,
    [switch]$PublishSingleFile = $true,
    [string]$Version = "1.0.0",
    [string]$OutputDir = ".\artifacts\installer",
    [string]$PackageName = "GestioneFoto"
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host "[BUILD+MSI] $Message"
}

function Invoke-Checked {
    param(
        [string]$FileName,
        [string[]]$Arguments,
        [string]$ErrorMessage
    )

    & $FileName @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$ErrorMessage (exit code $LASTEXITCODE)."
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectFullPath = Join-Path $repoRoot $ProjectPath
$scannerProjectFullPath = Join-Path $repoRoot $ScannerProjectPath
$publishDirForMsi = ".\\src\\GestioneFoto.App\\bin\\$Configuration\\net8.0-windows\\$Runtime\\publish"

if (-not (Test-Path -LiteralPath $projectFullPath)) {
    throw "Progetto non trovato: $projectFullPath"
}

if (-not (Test-Path -LiteralPath $scannerProjectFullPath)) {
    throw "Progetto scanner non trovato: $scannerProjectFullPath"
}

$projectResolved = (Resolve-Path -LiteralPath $projectFullPath).Path
$scannerProjectResolved = (Resolve-Path -LiteralPath $scannerProjectFullPath).Path
$publishPath = Join-Path $repoRoot "src\GestioneFoto.App\bin\$Configuration\net8.0-windows\$Runtime\publish"
$buildMsiScript = Join-Path $PSScriptRoot "Build-Msi.ps1"

if (-not (Test-Path -LiteralPath $buildMsiScript)) {
    throw "Script MSI non trovato: $buildMsiScript"
}

$dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnetCmd) {
    throw "Comando 'dotnet' non trovato. Installa .NET SDK 8."
}

Write-Step "Restore progetto: $projectResolved"
Invoke-Checked -FileName "dotnet" -Arguments @(
    "restore",
    $projectResolved,
    "/p:DisableImplicitNuGetFallbackFolder=true"
) -ErrorMessage "Restore fallito"

Write-Step "Build progetto ($Configuration)"
Invoke-Checked -FileName "dotnet" -Arguments @(
    "build",
    $projectResolved,
    "-c", $Configuration,
    "/p:DisableImplicitNuGetFallbackFolder=true"
) -ErrorMessage "Build fallita"

$selfContainedValue = "false"
if ($SelfContained.IsPresent) {
    $selfContainedValue = "true"
}

$publishSingleFileValue = "false"
if ($PublishSingleFile.IsPresent) {
    $publishSingleFileValue = "true"
}

$publishArgs = @(
    "publish",
    $projectResolved,
    "-c", $Configuration,
    "-r", $Runtime,
    "--self-contained", $selfContainedValue,
    "/p:PublishSingleFile=$publishSingleFileValue",
    "/p:IncludeNativeLibrariesForSelfExtract=$publishSingleFileValue",
    "/p:DisableImplicitNuGetFallbackFolder=true"
)

Write-Step "Publish progetto in: $publishPath"
Invoke-Checked -FileName "dotnet" -Arguments $publishArgs -ErrorMessage "Publish fallita"

Write-Step "Publish scanner in: $publishPath"
$scannerPublishArgs = @(
    "publish",
    $scannerProjectResolved,
    "-c", $Configuration,
    "-r", $Runtime,
    "--self-contained", $selfContainedValue,
    "-o", $publishPath,
    "/p:PublishSingleFile=$publishSingleFileValue",
    "/p:IncludeNativeLibrariesForSelfExtract=$publishSingleFileValue",
    "/p:DisableImplicitNuGetFallbackFolder=true"
)
Invoke-Checked -FileName "dotnet" -Arguments $scannerPublishArgs -ErrorMessage "Publish scanner fallito"

if (-not (Test-Path -LiteralPath $publishPath)) {
    throw "Cartella publish non trovata dopo il publish: $publishPath"
}

$exePath = Join-Path $publishPath "GestioneFoto.App.exe"
if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Eseguibile non trovato dopo il publish: $exePath"
}

$scannerExePath = Join-Path $publishPath "GestioneFoto.Scanner.exe"
if (-not (Test-Path -LiteralPath $scannerExePath)) {
    throw "Eseguibile scanner non trovato dopo il publish: $scannerExePath"
}

$binPath = Join-Path $repoRoot "bin"
if (-not (Test-Path -LiteralPath $binPath)) {
    New-Item -Path $binPath -ItemType Directory | Out-Null
}

Get-ChildItem -LiteralPath $publishPath -Force |
    Copy-Item -Destination $binPath -Recurse -Force
Write-Step "File di publish copiati in bin: $binPath"

Write-Step "Avvio generazione MSI versione $Version"
& $buildMsiScript `
    -PublishDir $publishDirForMsi `
    -OutputDir $OutputDir `
    -Version $Version `
    -PackageName $PackageName

if ($LASTEXITCODE -ne 0) {
    throw "Generazione MSI fallita (exit code $LASTEXITCODE)."
}

$msiPath = Join-Path (Join-Path $repoRoot $OutputDir) "$PackageName-$Version-x64.msi"
Write-Step "Pipeline completata. MSI: $msiPath"
