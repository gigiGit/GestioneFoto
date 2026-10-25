<#
.SYNOPSIS
    Cerca e copia file multimediali da percorsi specificati, evitando duplicati basati su hash.

.DESCRIPTION
    Questo script PowerShell scansiona percorsi definiti in config.txt (o tutte le unità disco come fallback) alla ricerca di file multimediali.
    Copia i file unici (basati su hash MD5) in una struttura organizzata per anno/mese sotto $destBase\backup\.
    Tiene traccia degli hash già copiati in hashes.dat per evitare duplicati.
    Salva i percorsi originali in original_paths.csv e logga errori e operazioni.

.PARAMETER (nessuno, usa config.txt)

.EXAMPLE
    .\cercafoto.ps1

.NOTES
    - Richiede PowerShell con accesso in lettura/scrittura.
    - Crea la cartella di destinazione se non esiste.
    - Estensioni supportate: jpg, jpeg, png, gif, bmp, tif, tiff, webp, mp4, avi, mov, mkv, wmv, flv, webm, m4v, 3gp, mpg, mpeg, nef, m2ts.
    - File di output: hashes.dat, original_paths.csv, scan_log_*.txt, scan_err_*.txt, duplicati.dat.
    - Struttura di copia: backup\anno\mese\

#>

# CercaFoto per Windows

# Estensioni supportate per immagini e video
$extensions = @("*.jpg", "*.jpeg", "*.png", "*.gif", "*.bmp","*.tif", "*.tiff", "*.webp", "*.mp4", "*.avi", "*.mov", "*.mkv", "*.wmv", "*.flv", "*.webm", "*.m4v", "*.3gp", "*.mpg", "*.mpeg","*.nef","*.m2ts")

# Disco del programma (escludere questo disco dalla scansione)
$programDrive = Split-Path -Path $PSScriptRoot -Qualifier

# Cartella di destinazione su disco del programma
#####$username = $env:USERNAME
$username = "Patty"
$computerName = $env:COMPUTERNAME
$destBase = "$programDrive\Mediabackup\$username\$computerName"

# Crea la cartella se non esiste
if (!(Test-Path $destBase)) {
    New-Item -ItemType Directory -Path $destBase -Force
}


# File per tracciare hash già copiati
$hashFile = "$destBase\hashes.dat"
if (!(Test-Path $hashFile)) {
    New-Item -ItemType File -Path $hashFile -Force
}

# File per tracciare percorsi originali
$originalFile = "$destBase\original_paths.csv"

# File di log per ogni file scansionato
$dt=$(Get-Date -Format 'yyyyMMdd_HHmmss')
$logFile = "$destBase\log\scan_log_$dt.txt"
$errFile = "$destBase\log\scan_err_$dt.txt"
if (!(Test-Path "$destBase\log")) {
    New-Item -ItemType Directory -Path "$destBase\log"  -Force
}
# File per duplicati per nome
$duplicatesFile = "$destBase\duplicati.dat"

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
"  Dimensione minima file: $minSize bytes" | Out-File -FilePath $logFile -Append
"  Percorsi di scansione:" | Out-File -FilePath $logFile -Append
foreach ($path in $scanPaths) {
    "    $path" | Out-File -FilePath $logFile -Append
}
"  Cartelle escluse:" | Out-File -FilePath $logFile -Append
foreach ($exclude in $excludes) {
    "    $exclude" | Out-File -FilePath $logFile -Append
}

# Aggiorna l'header del CSV se necessario
$header = "Hash,OriginalPath,DestPath,DestFilename,DateSource,Status"
if (Test-Path $originalFile) {
    $lines = Get-Content $originalFile
    if ($lines.Count -gt 0) {
        $lines[0] = $header
        for ($i = 1; $i -lt $lines.Count; $i++) {
            $fields = $lines[$i] -split ','
            if ($fields.Count -eq 5) {
                $lines[$i] = "$($fields[0]),$($fields[1]),$($fields[2]),$($fields[3]),""File"",$($fields[4])"
            }
        }
        $lines | Out-File -FilePath $originalFile
    }
} else {
    $header | Out-File -FilePath $originalFile
}

# Aggiungi sempre la cartella di destinazione alle esclusioni
$excludes += $destBase

# Carica hash esistenti
$seenHashes = Get-Content $hashFile | Where-Object { $_ -ne "" }

# Contatori per resoconto
$scanned = 0
$copied = 0
$skipped = 0
$excluded = 0
$tooSmall = 0
$lastScannedFolder = ""

# Funzione per validare una data plausibile
function Test-UsableDate {
    param([DateTime]$date)

    $minYear = 1990
    $maxDate = (Get-Date).AddYears(1)
    return ($date.Year -ge $minYear -and $date -le $maxDate)
}

# Funzione per ottenere la data con priorita: acquisizione, modifica, file
function Get-DateFromMetadata {
    param([string]$filePath)

    $item = Get-Item -LiteralPath $filePath
    $metadataDate = $null
    $extension = [System.IO.Path]::GetExtension($filePath).ToLower()

    if ($extension -in @('.jpg', '.jpeg', '.png', '.tiff', '.bmp')) {
        try {
            $image = [System.Drawing.Image]::FromFile($filePath)
            $metadataTags = @(36867, 36868, 306) # DateTaken, DateDigitized, DateTime
            foreach ($tag in $metadataTags) {
                if ($image.PropertyIdList -contains $tag) {
                    $raw = [System.Text.Encoding]::ASCII.GetString($image.GetPropertyItem($tag).Value).Trim([char]0).Trim()
                    if ($raw) {
                        try {
                            $candidate = [DateTime]::ParseExact($raw, 'yyyy:MM:dd HH:mm:ss', $null)
                            if (Test-UsableDate $candidate) {
                                $metadataDate = $candidate
                                break
                            }
                        } catch {
                            # Ignora il formato EXIF non valido per il tag corrente
                        }
                    }
                }
            }
        } catch {
            # Log dell'errore per file corrotti
            "$((Get-Date).ToString('yyyy-MM-dd HH:mm:ss')) - Error reading metadata: $($_.Exception.Message) - $filePath" | Out-File -FilePath $errFile -Append
        } finally {
            if ($image) { $image.Dispose() }
        }
    }

    if ($metadataDate) {
        return @{ Date = $metadataDate; Source = "Acquisition" }
    }

    if (Test-UsableDate $item.LastWriteTime) {
        return @{ Date = $item.LastWriteTime; Source = "Modified" }
    }

    if (Test-UsableDate $item.CreationTime) {
        return @{ Date = $item.CreationTime; Source = "File" }
    }

    return @{ Date = $item.LastWriteTime; Source = "File" }
}

# Funzione per copiare file
function Copy-File {
    param([string]$src)

    $filename = [System.IO.Path]::GetFileName($src)
    $currentFolder = Split-Path -Path $src -Parent

    $script:scanned++

    if ($script:lastScannedFolder -ne $currentFolder) {
        Write-Host "Scansione sottocartella: $currentFolder"
        $script:lastScannedFolder = $currentFolder
    }

    try {

    # Controlla esclusioni
    foreach ($exclude in $excludes) {
        if ($src -like "$exclude\*") {
            $script:excluded++
            "$((Get-Date).ToString('yyyy-MM-dd HH:mm:ss')) - excluded : $src" | Out-File -FilePath $logFile -Append
            return
        }
    }

    # Controlla dimensione minima
    $size = (Get-Item -LiteralPath $src).Length
    if ($size -lt $minSize) {
        $script:tooSmall++
        "$((Get-Date).ToString('yyyy-MM-dd HH:mm:ss')) - too small : $src" | Out-File -FilePath $logFile -Append
        return
    }

    $hash = (Get-FileHash -LiteralPath $src -Algorithm MD5).Hash

    $dateInfo = Get-DateFromMetadata $src
    $dateSource = $dateInfo.Source

    $status = if ($seenHashes -contains $hash) { $script:skipped++; "skipped" } else { "copied"; $hash | Out-File -FilePath $hashFile -Append }

    if ($status -eq "copied") {
        $script:copied++
        # Ottieni data dai metadati o dal file
        $mtime = $dateInfo.Date
        $year = $mtime.Year.ToString()
        $month = $mtime.Month.ToString("00")

        $destDir = "$destBase\backup\$year\$month"
        if (!(Test-Path $destDir)) {
            New-Item -ItemType Directory -Path $destDir | Out-Null
        }

        $filename = [System.IO.Path]::GetFileName($src)
        $destPath = "$destDir\$filename"

        # Controlla se esiste già un file con lo stesso nome
        if (Test-Path -LiteralPath $destPath) {
            $status = "duplicate_name"
            $script:skipped++
            # Scrivi nel file duplicati.dat
            "$((Get-Date).ToString('yyyy-MM-dd HH:mm:ss')) - Duplicate name: $src -> $destPath" | Out-File -FilePath $duplicatesFile -Append
        } else {
            Copy-Item -LiteralPath $src -Destination $destPath
        }
    }

    # Registra percorso originale
    $destPathForCsv = if ($status -eq "copied") { $destPath } else { "" }
    "$hash,""$src"",""$destPathForCsv"",""$filename"",""$dateSource"",$status" | Out-File -FilePath $originalFile -Append

    # Log del file scansionato con operazione
    "$((Get-Date).ToString('yyyy-MM-dd HH:mm:ss')) - $status : $src" | Out-File -FilePath $logFile -Append
    } catch {
        "$((Get-Date).ToString('yyyy-MM-dd HH:mm:ss')) - Error processing file: $($_.Exception.Message) - $src" | Out-File -FilePath $errFile -Append
        "$((Get-Date).ToString('yyyy-MM-dd HH:mm:ss')) - error : $src" | Out-File -FilePath $logFile -Append
        return
    }
}

# Scansiona i percorsi definiti in config.txt
if ($scanPaths) {
    foreach ($path in $scanPaths) {
        if (Test-Path $path) {
            Write-Host "Scansione in corso su: $path"
            foreach ($ext in $extensions) {
                Get-ChildItem -Path $path -Recurse -Include $ext -ErrorAction SilentlyContinue | ForEach-Object {
                    Copy-File $_.FullName
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
                Copy-File $_.FullName
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