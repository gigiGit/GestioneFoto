# CercaFoto e Analisi Media

Questo progetto contiene script per la gestione di file multimediali (immagini e video) su sistemi Windows. Include due script principali: uno per copiare file unici evitando duplicati, e uno per analizzare cartelle e identificare duplicati.

## Script Disponibili

### cercafoto.ps1
Script PowerShell per cercare e copiare file multimediali da percorsi specificati, evitando duplicati basati su hash MD5.

- **Scopo**: Copia file unici in una struttura organizzata per anno/mese.
- **Utilizzo**: `.\cercafoto.ps1`
- **Configurazione**: Usa `config.txt` per percorsi di scansione, esclusioni e dimensione minima.
- **Output**:
  - File copiati in `$destBase\backup\anno\mese\`
  - `hashes.dat`: traccia degli hash copiati
  - `original_paths.csv`: percorsi originali
  - `scan_log_*.txt`: log delle operazioni
  - `scan_err_*.txt`: errori
  - `duplicati.dat`: duplicati per nome

### visualizza_foto.ps1
Interfaccia grafica per visualizzare le foto dal backup e assegnare album.

- **Scopo**: Permette scorrimento veloce delle immagini usando `original_paths.csv`, con possibilità di editare il campo "Album".
- **Utilizzo**: `.\visualizza_foto.ps1`
- **Funzionalità**:
  - Lista dei file a sinistra per selezione (supporta selezione multipla con Shift per contigue, Ctrl per non contigue).
  - Anteprima delle immagini selezionate come miniature a destra.
  - Mostra il percorso originale del file.
  - Propone come nome album il nome della directory originale (se il campo è vuoto).
  - Pulsanti Precedente/Successivo per navigazione.
  - Tasti freccia sinistra/destra per scorrimento veloce.
  - Campo testo per inserire/modificare il nome dell'album.
  - Pulsante Salva per aggiornare il CSV (applica l'album a tutti i file selezionati; mostra messaggio temporaneo senza bloccare l'interfaccia).
- **Requisiti**: .NET Framework per Windows Forms.

### ricostruisci_album.ps1
Script PowerShell per ricostruire la struttura degli album basata sul campo "Album" nel CSV.

- **Scopo**: Crea cartelle per ogni album distinto e collega i file corrispondenti tramite hard link.
- **Utilizzo**: `.\ricostruisci_album.ps1`
- **Configurazione**: Usa `original_paths.csv` per i dati.
- **Output**:
  - Cartelle album in `$destBase\Album\`
  - File collegati come hard link per risparmiare spazio.
  - Solo i file con album assegnato vengono inclusi.
- **Requisiti**: Accesso alla cartella di backup e permessi per creare link.

### prealbum.ps1
Script PowerShell per precompilare il campo "Album" nel CSV basato sulla directory originale e l'anno di scatto.

- **Scopo**: Aggiorna automaticamente il campo Album per file la cui directory parent non è numerica, usando il template "NomeCartella - Anno".
- **Utilizzo**: `.\prealbum.ps1`
- **Logica**: Aggiorna solo i campi Album vuoti, saltando se la directory parent è numerica; imposta a "NomeCartella - AnnoScatto".
- **Output**: CSV aggiornato con album precompilati.

## Estensioni Supportate
jpg, jpeg, png, gif, bmp, tif, tiff, webp, mp4, avi, mov, mkv, wmv, flv, webm, m4v, 3gp, mpg, mpeg, nef.

## Configurazione
- **config.txt**: percorsi di scansione (`ScanPath=`), cartelle escluse, dimensione minima (`MinSize=`).
- Se `config.txt` non esiste, viene creato con valori predefiniti.

## Requisiti
- Windows con PowerShell.
- Accesso in lettura/scrittura alle cartelle.

## Note
- La scansione può richiedere tempo a seconda della dimensione del disco.
- Permessi amministrativi potrebbero essere necessari per alcune cartelle.
- I duplicati sono identificati tramite hash MD5.
- Per `analisi.ps1`, la data è estratta da EXIF (per immagini) o data del file.
- La cartella di destinazione (`$destBase`) viene creata automaticamente se non esiste.

## Esempi
- Copia da percorsi configurati: `.\cercafoto.ps1`
- Analizza una cartella specifica: `.\analisi.ps1 -inputFolder "J:\Photos"`