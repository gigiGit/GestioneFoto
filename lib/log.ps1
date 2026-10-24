function InitLog{
        param($panel)


    # TextBox multilinea per log
    $global:txtLog = New-Object System.Windows.Forms.TextBox
    $global:txtLog.Multiline = $true
    $global:txtLog.ScrollBars = 'Vertical'
    $global:txtLog.Dock = 'Fill'
    $global:txtLog.ReadOnly = $true
    $global:txtLog.Font = New-Object System.Drawing.Font('Arial', 10)
    $global:txtLog.text = "Log avviato...`r`n"
    $panel.Controls.Add($global:txtLog)


    # Label per info avanzamento
    $global:lblProgressInfo = New-Object System.Windows.Forms.Label
    $global:lblProgressInfo.Dock = 'Bottom'
    $global:lblProgressInfo.TextAlign = 'MiddleCenter'
    $global:lblProgressInfo.Height = 18
    $global:lblProgressInfo.Text = "Nessuna operazione in corso."
    $panel.Controls.Add($global:lblProgressInfo)

    # ProgressBar in fondo al pannello, larga quanto tutta la finestra
    $global:logProgressBar = New-Object System.Windows.Forms.ProgressBar
    $global:logProgressBar.Dock = 'Bottom'
    $global:logProgressBar.Style = 'Continuous'
    $global:logProgressBar.Minimum = 0
    $global:logProgressBar.Maximum = 100
    $global:logProgressBar.Value = 0
    $panel.Controls.Add($global:logProgressBar)

    # Pannello per i pulsanti del log
    $panelLogButtons = New-Object System.Windows.Forms.FlowLayoutPanel
    $panelLogButtons.Dock = 'Right'
    $panelLogButtons.Width = 240
    $panelLogButtons.FlowDirection = 'LeftToRight'
    $panel.Controls.Add($panelLogButtons)

    # Pulsante Clear
    $btnClear = New-Object System.Windows.Forms.Button
    $btnClear.Text = 'Clear'
    $btnClear.Width = 70
    $btnClear.Height = 28
    #$btnClear.Add_Click({ $global:txtLog.Clear() })
    $panelLogButtons.Controls.Add($btnClear)
    
    $btnClear.Add_Click({ Write-GuiLog $global:INFO "Log pulito manualmente."  })
    # Pulsante Prima riga
    $btnFirst = New-Object System.Windows.Forms.Button
    $btnFirst.Text = 'Prima riga'

    

    # TextBox per filtro log
    $txtFilter = New-Object System.Windows.Forms.TextBox
    $txtFilter.Width = 180
    $txtFilter.Dock = 'Left'
    $panelLogButtons.Controls.Add($txtFilter)

    # Buffer per tutte le righe di log
    $logBuffer = New-Object System.Collections.Generic.List[string]

    # Funzione di filtro log
    $applyFilter = {
        $filter = $txtFilter.Text
        if ([string]::IsNullOrWhiteSpace($filter)) {
            $global:txtLog.Lines = $logBuffer.ToArray()
        }
        else {
            $global:txtLog.Lines = $logBuffer | Where-Object { $_ -like "*${filter}*" }
        }
        $global:txtLog.SelectionStart = $global:txtLog.TextLength
        $global:txtLog.ScrollToCaret()
    }
    $txtFilter.Add_TextChanged($applyFilter)
   
    # Thread per leggere dalla pipe e aggiornare la GUI
   return $global:txtLog
}
# Avvia una finestra di log separata e si mette in ascolto su una named pipe
function Start-LogWindow {
    
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
    $form = New-Object System.Windows.Forms.Form
    $form.Text = "Log GestioneFoto"
    $form.Size = New-Object System.Drawing.Size(800, 300)
    $form.StartPosition = 'CenterScreen'
    $form.TopMost = $true
    InitLog $form

    $form.ShowDialog()
}
# lib/log.ps1
function Write-GuiLog {
    param($level, $message)
    # Assicura che LogLevel sia numerico
    if (-not ($global:LogLevel -is [int])) {
        $parsed = 0
        [void][int]::TryParse($global:LogLevel, [ref]$parsed)
        $global:LogLevel = $parsed
    }
    # Assicura che anche $level sia numerico
    if (-not ($level -is [int])) {
        $parsedLevel = 0
        [void][int]::TryParse($level, [ref]$parsedLevel)
        $level = $parsedLevel
    }
    if ($level -le $global:LogLevel) {
        $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        $line = "[$timestamp] [$($level.ToString())] $message"
        # Aggiungi al buffer completo
        if ($global:txtLog) {
            if ($global:txtLog.InvokeRequired) {
                $null = $global:txtLog.Invoke([Action]{ $global:txtLog.AppendText("$line`r`n") })
            } else {
                $global:txtLog.AppendText("$line`r`n")
            }
        } else {
            Write-Host "$line"
        }
    }
}

# Livelli di log disponibili

$global:DEBUG=100
$global:INFO=50
$global:WARNING=20
#$global:ERROR=10

$global:LogLevel = $global:WARNING

$global:LogGuiWriter = $null

function Set-LogGuiWriter {
    param($writer)
    $global:LogGuiWriter = $writer
}

function Set-LogLevel {
    param([int]$Level)
    $global:LogLevel = $Level
}

function Set-LogLevel-ByName {
    param([String]$Level)
    if($Level -eq'DEBUG') {
        $global:LogLevel = 100
    } elseif ($Level -eq 'INFO') {
        $global:LogLevel = 50
    } elseif ($Level -eq 'WARNING') {
        $global:LogLevel = 20
    } elseif ($Level -eq 'ERROR') {
        $global:LogLevel = 10
    } else {
        throw "Livello di log sconosciuto: $Level"
    }
}

#Start-Process powershell -ArgumentList "-NoExit", "-Command", ". 'Start-LogWindow"
#Start-LogWindow