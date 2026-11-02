REM Utilizzo : crea-iso.cmd <cartella_sorgente> <file_iso_destinazione>
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Destinazione
)

$Url = "https://go.microsoft.com/fwlink/?linkid=2289980"
$Installer = "$env:TEMP\adksetup.exe"
Invoke-WebRequest -Uri $Url -OutFile $Installer
Start-Process -FilePath $Installer `
     -ArgumentList "/quiet /installpath C:\ADK /features OptionId.DeploymentTools" `
     -Wait
$Oscdimg = "C:\ADK\Deployment Tools\amd64\Oscdimg\oscdimg.exe"
$Dir = Split-Path $Oscdimg
[Environment]::SetEnvironmentVariable(
    "Path",
    [Environment]::GetEnvironmentVariable("Path", "Machine") + ";$Dir",
    "Machine"
)

function Rimuovi-Duplicati {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Cartella
    )
    Write-Host "`nRicerca duplicati..." -ForegroundColor Cyan
    $fileEliminati = 0
    [long]$spazioRecuperato = 0
    $files = Get-ChildItem -LiteralPath $Cartella -File -Recurse
    # Raggruppa per nome e dimensione
    $gruppi = $files | Group-Object {
        "$($_.Name)|$($_.Length)"
    }
    foreach ($gruppo in $gruppi) {

        if ($gruppo.Count -lt 2) {
            continue
        }
        # Calcola SHA256 per verificare l'identità
        $hashes = $gruppo.Group |
            Group-Object {
                (Get-FileHash -LiteralPath $_.FullName `
                    -Algorithm SHA256).Hash
            }

        foreach ($hashGroup in $hashes) {

            if ($hashGroup.Count -lt 2) {
                continue
            }

            # Mantiene il primo file
            $duplicati = $hashGroup.Group | Select-Object -Skip 1

            foreach ($file in $duplicati) {

                Write-Host "Duplicato: $($file.FullName)" `
                    -ForegroundColor Yellow

                $spazioRecuperato += $file.Length

                Remove-Item -LiteralPath $file.FullName -Force

                $fileEliminati++
            }
        }
    }

    Write-Host "`nPulizia completata." -ForegroundColor Green
    Write-Host "File eliminati: $fileEliminati"
    Write-Host ("Spazio recuperato: {0:N2} GB" -f `
        ($spazioRecuperato / 1GB))
}

Rimuovi-Duplicati -Cartella $Destinazione

REM oscdimg.exe -m -o -u2 -udfver102 %1 %2