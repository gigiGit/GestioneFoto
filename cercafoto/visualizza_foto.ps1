# Interfaccia per visualizzare e editare album delle foto da original_paths.csv

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

# Percorso del CSV
$programDrive = Split-Path -Path $PSScriptRoot -Qualifier
$username = $env:USERNAME
$computerName = $env:COMPUTERNAME
$destBase = "$programDrive\Mediabackup\$username\$computerName"

$csvPath = "$destBase\original_paths.csv"

# Carica i dati
$data = Import-Csv -Path $csvPath

# Converti in DataTable per prestazioni migliori con grandi file
function ConvertTo-DataTable {
    param([array]$data)
    $dataTable = New-Object System.Data.DataTable
    if ($data.Count -eq 0) { return $dataTable }
    $columns = $data[0].PSObject.Properties.Name
    foreach ($col in $columns) {
        $dataTable.Columns.Add($col)
    }
    foreach ($row in $data) {
        $newRow = $dataTable.NewRow()
        foreach ($col in $columns) {
            $newRow[$col] = $row.$col
        }
        $dataTable.Rows.Add($newRow)
    }
    return $dataTable
}

$dataTable = ConvertTo-DataTable $data

# Indice corrente
$script:currentIndex = 0

# Funzione per aggiornare la visualizzazione
function Update-Display {
    if ($script:currentIndex -ge 0 -and $script:currentIndex -lt $dataTable.Rows.Count) {
        $row = $dataTable.Rows[$script:currentIndex]
        if ($row -ne $null) {
            $originalPathLabel.Text = $row.OriginalPath
            $fileNameLabel.Text = $row.DestFilename
            if ([string]::IsNullOrEmpty($row.Album)) {
                if ([string]::IsNullOrEmpty($row.OriginalPath)) {
                    $albumTextBox.Text = ""
                } else {
                    $albumTextBox.Text = Split-Path (Split-Path $row.OriginalPath -Parent) -Leaf
                }
            } else {
                $albumTextBox.Text = $row.Album
            }
            $indexLabel.Text = "$($script:currentIndex + 1) / $($dataTable.Rows.Count)"
        }
    }
}

# Funzione per aggiornare le miniature
function Update-Thumbnails {
    $thumbnailPanel.Controls.Clear()
    $selectedRows = $dataGridView.SelectedRows
    if ($selectedRows.Count -eq 0) { return }
    $script:startTime = Get-Date
    $script:loadedImages = 0
    $script:totalImages = $selectedRows.Count
    $progressBar.Maximum = $script:totalImages
    $progressBar.Value = 0
    $progressBar.Visible = $true
    $remainingLabel.Visible = $true
    $remainingLabel.Text = "Caricamento in corso..."
    foreach ($selectedRow in $selectedRows) {
        $row = $selectedRow.DataBoundItem
        $thumb = New-Object System.Windows.Forms.PictureBox
        $thumb.Size = New-Object System.Drawing.Size(100, 100)
        $thumb.SizeMode = [System.Windows.Forms.PictureBoxSizeMode]::Zoom
        $thumb.ImageLocation = $row.DestPath
        $thumb.Add_LoadCompleted({
            $script:loadedImages++
            $progressBar.Value = $script:loadedImages
            $elapsed = (Get-Date) - $script:startTime
            $avgTime = $elapsed.TotalSeconds / $script:loadedImages
            $remaining = ($script:totalImages - $script:loadedImages) * $avgTime
            $remainingLabel.Text = "Tempo rimanente: $([math]::Round($remaining)) secondi"
            if ($script:loadedImages -eq $script:totalImages) {
                $progressBar.Visible = $false
                $remainingLabel.Visible = $false
            }
        })
        $thumbnailPanel.Controls.Add($thumb)
    }
}

# Crea la form
$form = New-Object System.Windows.Forms.Form
$form.Text = "Visualizzatore Foto Backup"
$form.Size = New-Object System.Drawing.Size(1200, 640)
$form.StartPosition = "CenterScreen"
$form.KeyPreview = $true
$form.Add_KeyDown({
    if ($_.KeyCode -eq [System.Windows.Forms.Keys]::Left) {
        if ($script:currentIndex -gt 0 -and $dataTable.Rows.Count -gt 0 -and $dataGridView.Rows.Count -gt 0) {
            $script:currentIndex--
            $dataGridView.CurrentCell = $dataGridView.Rows[$script:currentIndex].Cells[0]
        }
    } elseif ($_.KeyCode -eq [System.Windows.Forms.Keys]::Right) {
        if ($script:currentIndex -lt ($dataTable.Rows.Count - 1) -and $dataTable.Rows.Count -gt 0 -and $dataGridView.Rows.Count -gt 0) {
            $script:currentIndex++
            $dataGridView.CurrentCell = $dataGridView.Rows[$script:currentIndex].Cells[0]
        }
    }
})

# DataGridView per la lista dei file
$dataGridView = New-Object System.Windows.Forms.DataGridView
$dataGridView.Size = New-Object System.Drawing.Size(300, 400)
$dataGridView.Location = New-Object System.Drawing.Point(50, 50)
$dataGridView.ReadOnly = $true
$dataGridView.MultiSelect = $true
$dataGridView.SelectionMode = [System.Windows.Forms.DataGridViewSelectionMode]::FullRowSelect
$dataGridView.AllowUserToAddRows = $false
$dataGridView.AllowUserToDeleteRows = $false
$form.Controls.Add($dataGridView)
$dataGridView.DataSource = $dataTable
$col = ($dataGridView.Columns | Where-Object { $_.Name -eq "OriginalPath" })[0]
if ($col) { $col.Visible = $false }
$col = ($dataGridView.Columns | Where-Object { $_.Name -eq "DestPath" })[0]
if ($col) { $col.Visible = $false }
$col = ($dataGridView.Columns | Where-Object { $_.Name -eq "Hash" })[0]
if ($col) { $col.Visible = $false }
$col = ($dataGridView.Columns | Where-Object { $_.Name -eq "DateSource" })[0]
if ($col) { $col.Visible = $false }
$col = ($dataGridView.Columns | Where-Object { $_.Name -eq "Status" })[0]
if ($col) { $col.Visible = $false }

# FlowLayoutPanel per le miniature
$thumbnailPanel = New-Object System.Windows.Forms.FlowLayoutPanel
$thumbnailPanel.Size = New-Object System.Drawing.Size(600, 400)
$thumbnailPanel.Location = New-Object System.Drawing.Point(400, 50)
$thumbnailPanel.AutoScroll = $true
$form.Controls.Add($thumbnailPanel)

# ProgressBar per il caricamento delle immagini
$progressBar = New-Object System.Windows.Forms.ProgressBar
$progressBar.Location = New-Object System.Drawing.Point(400, 460)
$progressBar.Size = New-Object System.Drawing.Size(600, 20)
$progressBar.Visible = $false
$form.Controls.Add($progressBar)

# Label per il tempo rimanente
$remainingLabel = New-Object System.Windows.Forms.Label
$remainingLabel.Location = New-Object System.Drawing.Point(400, 480)
$remainingLabel.Size = New-Object System.Drawing.Size(300, 20)
$remainingLabel.Visible = $false
$form.Controls.Add($remainingLabel)

# Evento per selezione cambiata
$dataGridView.Add_SelectionChanged({
    $selectedRows = $dataGridView.SelectedRows
    if ($selectedRows.Count -ge 1 -and $dataTable.Rows.Count -gt 0) {
        $script:currentIndex = $selectedRows[0].Index
        Update-Display
        Update-Thumbnails
    }
})

# Label per il percorso originale
$originalPathLabel = New-Object System.Windows.Forms.Label
$originalPathLabel.Location = New-Object System.Drawing.Point(50, 460)
$originalPathLabel.Size = New-Object System.Drawing.Size(600, 20)
$form.Controls.Add($originalPathLabel)

# Label per il nome del file
$fileNameLabel = New-Object System.Windows.Forms.Label
$fileNameLabel.Location = New-Object System.Drawing.Point(50, 480)
$fileNameLabel.Size = New-Object System.Drawing.Size(600, 20)
$form.Controls.Add($fileNameLabel)

# Label per indice
$indexLabel = New-Object System.Windows.Forms.Label
$indexLabel.Location = New-Object System.Drawing.Point(50, 500)
$indexLabel.Size = New-Object System.Drawing.Size(100, 20)
$form.Controls.Add($indexLabel)

# TextBox per Album
$albumLabel = New-Object System.Windows.Forms.Label
$albumLabel.Text = "Album:"
$albumLabel.Location = New-Object System.Drawing.Point(50, 520)
$albumLabel.Size = New-Object System.Drawing.Size(50, 20)
$form.Controls.Add($albumLabel)

$albumTextBox = New-Object System.Windows.Forms.TextBox
$albumTextBox.Location = New-Object System.Drawing.Point(100, 520)
$albumTextBox.Size = New-Object System.Drawing.Size(200, 20)
$form.Controls.Add($albumTextBox)

# Pulsante Precedente
$prevButton = New-Object System.Windows.Forms.Button
$prevButton.Text = "Precedente"
$prevButton.Location = New-Object System.Drawing.Point(350, 520)
$prevButton.Size = New-Object System.Drawing.Size(80, 30)
$prevButton.Add_Click({
    if ($script:currentIndex -gt 0 -and $dataTable.Rows.Count -gt 0 -and $dataGridView.Rows.Count -gt 0) {
        $script:currentIndex--
        $dataGridView.CurrentCell = $dataGridView.Rows[$script:currentIndex].Cells[0]
    }
})
$form.Controls.Add($prevButton)

# Pulsante Successivo
$nextButton = New-Object System.Windows.Forms.Button
$nextButton.Text = "Successivo"
$nextButton.Location = New-Object System.Drawing.Point(440, 520)
$nextButton.Size = New-Object System.Drawing.Size(80, 30)
$nextButton.Add_Click({
    if ($script:currentIndex -lt ($dataTable.Rows.Count - 1) -and $dataTable.Rows.Count -gt 0 -and $dataGridView.Rows.Count -gt 0) {
        $script:currentIndex++
        $dataGridView.CurrentCell = $dataGridView.Rows[$script:currentIndex].Cells[0]
    }
})
$form.Controls.Add($nextButton)

# Pulsante Salva
$saveButton = New-Object System.Windows.Forms.Button
$saveButton.Text = "Salva Album"
$saveButton.Location = New-Object System.Drawing.Point(530, 520)
$saveButton.Size = New-Object System.Drawing.Size(80, 30)
$saveButton.Add_Click({
    $selectedRows = $dataGridView.SelectedRows
    if ($selectedRows.Count -eq 0) {
        $statusLabel.Text = "Seleziona almeno un file"
        return
    }
    foreach ($selectedRow in $selectedRows) {
        $selectedRow.DataBoundItem.Album = $albumTextBox.Text
    }
    # Converti DataTable in oggetti per esportazione
    $objects = $dataTable.Rows | ForEach-Object {
        [PSCustomObject]@{
            OriginalPath = $_.OriginalPath
            DestPath = $_.DestPath
            DestFilename = $_.DestFilename
            Album = $_.Album
        }
    }
    $objects | Export-Csv -Path $csvPath -NoTypeInformation -Encoding UTF8
    $statusLabel.Text = "Album applicato a $($selectedRows.Count) file"
    # Timer per svuotare dopo 2 secondi
    $timer = New-Object System.Timers.Timer
    $timer.Interval = 2000
    $timer.AutoReset = $false
    $timer.Add_Elapsed({
        $form.Invoke([Action]{ $statusLabel.Text = "" })
        $timer.Dispose()
    })
    $timer.Start()
})
$form.Controls.Add($saveButton)

# Label per status
$statusLabel = New-Object System.Windows.Forms.Label
$statusLabel.Location = New-Object System.Drawing.Point(50, 550)
$statusLabel.Size = New-Object System.Drawing.Size(200, 20)
$form.Controls.Add($statusLabel)

$form.Add_Shown({
    if ($dataTable.Rows.Count -gt 0 -and $dataGridView.Rows.Count -gt 0) {
        $dataGridView.CurrentCell = $dataGridView.Rows[0].Cells[0]
        Update-Display
        Update-Thumbnails
    }
})

# Inizializza la visualizzazione
# (moved to Shown event)

# Mostra la form
$form.ShowDialog()