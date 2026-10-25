<#
.SYNOPSIS
    Versione PowerShell di ricerca.sh.

.DESCRIPTION
    Scansiona una sorgente, cerca file multimediali per estensione, evita duplicati tramite hash MD5
    e copia i file in una struttura anno\mese sotto la cartella di destinazione.
    Registra log, errori e un CSV con i percorsi originali e di destinazione.

.PARAMETER Src
    Percorso sorgente da scansionare. Default: M:\

.PARAMETER Dst
    Cartella destinazione. Default: K:\MediaBackup\Patty\<COMPUTERNAME>
#>

param(
    [string]$Src = "M:\",
    [string]$Dst = "K:\MediaBackup\Patty\$env:COMPUTERNAME"
)

$extensions = @('jpg', 'jpeg', 'png', 'gif', 'bmp', 'tif', 'tiff', 'webp', 'mp4', 'avi', 'mov', 'mkv', 'wmv', 'flv', 'webm', 'm4v', '3gp', 'mpg', 'mpeg', 'nef', 'm2ts')
$timestamp = Get-Date -Format 'dd.MM.yyyy.HH.mm.ss'
$hashFile = Join-Path $Dst 'hashes.txt'
$csvFile = Join-Path $Dst 'media.csv'
$logDir = Join-Path $Dst 'log'
$logFile = Join-Path $logDir "$timestamp.log"
$errFile = Join-Path $logDir "$timestamp.err.log"
$excludedPatterns = @(
    'found.*',
    '*RECYCLE*',
    'System Volume Information'
)

if (!(Test-Path -LiteralPath $Dst)) {
    New-Item -ItemType Directory -Path $Dst -Force | Out-Null
}

if (!(Test-Path -LiteralPath $logDir)) {
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
}

if (!(Test-Path -LiteralPath $hashFile)) {
    New-Item -ItemType File -Path $hashFile -Force | Out-Null
}

if (!(Test-Path -LiteralPath $csvFile)) {
    'Hash,OriginalPath,DestPath,DestFilename,DateSource,Status' | Out-File -FilePath $csvFile -Encoding utf8
}

$seenHashes = Get-Content -LiteralPath $hashFile | Where-Object { $_ -ne '' }
$script:scanned = 0
$script:copied = 0
$script:skipped = 0
$script:excluded = 0
$script:errors = 0
$script:lastFolder = ''

function Test-UsableDate {
    param([DateTime]$Date)

    $minYear = 1990
    $maxDate = (Get-Date).AddYears(1)
    return ($Date.Year -ge $minYear -and $Date -le $maxDate)
}

function Get-DateInfo {
    param([string]$FilePath)

    $item = Get-Item -LiteralPath $FilePath
    $metadataDate = $null
    $extension = [System.IO.Path]::GetExtension($FilePath).ToLower()

    if ($extension -in @('.jpg', '.jpeg', '.png', '.tif', '.tiff', '.bmp')) {
        try {
            $image = [System.Drawing.Image]::FromFile($FilePath)
            $metadataTags = @(36867, 36868, 306)
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
                        }
                    }
                }
            }
        } catch {
            "$((Get-Date).ToString('yyyy-MM-dd HH:mm:ss')) - Error reading metadata: $($_.Exception.Message) - $FilePath" | Out-File -FilePath $errFile -Append
        } finally {
            if ($image) { $image.Dispose() }
        }
    }

    if ($metadataDate) {
        return @{ Date = $metadataDate; Source = 'Acquisition' }
    }

    if (Test-UsableDate $item.LastWriteTime) {
        return @{ Date = $item.LastWriteTime; Source = 'Modified' }
    }

    if (Test-UsableDate $item.CreationTime) {
        return @{ Date = $item.CreationTime; Source = 'File' }
    }

    return @{ Date = $item.LastWriteTime; Source = 'File' }
}

function Test-IsExcluded {
    param([string]$FilePath)

    foreach ($pattern in $excludedPatterns) {
        if ($FilePath -like "*$pattern*") {
            return $true
        }
    }

    return $false
}

function Add-CsvRecord {
    param(
        [string]$Hash,
        [string]$OriginalPath,
        [string]$DestPath,
        [string]$DestFilename,
        [string]$DateSource,
        [string]$Status
    )

    "$Hash,\"$OriginalPath\",\"$DestPath\",\"$DestFilename\",\"$DateSource\",$Status" | Out-File -FilePath $csvFile -Append -Encoding utf8
}

function Handle-File {
    param([string]$FilePath)

    $script:scanned++
    $currentFolder = Split-Path -Path $FilePath -Parent

    if ($script:lastFolder -ne $currentFolder) {
        Write-Host "Scansione sottocartella: $currentFolder"
        $script:lastFolder = $currentFolder
    }

    if (Test-IsExcluded $FilePath) {
        $script:excluded++
        "$((Get-Date).ToString('yyyy-MM-dd HH:mm:ss')) - excluded : $FilePath" | Out-File -FilePath $logFile -Append
        return
    }

    try {
        $hash = (Get-FileHash -LiteralPath $FilePath -Algorithm MD5).Hash
        $fileName = [System.IO.Path]::GetFileName($FilePath)
        $dateInfo = Get-DateInfo -FilePath $FilePath
        $destPath = ''
        $status = 'copied'

        if ($seenHashes -contains $hash) {
            $script:skipped++
            $status = 'skipped'
            Add-CsvRecord -Hash $hash -OriginalPath $FilePath -DestPath '' -DestFilename $fileName -DateSource $dateInfo.Source -Status $status
            "$((Get-Date).ToString('yyyy-MM-dd HH:mm:ss')) - skipped : $FilePath" | Out-File -FilePath $logFile -Append
            return
        }

        $year = $dateInfo.Date.Year.ToString()
        $month = $dateInfo.Date.Month.ToString('00')
        $destDir = Join-Path (Join-Path $Dst $year) $month

        if (!(Test-Path -LiteralPath $destDir)) {
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
        }

        $destPath = Join-Path $destDir $fileName
        if (Test-Path -LiteralPath $destPath) {
            $script:skipped++
            $status = 'duplicate_name'
            "$((Get-Date).ToString('yyyy-MM-dd HH:mm:ss')) - Duplicate name: $FilePath -> $destPath" | Out-File -FilePath $logFile -Append
        } else {
            Copy-Item -LiteralPath $FilePath -Destination $destPath
            (Get-Item -LiteralPath $destPath).CreationTime = $dateInfo.Date
            (Get-Item -LiteralPath $destPath).LastWriteTime = $dateInfo.Date
            $hash | Out-File -FilePath $hashFile -Append
            $seenHashes += $hash
            $script:copied++
        }

        Add-CsvRecord -Hash $hash -OriginalPath $FilePath -DestPath $destPath -DestFilename $fileName -DateSource $dateInfo.Source -Status $status
        "$((Get-Date).ToString('yyyy-MM-dd HH:mm:ss')) - $status : $FilePath" | Out-File -FilePath $logFile -Append
    } catch {
        $script:errors++
        "$((Get-Date).ToString('yyyy-MM-dd HH:mm:ss')) - Error processing file: $($_.Exception.Message) - $FilePath" | Out-File -FilePath $errFile -Append
        "$((Get-Date).ToString('yyyy-MM-dd HH:mm:ss')) - error : $FilePath" | Out-File -FilePath $logFile -Append
    }
}

if (!(Test-Path -LiteralPath $Src)) {
    throw "Percorso sorgente non trovato: $Src"
}

Write-Host "Iniziando scansione: $Src"
"Iniziando scansione: $Src" | Out-File -FilePath $logFile -Append

foreach ($ext in $extensions) {
    Write-Host "Estensione in corso: *.$ext"
    Get-ChildItem -Path $Src -Recurse -File -Filter "*.$ext" -ErrorAction SilentlyContinue | ForEach-Object {
        Handle-File -FilePath $_.FullName
    }
}

Write-Host 'Scansione completata.'
Write-Host "  File scansionati: $script:scanned"
Write-Host "  File copiati: $script:copied"
Write-Host "  Duplicati saltati: $script:skipped"
Write-Host "  File esclusi: $script:excluded"
Write-Host "  Errori: $script:errors"