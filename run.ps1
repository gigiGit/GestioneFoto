[CmdletBinding()]
param(
    [string]$ProjectPath = ".\src\GestioneFoto.App\GestioneFoto.App.csproj",
    [string]$Profile,
    [switch]$ViewDb,
    [switch]$NoBuild,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$AppArgs
)

$ErrorActionPreference = "Stop"

$projectFullPath = Join-Path $PSScriptRoot $ProjectPath
if (-not (Test-Path -LiteralPath $projectFullPath)) {
    throw "Progetto C# non trovato: $projectFullPath"
}

$dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnetCmd) {
    throw "Comando 'dotnet' non trovato. Installa .NET SDK 8."
}

$dotnetArgs = @(
    "run",
    "--project", (Resolve-Path -LiteralPath $projectFullPath).Path,
    "-c", "Debug"
)

if ($NoBuild.IsPresent) {
    $dotnetArgs += "--no-build"
}

$appRuntimeArgs = @()
if (-not [string]::IsNullOrWhiteSpace($Profile)) {
    $appRuntimeArgs += "--profile=$Profile"
}

if ($ViewDb.IsPresent) {
    $appRuntimeArgs += "--view-db"
}

if ($AppArgs) {
    $appRuntimeArgs += $AppArgs
}

if ($appRuntimeArgs.Count -gt 0) {
    $dotnetArgs += "--"
    $dotnetArgs += $appRuntimeArgs
}

Write-Host "[RUN] Avvio GestioneFoto.App in Debug..."
& dotnet @dotnetArgs

if ($LASTEXITCODE -ne 0) {
    throw "Esecuzione fallita (exit code $LASTEXITCODE)."
}
