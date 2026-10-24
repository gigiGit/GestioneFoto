# gui-v2.ps1

# Finestra principale con TabPanel in alto, pannello centrale e pannello inferiore
function ListaProfili{
    $configDir = Join-Path $PSScriptRoot "..\config"
    $profileFiles = Get-ChildItem -Path $configDir -Filter '*.profile' | Sort-Object Name
    $profileNames = @()
    foreach ($file in $profileFiles) {
        $name = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
        $profileNames += $name
    }
    return $profileNames
}
function EnableProfileButtons {
    $global:btnSaveProfile.Enabled = $true
    $global:btnReloadProfile.Enabled = $true
    $global:btnSaveAsProfile.Enabled = $true
}

function DisableProfileButtons {
    $global:btnSaveProfile.Enabled = $false
    $global:btnReloadProfile.Enabled = $false
    #$global:btnSaveAsProfile.Enabled = $false
}

function InitCenterPanel {
    # Logica pulsante Salva Profilo come...
    param([System.Windows.Forms.Panel]$panelCenter,$config)
    # --- Pannello pulsanti profilo ---

    $panelProfileButtons = New-Object System.Windows.Forms.FlowLayoutPanel
    $panelProfileButtons.Dock = 'Bottom'
    $panelProfileButtons.Height = 40
    $panelProfileButtons.FlowDirection = 'LeftToRight'
    $panelProfileButtons.WrapContents = $false
    $panelProfileButtons.Padding = '10,5,0,0'

    $global:btnSaveProfile = New-Object System.Windows.Forms.Button
    $global:btnSaveProfile.Text = 'Salva Profilo'
    $global:btnSaveProfile.Width = 120
    $global:btnSaveProfile.Enabled = $false
    $panelProfileButtons.Controls.Add($global:btnSaveProfile)

    $global:btnReloadProfile = New-Object System.Windows.Forms.Button
    $global:btnReloadProfile.Text = 'Rileggi Profilo'
    $global:btnReloadProfile.Width = 120
    $global:btnReloadProfile.Enabled = $false
    $panelProfileButtons.Controls.Add($global:btnReloadProfile)

    $global:btnSaveAsProfile = New-Object System.Windows.Forms.Button
    $global:btnSaveAsProfile.Text = 'Salva Profilo come...'
    $global:btnSaveAsProfile.Width = 160
    $global:btnSaveAsProfile.Enabled = $false
    $panelProfileButtons.Controls.Add($global:btnSaveAsProfile)

    $panelCenter.Controls.Add($panelProfileButtons)
    

    # Esempio: aggiungi una label di test al centro
    $startY=50
    $labelFont = New-Object System.Drawing.Font('Arial', 10, [System.Drawing.FontStyle]::Regular)
    $labelCenter = New-Object System.Windows.Forms.Label
    $labelCenter.Text = "Profilo"
    $labelCenter.Font = $labelFont
    $labelCenter.AutoSize = $true
    $labelCenter.Location = New-Object System.Drawing.Point(30, $startY)
    $panelCenter.Controls.Add($labelCenter)

    # ComboBox profili
    $global:comboProfiles = New-Object System.Windows.Forms.ComboBox
    $global:comboProfiles.Width = 180
    $global:comboProfiles.DropDownStyle = 'DropDownList'
    # Calcola la posizione Y per centrare verticalmente la ComboBox rispetto alla label
    $global:comboProfiles.Location = New-Object System.Drawing.Point(($labelCenter.Location.X + $labelCenter.Width + 20), $startY)
    # Carica dinamicamente i profili dalla cartella config
    $profileNames = ListaProfili
    $global:comboProfiles.Items.Clear()
    $global:comboProfiles.Items.AddRange($profileNames)
    # Se esiste $config.ActiveProfile e il valore è tra i profili, selezionalo
    if ($config -and $config.ActiveProfile) {
        $idx = $profileNames.IndexOf($config.ActiveProfile)
        if ($idx -ge 0) {
            $global:comboProfiles.SelectedIndex = $idx
        } else {
            $global:comboProfiles.SelectedIndex = 0
        }
    } else {
        $global:comboProfiles.SelectedIndex = 0
    }

    $panelCenter.Controls.Add($global:comboProfiles)
    # --- Altre coppie etichetta:controllo statiche ---
    $startY = $startY+35
    $labelWidth = 150
    $boxWidth = 220
    $col1X = 30
    $col2X = $col1X + $labelWidth + $boxWidth + 40

    # Riga 1: UserName, IncludePath
    $labelUser = New-Object System.Windows.Forms.Label
    $labelUser.Text = "UserName"
    $labelUser.Font = $labelFont
    $labelUser.AutoSize = $true
    $labelUser.Location = New-Object System.Drawing.Point([int]$col1X, [int]$startY)
    $panelCenter.Controls.Add($labelUser)
    $global:txtUser = New-Object System.Windows.Forms.TextBox
    $global:txtUser.Width = $boxWidth
    $global:txtUser.Location = New-Object System.Drawing.Point([int]($col1X + $labelWidth), [int]($startY - 2))
    $panelCenter.Controls.Add($global:txtUser)

    $labelInclude = New-Object System.Windows.Forms.Label
    $labelInclude.Text = "IncludePath"
    $labelInclude.Font = $labelFont
    $labelInclude.AutoSize = $true
    $labelInclude.Location = New-Object System.Drawing.Point([int]$col2X, [int]$startY)
    $panelCenter.Controls.Add($labelInclude)
    $global:txtInclude = New-Object System.Windows.Forms.TextBox
    $global:txtInclude.Name = "txtInclude"
    $global:txtInclude.Width = $boxWidth - 30
    $global:txtInclude.Location = New-Object System.Drawing.Point([int]($col2X + $labelWidth), [int]($startY - 2))
    $panelCenter.Controls.Add($global:txtInclude)

    $btnInclude = New-Object System.Windows.Forms.Button
    $btnInclude.Text = '...'
    $btnInclude.Width = 28
    $btnInclude.Height = $global:txtInclude.Height
    $btnInclude.Location = New-Object System.Drawing.Point([int]($global:txtInclude.Location.X + $global:txtInclude.Width + 2), [int]$global:txtInclude.Location.Y)
    $btnInclude.Add_Click({
            $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
            if ($dialog.ShowDialog() -eq 'OK') {
                $global:txtInclude.Text = $dialog.SelectedPath
            }
        })
    $panelCenter.Controls.Add($btnInclude)

    # Riga 2: ExcludePaths, Extensions
    $row2Y = $startY + 35
    $labelExclude = New-Object System.Windows.Forms.Label
    $labelExclude.Text = "ExcludePaths"
    $labelExclude.Font = $labelFont
    $labelExclude.AutoSize = $true
    $labelExclude.Location = New-Object System.Drawing.Point($col1X, $row2Y)
    $panelCenter.Controls.Add($labelExclude)

    $global:txtExclude = New-Object System.Windows.Forms.TextBox
    $global:txtExclude.Width = $boxWidth - 30
    $global:txtExclude.Location = New-Object System.Drawing.Point([int]($col1X + $labelWidth), [int]($row2Y - 2))
    $panelCenter.Controls.Add($global:txtExclude)

    $btnExclude = New-Object System.Windows.Forms.Button
    $btnExclude.Text = '...'
    $btnExclude.Width = 28
    $btnExclude.Height = $global:txtExclude.Height
    $btnExclude.Location = New-Object System.Drawing.Point([int]($global:txtExclude.Location.X + $global:txtExclude.Width + 2), [int]$global:txtExclude.Location.Y)
    $btnExclude.Add_Click({
            $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
            if ($dialog.ShowDialog() -eq 'OK') {
                $global:txtExclude.Text = $dialog.SelectedPath
            }
        })
    $panelCenter.Controls.Add($btnExclude)

    $labelExt = New-Object System.Windows.Forms.Label
    $labelExt.Text = "Extensions"
    $labelExt.Font = $labelFont
    $labelExt.AutoSize = $true
    $labelExt.Location = New-Object System.Drawing.Point($col2X, $row2Y)
    $panelCenter.Controls.Add($labelExt)
    $global:txtExt = New-Object System.Windows.Forms.TextBox
    $global:txtExt.Width = $boxWidth
    $global:txtExt.Location = New-Object System.Drawing.Point([int]($col2X + $labelWidth), [int]($row2Y - 2))
    $panelCenter.Controls.Add($global:txtExt)

    # Riga 3: DestinationRoot, DatabasePath
    $row3Y = $row2Y + 35
    $labelDestRoot = New-Object System.Windows.Forms.Label
    $labelDestRoot.Text = "DestinationRoot"
    $labelDestRoot.Font = $labelFont
    $labelDestRoot.AutoSize = $true
    $labelDestRoot.Location = New-Object System.Drawing.Point($col1X, $row3Y)
    $panelCenter.Controls.Add($labelDestRoot)

    $global:txtDestRoot = New-Object System.Windows.Forms.TextBox
    $global:txtDestRoot.Width = $boxWidth - 30
    $global:txtDestRoot.Location = New-Object System.Drawing.Point([int]($col1X + $labelWidth), [int]($row3Y - 2))
    $panelCenter.Controls.Add($global:txtDestRoot)

    $btnDestRoot = New-Object System.Windows.Forms.Button
    $btnDestRoot.Text = '...'
    $btnDestRoot.Width = 28
    $btnDestRoot.Height = $global:txtDestRoot.Height
    $btnDestRoot.Location = New-Object System.Drawing.Point([int]($global:txtDestRoot.Location.X + $global:txtDestRoot.Width + 2), [int]$global:txtDestRoot.Location.Y)
    $btnDestRoot.Add_Click({
            $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
            if ($dialog.ShowDialog() -eq 'OK') {
                $global:txtDestRoot.Text = $dialog.SelectedPath
            }
        })
    $panelCenter.Controls.Add($btnDestRoot)

    $labelDbPath = New-Object System.Windows.Forms.Label
    $labelDbPath.Text = "DatabasePath"
    $labelDbPath.Font = $labelFont
    $labelDbPath.AutoSize = $true
    $labelDbPath.Location = New-Object System.Drawing.Point($col2X, $row3Y)
    $panelCenter.Controls.Add($labelDbPath)

    $global:txtDbPath = New-Object System.Windows.Forms.TextBox
    $global:txtDbPath.Width = $boxWidth - 30
    $global:txtDbPath.Location = New-Object System.Drawing.Point([int]($col2X + $labelWidth), [int]($row3Y - 2))
    $panelCenter.Controls.Add($global:txtDbPath)

    $btnDbPath = New-Object System.Windows.Forms.Button
    $btnDbPath.Text = '...'
    $btnDbPath.Width = 28
    $btnDbPath.Height = $global:txtDbPath.Height
    $btnDbPath.Location = New-Object System.Drawing.Point([int]($global:txtDbPath.Location.X + $global:txtDbPath.Width + 2), [int]$global:txtDbPath.Location.Y)
    $btnDbPath.Add_Click({
            $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
            if ($dialog.ShowDialog() -eq 'OK') {
                $global:txtDbPath.Text = $dialog.SelectedPath
            }
        })
    $panelCenter.Controls.Add($btnDbPath)

    # Riga 4: DestinationTemplate
    $row4Y = $row3Y + 35
    $labelDestTpl = New-Object System.Windows.Forms.Label
    $labelDestTpl.Text = "DestinationTemplate"
    $labelDestTpl.Font = $labelFont
    $labelDestTpl.AutoSize = $true
    $labelDestTpl.Location = New-Object System.Drawing.Point($col1X, $row4Y)
    $panelCenter.Controls.Add($labelDestTpl)
    $global:comboDestTpl = New-Object System.Windows.Forms.ComboBox
    $global:comboDestTpl.Width = $boxWidth
    $global:comboDestTpl.DropDownStyle = 'DropDownList'
    $global:comboDestTpl.Items.AddRange(@('YYYY/MM', 'YYYY/MM/DD'))
    $global:comboDestTpl.SelectedIndex = 0
    $global:comboDestTpl.Location = New-Object System.Drawing.Point([int]($col1X + $labelWidth), [int]($row4Y - 2))
    $panelCenter.Controls.Add($global:comboDestTpl)

        
    $labelMinSize = New-Object System.Windows.Forms.Label
    $labelMinSize.Text = "Min Size"
    $labelMinSize.Font = $labelFont
    $labelMinSize.AutoSize = $true
    $labelMinSize.Location = New-Object System.Drawing.Point([int]$col2X, [int]$row4Y)
    $panelCenter.Controls.Add($labelMinSize)
    $global:txtMinSize = New-Object System.Windows.Forms.TextBox
    $global:txtMinSize.Width = 80
    $global:txtMinSize.Location = New-Object System.Drawing.Point([int]($col2X + $labelWidth), [int]($row4Y - 2))
    $panelCenter.Controls.Add($global:txtMinSize)


    # Aggiungi evento TextChanged a tutti i controlli di input
    $global:txtUser.Add_TextChanged({ EnableProfileButtons })
    $global:txtInclude.Add_TextChanged({ EnableProfileButtons })
    $global:txtExclude.Add_TextChanged({ EnableProfileButtons })
    $global:txtExt.Add_TextChanged({ EnableProfileButtons })
    $global:txtDestRoot.Add_TextChanged({ EnableProfileButtons })
    $global:txtDbPath.Add_TextChanged({ EnableProfileButtons })
    $global:txtMinSize.Add_TextChanged({ EnableProfileButtons })
    $global:comboDestTpl.Add_SelectedIndexChanged({ EnableProfileButtons })
    $btnSaveProfile.Add_Click({
            # Salva i dati correnti nei controlli nel file del profilo attivo
        $btn3.Add_Click({
            $profilo = Read-Profile -ProfileName $global:comboProfiles.SelectedItem
            $worker = New-Object System.ComponentModel.BackgroundWorker
            $worker.WorkerReportsProgress = $true

            # Evento ProgressChanged: aggiorna la progress bar e la label
            $worker.add_ProgressChanged({
                param($sender, $e)
                if ($global:progressBar -and $global:progressLabel) {
                    $global:progressBar.Value = $e.ProgressPercentage
                    $global:progressLabel.Text = $e.UserState
                }
                Write-GuiLog $global:INFO "Avanzamento: $($e.ProgressPercentage)% - $($e.UserState)"
            })

            # Evento DoWork: esegue il lavoro in background
            $worker.add_DoWork({
                param($sender, $e)
                $totale = 10
                for ($i = 1; $i -le $totale; $i++) {
                    Start-Sleep -Milliseconds 200
                    $percent = [int](($i / $totale) * 100)
                    $sender.ReportProgress($percent, "Copia file $i di $totale")
                }
                # Qui puoi chiamare Wrapper-Copia $global:gui $profilo al posto del ciclo di test
                # Wrapper-Copia $global:gui $profilo
            })

            $worker.RunWorkerAsync()
            Write-GuiLog $global:INFO "BackgroundWorker avviato per Copia."
        })
            Write-GuiLog $global:INFO "Profilo '$activeProfile' salvato con successo."
            DisableProfileButtons
        })
        $btnReloadProfile.Add_Click({
            $selectedProfile = $global:comboProfiles.SelectedItem
            if (-not $selectedProfile) {
                Write-GuiLog $global:ERROR "Nessun profilo selezionato per il reload."
                return
            }
            UpdateInfo $global:gui $selectedProfile
            Write-GuiLog $global:INFO "Profilo '$selectedProfile' ricaricato con successo."
            DisableProfileButtons
        })
    $btnSaveAsProfile.Add_Click({
            # Chiedi nuovo nome profilo
            $inputDialog = New-Object System.Windows.Forms.Form
            $inputDialog.Text = 'Nuovo nome profilo'
            $inputDialog.Width = 350
            $inputDialog.Height = 140
            $inputDialog.StartPosition = 'CenterParent'
            $lbl = New-Object System.Windows.Forms.Label
            $lbl.Text = 'Nome nuovo profilo:'
            $lbl.AutoSize = $true
            $lbl.Location = New-Object System.Drawing.Point(10, 15)
            $inputDialog.Controls.Add($lbl)
            $txt = New-Object System.Windows.Forms.TextBox
            $txt.Width = 200
            $txt.Location = New-Object System.Drawing.Point(130, 12)
            $inputDialog.Controls.Add($txt)
            $okBtn = New-Object System.Windows.Forms.Button
            $okBtn.Text = 'OK'
            $okBtn.Width = 60
            $okBtn.Location = New-Object System.Drawing.Point(80, 50)
            $okBtn.DialogResult = [System.Windows.Forms.DialogResult]::OK
            $inputDialog.AcceptButton = $okBtn
            $inputDialog.Controls.Add($okBtn)
            $cancelBtn = New-Object System.Windows.Forms.Button
            $cancelBtn.Text = 'Annulla'
            $cancelBtn.Width = 60
            $cancelBtn.Location = New-Object System.Drawing.Point(160, 50)
            $cancelBtn.DialogResult = [System.Windows.Forms.DialogResult]::Cancel
            $inputDialog.CancelButton = $cancelBtn
            $inputDialog.Controls.Add($cancelBtn)
            if ($inputDialog.ShowDialog() -eq 'OK' -and -not [string]::IsNullOrWhiteSpace($txt.Text)) {
                $newProfileName = $txt.Text.Trim()
                # Costruisci path nuovo file profilo
                $configDir = Join-Path $PSScriptRoot "..\config"
                $newProfilePath = Join-Path $configDir ("$newProfileName.profile")
                # Salva i dati correnti nei controlli nel nuovo file
                $lines = @(
                    "ProfileName=$newProfileName"
                    "MinSize=$($global:txtMinSize.Text)"
                    "UserName=$($global:txtUser.Text)"
                    "IncludePath=$($global:txtInclude.Text)"
                    "ExcludePaths=$($global:txtExclude.Text)"
                    "Extensions=$($global:txtExt.Text)"
                    "DestinationRoot=$($global:txtDestRoot.Text)"
                    "DatabasePath=$($global:txtDbPath.Text)"
                    "DestinationTemplate=$($global:comboDestTpl.SelectedItem)"
                )
                Set-Content -Path $newProfilePath -Value $lines -Encoding UTF8
                Write-GuiLog $global:INFO "Profilo salvato come $newProfileName ($newProfilePath)"
                DisableProfileButtons
                # Aggiorna la lista dei profili nella combo
                $profileFiles = Get-ChildItem -Path $configDir -Filter '*.profile' | Sort-Object Name
                $profileNames =ListaProfili
                $global:comboProfiles.Items.Clear()
                $global:comboProfiles.Items.AddRange($profileNames)
                $global:comboProfiles.SelectedIndex = $profileNames.IndexOf($newProfileName)
            }
        })


    UpdateInfo $global:gui  $global:comboProfiles.SelectedItem
    DisableProfileButtons
    # Evento: aggiorna i controlli al cambio profilo
    $global:comboProfiles.Add_SelectedIndexChanged({
            $selectedProfile = $global:comboProfiles.SelectedItem
            Write-GuiLog $global:INFO "Profilo selezionato: $selectedProfile"
            UpdateInfo $global:gui $selectedProfile
        })
}

function Show-UtilityMenu {
    param($gui, $profilo)
    Write-GuiLog $global:DEBUG "[Show-UtilityMenu] Menu Utility avviato."
    
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
    
    # Crea la form del menu utility
    $form = New-Object System.Windows.Forms.Form
    $form.Text = "Menu Utility - Gestione Foto"
    $form.Size = New-Object System.Drawing.Size(400, 320)
    $form.StartPosition = "CenterScreen"
    $form.FormBorderStyle = "FixedDialog"
    $form.MaximizeBox = $false
    $form.MinimizeBox = $false
    
    # Label titolo
    $labelTitle = New-Object System.Windows.Forms.Label
    $labelTitle.Text = "Utility - Gestione Database"
    $labelTitle.Font = New-Object System.Drawing.Font('Arial', 12, [System.Drawing.FontStyle]::Bold)
    $labelTitle.Location = New-Object System.Drawing.Point(20, 20)
    $labelTitle.Size = New-Object System.Drawing.Size(360, 30)
    $form.Controls.Add($labelTitle)
    
    # Pulsante Controllo (Rebuild Hash)
    $btnControl = New-Object System.Windows.Forms.Button
    $btnControl.Text = "Controllo - Ricostruisci Tabella Hash"
    $btnControl.Location = New-Object System.Drawing.Point(20, 70)
    $btnControl.Size = New-Object System.Drawing.Size(360, 40)
    $btnControl.Font = New-Object System.Drawing.Font('Arial', 10)
    $btnControl.Add_Click({
        if (-not $profilo) {
            [System.Windows.Forms.MessageBox]::Show("Profilo non specificato!", "Errore", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error)
            return
        }
        Write-GuiLog $global:INFO "Avvio ricostruzione hash da DestinationRoot..."
        Rebuild-HashDatabase $global:gui $profilo
        Write-GuiLog $global:INFO "Ricostruzione hash completata."
        [System.Windows.Forms.MessageBox]::Show("Ricostruzione tabella hash completata. Verifica il log per i dettagli.", "Successo", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information)
    })
    $form.Controls.Add($btnControl)
    
    # Pulsante Ricalcola EXIF
    $btnRecalcExif = New-Object System.Windows.Forms.Button
    $btnRecalcExif.Text = "Ricalcola - Metadati EXIF Mancanti"
    $btnRecalcExif.Location = New-Object System.Drawing.Point(20, 125)
    $btnRecalcExif.Size = New-Object System.Drawing.Size(360, 40)
    $btnRecalcExif.Font = New-Object System.Drawing.Font('Arial', 10)
    $btnRecalcExif.Add_Click({
        if (-not $profilo) {
            [System.Windows.Forms.MessageBox]::Show("Profilo non specificato!", "Errore", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error)
            return
        }
        Write-GuiLog $global:INFO "Avvio ricalcolo metadati EXIF..."
        Recalculate-ExifDates $global:gui $profilo
        Write-GuiLog $global:INFO "Ricalcolo EXIF completato."
        [System.Windows.Forms.MessageBox]::Show("Ricalcolo metadati EXIF completato. Verifica il log per i dettagli.", "Successo", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information)
    })
    $form.Controls.Add($btnRecalcExif)
    
    # Pulsante Esci
    $btnExit = New-Object System.Windows.Forms.Button
    $btnExit.Text = "Esci"
    $btnExit.Location = New-Object System.Drawing.Point(20, 180)
    $btnExit.Size = New-Object System.Drawing.Size(360, 40)
    $btnExit.Font = New-Object System.Drawing.Font('Arial', 10)
    $btnExit.Add_Click({
        $form.Close()
    })
    $form.Controls.Add($btnExit)
    
    # Mostra la form
    $form.ShowDialog() | Out-Null
    Write-GuiLog $global:DEBUG "[Show-UtilityMenu] Menu Utility chiuso."
}

function InitGui {
    param($config)
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
    $global:font = New-Object System.Drawing.Font('Segoe UI', 10, [System.Drawing.FontStyle]::Bold)

    # Crea la finestra principale
    $form = New-Object System.Windows.Forms.Form
    $form.Text = "GestioneFoto - V2"
    $form.Size = New-Object System.Drawing.Size(900, 600)
    $form.StartPosition = 'CenterScreen'
    $form.MinimumSize = New-Object System.Drawing.Size(600, 400)
    $form.MaximizeBox = $true
    $form.FormBorderStyle = 'Sizable'

    # Crea la finestra principale
    $form = New-Object System.Windows.Forms.Form
    $form.Text = "GestioneFoto - V2"
    $form.Size = New-Object System.Drawing.Size(900, 600)
    $form.StartPosition = 'CenterScreen'
    $form.MinimumSize = New-Object System.Drawing.Size(600, 400)
    $form.MaximizeBox = $true
    $form.FormBorderStyle = 'Sizable'


    # Pannello superiore (TOP) con TabControl
    $panelTop = New-Object System.Windows.Forms.Panel
    $panelTop.Dock = 'Top'
    $panelTop.Height = 35
    $panelTop.BorderStyle = [System.Windows.Forms.BorderStyle]::FixedSingle

    # Pulsanti Normali
    $btn1 = New-Object System.Windows.Forms.Button
    $btn1.Text = "Scansione"
    $btn1.Width = 110
    $btn1.Height = 32
    $btn1.Dock = 'Left'
    $btn1.Add_Click({
        $profilo = Read-Profile -ProfileName $global:comboProfiles.SelectedItem
        Start-Scansione $global:gui $profilo
    })
    $btn2 = New-Object System.Windows.Forms.Button
    $btn2.Text = "Visualizza"
    $btn2.Width = 110
    $btn2.Height = 32
    $btn2.Dock = 'Left'
    $btn2.Add_Click({
               $profilo = Read-Profile -ProfileName $global:comboProfiles.SelectedItem
                Wrapper-Visualizza  $global:gui $profilo
            })

    $btn3 = New-Object System.Windows.Forms.Button
    $btn3.Text = "Copia"
    $btn3.Width = 110
    $btn3.Height = 32
    $btn3.Dock = 'Left'
    $btn3.Add_Click({
                $profilo = Read-Profile -ProfileName $global:comboProfiles.SelectedItem
                Wrapper-Copia  $global:gui $profilo
            })
 $btnExit = New-Object System.Windows.Forms.Button
    $btnExit.Text = "Esci"
    $btnExit.Width = 110
    $btnExit.Height = 32
    $btnExit.Dock = 'Left'
    $btnExit.Add_Click({
                 $profilo = Read-Profile -ProfileName $global:comboProfiles.SelectedItem
                Close-Exit   $global:gui $profilo
            })

 $btn5 = New-Object System.Windows.Forms.Button
    $btn5.Text = "Album"
    $btn5.Width = 110
    $btn5.Height = 32
    $btn5.Dock = 'Left'
    $btn5.Add_Click({
              $profilo = Read-Profile -ProfileName $global:comboProfiles.SelectedItem
              Wrapper-Album $global:gui $profilo
            })

 $btnUtility = New-Object System.Windows.Forms.Button
    $btnUtility.Text = "Utility"
    $btnUtility.Width = 110
    $btnUtility.Height = 32
    $btnUtility.Dock = 'Left'
    $btnUtility.Add_Click({
              $profilo = Read-Profile -ProfileName $global:comboProfiles.SelectedItem
              Show-UtilityMenu $global:gui $profilo
            })

 $btnHelp = New-Object System.Windows.Forms.Button
    $btnHelp.Text = "Help"
    $btnHelp.Width = 110
    $btnHelp.Height = 32
    $btnHelp.Dock = 'Left'
    $btnHelp.Add_Click({
              $profilo = Read-Profile -ProfileName $global:comboProfiles.SelectedItem
              Wrapper-Help $global:gui
            })
    $panelTop.Controls.Add($btnHelp)
    $panelTop.Controls.Add($btnExit)
    $panelTop.Controls.Add($btnUtility)
    $panelTop.Controls.Add($btn5)
    $panelTop.Controls.Add($btn3)
    $panelTop.Controls.Add($btn2)
    $panelTop.Controls.Add($btn1)



    # Pannello centrale (CENTER)
    $panelCenter = New-Object System.Windows.Forms.Panel
    $panelCenter.Dock = 'Fill'
    $panelCenter.MinimumSize = New-Object System.Drawing.Size(600, 300)
    $panelCenter.BorderStyle = [System.Windows.Forms.BorderStyle]::FixedSingle
    $panelCenter.BackColor = [System.Drawing.Color]::WhiteSmoke
    InitCenterPanel $panelCenter $config
    # Pannello in basso (BOTTOM)
    $panelBottom = New-Object System.Windows.Forms.Panel
    $panelBottom.Dock = 'Bottom'
     $panelBottom.Height = 400
    $panelBottom.BorderStyle = [System.Windows.Forms.BorderStyle]::FixedSingle
    $panelBottom.BackColor = [System.Drawing.Color]::WhiteSmoke
    $Log=InitLog $panelBottom

    # Aggiungi i pannelli al form
    $form.Controls.Add($panelTop)
    $form.Controls.Add($panelCenter)
    $form.Controls.Add($panelBottom)

    # Mostra la finestra
    
    return @{ Form = $form; PanelCenter = $panelCenter ; Log = $Log}
}

function UpdateInfo {
    param($gui, $ProfileName)
    Write-GuiLog $global:INFO "Aggiorna GUI per profilo: $ProfileName"
    
    $prf = Read-Profile -ProfileName $ProfileName
    Write-GuiLog $global:INFO "Caricato profilo: $ProfileName"
    Write-GuiLog $global:INFO "UserName: $($prf.UserName)"
    Write-GuiLog $global:INFO "IncludePath: $($prf.IncludePath)"
    Write-GuiLog $global:INFO "DestinationRoot: $($prf.DestinationRoot)"
    Write-GuiLog $global:INFO "DatabasePath: $($prf.DatabasePath)"
    Write-GuiLog $global:INFO "ExcludePaths: $($prf.ExcludePaths)"
    Write-GuiLog $global:INFO "Extensions: $($prf.Extensions)"
    Write-GuiLog $global:INFO "DestinationTemplate: $($prf.DestinationTemplate)"
    Write-GuiLog $global:INFO "MinSize: $($prf.MinSize)"

    # Aggiorna tutti i controlli della GUI
    $global:txtUser.Text = $prf.UserName
    $global:txtInclude.Text = $prf.IncludePath
    $global:txtExclude.Text = $prf.ExcludePaths
    $global:txtExt.Text = $prf.Extensions
    $global:txtDestRoot.Text = $prf.DestinationRoot
    $global:txtDbPath.Text = $prf.DatabasePath
    if ($prf.DestinationTemplate -eq 'YYYY/MM/DD') {
        $global:comboDestTpl.SelectedIndex = 1
    }
    else {
        $global:comboDestTpl.SelectedIndex = 0
    }
    $global:txtMinSize.Text = $prf.MinSize
    #Write-GuiLog $global:INFO "Controlli GUI aggiornati con i valori del profilo."
    #Write-GuiLog $global:INFO "DEBUG GUI: txtUser.Text=$($global:txtUser.Text)"
    #Write-GuiLog $global:INFO "DEBUG GUI: txtInclude.Text=$($global:txtInclude.Text)"
    #Write-GuiLog $global:INFO "DEBUG GUI: txtExclude.Text=$($global:txtExclude.Text)"
    #Write-GuiLog $global:INFO "DEBUG GUI: txtExt.Text=$($global:txtExt.Text)"
    #Write-GuiLog $global:INFO "DEBUG GUI: txtDestRoot.Text=$($global:txtDestRoot.Text)"
    #Write-GuiLog $global:INFO "DEBUG GUI: txtDbPath.Text=$($global:txtDbPath.Text)"
    #Write-GuiLog $global:INFO "DEBUG GUI: comboDestTpl.SelectedIndex=$($global:comboDestTpl.SelectedIndex)"
#    Write-GuiLog $global:INFO "DEBUG GUI: txtMinSize.Text=$($global:txtMinSize.Text)"
}


function Wait-GuiButtonPress {
    param($gui)
    # TODO: implementazione reale
    return 'Esci' # placeholder: termina subito
}





#$gui=InitGui