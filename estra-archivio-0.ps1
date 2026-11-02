
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Archivio,

    [Parameter(Mandatory = $true)]
    [Alias("d")]
    [string]$Destinazione
)

# Verifica disponibilità di tar
if (-not (Get-Command tar -ErrorAction SilentlyContinue)) {
    Write-Error "Il comando tar non è disponibile."
    exit 1
}

# Crea la cartella di destinazione
if (-not (Test-Path -LiteralPath $Destinazione)) {
    New-Item -ItemType Directory -Path $Destinazione -Force |
        Out-Null
}

# Ricerca degli archivi
$archivi = Get-ChildItem -Path $Archivio -File -ErrorAction SilentlyContinue

if (-not $archivi) {
    Write-Warning "Nessun archivio trovato: $Archivio"
    exit 1
}

$errori = 0

foreach ($file in $archivi) {

    Write-Host "`nEstrazione: $($file.Name)" -ForegroundColor Cyan

    try {
        switch -Regex ($file.Extension.ToLower()) {

            '\.zip' {
                Expand-Archive -LiteralPath $file.FullName `
                    -DestinationPath $Destinazione -Force `
                    -ErrorAction Stop
                break
            }

            '\.tgz' {
                & tar -xzf $file.FullName -C $Destinazione

                if ($LASTEXITCODE -ne 0) {
                    throw "Errore durante l'estrazione TGZ."
                }
                break
            }

            '\.gz' {
                if ($file.Name -match '\.tar\.gz$') {
                    & tar -xzf $file.FullName -C $Destinazione

                    if ($LASTEXITCODE -ne 0) {
                        throw "Errore durante l'estrazione TAR.GZ."
                    }
                }
                else {
                    throw "Formato GZ non supportato."
                }
                break
            }

            default {
                throw "Formato non supportato: $($file.Extension)"
            }
        }

        Write-Host "  OK" -ForegroundColor Green
    }
    catch {
        Write-Host "  ERRORE: $_" -ForegroundColor Red
        $errori++
    }
}

Write-Host "`nEstrazione completata." -ForegroundColor Green
Write-Host "Archivi elaborati: $($archivi.Count)"
Write-Host "Errori: $errori"
