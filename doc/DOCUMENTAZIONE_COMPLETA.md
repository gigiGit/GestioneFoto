# Documentazione Completa - GestioneFoto.App

## 1. Obiettivo

GestioneFoto.App e' una applicazione desktop C# per backup foto/video con workflow guidato, pensata per ridurre errori operativi.

Scenario d'uso tipico:

1. L'utente scarica i nuovi file da telefono/fotocamera in una cartella locale (`IncludePath`).
2. L'app riconosce i file nuovi e li traccia su database.
3. L'app copia i file nel backup definitivo (`DestinationRoot`) in struttura per data.
4. L'app puo' generare vista album in `AlbumDestination` tramite collegamenti ai file copiati.

## 2. Architettura

Componenti principali:

1. UI WinForms:
  - [src/GestioneFoto.App/UI/MainForm.cs](../src/GestioneFoto.App/UI/MainForm.cs)
  - [src/GestioneFoto.App/UI/DatabaseViewerForm.cs](../src/GestioneFoto.App/UI/DatabaseViewerForm.cs)
  - [src/GestioneFoto.App/UI/ProfileEditorForm.cs](../src/GestioneFoto.App/UI/ProfileEditorForm.cs)
2. Servizi:
  - [src/GestioneFoto.App/Services/ScanCoordinator.cs](../src/GestioneFoto.App/Services/ScanCoordinator.cs)
  - [src/GestioneFoto.App/Services/CopyCoordinator.cs](../src/GestioneFoto.App/Services/CopyCoordinator.cs)
  - [src/GestioneFoto.App/Services/HashDbWriter.cs](../src/GestioneFoto.App/Services/HashDbWriter.cs)
3. Configurazione profili:
  - [src/GestioneFoto.App/Config/ProfileLoader.cs](../src/GestioneFoto.App/Config/ProfileLoader.cs)
  - `config/*.profile`

## 3. Modello dati

Tabelle principali SQLite:

1. `SRC`: file sorgenti scansionati, con codice identificativo univoco del file, metadati data e `scanSession`.
2. `HASH` (nome tecnico tabella copia): file candidati alla copia e stato `copied`.
3. `APP_STATE`: stato workflow (`currentScanSession`, `lastPreparedScanSession`).

## 4. Regole di workflow (anti errore)

### 4.1 Scansione

1. avvia una nuova sessione scansione;
2. inserisce in `SRC` solo file non gia' presenti con stessa coppia `(codice identificativo file, percorso file)`;
3. i record gia' presenti non vengono ritoccati;
4. al termine esegue automaticamente la preparazione dei nuovi record per la copia.

### 4.2 Preparazione automatica (interna)

1. viene eseguita automaticamente da `Scansione`;
2. lavora solo sui file dell'ultima `scanSession`;
3. inserisce nella tabella copia (`HASH`) solo record nuovi;
4. non sovrascrive record gia' esistenti nella tabella copia (`HASH`), quindi preserva modifiche manuali.

### 4.3 Visualizza

1. permette modifica di campi operativi (es. data acquisizione, album);
2. tali modifiche restano persistenti perche' la preparazione automatica non riscrive record gia' presenti.

### 4.4 Copia

1. copia solo record con `copied = 0`;
2. al successo imposta `copied = 1`;
3. supporta Pausa/Riprendi/Stop.

### 4.5 Rigenera Album

1. eseguibile in qualsiasi momento;
2. crea collegamenti nelle cartelle album sotto `AlbumDestination`;
3. in caso di collisioni nome, il record viene ignorato senza errore bloccante.

## 5. Ordine operativo consigliato

1. Copiare nuovi file in `IncludePath`.
  - Telefono: cavo USB, abilita trasferimento file/immagini, copia con Esplora File.
  - Fotocamera: cavo USB o scheda SD nel computer, poi copia in `IncludePath`.
  - Consiglio operativo: crea sottocartelle con nomi significativi (es. `Festa di Laurea`, `Riccardo`, `Gita a Firenze`) per mantenere ordine e facilitare controllo/album.
2. `Scansione`.
3. `Visualizza` (opzionale ma consigliato).
4. `Copia`.
5. `Rigenera Album` quando serve riallineare gli album.

## 6. GUI e comandi

Menu:

1. `File -> Exit`
2. `Help -> Guida Utente`
3. `Help -> README`
4. `Help -> Documentazione Completa`
5. `Help -> Informazioni`

Pulsanti principali in ordine workflow:

1. Scansione
2. Visualizza
3. Copia
4. Rigenera Album

Controlli `Pausa/Riprendi/Stop`:

1. sono in pannello dedicato sotto i pulsanti principali;
2. diventano attivi solo nelle operazioni che li supportano (Scansione e Copia).

## 7. Profili

Campi principali profilo:

1. `ProfileName`
2. `UserName`
3. `IncludePath`
4. `ExcludePaths`
5. `Extensions`
6. `DestinationRoot`
7. `DatabasePath`
8. `DestinationTemplate`
9. `AlbumDestination`
10. `MinSize`

Significato operativo di ogni attributo:

1. `ProfileName`
  Nome logico del profilo mostrato nella GUI. Se manca, viene usato il nome del file `.profile`.
2. `UserName`
  Nome utente descrittivo (informativo) associato al profilo. Non modifica la logica di copia.
3. `IncludePath`
  Cartella sorgente dove vengono copiati i nuovi file da telefono/fotocamera prima della scansione.
4. `ExcludePaths`
  Elenco di sotto-percorsi da ignorare durante la scansione (separati da virgola o punto e virgola).
5. `Extensions`
  Elenco estensioni file ammesse (es. `jpg,png,mp4`). Nel parsing vengono normalizzate senza punto e in minuscolo.
6. `DestinationRoot`
  Cartella radice del backup finale; qui verranno creati i file ordinati per data secondo il template.
7. `DatabasePath`
  Percorso del database SQLite (`.db`) che mantiene stato scansione/copia (`SRC`, `HASH`, `APP_STATE`).
8. `DestinationTemplate`
  Regola di costruzione delle sottocartelle in destinazione basata sulla data (`YYYY`, `MM`, `DD`).
9. `AlbumDestination`
  Cartella radice dove vengono generati i collegamenti album. Se assente/vuota, la fase album viene saltata.
10. `MinSize`
   Dimensione minima file (in byte) per includere un elemento nella scansione.

Significato di `DestinationTemplate`:

1. `YYYY` = anno
2. `MM` = mese a due cifre
3. `DD` = giorno a due cifre

Esempio con data 1 giugno 2026:

1. `YYYY/MM` produce cartella `2026/06`
2. `YYYY/MM/DD` produce cartella `2026/06/01`

Gestione da GUI:

1. modifica profilo corrente;
2. `Salva` su stesso file;
3. `Salva come` su nuovo file.

## 8. Output finale per l'utente

1. Backup ordinato per data in `DestinationRoot`.
2. Tracciamento robusto su DB per evitare ricopie e sovrascritture indesiderate.
3. Struttura album separata in `AlbumDestination` basata su collegamenti.
