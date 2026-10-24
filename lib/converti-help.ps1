# Script per convertire i file .md di help in HTML usando la versione locale di Pandoc

$PandocExe = Join-Path $PSScriptRoot 'pandoc/pandoc.exe'
if (-not (Test-Path $PandocExe)) {
    $pandocCommand = Get-Command 'pandoc.exe' -ErrorAction SilentlyContinue
    if (-not $pandocCommand) {
        $pandocCommand = Get-Command 'pandoc' -ErrorAction SilentlyContinue
    }
    if (-not $pandocCommand -or $pandocCommand.CommandType -ne 'Application') {
        throw "Pandoc non trovato. Esegui setup\Inizializza-Progetto.ps1 oppure installalo con winget."
    }
    $PandocExe = $pandocCommand.Source
}

$mdFiles = @(
    '..\doc\GUIDA_UTENTE_BREVE.md',
    '..\doc\DOCUMENTAZIONE_COMPLETA.md',
    '..\doc\README.md'
    # aggiungi qui altri file markdown da convertire se necessario
)

foreach ($md in $mdFiles) {
    $mdPath = Join-Path $PSScriptRoot $md
    if (Test-Path $mdPath) {
        $htmlPath = [System.IO.Path]::ChangeExtension($mdPath, '.html')
        & $PandocExe $mdPath -o $htmlPath
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Convertito: $md -> $htmlPath"
        } else {
            Write-Host "ERRORE nella conversione di $md"
        }
    } else {
        Write-Host "File non trovato: $mdPath"
    }
}
