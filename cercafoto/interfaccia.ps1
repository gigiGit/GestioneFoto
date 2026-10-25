# Interfaccia per configurazione ed esecuzione di cercafoto.ps1

param(
    [string]$ConfigFile
)

# Disco del programma (escludere questo disco dalla scansione)
$programDrive = Split-Path -Path $PSScriptRoot -Qualifier

# Cartella di destinazione su disco del programma
#$username = $env:USERNAME
$username = "Patty"
$computerName = $env:COMPUTERNAME
$destBase = "$programDrive\Mediabackup\$username\$computerName"

# Crea la cartella se non esiste
if (!(Test-Path $destBase)) {
    New-Item -ItemType Directory -Path $destBase -Force
}

if (-not $ConfigFile) {
    $ConfigFile = "$destBase\config.txt"
}

# Funzione per caricare configurazione
function Load-Config {
    param([string]$filePath)
    if (Test-Path $filePath) {
        $content = Get-Content $filePath
        $minSize = 1024
        $scanPaths = @()
        $excludes = @()
        foreach ($line in $content) {
            if ($line -match "^MinSize=") {
                $minSize = [int]$line.Split('=')[1]
            } elseif ($line -match "^ScanPath=") {
                $scanPaths += $line.Split('=',2)[1]
            } elseif ($line -notmatch "^#" -and $line -ne "") {
                $excludes += $line
            }
        }
        return @{
            MinSize = $minSize
            ScanPaths = $scanPaths
            Excludes = $excludes
        }
    } else {
        # Create default config.txt
        $defaultContent = @("MinSize=1024", "# Scan paths", "ScanPath=C:\Users\$env:USERNAME\Pictures", "# Excluded folders", "C:\Windows", "C:\Program Files", "C:\Program Files (x86)")
        $defaultContent | Out-File -FilePath $filePath -Encoding UTF8
        return @{
            MinSize = 1024
            ScanPaths = @("C:\Users\$env:USERNAME\Pictures")
            Excludes = @("C:\Windows", "C:\Program Files", "C:\Program Files (x86)")
        }
    }
}

# Funzione per salvare configurazione
function Save-Config {
    param([string]$filePath, [hashtable]$config)
    $content = @("# Configurazione per cercafoto.ps1", "# Dimensione minima del file in byte (file più piccoli vengono scartati)", "MinSize=$($config.MinSize)", "", "# Scan paths")
    foreach ($path in $config.ScanPaths) {
        $content += "ScanPath=$path"
    }
    $content += "", "# Excluded folders"
    foreach ($exclude in $config.Excludes) {
        $content += $exclude
    }
    $content | Out-File -FilePath $filePath -Encoding UTF8
}

# Carica configurazione iniziale
$config = Load-Config -filePath $ConfigFile

# Menu principale
function Show-Menu {
    Clear-Host
    Write-Host "=== Interfaccia per cercafoto.ps1 ==="
    Write-Host "Config file: $ConfigFile"
    Write-Host "Percorsi attuali: $($config.ScanPaths -join ', ')"
    Write-Host "1. Modifica dimensione minima file (attuale: $($config.MinSize))"
    Write-Host "2. Gestisci percorsi di scansione"
    Write-Host "3. Gestisci cartelle escluse"
    Write-Host "4. Salva configurazione"
    Write-Host "5. Esegui cercafoto.ps1"
    Write-Host "6. Esci"
    Write-Host ""
}

# Loop principale
while ($true) {
    Show-Menu
    $choice = Read-Host "Scegli un'opzione"

    switch ($choice) {
        1 {
            $newMinSize = Read-Host "Inserisci nuova dimensione minima (byte)"
            if ($newMinSize -match '^\d+$') {
                $config.MinSize = [int]$newMinSize
                Write-Host "Dimensione minima aggiornata a $($config.MinSize)"
            } else {
                Write-Host "Valore non valido. Deve essere un numero."
            }
            Read-Host "Premi Invio per continuare"
        }
        2 {
            # Sottomenu per scan paths
            while ($true) {
                Clear-Host
                Write-Host "=== Gestisci percorsi di scansione ==="
                Write-Host "Percorsi attuali:"
                for ($i = 0; $i -lt $config.ScanPaths.Count; $i++) {
                    Write-Host "$($i+1). $($config.ScanPaths[$i])"
                }
                Write-Host ""
                Write-Host "a. Aggiungi percorso"
                Write-Host "r. Rimuovi percorso"
                Write-Host "b. Torna al menu principale"
                $subChoice = Read-Host "Scegli"
                if ($subChoice -eq 'a') {
                    $newPath = Read-Host "Inserisci nuovo percorso"
                    if (Test-Path $newPath) {
                        $config.ScanPaths += $newPath
                        Write-Host "Percorso aggiunto."
                    } else {
                        Write-Host "Percorso non valido o non esistente."
                    }
                } elseif ($subChoice -eq 'r') {
                    $index = Read-Host "Inserisci numero del percorso da rimuovere"
                    if ($index -match '^\d+$' -and [int]$index -ge 1 -and [int]$index -le $config.ScanPaths.Count) {
                        $config.ScanPaths = $config.ScanPaths | Where-Object { $_ -ne $config.ScanPaths[[int]$index - 1] }
                        Write-Host "Percorso rimosso."
                    } else {
                        Write-Host "Numero non valido."
                    }
                } elseif ($subChoice -eq 'b') {
                    break
                } else {
                    Write-Host "Scelta non valida."
                }
                Read-Host "Premi Invio per continuare"
            }
        }
        3 {
            # Sottomenu per esclusioni
            while ($true) {
                Clear-Host
                Write-Host "=== Gestisci cartelle escluse ==="
                Write-Host "Cartelle escluse attuali:"
                for ($i = 0; $i -lt $config.Excludes.Count; $i++) {
                    Write-Host "$($i+1). $($config.Excludes[$i])"
                }
                Write-Host ""
                Write-Host "a. Aggiungi esclusione"
                Write-Host "r. Rimuovi esclusione"
                Write-Host "b. Torna al menu principale"
                $subChoice = Read-Host "Scegli"
                if ($subChoice -eq 'a') {
                    $newExclude = Read-Host "Inserisci nuova cartella esclusa"
                    $config.Excludes += $newExclude
                    Write-Host "Esclusione aggiunta."
                } elseif ($subChoice -eq 'r') {
                    $index = Read-Host "Inserisci numero dell'esclusione da rimuovere"
                    if ($index -match '^\d+$' -and [int]$index -ge 1 -and [int]$index -le $config.Excludes.Count) {
                        $config.Excludes = $config.Excludes | Where-Object { $_ -ne $config.Excludes[[int]$index - 1] }
                        Write-Host "Esclusione rimossa."
                    } else {
                        Write-Host "Numero non valido."
                    }
                } elseif ($subChoice -eq 'b') {
                    break
                } else {
                    Write-Host "Scelta non valida."
                }
                Read-Host "Premi Invio per continuare"
            }
        }
        4 {
            Save-Config -filePath $ConfigFile -config $config
            Write-Host "Configurazione salvata."
            Read-Host "Premi Invio per continuare"
        }
        5 {
            Write-Host "Eseguendo cercafoto.ps1..."
            & ".\cercafoto.ps1"
            Write-Host "Esecuzione completata."
            Read-Host "Premi Invio per continuare"
        }
        6 {
            Write-Host "Uscita."
            exit 0
            break
        }
        default {
            Write-Host "Scelta non valida."
            Read-Host "Premi Invio per continuare"
        }
    }
}