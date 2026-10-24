# Script: Organizza le foto per album creando link simbolici nella cartella AlbumDestinazione
param(
    [string]$DatabasePath,
    [string]$AlbumDestinazione
)
# Verifica parametri
if (-not (Test-Path $DatabasePath)) {
    Write-Host "Database non trovato: $DatabasePath"
    exit 1
}
if (-not (Test-Path $AlbumDestinazione)) {
    New-Item -ItemType Directory -Path $AlbumDestinazione | Out-Null
}
# Recupera tutti i record con album valorizzato
$sqliteCli = Join-Path $PSScriptRoot 'sqlite3.exe'
if (-not (Test-Path -LiteralPath $sqliteCli)) {
    $sqliteCommand = Get-Command 'sqlite3.exe' -ErrorAction SilentlyContinue
    if (-not $sqliteCommand) {
        $sqliteCommand = Get-Command 'sqlite3' -ErrorAction SilentlyContinue
    }
    if (-not $sqliteCommand -or $sqliteCommand.CommandType -ne 'Application') {
        throw "sqlite3 non trovato. Esegui setup\Inizializza-Progetto.ps1 oppure installa il CLI SQLite e aggiungilo al PATH."
    }
    $sqliteCli = $sqliteCommand.Source
}
$sql = "SELECT file, album FROM HASH WHERE album IS NOT NULL AND TRIM(album) != ''"
$records = & $sqliteCli $DatabasePath ".timeout 15000" "$sql" 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "Errore query: $($records -join '; ')"
    exit 2
}
foreach ($rec in $records) {
    $fields = $rec -split '\|'
    if ($fields.Count -ne 2) { continue }
    $file = $fields[0]
    $album = $fields[1].Trim()
    if (-not $album) { continue }
    $albumFolder = Join-Path $AlbumDestinazione $album
    if (-not (Test-Path $albumFolder)) {
        New-Item -ItemType Directory -Path $albumFolder | Out-Null
    }
    $fileName = Split-Path $file -Leaf
    $linkPath = Join-Path $albumFolder $fileName
    # Crea link simbolico se non esiste già
    if (-not (Test-Path $linkPath)) {
        try {
            cmd /c mklink "$linkPath" "$file" | Out-Null
            Write-Host "Creato link: $linkPath -> $file"
            # Imposta la data del link uguale all'originale
            if (Test-Path $file -PathType Leaf) {
                $orig = Get-Item $file
                try {
                    Set-ItemProperty -Path $linkPath -Name CreationTime -Value $orig.CreationTime
                    Set-ItemProperty -Path $linkPath -Name LastWriteTime -Value $orig.LastWriteTime
                } catch {
                    Write-Host "Impossibile impostare data su ${linkPath}: $_"
                }
            }
        } catch {
            Write-Host "Errore creazione link per ${file}: $_"
        }
    }
}
Write-Host "Organizzazione per album completata."
