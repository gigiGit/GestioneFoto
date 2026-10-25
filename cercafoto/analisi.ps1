<#
.SYNOPSIS
    Analizza una cartella per file multimediali e genera un report CSV con informazioni sui file, identificando duplicati.

.DESCRIPTION
    Questo script PowerShell scansiona una cartella specificata (o percorsi da config.txt) alla ricerca di file multimediali (immagini e video).
    Per ciascun file valido (non escluso, di qualsiasi dimensione), calcola l'hash MD5, la data (da metadati EXIF o data del file), la dimensione e salva le informazioni in analisi.csv.
    Successivamente, analizza il CSV per identificare file con lo stesso hash ma percorsi originali diversi, salvandoli in duplicati.csv.

.PARAMETER inputFolder
    La cartella da analizzare. Se non specificata, usa i percorsi definiti in config.txt.

.EXAMPLE
    .\analisi.ps1 -inputFolder "J:\Photo\Photo"

.NOTES
    - Richiede PowerShell con accesso in lettura alle cartelle.
    - Crea la cartella di destinazione se non esiste.
    - Estensioni supportate: jpg, jpeg, png, gif, bmp, tif, tiff, webp, mp4, avi, mov, mkv, wmv, flv, webm, m4v, 3gp, mpg, mpeg, nef.
    - Output: analisi.csv e duplicati.csv in $destBase.

#>

# Analisi per Windows

param([string]$inputFolder)

# Estensioni supportate per immagini e video
$extensions = @("*.jpg", "*.jpeg", "*.png", "*.gif", "*.bmp","*.tif", "*.tiff", "*.webp", "*.mp4", "*.avi", "*.mov", "*.mkv", "*.wmv", "*.flv", "*.webm", "*.m4v", "*.3gp", "*.mpg", "*.mpeg","*.nef")

# Disco del programma 
$programDrive = Split-Path -Path $PSScriptRoot -Qualifier

# Cartella di destinazione su disco del programma
$username = $env:USERNAME
$computerName = $env:COMPUTERNAME
$destBase = "$programDrive\Mediabackup\$username\$computerName"

# Crea la cartella se non esiste
if (!(Test-Path $destBase)) {
    New-Item -ItemType Directory -Path $destBase -Force
}

# File Analisi foto
$resultFile = "$destBase\analisi.csv"

# File di log per ogni file scansionato
$dt=$(Get-Date -Format 'yyyyMMdd_HHmmss')
$logFile = "$destBase\log\analisi_log_$dt.txt"
$errFile = "$destBase\log\analisi_err_$dt.txt"
if (!(Test-Path "$destBase\log")) {
    New-Item -ItemType Directory -Path "$destBase\log" -Force
}

# File di configurazione (specifico per utente)
$configFile = "$destBase\config.txt"
$minSize = 1024
$excludes = @()
if (Test-Path $configFile) {
    $configContent = Get-Content $configFile
    $minSizeLine = $configContent | Where-Object { $_ -match "^MinSize=" } | Select-Object -First 1
    if ($minSizeLine) {
        $minSize = [int]$minSizeLine.Split('=')[1]
    }
    $excludes = $configContent | Where-Object { $_ -notmatch "^MinSize=" -and $_ -notmatch "^ScanPath=" -and $_ -notmatch "^#" -and $_ -ne "" } | ForEach-Object { $_.TrimEnd('\') }
    $scanPaths = $configContent | Where-Object { $_ -match "^ScanPath=" } | ForEach-Object { $_.Split('=',2)[1] }
} else {
    # Create default config.txt based on exclude.txt
    $excludeFile = "$destBase\exclude.txt"
    $defaultExcludes = if (Test-Path $excludeFile) { Get-Content $excludeFile | Where-Object { $_ -ne "" } | ForEach-Object { $_.TrimEnd('\') } } else { @() }
    $defaultContent = @("MinSize=1024", "# Scan paths", "ScanPath=C:\Users\$env:USERNAME\Pictures", "# Excluded folders") + $defaultExcludes
    $defaultContent | Out-File -FilePath $configFile
    $excludes = $defaultExcludes
    $scanPaths = @("C:\Users\$env:USERNAME\Pictures")
}

# Mostra i dettagli di configurazione letti
Write-Host "Configurazione caricata:"
if ($inputFolder) {
    Write-Host "  Cartella di lavoro: $inputFolder"
}
Write-Host "  Dimensione minima file: $minSize bytes"
Write-Host "  Percorsi di scansione:"
foreach ($path in $scanPaths) {
    Write-Host "    $path"
}
Write-Host "  Cartelle escluse:"
foreach ($exclude in $excludes) {
    Write-Host "    $exclude"
}

# Log delle informazioni di configurazione
"Configurazione caricata:" | Out-File -FilePath $logFile -Append
if ($inputFolder) {
    "  Cartella di lavoro: $inputFolder" | Out-File -FilePath $logFile -Append
}
"  Dimensione minima file: $minSize bytes" | Out-File -FilePath $logFile -Append
"  Percorsi di scansione:" | Out-File -FilePath $logFile -Append
foreach ($path in $scanPaths) {
    "    $path" | Out-File -FilePath $logFile -Append
}
"  Cartelle escluse:" | Out-File -FilePath $logFile -Append
foreach ($exclude in $excludes) {
    "    $exclude" | Out-File -FilePath $logFile -Append
}

# Crea il file CSV con header se non esiste
if (!(Test-Path $resultFile)) {
    $header | Out-File -FilePath $resultFile
}

# Contatori per resoconto
$scanned = 0
$copied = 0
$skipped = 0
$excluded = 0
$tooSmall = 0

# Funzione per ottenere la data dai metadati o dal file
function Get-DateFromMetadata {
    param([string]$filePath)

    $extension = [System.IO.Path]::GetExtension($filePath).ToLower()
    if ($extension -in @('.jpg', '.jpeg', '.png', '.tiff', '.bmp')) {
        try {
            $image = [System.Drawing.Image]::FromFile($filePath)
            $dateTaken = $image.GetPropertyItem(36867) # EXIF DateTaken
            if ($dateTaken) {
                $dateString = [System.Text.Encoding]::ASCII.GetString($dateTaken.Value).Trim()
                $date = [DateTime]::ParseExact($dateString, 'yyyy:MM:dd HH:mm:ss', $null)
                return @{ Date = $date; Source = "EXIF" }
            }
        } catch {
            # Log dell'errore per file corrotti
            "$((Get-Date).ToString('yyyy-MM-dd HH:mm:ss')) - Error reading metadata: $($_.Exception.Message) - $filePath" | Out-File -FilePath $errFile -Append
        } finally {
            if ($image) { $image.Dispose() }
        }
    }
    # Fallback to file date
    $date = (Get-Item $filePath).LastWriteTime
    return @{ Date = $date; Source = "File" }
}

# Funzione per analizzare il file e scrivere nel CSV
function Analyze-File {
    param([string]$src)

    $size = (Get-Item $src).Length
    $hash = (Get-FileHash -Path $src -Algorithm MD5).Hash
    $dateInfo = Get-DateFromMetadata $src
    $dateSource = $dateInfo.Source
    $date = $dateInfo.Date.ToString('yyyy-MM-dd HH:mm:ss')

    # Scrivi nel CSV
    "$hash,$src,$date,$dateSource,$size" | Out-File -FilePath $resultFile -Append

    $script:scanned++
}

# Scansiona la cartella fornita in input o i percorsi definiti in config.txt
if ($inputFolder) {
    if (Test-Path $inputFolder) {
        foreach ($ext in $extensions) {
            Get-ChildItem -Path $inputFolder -Recurse -Include $ext -ErrorAction SilentlyContinue | ForEach-Object {
                Analyze-File $_.FullName
            }
        }
    } else {
        Write-Host "Cartella di input non trovata: $inputFolder"
        exit 1
    }
} elseif ($scanPaths) {
    foreach ($path in $scanPaths) {
        if (Test-Path $path) {
            foreach ($ext in $extensions) {
                Get-ChildItem -Path $path -Recurse -Include $ext -ErrorAction SilentlyContinue | ForEach-Object {
                    Analyze-File $_.FullName
                }
            }
        } else {
            Write-Host "Percorso di scansione non trovato: $path"
        }
    }
} else {
    # Fallback: scansiona tutte le unità disco escluse quella del programma e i dischi di rete
    $drives = Get-Volume | Where-Object { $_.DriveLetter -and $_.DriveLetter -ne $programDrive.TrimEnd(':') -and $_.DriveType -ne 4 } | ForEach-Object { $_.DriveLetter + ':' }
    foreach ($drive in $drives) {
        foreach ($ext in $extensions) {
            Get-ChildItem -Path $drive -Recurse -Include $ext -ErrorAction SilentlyContinue | ForEach-Object {
                Analyze-File $_.FullName
            }
        }
    }
}

# Resoconto finale
Write-Host "Scansione completata."
Write-Host "Resoconto:"
Write-Host "  File scansionati: $scanned"
Write-Host "  File copiati: $copied"
Write-Host "  Duplicati saltati: $skipped"
Write-Host "  File esclusi: $excluded"
Write-Host "  File troppo piccoli: $tooSmall"

# Analizza duplicati
$duplicatiFile = "$destBase\duplicati.csv"
if (Test-Path $resultFile) {
    $data = Import-Csv $resultFile
    $grouped = $data | Group-Object -Property Hash
    $duplicates = @()
    foreach ($group in $grouped) {
        if ($group.Count -gt 1) {
            $paths = $group.Group | Select-Object -ExpandProperty OriginalPath | Sort-Object -Unique
            if ($paths.Count -gt 1) {
                $duplicates += $group.Group
            }
        }
    }
    if ($duplicates) {
        $duplicates | Export-Csv -Path $duplicatiFile -NoTypeInformation
        Write-Host "File duplicati salvati in: $duplicatiFile"
    } else {
        Write-Host "Nessun duplicato trovato."
    }
}
Write-Host "  File scansionati: $scanned"
