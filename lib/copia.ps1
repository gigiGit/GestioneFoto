function Wrapper-Copia-Gui {
    param($profilo)


    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
    [System.Windows.Forms.Application]::EnableVisualStyles()

    # Variabili di controllo globali
    $global:taskPaused = $false
    $global:taskStopped = $false
    $global:taskRestart = $false

    # Finestra di dialogo
    $form = New-Object System.Windows.Forms.Form
    $form.Text = "Copia avanzata - GestioneFoto"
    $form.Size = New-Object System.Drawing.Size(500, 180)
    $form.StartPosition = 'CenterScreen'
    $form.Topmost = $true
    $form.FormBorderStyle = 'FixedDialog'
    $form.MaximizeBox = $false
    $form.MinimizeBox = $false

    # ProgressBar
    $progressBar = New-Object System.Windows.Forms.ProgressBar
    $progressBar.Minimum = 0
    $progressBar.Maximum = 100
    $progressBar.Value = 0
    $progressBar.Width = 440
    $progressBar.Height = 22
    $progressBar.Location = New-Object System.Drawing.Point(20, 20)
    $form.Controls.Add($progressBar)

    # Label avanzamento
    $progressLabel = New-Object System.Windows.Forms.Label
    $progressLabel.Text = "Avanzamento: 0%"
    $progressLabel.AutoSize = $true
    $progressLabel.Location = New-Object System.Drawing.Point(20, 55)
    $form.Controls.Add($progressLabel)

    # Pulsanti
    $btnPause = New-Object System.Windows.Forms.Button
    $btnPause.Text = "Pausa"
    $btnPause.Width = 80
    $btnPause.Location = New-Object System.Drawing.Point(20, 90)
    $form.Controls.Add($btnPause)

    $btnResume = New-Object System.Windows.Forms.Button
    $btnResume.Text = "Riprendi"
    $btnResume.Width = 80
    $btnResume.Location = New-Object System.Drawing.Point(120, 90)
    $btnResume.Enabled = $false
    $form.Controls.Add($btnResume)

    $btnStop = New-Object System.Windows.Forms.Button
    $btnStop.Text = "Stop"
    $btnStop.Width = 80
    $btnStop.Location = New-Object System.Drawing.Point(220, 90)
    $form.Controls.Add($btnStop)

    $btnRestart = New-Object System.Windows.Forms.Button
    $btnRestart.Text = "Restart"
    $btnRestart.Width = 80
    $btnRestart.Location = New-Object System.Drawing.Point(320, 90)
    $form.Controls.Add($btnRestart)

    # Eventi pulsanti
    $btnPause.Add_Click({
        $global:taskPaused = $true
        $btnPause.Enabled = $false
        $btnResume.Enabled = $true
    })
    $btnResume.Add_Click({
        $global:taskPaused = $false
        $btnPause.Enabled = $true
        $btnResume.Enabled = $false
    })
    $btnStop.Add_Click({
        $global:taskStopped = $true
        $form.Close()
    })
    $btnRestart.Add_Click({
        $global:taskRestart = $true
        $global:taskPaused = $false
        $btnPause.Enabled = $true
        $btnResume.Enabled = $false
    })

    # Timer per avanzamento step-by-step
    $global:taskIndex = 1
    $global:taskTotale = 1000 # Sostituisci con il numero reale di file da copiare
    $timer = New-Object System.Windows.Forms.Timer
    $timer.Interval = 50

    $timer.Add_Tick({
        if ($global:taskStopped) {
            $timer.Stop()
            $progressLabel.Text = "Operazione interrotta."
            $progressBar.Value = 0
            return
        }
        if ($global:taskPaused) {
            return
        }
        if ($global:taskIndex -gt $global:taskTotale) {
            $timer.Stop()
            $progressBar.Value = 100
            $progressLabel.Text = "Operazione completata."
            return
        }
        $percent = [int](($global:taskIndex / $global:taskTotale) * 100)
        $progressBar.Value = $percent
        $progressLabel.Text = "Copia $($global:taskIndex) di $($global:taskTotale) ($percent%)"
        $global:taskIndex++
    })

    $btnPause.Add_Click({
        $global:taskPaused = $true
        $btnPause.Enabled = $false
        $btnResume.Enabled = $true
    })
    $btnResume.Add_Click({
        $global:taskPaused = $false
        $btnPause.Enabled = $true
        $btnResume.Enabled = $false
    })
    $btnStop.Add_Click({
        $global:taskStopped = $true
        $form.Close()
    })
    $btnRestart.Add_Click({
        $global:taskRestart = $true
        $global:taskPaused = $false
        $global:taskStopped = $false
        $global:taskIndex = 1
        $btnPause.Enabled = $true
        $btnResume.Enabled = $false
        $progressBar.Value = 0
        $progressLabel.Text = "Avanzamento: 0%"
        $timer.Start()
    })

    $form.Add_Shown({
        $global:taskIndex = 1
        $global:taskPaused = $false
        $global:taskStopped = $false
        $progressBar.Value = 0
        $progressLabel.Text = "Avanzamento: 0%"
        $timer.Start()
    })
    $form.ShowDialog() | Out-Null
}

Wrapper-Copia-Gui -profilo $null
