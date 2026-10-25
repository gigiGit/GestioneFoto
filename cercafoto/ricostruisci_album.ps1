# Ricostruisci Album

# Cartella di destinazione
$username = $env:USERNAME
$computername = $env:COMPUTERNAME
$programDrive = Split-Path -Path $PSScriptRoot -Qualifier
$destBase = "$programDrive\Mediabackup\$username\$computername"
$albumBase = "$destBase\Album"
$originalFile = "$destBase\original_paths.csv"

if (!(Test-Path $originalFile)) {
    Write-Host "File original_paths.csv non trovato. Esegui prima cercafoto.ps1"
    exit
}

# Crea cartella Album se non esiste
if (!(Test-Path $albumBase)) {
    New-Item -ItemType Directory -Path $albumBase | Out-Null
} else {
    # Pulisci la cartella Album esistente per evitare duplicati
    Remove-Item -Path "$albumBase\*" -Recurse -Force
}

# Leggi il CSV
$data = Import-Csv -Path $originalFile

# Filtra solo i file copiati
$copiedFiles = $data | Where-Object { $_.Status -eq "copied" -and $_.DestPath -ne "" }

# Raggruppa per Album, escludendo quelli vuoti
$grouped = $copiedFiles | Where-Object { ![string]::IsNullOrWhiteSpace($_.Album) } | Group-Object -Property Album

foreach ($group in $grouped) {
    $albumName = $group.Name

    $albumDir = "$albumBase\$albumName"
    if (!(Test-Path $albumDir)) {
        New-Item -ItemType Directory -Path $albumDir | Out-Null
    }

    foreach ($item in $group.Group) {
        $fileName = Split-Path -Path $item.DestFilename -Leaf
        $linkPath = "$albumDir\$fileName"

        # Gestisci conflitti di nome
        $counter = 1
        $base = [System.IO.Path]::GetFileNameWithoutExtension($fileName)
        $ext = [System.IO.Path]::GetExtension($fileName)
        while (Test-Path $linkPath) {
            $linkPath = "$albumDir\$base`_$counter$ext"
            $counter++
        }

        # Crea hard link
        try {
            New-Item -ItemType HardLink -Path $linkPath -Target "$($item.DestPath)" -ErrorAction Stop | Out-Null
        } catch {
            Write-Host "Errore nel creare link per $($item.DestPath): $($_.Exception.Message)"
        }
    }
}

Write-Host "Ricostruzione album completata in $albumBase"