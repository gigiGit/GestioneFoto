[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
function Get-SqliteCliPath {
    $cliPath = Join-Path $PSScriptRoot 'sqlite3.exe'
    if (Test-Path -LiteralPath $cliPath) {
        return $cliPath
    }

    $command = Get-Command 'sqlite3.exe' -ErrorAction SilentlyContinue
    if (-not $command) {
        $command = Get-Command 'sqlite3' -ErrorAction SilentlyContinue
    }
    if ($command -and $command.CommandType -eq 'Application') {
        return $command.Source
    }

    throw "sqlite3 non trovato. Esegui setup\Inizializza-Progetto.ps1 oppure installa il CLI SQLite e aggiungilo al PATH."
}

function Invoke-SqliteQuery {
    param(
        [string]$DatabasePath,
        [string]$Sql
    )
    $sqliteCli = Get-SqliteCliPath
    $output = & $sqliteCli $DatabasePath ".timeout 15000" $Sql 2>&1
    if ($LASTEXITCODE -eq 0) {
        return @($output | ForEach-Object { [string]$_ })
    } else {
        throw "Query SQLite fallita: $($output -join '; ')"
    }
}

function InitDatabase {
    param($DatabasePath)
    $sqlCreate = "CREATE TABLE IF NOT EXISTS HASH (hash TEXT PRIMARY KEY ,data_acquisizione TEXT ,file TEXT,album text,copiato INTEGER);"
    Invoke-SqliteQuery -DatabasePath $DatabasePath -Sql $sqlCreate | Out-Null
    $sqlCreate = "CREATE TABLE IF NOT EXISTS SRC (hash TEXT ,file text,dtExif text,dtFileName text, dtFile1 text,dtFile2 text,PRIMARY KEY (hash, file));"
    Invoke-SqliteQuery -DatabasePath $DatabasePath -Sql $sqlCreate | Out-Null
}


function Close-Exit {
    param( $gui,$profilo)
    Write-GuiLog $global:DEBUG "[Close-Exit] Invocata." 
    # Chiude la finestra principale della GUI e fa cleanup
    if ($gui -and $gui.Form) {
        $gui.Form.Close()
    }
    # Eventuale cleanup aggiuntivo qui
    # Non chiamare exit qui, lasciamo che sia il main loop a terminarlo

}

function Start-Scansione {
    param($gui, $profilo)
    Write-Host "[Start-Scansione] Invocata."
    Write-GuiLog $global:INFO "[Start-Scansione] Invocata."
    Write-GuiLog $global:INFO "La prima Scansione puo' impiegare molto tempo attendere pazientemente"
    # Recupera percorso e estensioni dal config
    $IncludePath = $profilo.IncludePath
    $extString = $profilo.Extensions
    Write-GuiLog $global:DEBUG "IncludePath: $IncludePath, Extensions: $extString"
    if (-not $IncludePath -or -not $extString) {
        Write-GuiLog $global:ERROR "Percorso o estensioni non specificate nel profilo."
        return
    }
    # Supporta separatore virgola o spazio
    $exts = $extString -split '[,; ]+' | Where-Object { $_ -ne '' } | ForEach-Object { $_.Trim().TrimStart('.') }

    # Trova tutti i file con le estensioni richieste (compatibile con tutte le versioni di PowerShell)
   
    
    $minSize = if ( $profilo.MinSize -and $profilo.MinSize -match '^[0-9]+$') { [int]$profilo.MinSize } else { 0 }  
 
    $files = Get-ChildItem -Path $IncludePath -Recurse -ErrorAction SilentlyContinue |
        Where-Object { 
            -not $_.PSIsContainer -and 
            ($exts -contains $_.Extension.TrimStart('.')) -and 
            $_.Length -ge $minSize 
        }
    Write-GuiLog $global:INFO ("File da elaborare: {0}" -f $files.Count)
    # Recupero DatabasePath dal profilo
    $DatabasePath = $profilo.DatabasePath
    if (-not $DatabasePath) {
        Write-GuiLog $global:DEBUG "[AnalisiFoto] DatabasePath non trovato nel profilo. DatabasePath: $DatabasePath"
        return
    }
    # Crea tabella HASH se non esiste
    InitDatabase $DatabasePath
    $excludeList = $profilo.ExcludePaths -split '[;,]' | ForEach-Object { $_.Trim() } | Where-Object { $_ }

    $files = $files | Where-Object {
        $filePath = $_.FullName  
        -not ($excludeList | Where-Object { $filePath -like ($_ + '*') })
    }
    $total = $files.Count
    $current = 0
    $delta=[math]::Ceiling($total/100)
    if($delta -eq 0) { $delta = 1 }
    foreach ($file in $files) {
        $current++
        # Aggiorna la progress bar
        if ($global:logProgressBar -and $current % $delta -eq 0) {
            $percent = [math]::Floor(($current / $total) * 100)
            if ($percent -gt 100) { $percent = 100 }
            $global:logProgressBar.Value = $percent
            $global:lblProgressInfo.Text = "Scansione in corso... $current di $total file elaborati."   
        }
        $FilePath = $file.FullName
        # Cerca se esiste già un hash identico
        $hash = Get-FileHash -Algorithm SHA256 -Path $FilePath
        if(-not $hash) {
            Write-GuiLog $global:DEBUG "Impossibile calcolare hash per file: $($file.FullName), salto."
            continue
        }
        $HashSql=$hash.Hash.Replace("'", "''") # Escape per SQL
        $filePathSql = $FilePath.Replace("'", "''")
        try{
            $sqlInsert = "INSERT INTO src (hash,  file) VALUES ( '$HashSql', '$filePathSql');" 
            $result = Invoke-SqliteQuery -DatabasePath $DatabasePath -Sql $sqlInsert
        }catch{
            # File già presente , trascuriamo l'errore e proseguiamo con l'analisi della data di acquisizione
            continue
        }
        #Write-GuiLog $global:DEBUG "[AnalisiFoto] Calcolato hash per $($file.FullName): $($HashSql)"
        $sqlCheck = "SELECT count(*) FROM HASH WHERE hash = '$HashSql';"
        $result = Invoke-SqliteQuery -DatabasePath $DatabasePath -Sql $sqlCheck
        if ($result -gt 0) {
          #Write-GuiLog $global:DEBUG "[AnalisiFoto] HASH già presente su DB, salto inserimento."
        }else{
            $dato= AnalisiFoto $gui $file
            try{
            $dateTakenSql = $dato.dateTaken.Replace("'", "''")
            $sqlInsert = "INSERT INTO HASH (hash,  data_acquisizione,file,copiato) VALUES ( '$HashSql', '$dateTakenSql', '$filePathSql',0);"
            Invoke-SqliteQuery -DatabasePath $DatabasePath -Sql $sqlInsert | Out-Null
            switch ($dato.tipo) {
                'dtExif' { $sqlUpdate = "update src set dtExif='$dateTakenSql' where hash='$HashSql' and file='$filePathSql';" }
                'dtFileName' { $sqlUpdate = "update src set dtFileName='$dateTakenSql' where hash='$HashSql' and file='$filePathSql';" }
                'dtFile1' { $sqlUpdate = "update src set dtFile1='$dateTakenSql' where hash='$HashSql' and file='$filePathSql';" }
                'dtFile2' { $sqlUpdate = "update src set dtFile2='$dateTakenSql' where hash='$HashSql' and file='$filePathSql';" }
                default { 
                    $sqlUpdate = "update src set dtExit=null where hash='$HashSql' and file='$filePathSql';"
                    Write-GuiLog $global:INFO "[AnalisiFoto]  data acquisizione non disponibile per ${file.FullName}." 
                }
            }
            
            Invoke-SqliteQuery -DatabasePath $DatabasePath -Sql $sqlUpdate | Out-Null
            #Write-GuiLog $global:DEBUG "[AnalisiFoto] HASH e data acquisizione salvati su DB. $dateTaken"   
            }catch {
                Write-GuiLog $global:DEBUG "Errore durante inserimento su DB per file $($file.FullName): $_"
            }
        }  
    }
    # Al termine, porta la progress bar a 100%
    if ($global:logProgressBar) { $global:logProgressBar.Value = 100 }
    Write-GuiLog $global:DEBUG "[Start-Scansione] Completata."
}

function Recuperadata-ByName {
    param([string]$fileName)
    # Esempi gestiti:
    # shot-2017-06-07_18-28-04.png, IMG_20240531_123456.jpg, 20240531_123456.jpg, 2024-05-31.jpg, 20240607.jpg, 2024-06-07_18-28-04.jpg, 20240607-182804.jpg, shot-2017-06-07.png, shot-2017-06-07_18-28.png

    # shot-2017-06-07_18-28-04 o 2024-06-07_18-28-04
    if ($fileName -match '2(\d{3})-(\d{2})-(\d{2})[_-](\d{2})[-_](\d{2})[-_](\d{2})') {
        return "2$($matches[1])-$($matches[2])-$($matches[3]) $($matches[4]):$($matches[5]):$($matches[6])"
    }
    # 20240531_123456 o IMG_20240531_123456
    if ($fileName -match '2(\d{3})(\d{2})(\d{2})[_-](\d{2})(\d{2})(\d{2})') {
        return "2$($matches[1])-$($matches[2])-$($matches[3]) $($matches[4]):$($matches[5]):$($matches[6])"
    }
    # shot-2017-06-07_18-28
    if ($fileName -match '2(\d{3})-(\d{2})-(\d{2})[_-](\d{2})[-_](\d{2})') {
        return "2$($matches[1])-$($matches[2])-$($matches[3]) $($matches[4]):$($matches[5]):00"
    }
    # 2024-06-07
    if ($fileName -match '2(\d{3})-(\d{2})-(\d{2})') {
        return "2$($matches[1])-$($matches[2])-$($matches[3]) 00:00:00"
    }
    # 20240607
    if ($fileName -match '2(\d{3})(\d{2})(\d{2})') {
        return "2$($matches[1])-$($matches[2])-$($matches[3]) 00:00:00"
    }
    # dd-MM-yyyy_HHmmss
    if ($fileName -match '(\d{2})-(\d{2})-2(\d{3})[-_](\d{2})(\d{2})(\d{2})') {
        return "2$($matches[3])-$($matches[2])-$($matches[1]) $($matches[4]):$($matches[5]):$($matches[6])"
    }
    return $null
}

function AnalisiFoto{
    param($gui, $file)  
    #Write-GuiLog $global:DEBUG "[AnalisiFoto]: $($file.FullName)"
    # Recupero data di acquisizione dai metadati EXIF se presenti
    $dateTaken = $null
    $isVideo = @('mp4', 'avi', 'mov', 'mkv', 'flv', 'wmv', 'webm', 'asf', 'mts', 'm2ts', 'mod', 'tod') -contains $file.Extension.TrimStart('.').ToLower()
    
    try {
        $shell = New-Object -ComObject Shell.Application
        $folder = $shell.Namespace((Split-Path $file.FullName))
        $item = $folder.ParseName($file.Name)
        
        # Prova indici diversi: 12 per foto, 208-210 per video (Media Created, Date Taken, ecc)
        $indicesToTry = if ($isVideo) { @(208, 209, 12, 210) } else { @(12, 208, 209) }
        
        foreach ($index in $indicesToTry) {
            try {
                $dateExifRaw = $folder.GetDetailsOf($item, $index)
                if ([string]::IsNullOrWhiteSpace($dateExifRaw)) { continue }
                
                # Prova a convertire e formattare la data EXIF (formato atteso: DD/MM/YYYY HH:MI[:SS])
                $dateExifClean = -join ($dateExifRaw.ToCharArray() | Where-Object { [int]$_ -ge 32 -and [int]$_ -le 127 -or $_ -eq ' ' })
                $dateExifClean = $dateExifClean -replace '[^0-9/: ]','' -replace '\s+', ' '
                $dateExif = $dateExifClean.Trim()
                
                if ([string]::IsNullOrWhiteSpace($dateExif)) { continue }
                
                try {
                    $dateObj = [DateTime]::ParseExact($dateExif, 'dd/MM/yyyy HH:mm:ss', $null)
                    $dateTaken = $dateObj.ToString('yyyy-MM-dd HH:mm:ss')
                    Write-GuiLog $global:DEBUG "Data EXIF recuperata (indice $index): $dateTaken per $($file.Name)"
                    return @{dateTaken=$dateTaken; tipo='dtExif'}
                } catch {
                    try {
                        $dateObj = [DateTime]::ParseExact($dateExif, 'dd/MM/yyyy HH:mm', $null)
                        $dateTaken = $dateObj.ToString('yyyy-MM-dd HH:mm:ss')
                        Write-GuiLog $global:DEBUG "Data EXIF recuperata (indice $index, formato HH:mm): $dateTaken per $($file.Name)"
                        return @{dateTaken=$dateTaken; tipo='dtExif'}
                    } catch {
                        try {
                            $dateObj = [DateTime]::ParseExact($dateExif, 'dd/MM/yyyy', $null)
                            $dateTaken = $dateObj.ToString('yyyy-MM-dd HH:mm:ss')
                            Write-GuiLog $global:DEBUG "Data EXIF recuperata (indice $index, formato data): $dateTaken per $($file.Name)"
                            return @{dateTaken=$dateTaken; tipo='dtExif'}
                        } catch {
                            # Continua con il prossimo indice
                        }
                    }
                }
            } catch {
                # Continua con il prossimo indice
            }
        }
    } catch {
        $dateTaken = $null
    }
    # Prova a recuperare la data di acquisizione dal nome file (formato: YYYYMMDD o YYYY-MM-DD)
    $dateTaken = recuperadata-ByName $file.Name
    #Write-GuiLog $global:DEBUG "Data recuperata dal nome file:$file.Name -> $dateTaken"
    if (-not $dateTaken -or [string]::IsNullOrWhiteSpace($dateTaken)) {
        $dataCreazione = Get-Item $file.FullName | Select-Object -ExpandProperty CreationTime
        $dataUltimaModifica = Get-Item $file.FullName | Select-Object -ExpandProperty LastWriteTime
        if ($dataCreazione -lt $dataUltimaModifica) {
            $dateTaken = "$dataCreazione"
        } else {
            $dateTaken = "$dataUltimaModifica"
        }

    }else{
        return @{dateTaken=$dateTaken; tipo='dtFileName'}
    }
    # Se $dateTaken è una data, restituiscila come stringa formattata
    if ($dateTaken -is [DateTime]) {
        return @{dateTaken=$dateTaken.ToString('yyyy-MM-dd HH:mm:ss'); tipo='dtFile1'}
    } else {
        return @{dateTaken=$dateTaken; tipo='dtFile2'}
    }
}

function Wrapper-Copia {
    param($gui,$profilo)
    Write-GuiLog $global:INFO "[Wrapper-Copia] Invocata."
    # TODO: implementazione reale
    Write-GuiLog $global:INFO "profilo:$($profilo | Out-String)"

    # Lettura e visualizzazione di tutti i record della tabella HASH
    if ($profilo.DatabasePath) {
        InitDatabase $profilo.DatabasePath # Assicurati che la tabella esista
        $sqlReadAll = "SELECT * FROM HASH where copiato=0;"
        $records = Invoke-SqliteQuery -DatabasePath $profilo.DatabasePath -Sql $sqlReadAll
    $total = $records.Count
    $current = 0
    $delta=[math]::Ceiling($total/100)
    Write-GuiLog $global:INFO ("File da copiare: {0}" -f $total)
    if($delta -eq 0) { $delta = 1 }
        foreach ($rec in $records) {
        $current++
        # Aggiorna la progress bar
        if ($global:logProgressBar -and $current % $delta -eq 0) {
            $percent = [math]::Floor(($current / $total) * 100)
            if ($percent -gt 100) { $percent = 100 }
            $global:logProgressBar.Value = $percent
            $global:lblProgressInfo.Text = "Scansione in corso... $current di $total file elaborati."   
            Write-GuiLog $global:INFO ("File copiati: {0}/{1}" -f $current, $total)
        }
                $HASH=$rec.Split('|')[0]
                $DataAcquisizione=$rec.Split('|')[1]    
                $File=$rec.Split('|')[2]
                $Copiato=$rec.Split('|')[3]
                # Costruisci il path destinazione recuperando la data di acquisizione e creando una struttura ad albero
                $dateObj = Get-Date $DataAcquisizione
                $destPath = Join-Path $profilo.DestinationRoot ($dateObj.ToString('yyyy'))
                $destPath = Join-Path $destPath ($dateObj.ToString('MM'))
                if($profilo.DestinationTemplate -eq 'YYYY/MM/DD' ){
                    $destPath = Join-Path $destPath ($dateObj.ToString('dd'))
                }
                # Crea cartelle se non esistono
                if (-not (Test-Path -Path $destPath)) {
                    New-Item -ItemType Directory -Path $destPath | Out-Null
                }
                # Copia il file nella cartella di destinazione
                $destFile = Join-Path $destPath (Split-Path $File -Leaf)
                Copy-Item -Path $File -Destination $destFile -Force
                # Imposta la data di creazione e modifica del file copiato
                $dt = [datetime]::ParseExact($DataAcquisizione, 'yyyy-MM-dd HH:mm:ss', $null)
                Set-ItemProperty -Path $destFile -Name CreationTime -Value $dt
                Set-ItemProperty -Path $destFile -Name LastWriteTime -Value $dt
                # Aggiorna i campi file e copiato nella tabella HASH
                $fileSql = $destFile.Replace("'", "''")
                $sqlUpdate = "UPDATE HASH SET file = '$fileSql', copiato = 1 WHERE hash = '$HASH';"
                Invoke-SqliteQuery -DatabasePath $profilo.DatabasePath -Sql $sqlUpdate | Out-Null
                Write-GuiLog $global:DEBUG ("$HASH | $DataAcquisizione | $File | $Copiato | $destPath | Copiato in: $destFile")
            }
    } else {
        Write-GuiLog $global:WARNING "DatabasePath non specificato nel profilo."
    }
    Write-GuiLog $global:INFO "[Wrapper-Copia] Completata."
}

function Wrapper-Visualizza {
    param($gui, $profilo)
     Write-GuiLog $global:DEBUG "Wrapper-Visualizza: Profilo attivo: $($profilo | Out-String)"
     if (-not $profilo) {
        Write-GuiLog $global:ERROR "Profilo non specificato per la visualizzazione."
        return
    }   
    
        InitDatabase $profilo.DatabasePath # Assicurati che la tabella esista
       
        
# 1. Query
$records = Invoke-SqliteQuery -DatabasePath $profilo.DatabasePath -Sql "SELECT hash, data_acquisizione, file,copiato FROM HASH;"
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

# Crea la form
$form = New-Object System.Windows.Forms.Form
$form.Text = "Gestione Foto - Visualizza Database"
$form.Size = New-Object System.Drawing.Size(1000,600)
$form.StartPosition = "CenterScreen"

# DataGridView per i record
$dataGrid = New-Object System.Windows.Forms.DataGridView
$dataGrid.Location = New-Object System.Drawing.Point(10,10)
$dataGrid.Size = New-Object System.Drawing.Size(500,540)
$dataGrid.SelectionMode = 'FullRowSelect'
$dataGrid.ReadOnly = $true
$dataGrid.MultiSelect = $false

# PictureBox per l'immagine
$pictureBox = New-Object System.Windows.Forms.PictureBox
$pictureBox.Location = New-Object System.Drawing.Point(520,10)
$pictureBox.Size = New-Object System.Drawing.Size(450,400)
$pictureBox.SizeMode = 'Zoom'

# Label per dettagli file
$labelData = New-Object System.Windows.Forms.Label
$labelData.Location = New-Object System.Drawing.Point(520,420)
$labelData.Size = New-Object System.Drawing.Size(450,30)
$labelData.Text = "Data: "

$labelSize = New-Object System.Windows.Forms.Label
$labelSize.Location = New-Object System.Drawing.Point(520,460)
$labelSize.Size = New-Object System.Drawing.Size(450,30)
$labelSize.Text = "Dimensione: "


# 2. Crea DataTable e popola
$dataTable = New-Object System.Data.DataTable
$dataTable.Columns.AddRange(@("Hash","DataAcquisizione","File","Copiato"))
foreach ($rec in $records) {
    $fields = $rec -split '\|'
    $dataTable.Rows.Add($fields[0], $fields[1], $fields[2], $fields[3])
}

# 3. Associa a DataGridView
$dataGrid.DataSource = $dataTable

# 4. Evento selezione riga
$dataGrid.add_SelectionChanged({
    $row = $dataGrid.CurrentRow
    try {
    $filePath = $row.Cells["File"].Value
    $pictureBox.Image = [System.Drawing.Image]::FromFile($filePath)
    $fileInfo = Get-Item $filePath
    $labelData.Text = $fileInfo.CreationTime
    $labelSize.Text = "Dimensione:  $($fileInfo.Length)  bytes"
    } catch {
    
    $pictureBox.Image = $null
    $labelData.Text = "Data: "
    $labelSize.Text = "Dimensione: "
    }
})
# Mostra la form (il popolamento e la logica evento vanno aggiunti nel tuo script principale)
# Aggiungi controlli alla form
$form.Controls.Add($dataGrid)
$form.Controls.Add($pictureBox)
$form.Controls.Add($labelData)
$form.Controls.Add($labelSize)

$form.ShowDialog()


    
}

function Wrapper-Help{
    param($gui)
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing

    $guidaPath = Join-Path (Join-Path $PSScriptRoot '..\doc') 'GUIDA_UTENTE_BREVE.html'
    if (-not (Test-Path $guidaPath)) {
        [System.Windows.Forms.MessageBox]::Show("File GUIDA_UTENTE_BREVE.html non trovato!","Errore Help",'OK','Error')
        return
    }

    # Crea la form di help
    $form = New-Object System.Windows.Forms.Form
    $form.Text = "Guida Utente Breve"
    $form.Size = New-Object System.Drawing.Size(700, 500)
    $form.StartPosition = 'CenterScreen'
    $form.Topmost = $true
    $form.FormBorderStyle = 'Sizable'

    # WebBrowser per visualizzare l'HTML
    $wb = New-Object System.Windows.Forms.WebBrowser
    $wb.Dock = 'Fill'
    $wb.Url = (New-Object System.Uri $guidaPath)
    $form.Controls.Add($wb)
    $form.ShowDialog() | Out-Null
}

function Wrapper-Album {
    param($gui, $profilo)
    Write-GuiLog $global:DEBUG "[Wrapper-Album] Invocata."
    # Recupera DatabasePath e AlbumDestinazione dal profilo
    $DatabasePath = $profilo.DatabasePath
    $AlbumDestinazione = $profilo.AlbumDestination
    if (-not $DatabasePath) {
        Write-GuiLog $global:ERROR "DatabasePath non specificato nel profilo."
        return
    }
    if (-not $AlbumDestinazione) {
        Write-GuiLog $global:ERROR "AlbumDestinazione non specificato nel profilo."
        return
    }
    # Chiama lo script di gestione album come procedura
    $albumScript = Join-Path $PSScriptRoot 'GestioneAlbum.ps1'
    if (-not (Test-Path $albumScript)) {        
        Write-GuiLog $global:ERROR "Script GestioneAlbum.ps1 non trovato in lib."
        return
    }
    & $albumScript $DatabasePath $AlbumDestinazione

}

function Rebuild-HashDatabase {
    param($gui, $profilo)
    Write-GuiLog $global:INFO "[Rebuild-HashDatabase] Ricostruzione hash dalla DestinationRoot avviata."
    
    if (-not $profilo) {
        Write-GuiLog $global:ERROR "Profilo non specificato."
        return
    }
    if (-not $profilo.DatabasePath) {
        Write-GuiLog $global:ERROR "DatabasePath non specificato nel profilo."
        return
    }
    if (-not $profilo.DestinationRoot) {
        Write-GuiLog $global:ERROR "DestinationRoot non specificato nel profilo."
        return
    }
    
    $DatabasePath = $profilo.DatabasePath
    $DestinationRoot = $profilo.DestinationRoot
    
    # Verifica che DestinationRoot esista
    if (-not (Test-Path -Path $DestinationRoot)) {
        Write-GuiLog $global:ERROR "DestinationRoot non trovato: $DestinationRoot"
        return
    }
    
    # Crea tabella HASH se non esiste
    InitDatabase $DatabasePath
    
    # Estensioni fotografie comuni
    $photoExts = @('jpg', 'jpeg', 'png', 'gif', 'bmp', 'tiff', 'raw', 'cr2', 'nef', 'arw', 'dng')
    
    # Scansiona tutti i file nella DestinationRoot
    $files = Get-ChildItem -Path $DestinationRoot -Recurse -ErrorAction SilentlyContinue |
        Where-Object { 
            -not $_.PSIsContainer -and 
            $photoExts -contains $_.Extension.TrimStart('.').ToLower()
        }
    
    Write-GuiLog $global:INFO "File fotografie trovati: $($files.Count)"
    
    $total = $files.Count
    if ($total -eq 0) {
        Write-GuiLog $global:WARNING "[Rebuild-HashDatabase] Nessun file trovato in DestinationRoot."
        return
    }
    
    $current = 0
    $updated = 0
    $inserted = 0
    $errors = 0
    $delta = [math]::Ceiling($total / 100)
    if ($delta -eq 0) { $delta = 1 }
    
    foreach ($file in $files) {
        $current++
        
        # Aggiorna la progress bar
        if ($global:logProgressBar -and $current % $delta -eq 0) {
            $percent = [math]::Floor(($current / $total) * 100)
            if ($percent -gt 100) { $percent = 100 }
            $global:logProgressBar.Value = $percent
            $global:lblProgressInfo.Text = "Controllo hash... $current di $total file elaborati."
        }
        
        try {
            # Calcola l'hash SHA256 del file
            $hash = Get-FileHash -Algorithm SHA256 -Path $file.FullName
            if (-not $hash) {
                Write-GuiLog $global:DEBUG "Impossibile calcolare hash per: $($file.FullName)"
                $errors++
                continue
            }
            
            $HashSql = $hash.Hash.Replace("'", "''")
            $filePathSql = $file.FullName.Replace("'", "''")
            
            # Estrai la data acquisizione dalla struttura di cartelle YYYY/MM/[DD]
            # Esempio: DestinationRoot\2024\06\15\foto.jpg -> 2024-06-15
            $relativePath = $file.FullName.Substring($DestinationRoot.Length).TrimStart('\')
            $pathParts = $relativePath -split '\\'
            
            $dateTaken = $null
            if ($pathParts.Count -ge 2) {
                $year = $pathParts[0]
                $month = $pathParts[1]
                $day = if ($pathParts.Count -ge 3 -and $pathParts[2] -match '^\d{2}$') { $pathParts[2] } else { "01" }
                
                # Valida i valori
                if ($year -match '^\d{4}$' -and $month -match '^\d{2}$' -and $day -match '^\d{2}$') {
                    try {
                        $dateObj = [DateTime]::ParseExact("$year$month$day", 'yyyyMMdd', $null)
                        $dateTaken = $dateObj.ToString('yyyy-MM-dd HH:mm:ss')
                    } catch {
                        Write-GuiLog $global:DEBUG "Data non valida dalla cartella: $year/$month/$day"
                    }
                }
            }
            
            # Se non riesce a estrarre dalla cartella, usa la data del file
            if (-not $dateTaken) {
                $fileInfo = Get-Item $file.FullName
                $dateTaken = $fileInfo.CreationTime.ToString('yyyy-MM-dd HH:mm:ss')
                Write-GuiLog $global:DEBUG "Data da cartella non valida, usata data file: $dateTaken per $($file.Name)"
            }
            
            # Controlla se l'hash esiste già nel database
            $sqlCheck = "SELECT COUNT(*) FROM HASH WHERE hash = '$HashSql';"
            $result = Invoke-SqliteQuery -DatabasePath $DatabasePath -Sql $sqlCheck
            
            if ($result -gt 0) {
                # Aggiorna il record esistente con il percorso completo
                $sqlUpdate = "UPDATE HASH SET file = '$filePathSql', data_acquisizione = '$dateTaken', copiato = 1 WHERE hash = '$HashSql';"
                Invoke-SqliteQuery -DatabasePath $DatabasePath -Sql $sqlUpdate | Out-Null
                $updated++
                Write-GuiLog $global:DEBUG "[Rebuild] Aggiornato hash esistente: $($file.Name)"
            } else {
                # Inserisce il nuovo record
                $sqlInsert = "INSERT INTO HASH (hash, data_acquisizione, file, copiato) VALUES ('$HashSql', '$dateTaken', '$filePathSql', 1);"
                Invoke-SqliteQuery -DatabasePath $DatabasePath -Sql $sqlInsert | Out-Null
                $inserted++
                Write-GuiLog $global:DEBUG "[Rebuild] Inserito nuovo hash: $($file.Name)"
            }
        } catch {
            Write-GuiLog $global:DEBUG "Errore durante elaborazione file $($file.FullName): $_"
            $errors++
        }
    }
    
    # Porta la progress bar a 100%
    if ($global:logProgressBar) { $global:logProgressBar.Value = 100 }
    
    Write-GuiLog $global:INFO "[Rebuild-HashDatabase] Completata."
    Write-GuiLog $global:INFO "Riepilogo: Inseriti=$inserted, Aggiornati=$updated, Errori=$errors, Totale=$total"
}

function Recalculate-ExifDates {
    param($gui, $profilo)
    Write-GuiLog $global:INFO "[Recalculate-ExifDates] Ricalcolo dei metadati EXIF avviato."
    
    if (-not $profilo) {
        Write-GuiLog $global:ERROR "Profilo non specificato."
        return
    }
    if (-not $profilo.DatabasePath) {
        Write-GuiLog $global:ERROR "DatabasePath non specificato nel profilo."
        return
    }
    
    $DatabasePath = $profilo.DatabasePath
    
    # Verifica che il database esista
    if (-not (Test-Path -Path $DatabasePath)) {
        Write-GuiLog $global:ERROR "Database non trovato: $DatabasePath"
        return
    }
    
    # Seleziona i record dalla tabella SRC dove dtExif è vuoto o NULL
    $sqlSelect = "SELECT hash, file FROM SRC WHERE dtExif IS NULL OR dtExif = '';"
    $records = Invoke-SqliteQuery -DatabasePath $DatabasePath -Sql $sqlSelect
    
    $total = if ($records) { if ($records -is [array]) { $records.Count } else { 1 } } else { 0 }
    
    if ($total -eq 0) {
        Write-GuiLog $global:INFO "[Recalculate-ExifDates] Nessun record da ricalcolare."
        return
    }
    
    Write-GuiLog $global:INFO "Record da ricalcolare: $total"
    
    $current = 0
    $updatedSrc = 0
    $updatedHash = 0
    $movedFiles = 0
    $errors = 0
    $delta = [math]::Ceiling($total / 100)
    if ($delta -eq 0) { $delta = 1 }
    
    # Assicura che $records sia sempre un array
    if ($records -isnot [array]) { $records = @($records) }
    
    foreach ($record in $records) {
        $current++
        
        # Aggiorna la progress bar
        if ($global:logProgressBar -and $current % $delta -eq 0) {
            $percent = [math]::Floor(($current / $total) * 100)
            if ($percent -gt 100) { $percent = 100 }
            $global:logProgressBar.Value = $percent
            $global:lblProgressInfo.Text = "Ricalcolo EXIF... $current di $total record elaborati."
        }
        
        try {
            if ($record -is [string]) {
                $parts = $record.Split('|')
                $hashValue = $parts[0]
                $filePath = $parts[1]
            } else {
                $hashValue = $record.hash
                $filePath = $record.file
            }

            if ([string]::IsNullOrWhiteSpace($hashValue) -or [string]::IsNullOrWhiteSpace($filePath)) {
                Write-GuiLog $global:DEBUG "Record SRC non valido: $record"
                $errors++
                continue
            }
            
            # Verifica che il file esista
            if (-not (Test-Path -Path $filePath)) {
                Write-GuiLog $global:DEBUG "File non trovato: $filePath"
                $errors++
                continue
            }
            
            # Estrai la data EXIF dal file usando AnalisiFoto
            $fileObject = Get-Item $filePath
            $exifData = AnalisiFoto $gui $fileObject
            
            if (-not $exifData -or -not $exifData.dateTaken) {
                Write-GuiLog $global:DEBUG "Impossibile estrarre data da: $filePath"
                $errors++
                continue
            }
            
            # Se la data proviene da EXIF, aggiorna il database
            if ($exifData.tipo -eq 'dtExif') {
                $dateSql = $exifData.dateTaken.Replace("'", "''")
                $hashSql = $hashValue.Replace("'", "''")
                $fileSql = $filePath.Replace("'", "''")
                
                $sqlUpdate = "UPDATE SRC SET dtExif = '$dateSql' WHERE hash = '$hashSql' AND file = '$fileSql';"
                Invoke-SqliteQuery -DatabasePath $DatabasePath -Sql $sqlUpdate | Out-Null

                $updatedSrc++

                # Confronta con la data usata nella copia (HASH.data_acquisizione)
                $sqlHashRead = "SELECT data_acquisizione, file, copiato FROM HASH WHERE hash = '$hashSql';"
                $hashRec = Invoke-SqliteQuery -DatabasePath $DatabasePath -Sql $sqlHashRead
                if ($hashRec) {
                    if ($hashRec -is [array]) { $hashRec = $hashRec[0] }

                    if ($hashRec -is [string]) {
                        $hashParts = $hashRec.Split('|')
                        $oldDataAcq = $hashParts[0]
                        $hashFile = $hashParts[1]
                        $copiato = $hashParts[2]
                    } else {
                        $oldDataAcq = $hashRec.data_acquisizione
                        $hashFile = $hashRec.file
                        $copiato = $hashRec.copiato
                    }

                    $newDateObj = [datetime]::ParseExact($exifData.dateTaken, 'yyyy-MM-dd HH:mm:ss', $null)
                    $newDay = $newDateObj.ToString('yyyy-MM-dd')
                    $oldDay = $null
                    if (-not [string]::IsNullOrWhiteSpace($oldDataAcq)) {
                        try {
                            $oldDateObj = [datetime]::Parse($oldDataAcq)
                            $oldDay = $oldDateObj.ToString('yyyy-MM-dd')
                        } catch {
                            $oldDay = $null
                        }
                    }

                    if ($newDay -ne $oldDay) {
                        if ([int]$copiato -eq 1 -and -not [string]::IsNullOrWhiteSpace($hashFile) -and (Test-Path -Path $hashFile)) {
                            # File gia' copiato: sposta nella cartella coerente con la nuova data EXIF.
                            $destPath = Join-Path $profilo.DestinationRoot ($newDateObj.ToString('yyyy'))
                            $destPath = Join-Path $destPath ($newDateObj.ToString('MM'))
                            if ($profilo.DestinationTemplate -eq 'YYYY/MM/DD') {
                                $destPath = Join-Path $destPath ($newDateObj.ToString('dd'))
                            }
                            if (-not (Test-Path -Path $destPath)) {
                                New-Item -ItemType Directory -Path $destPath | Out-Null
                            }

                            $targetFile = Join-Path $destPath (Split-Path $hashFile -Leaf)
                            if ($targetFile -ne $hashFile) {
                                if (Test-Path -Path $targetFile) {
                                    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($targetFile)
                                    $ext = [System.IO.Path]::GetExtension($targetFile)
                                    $i = 1
                                    do {
                                        $candidate = Join-Path $destPath ("{0}_{1}{2}" -f $baseName, $i, $ext)
                                        $i++
                                    } while (Test-Path -Path $candidate)
                                    $targetFile = $candidate
                                }
                                Move-Item -Path $hashFile -Destination $targetFile -Force
                                $movedFiles++
                            }

                            Set-ItemProperty -Path $targetFile -Name CreationTime -Value $newDateObj
                            Set-ItemProperty -Path $targetFile -Name LastWriteTime -Value $newDateObj

                            $targetFileSql = $targetFile.Replace("'", "''")
                            $sqlHashUpdate = "UPDATE HASH SET data_acquisizione = '$dateSql', file = '$targetFileSql' WHERE hash = '$hashSql';"
                            Invoke-SqliteQuery -DatabasePath $DatabasePath -Sql $sqlHashUpdate | Out-Null
                            $updatedHash++
                            Write-GuiLog $global:DEBUG "[Recalculate] Spostato file copiato e aggiornata data HASH: $targetFile"
                        } else {
                            # Non ancora copiato (o file non disponibile): aggiorna solo la data usata in HASH.
                            $sqlHashUpdate = "UPDATE HASH SET data_acquisizione = '$dateSql' WHERE hash = '$hashSql';"
                            Invoke-SqliteQuery -DatabasePath $DatabasePath -Sql $sqlHashUpdate | Out-Null
                            $updatedHash++
                            Write-GuiLog $global:DEBUG "[Recalculate] Aggiornata data HASH per hash=$hashValue"
                        }
                    }
                }
                
                Write-GuiLog $global:DEBUG "[Recalculate] Aggiornato EXIF per: $($fileObject.Name)"
            }
        } catch {
            Write-GuiLog $global:DEBUG "Errore durante elaborazione: $_"
            $errors++
        }
    }
    
    # Porta la progress bar a 100%
    if ($global:logProgressBar) { $global:logProgressBar.Value = 100 }
    
    Write-GuiLog $global:INFO "[Recalculate-ExifDates] Completata."
    Write-GuiLog $global:INFO "Riepilogo: SRC aggiornati=$updatedSrc, HASH aggiornati=$updatedHash, File spostati=$movedFiles, Errori=$errors, Totale=$total"
}