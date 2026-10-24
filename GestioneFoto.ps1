# GestioneFoto-V3.ps1
function Write-GuiLog_Loc {
    param([int]$LogLevel, [string]$msg,$global:gui)
    # Aggiorna funzione appendLog per salvare nel buffer
    $stamp = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
    $line = "[$LogLevel]-$stamp- $msg"
    #$logFile = Join-Path $PSScriptRoot '..\log.txt'
    #  Add-Content -Path $logFile -Value $line
    
        Write-Host "$line"
    
}

# Carica librerie principali
. "$PSScriptRoot/lib/log.ps1"
. "$PSScriptRoot/lib/config.ps1"
. "$PSScriptRoot/lib/wrapper.ps1"
. "$PSScriptRoot/lib/gui.ps1"

Set-LogLevel-ByName 'DEBUG' # Imposta livello di log per debug iniziale
# Carica configurazione
$global:config = LoadConfig
Write-GuiLog $global:INFO "Configurazione principale caricata."
Write-GuiLog $global:DEBUG "DEBUG Config: $($global:config | Out-String)"
# & $appendLog 'INFO' 'Configurazione caricata.'
# Imposta livello di log da config se presente
if ($global:config.ContainsKey('LogLevel')) {
    Set-LogLevel-ByName $global:config['LogLevel']
}
$global:gui = InitGui $global:config



Write-GuiLog $global:DEBUG "GUI inizializzata. $($global:gui | Out-String)" $global:gui
Write-GuiLog $global:INFO "Inizio GUI" $global:gui

#read-host "Premi Invio per continuare..."
# Avvio GUI e log


# Inizializza interfaccia grafica con il profilo attivo





# Mostra dati iniziali nella GUI in base al profilo attivo
UpdateInfo $global:gui $global:config.ActiveProfile
$global:gui.Form.ShowDialog()


# Eventuale cleanup finale
