[CmdletBinding()]
param(
    [switch]$SkipInstall,
    [switch]$SkipRestore
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "src\GestioneFoto.App\GestioneFoto.App.csproj"

function Write-Step {
    param([string]$Message)

    Write-Host "[INIT] $Message"
}

function Refresh-Path {
    $machinePath = [Environment]::GetEnvironmentVariable("Path", "Machine")
    $userPath = [Environment]::GetEnvironmentVariable("Path", "User")
    $processPath = $env:Path
    $env:Path = (@($machinePath, $userPath, $processPath) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) -join ";"
}

function Get-Executable {
    param([string]$Name)

    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($command -and $command.CommandType -eq "Application") {
        return $command.Source
    }

    return $null
}

function Install-WingetPackage {
    param(
        [string]$PackageId,
        [string]$Executable,
        [switch]$InstallAnyway
    )

    if (-not $InstallAnyway -and (Get-Executable -Name $Executable)) {
        Write-Step "$Executable gia' disponibile."
        return
    }

    $winget = Get-Executable -Name "winget.exe"
    if (-not $winget) {
        $winget = Get-Executable -Name "winget"
    }
    if (-not $winget) {
        throw "winget non trovato. Installa App Installer oppure installa manualmente $PackageId."
    }

    Write-Step "Installazione $PackageId tramite winget..."
    & $winget install --id $PackageId --exact --source winget --accept-source-agreements --accept-package-agreements
    if ($LASTEXITCODE -ne 0) {
        throw "Installazione di $PackageId non riuscita (exit code $LASTEXITCODE)."
    }

    Refresh-Path
    if (-not (Get-Executable -Name $Executable)) {
        throw "$PackageId risulta installato, ma $Executable non e' nel PATH. Apri un nuovo terminale e riesegui questo script."
    }
}

function Ensure-DotNetSdk8 {
    $dotnet = Get-Executable -Name "dotnet.exe"
    if (-not $dotnet) {
        $dotnet = Get-Executable -Name "dotnet"
    }

    $hasSdk8 = $false
    if ($dotnet) {
        $sdkList = & $dotnet --list-sdks
        if ($LASTEXITCODE -eq 0) {
            $hasSdk8 = @($sdkList | Where-Object { $_ -match '^\s*8\.' }).Count -gt 0
        }
    }

    if (-not $hasSdk8) {
        Install-WingetPackage -PackageId "Microsoft.DotNet.SDK.8" -Executable "dotnet.exe" -InstallAnyway
    }

    $dotnet = Get-Executable -Name "dotnet.exe"
    if (-not $dotnet) {
        $dotnet = Get-Executable -Name "dotnet"
    }
    if (-not $dotnet) {
        throw "dotnet non trovato dopo l'installazione di .NET SDK 8."
    }

    $sdkList = & $dotnet --list-sdks
    if ($LASTEXITCODE -ne 0 -or @($sdkList | Where-Object { $_ -match '^\s*8\.' }).Count -eq 0) {
        throw ".NET SDK 8 non risulta disponibile. Verifica l'installazione e riapri il terminale."
    }
}

function Ensure-WixTool {
    if (Get-Executable -Name "wix.exe") {
        Write-Step "WiX gia' disponibile."
        return
    }

    $dotnet = Get-Executable -Name "dotnet.exe"
    if (-not $dotnet) {
        $dotnet = Get-Executable -Name "dotnet"
    }

    Write-Step "Installazione dello strumento WiX globale..."
    & $dotnet tool install --global wix
    if ($LASTEXITCODE -ne 0) {
        throw "Installazione di WiX non riuscita (exit code $LASTEXITCODE)."
    }

    Refresh-Path
    if (-not (Get-Executable -Name "wix.exe")) {
        throw "WiX risulta installato, ma wix.exe non e' nel PATH. Apri un nuovo terminale e riesegui questo script."
    }
}

Write-Step "Inizializzazione del progetto in $repoRoot"
if (-not (Test-Path -LiteralPath $projectPath)) {
    throw "Progetto .NET non trovato: $projectPath"
}

if (-not $SkipInstall) {
    Ensure-DotNetSdk8
    Ensure-WixTool
    Install-WingetPackage -PackageId "JohnMacFarlane.Pandoc" -Executable "pandoc.exe"
    Install-WingetPackage -PackageId "SQLite.SQLite" -Executable "sqlite3.exe"
}
else {
    foreach ($executable in @("dotnet.exe", "wix.exe", "pandoc.exe", "sqlite3.exe")) {
        if (-not (Get-Executable -Name $executable)) {
            throw "$executable non trovato nel PATH; rimuovi -SkipInstall per installare i prerequisiti."
        }
    }
}

$dotnet = Get-Executable -Name "dotnet.exe"
if (-not $dotnet) {
    $dotnet = Get-Executable -Name "dotnet"
}

if (-not $SkipRestore) {
    Write-Step "Ripristino dei pacchetti NuGet..."
    & $dotnet restore $projectPath
    if ($LASTEXITCODE -ne 0) {
        throw "Ripristino NuGet non riuscito (exit code $LASTEXITCODE)."
    }
}

Write-Step "Inizializzazione completata. Per compilare: .\setup\Build-AppAndMsi.ps1"
