[CmdletBinding()]
param(
    [string]$InstallRoot = "$env:USERPROFILE\GestioneFoto",
    [switch]$InstallDotNetSdk,
    [switch]$SkipDesktopShortcut,
    [switch]$SkipAppCopy,
    [string]$AppExePath,
    [switch]$NonInteractive
)

$ErrorActionPreference = 'Stop'

function Write-Step {
    param([string]$Message)
    Write-Host "[SETUP] $Message"
}

function Ensure-Directory {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path | Out-Null
    }
}

function Get-RepoRoot {
    $candidate = Split-Path -Parent $PSScriptRoot
    if (-not [string]::IsNullOrWhiteSpace($candidate)) {
        return $candidate
    }

    return (Get-Location).Path
}

function Ask-YesNo {
    param(
        [string]$Question,
        [bool]$DefaultYes = $true
    )

    $hint = if ($DefaultYes) { "[S/n]" } else { "[s/N]" }

    while ($true) {
        $answer = Read-Host "$Question $hint"
        if ([string]::IsNullOrWhiteSpace($answer)) {
            return $DefaultYes
        }

        switch ($answer.Trim().ToLowerInvariant()) {
            "s" { return $true }
            "si" { return $true }
            "y" { return $true }
            "yes" { return $true }
            "n" { return $false }
            "no" { return $false }
            default { Write-Host "Risposta non valida. Usa S o N." }
        }
    }
}

function Ask-Text {
    param(
        [string]$Question,
        [string]$DefaultValue = ""
    )

    if ([string]::IsNullOrWhiteSpace($DefaultValue)) {
        $value = Read-Host $Question
        return $value
    }

    $value = Read-Host "$Question [$DefaultValue]"
    if ([string]::IsNullOrWhiteSpace($value)) {
        return $DefaultValue
    }

    return $value
}

function Install-SdkIfRequested {
    if (-not $InstallDotNetSdk) {
        Write-Step "Installazione SDK non richiesta."
        return
    }

    $dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnetCmd) {
        Write-Step "dotnet gia' presente in PATH: $($dotnetCmd.Source)"
        return
    }

    $wingetCmd = Get-Command winget -ErrorAction SilentlyContinue
    if (-not $wingetCmd) {
        throw "winget non disponibile. Installa App Installer da Microsoft Store o installa manualmente .NET SDK 8."
    }

    Write-Step "Installo Microsoft.DotNet.SDK.8 tramite winget..."
    & winget install --id Microsoft.DotNet.SDK.8 --source winget --accept-source-agreements --accept-package-agreements
    if ($LASTEXITCODE -ne 0) {
        throw "Installazione SDK non riuscita (exit code $LASTEXITCODE)."
    }

    Write-Step "Installazione SDK completata."
}

function Copy-Profiles {
    param(
        [string]$RepoRoot,
        [string]$ConfigTargetDir
    )

    $sourceConfigDir = Join-Path $RepoRoot "config"
    $profileFiles = @()
    if (Test-Path -LiteralPath $sourceConfigDir) {
        $profileFiles = Get-ChildItem -LiteralPath $sourceConfigDir -Filter "*.profile" -File
    }

    if ($profileFiles.Count -gt 0) {
        foreach ($profile in $profileFiles) {
            $targetProfilePath = Join-Path $ConfigTargetDir $profile.Name
            Copy-Item -LiteralPath $profile.FullName -Destination $targetProfilePath -Force
        }

        Write-Step "Copiati $($profileFiles.Count) profili in $ConfigTargetDir"
        return
    }

    Write-Step "Nessun .profile trovato: creo un profilo base."
    $defaultTarget = Join-Path $ConfigTargetDir "default.profile"
    $fallback = @(
        "ProfileName=default",
        "MinSize=1024",
        "UserName=$env:USERNAME",
        "IncludePath=$env:USERPROFILE\Pictures",
        "ExcludePaths=",
        "Extensions=jpg,jpeg,png,gif,bmp,tif,tiff,webp,mp4,avi,mov,mkv,wmv,flv,webm,m4v,3gp,mpg,mpeg,nef,m2ts",
        "DestinationRoot=$InstallRoot\backup",
        "DatabasePath=$InstallRoot\GestioneFoto.db",
        "DestinationTemplate=YYYY/MM/DD",
        "AlbumDestination=$InstallRoot\Album"
    )
    Set-Content -LiteralPath $defaultTarget -Value $fallback -Encoding UTF8
}

function Resolve-AppExe {
    param([string]$RepoRoot)

    if (-not [string]::IsNullOrWhiteSpace($AppExePath)) {
        if (-not (Test-Path -LiteralPath $AppExePath)) {
            throw "AppExePath non trovato: $AppExePath"
        }

        return (Resolve-Path -LiteralPath $AppExePath).Path
    }

    $candidates = @(
        (Join-Path $RepoRoot "artifacts\verify-build\GestioneFoto.App.exe"),
        (Join-Path $RepoRoot "src\GestioneFoto.App\bin\Release\net8.0-windows\win-x64\publish\GestioneFoto.App.exe"),
        (Join-Path $RepoRoot "src\GestioneFoto.App\bin\Debug\net8.0-windows\GestioneFoto.App.exe")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    return $null
}

function Copy-AppPayload {
    param(
        [string]$SourceExe,
        [string]$TargetRoot
    )

    if ([string]::IsNullOrWhiteSpace($SourceExe) -or -not (Test-Path -LiteralPath $SourceExe)) {
        throw "Eseguibile sorgente non valido: $SourceExe"
    }

    Ensure-Directory -Path $TargetRoot

    $sourceDir = Split-Path -Parent $SourceExe
    $resolvedSourceDir = (Resolve-Path -LiteralPath $sourceDir).Path.TrimEnd('\\')
    $resolvedTargetRoot = (Resolve-Path -LiteralPath $TargetRoot).Path.TrimEnd('\\')

    if ($resolvedSourceDir.Equals($resolvedTargetRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        Write-Step "Origine e destinazione coincidono: salto copia payload applicazione."
        return
    }

    # Copia tutto il payload (dll, runtimes, asset nativi) mantenendo la struttura.
    $itemsToCopy = Get-ChildItem -LiteralPath $sourceDir -Force | Where-Object { $_.Name -ne "config" }
    foreach ($item in $itemsToCopy) {
        Copy-Item -LiteralPath $item.FullName -Destination $TargetRoot -Recurse -Force
    }
}

function Resolve-ShortcutIcon {
    param([string]$TargetExe)

    $customIcon = Join-Path $PSScriptRoot "assets\camera.ico"
    if (Test-Path -LiteralPath $customIcon) {
        return "$customIcon,0"
    }

    if (-not [string]::IsNullOrWhiteSpace($TargetExe) -and (Test-Path -LiteralPath $TargetExe)) {
        return "$TargetExe,0"
    }

    $fallbackIcon = Join-Path $env:WINDIR "System32\imageres.dll"
    if (Test-Path -LiteralPath $fallbackIcon) {
        # Fallback icon from Windows image resources.
        return "$fallbackIcon,67"
    }

    return ""
}

function Configure-InteractiveWizard {
    param([string]$RepoRoot)

    if ($NonInteractive) {
        return
    }

    Write-Host ""
    Write-Host "=== Setup Guidato GestioneFoto ==="

    if (-not $PSBoundParameters.ContainsKey("InstallRoot")) {
        $script:InstallRoot = Ask-Text -Question "Cartella installazione" -DefaultValue $script:InstallRoot
    }

    if (-not $PSBoundParameters.ContainsKey("InstallDotNetSdk")) {
        $installSdk = Ask-YesNo -Question "Installare .NET SDK 8 (solo se serve compilare/publish)?" -DefaultYes $false
        $script:InstallDotNetSdk = $installSdk
    }

    if (-not $PSBoundParameters.ContainsKey("SkipAppCopy")) {
        $copyApp = Ask-YesNo -Question "Copiare l'app nella cartella di installazione?" -DefaultYes $true
        $script:SkipAppCopy = -not $copyApp
    }

    if (-not $script:SkipAppCopy -and -not $PSBoundParameters.ContainsKey("AppExePath")) {
        $autoExe = Resolve-AppExe -RepoRoot $RepoRoot
        if ($autoExe) {
            $useAuto = Ask-YesNo -Question "Uso eseguibile rilevato automaticamente? $autoExe" -DefaultYes $true
            if ($useAuto) {
                $script:AppExePath = $autoExe
            }
            else {
                $manualPath = Ask-Text -Question "Percorso completo GestioneFoto.App.exe (vuoto per salto copia)"
                if ([string]::IsNullOrWhiteSpace($manualPath)) {
                    $script:SkipAppCopy = $true
                }
                else {
                    $script:AppExePath = $manualPath
                }
            }
        }
        else {
            $manualPath = Ask-Text -Question "Eseguibile non trovato. Inserisci percorso completo GestioneFoto.App.exe (vuoto per salto copia)"
            if ([string]::IsNullOrWhiteSpace($manualPath)) {
                $script:SkipAppCopy = $true
            }
            else {
                $script:AppExePath = $manualPath
            }
        }
    }

    if (-not $PSBoundParameters.ContainsKey("SkipDesktopShortcut")) {
        $createShortcut = Ask-YesNo -Question "Creare collegamento Desktop con icona significativa?" -DefaultYes $true
        $script:SkipDesktopShortcut = -not $createShortcut
    }
}

function Create-DesktopShortcut {
    param(
        [string]$TargetExe,
        [string]$WorkingDirectory
    )

    if ($SkipDesktopShortcut) {
        Write-Step "Creazione collegamento Desktop saltata."
        return
    }

    $desktop = [Environment]::GetFolderPath("Desktop")
    $shortcutPath = Join-Path $desktop "GestioneFoto.lnk"

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $TargetExe
    $shortcut.WorkingDirectory = $WorkingDirectory
    $shortcut.Description = "GestioneFoto.App"
    $shortcut.IconLocation = Resolve-ShortcutIcon -TargetExe $TargetExe
    $shortcut.Save()

    Write-Step "Creato collegamento Desktop: $shortcutPath"
}

Write-Step "Avvio setup iniziale..."
$repoRoot = Get-RepoRoot

Configure-InteractiveWizard -RepoRoot $repoRoot

Install-SdkIfRequested

Ensure-Directory -Path $InstallRoot
$configTargetDir = Join-Path $InstallRoot "config"
Ensure-Directory -Path $configTargetDir

Copy-Profiles -RepoRoot $repoRoot -ConfigTargetDir $configTargetDir

$installedExe = $null
$sourceExe = Resolve-AppExe -RepoRoot $repoRoot
if (-not $SkipAppCopy) {
    if ($sourceExe) {
        Copy-AppPayload -SourceExe $sourceExe -TargetRoot $InstallRoot
        $installedExe = Join-Path $InstallRoot "GestioneFoto.App.exe"
        if (Test-Path -LiteralPath $installedExe) {
            Write-Step "Copiato payload applicazione in $InstallRoot"
        }
        else {
            Write-Step "Payload copiato ma eseguibile non trovato nella destinazione: $installedExe"
            $installedExe = $null
        }
    }
    else {
        Write-Step "Nessun eseguibile trovato. Usa -AppExePath per specificare il percorso dell'app."
    }
}
else {
    Write-Step "Copia applicazione saltata."
}

if (-not $SkipDesktopShortcut) {
    if ($installedExe) {
        Create-DesktopShortcut -TargetExe $installedExe -WorkingDirectory $InstallRoot
    }
    elseif ($sourceExe) {
        $sourceWorking = Split-Path -Parent $sourceExe
        Create-DesktopShortcut -TargetExe $sourceExe -WorkingDirectory $sourceWorking
    }
    else {
        Write-Step "Collegamento Desktop non creato: eseguibile non disponibile."
    }
}

Write-Step "Setup completato."
Write-Host "InstallRoot: $InstallRoot"
Write-Host "Config: $configTargetDir"
Write-Host "Database consigliato: $InstallRoot\GestioneFoto.db"
if ($installedExe) {
    Write-Host "Eseguibile: $installedExe"
}
