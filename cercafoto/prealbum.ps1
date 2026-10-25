# Prealbum.ps1 - Precompila il campo Album nel CSV

# Percorso del CSV
$csvPath = "j:\MediaBackup\giuliano\DESKTOP-5BRNMGV\original_paths.csv"

# Carica i dati
$data = Import-Csv -Path $csvPath

$updated = 0

foreach ($row in $data) {
    # Ottieni la directory parent del percorso originale
    $parentDir = Split-Path -Path $row.OriginalPath -Parent
    $folderName = Split-Path -Path $parentDir -Leaf

    # Se la directory parent è un numero, salta
    if ($folderName -match '^\d+$') {
        continue
    }

    # Ottieni l'anno dal DestPath (backup\anno\mese\...)
    $destParts = $row.DestPath -split '\\'
    $year = $destParts | Where-Object { $_ -match '^\d{4}$' } | Select-Object -First 1

    if ($year) {
        $albumValue = "$folderName - $year"
        if ([string]::IsNullOrWhiteSpace($row.Album)) {
            $row.Album = $albumValue
            $updated++
        }
    }
}

# Salva il CSV aggiornato
$data | Export-Csv -Path $csvPath -NoTypeInformation -Encoding UTF8

Write-Host "Aggiornati $updated record nel CSV."