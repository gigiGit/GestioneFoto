# Guida Utente

Questa applicazione serve a fare **backup ordinato** di foto e video provenienti da telefono o fotocamera.

Obiettivo pratico:

1. Copi nel PC i nuovi file dentro una cartella di appoggio (`IncludePath`).
2. L'app li riconosce e li registra una sola volta.
3. Li copia nel backup finale (`DestinationRoot`) in cartelle per data.
4. Se vuoi, crea anche cartelle album (`AlbumDestination`) con collegamenti ai file gia' copiati.

## Prima configurazione (una volta sola)

1. Apri l'app C# `GestioneFoto.App`.
2. Seleziona un profilo o creane uno nuovo con `Modifica Profilo` + `Salva come`.
3. Controlla questi campi:
	- `IncludePath`: cartella temporanea dove scarichi le nuove foto/video.
	- `DestinationRoot`: cartella/disco di backup definitivo.
	- `AlbumDestination`: cartella radice degli album (opzionale ma consigliata).
	- `DatabasePath`: file `.db` usato per ricordare lo stato dei file.
	- `DestinationTemplate`: struttura cartelle di destinazione (`YYYY/MM` o `YYYY/MM/DD`).

Significato di `DestinationTemplate`:

1. `YYYY` = anno (es. `2026`)
2. `MM` = mese a due cifre (es. `06`)
3. `DD` = giorno a due cifre (es. `01`)

Esempio pratico con foto del 1 giugno 2026:

1. `YYYY/MM` crea la cartella `2026/06`
2. `YYYY/MM/DD` crea la cartella `2026/06/01`

Il template attivo viene applicato al momento della copia, anche ai file gia'
registrati nel database ma ancora da copiare. Cambiare template non sposta
automaticamente i file gia' copiati.

## Flusso consigliato (sempre uguale)

### 1) Copia i nuovi file in IncludePath

Questa parte la fai tu prima di premere `Scansione`.

Telefono:

1. collega il telefono al computer con cavo USB;
2. sul telefono abilita il trasferimento foto/file (MTP o modalita' simile);
3. apri Esplora File e accedi alla memoria del telefono;
4. copia le nuove foto/video nella cartella `IncludePath` del profilo.

Fotocamera:

1. collega la fotocamera al computer con cavo USB, se disponibile;
2. se il collegamento diretto non e' disponibile, estrai la scheda SD e inseriscila nel computer/lettore;
3. apri Esplora File e individua la cartella con i nuovi scatti/video;
4. copia i file nella cartella `IncludePath` del profilo.

Suggerimento pratico:

1. non cancellare subito i file originali da telefono/fotocamera;
2. cancellali solo dopo aver completato `Scansione` -> `Copia` e verificato il backup.
3. durante la copia in `IncludePath`, raggruppa i nuovi file in sottocartelle con nome significativo (es. `Festa di Laurea`, `Riccardo`, `Gita a Firenze`): ti aiuta nella verifica in `Visualizza` e nell'organizzazione album.

### 2) Premi `Scansione`

La scansione:

1. analizza `IncludePath`;
2. inserisce nel database solo file mai registrati prima (stesso codice identificativo del file + stesso percorso non vengono ritoccati);
3. prepara automaticamente i nuovi file alla fase di copia (non serve un comando separato di preparazione).

Usa `Pausa`, `Riprendi`, `Stop` se necessario.

### 3) Premi `Visualizza` (opzionale ma utile)

Qui puoi:

1. controllare i record;
2. usare `Solo duplicati` per mostrare i file con hash condiviso da piu' record; il filtro si combina con gli altri filtri e `Mostra tutti` lo disattiva;
3. selezionare un solo duplicato con la data corretta e premere `Allinea date duplicati` per assegnare quella data a tutti i file con lo stesso hash;
4. modificare data acquisizione;
5. impostare o correggere album.

L'allineamento aggiorna anche il percorso destinazione nel database secondo la data e il template attivi; non sposta file gia' copiati.

### 4) Premi `Copia`

La copia:

1. trasferisce solo i file non ancora copiati;
2. aggiorna lo stato nel database;
3. crea i file nel backup in `DestinationRoot`.

Usa `Pausa`, `Riprendi`, `Stop` se necessario.

### 5) Premi `Rigenera Album` (quando vuoi)

`Rigenera Album`:

1. crea/aggiorna cartelle album sotto `AlbumDestination`;
2. mette collegamenti ai file gia' presenti nel backup;
3. se trova nomi gia' presenti li ignora senza errori bloccanti.

## Cosa ottieni alla fine

1. **Backup ordinato per data** in `DestinationRoot`.
2. **Stato tracciato** nel database (evita ricopie inutili).
3. **Album trasversali** in `AlbumDestination` senza duplicare spazio disco.

## Frequenza operativa consigliata

1. Ogni volta che scarichi nuovi file: `Scansione` -> `Visualizza` (se vuoi) -> `Copia`.
2. Quando aggiorni album: `Rigenera Album`.
