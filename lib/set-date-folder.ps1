function Aggiorna-DataUltimaModificaCartella($dirPath) {
    if (-not (Test-Path $dirPath -PathType Container)) {
        Write-Host "Cartella non trovata: $dirPath"
        return
    }
    # Prendi tutte le date di ultima modifica di file e cartelle ricorsivamente
    $dates = Get-ChildItem -Path $dirPath -Recurse | Select-Object -ExpandProperty LastWriteTime
    if ($dates.Count -eq 0) {
        Write-Host "Nessun file o sottocartella trovata in $dirPath"
        return
    }
    $maxDate = ($dates | Measure-Object -Maximum).Maximum
    # Imposta la data di ultima modifica della cartella
    (Get-Item $dirPath).LastWriteTime = $maxDate
    Write-Host "Ultima modifica di $dirPath aggiornata a $maxDate"
}


function Set-DirLastWriteTime-Recursive($dirPath) {
    if (-not (Test-Path $dirPath -PathType Container)) { return }

    # Aggiorna prima tutte le sottocartelle (post-order)
    $subdirs = Get-ChildItem -Path $dirPath -Directory
    foreach ($sub in $subdirs) {
        Set-DirLastWriteTime-Recursive $sub.FullName
    }

    # Prendi tutte le date di file e sottocartelle (dopo aggiornamento)
    $dates = @()
    $files = Get-ChildItem -Path $dirPath -File
    if ($files) { $dates += $files | ForEach-Object { $_.LastWriteTime } }
    $folders = Get-ChildItem -Path $dirPath -Directory
    if ($folders) { $dates += $folders | ForEach-Object { (Get-Item $_.FullName).LastWriteTime } }

    if ($dates.Count -gt 0) {
        $maxDate = ($dates | Measure-Object -Maximum).Maximum
        (Get-Item $dirPath).LastWriteTime = $maxDate
        Write-Host "Impostata data $maxDate su $dirPath"
    }
}
Set-DirLastWriteTime-Recursive "d:\Mediabackup\Patty\backup"