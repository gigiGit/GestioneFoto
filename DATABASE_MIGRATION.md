# Database Migration & Schema Updates

## Overview

Il progetto GestioneFoto evolve nel tempo, e i database vecchi potrebbero non avere tutte le colonne richieste dalle nuove versioni. Questa documentazione spiega come mantenere i database aggiornati.

## Migrations Implemented

### 1. THUMBNAIL Column
- **Added**: Support for image thumbnails stored directly in database
- **Table**: SRC
- **Column**: `thumbnail BLOB`
- **Purpose**: Memorizzare anteprime JPEG per visualizzazione veloce senza estrarre archivi

### 2. Scan metadata columns
- **Added**: `dtExifOriginal`, `dtExifDigitized`, `dtExifDateTime`, `dtAcqOverride`, `dtFolderName`, `dtCreation`, `absolutePath`
- **Table**: SRC
- **Purpose**: Conservare le date EXIF, la data di creazione, il percorso assoluto e l'eventuale correzione manuale della data definitiva. `dtFileName` esiste già e contiene la data estratta dal nome del file; `dtFolderName` contiene la data estratta dalla cartella più vicina che ne contiene una valida.
- **Compatibility**: `dtExif` conserva la data EXIF preferita per i consumatori legacy.
- **Performance**: La scansione riutilizza connessione e statement SQLite e calcola l'hash degli archivi in streaming.

### 3. Definitive acquisition date view
- **View**: `V_HASH_ACQUISITION_DATE` (un record per hash)
- **Date priority**: correzione manuale, EXIF Original, EXIF Digitized, EXIF DateTime, EXIF legacy, nome file, cartella, creazione file, data file.
- **Consumers**: preparazione della copia, copia sul filesystem e dettagli nella visualizzazione database.
- **Editing**: "Modifica Data" registra `dtAcqOverride` in SRC per tutti i record con lo stesso hash, senza alterare i dati EXIF originali.

### 4. scanSession Column
- **Added**: Track which scan session created each record
- **Table**: SRC
- **Column**: `scanSession INTEGER NOT NULL DEFAULT 0`
- **Purpose**: Raggruppare file per sessione di scansione

### 5. copied Column (HASH table)
- **Added**: Track which files have been copied
- **Table**: HASH
- **Column**: `copied INTEGER NOT NULL DEFAULT 0`
- **Purpose**: Tracciare stato di copia durante l'organizzazione file

## Opzioni di Migrazione

### Opzione 1: Migrazione Automatica (Consigliato)

L'applicazione **automaticamente** aggiunge colonne mancanti quando apre un database vecchio:

```csharp
// In HashDbWriter.EnsureSchema()
EnsureSrcThumbnailColumn(conn);
EnsureSrcColumn(conn, "dtExifOriginal", "TEXT");
EnsureSrcColumn(conn, "dtExifDigitized", "TEXT");
EnsureSrcColumn(conn, "dtExifDateTime", "TEXT");
EnsureSrcColumn(conn, "dtAcqOverride", "TEXT");
EnsureSrcColumn(conn, "dtFolderName", "TEXT");
EnsureSrcColumn(conn, "dtCreation", "TEXT");
EnsureSrcColumn(conn, "absolutePath", "TEXT");
EnsureSrcScanSessionColumn(conn);
EnsureHashCopiedColumn(conn);
EnsureAcquisitionDateView(conn);
```

**Vantaggi:**
- ✓ Trasparente, nulla da fare
- ✓ Veloce (pochi millisecondi)
- ✓ Sicuro, usa ALTER TABLE

**Quando avviene:**
- Ogni volta che l'app apre il database
- Primo avvio della scansione

### Opzione 2: Migrazione Manuale (Esplicita)

Se preferisci controllare quando aggiornare il database:

#### Usando PowerShell Script

```powershell
.\upgrade-database.ps1 -DbPath "test-archives\test-scan.db"
```

Output:
```
=== Database Upgrade ===

Database: test-archives\test-scan.db
Size: 0.05 MB

Checking schema...

Checking SRC table:
  ✓ THUMBNAIL column exists
  ✓ dtExifOriginal, dtExifDigitized, dtExifDateTime columns exist
  ✓ dtAcqOverride, dtFolderName, dtCreation and absolutePath columns exist
  ✓ V_HASH_ACQUISITION_DATE view exists
  ✓ scanSession column exists

Checking HASH table:
  ✓ copied column exists

Verifying tables:
  ✓ SRC table exists
  ✓ DST table exists
  ✓ HASH table exists
  ✓ APP_STATE table exists

Database statistics:
  SRC: 4 rows
  DST: 4 rows
  HASH: 4 rows

✓ Upgrade completed successfully
```

#### Usando il Tool Standalone

```powershell
DatabaseUpgradeTool.exe "path\to\database.db"
```

### Opzione 3: Ricreare il Database (Clean Slate)

Se vuoi ricominciare da zero senza conservare i vecchi dati:

```powershell
Remove-Item test-archives\test-scan.db
# Poi esegui la scansione normalmente
.\bin\GestioneFoto.App.exe --profile=test-archives\test.profile
```

## Schema Attuale

### Tabella: SRC (Source Files)
```sql
CREATE TABLE SRC (
    hash TEXT NOT NULL,
    file TEXT NOT NULL,
    size INTEGER,
    dtExif TEXT,
    dtExifOriginal TEXT,
    dtExifDigitized TEXT,
    dtExifDateTime TEXT,
    dtAcqOverride TEXT,
    dtFileName TEXT,
    dtFolderName TEXT,
    dtFile TEXT,
    dtCreation TEXT,
    absolutePath TEXT,
    scanSession INTEGER NOT NULL DEFAULT 0,
    thumbnail BLOB,
    PRIMARY KEY (hash, file)
);
```

`file` rimane il percorso usato dalle altre funzioni applicative ed è assoluto per le nuove scansioni; per gli elementi di archivio il valore mantiene il formato `percorso-assoluto-archivio|percorso-interno`. `absolutePath` ripete esplicitamente tale percorso. `dtCreation` e `absolutePath` restano NULL per righe preesistenti, per le quali non è possibile ricostruire in modo affidabile tali informazioni senza rileggere la sorgente.

`V_HASH_ACQUISITION_DATE` sceglie una data definitiva per ogni hash secondo la priorità documentata. Se non è disponibile alcuna data, la copia non crea una directory basata sulla data e salva il file direttamente in `DestinationRoot`.

### Tabella: HASH
```sql
CREATE TABLE HASH (
    hash TEXT NOT NULL PRIMARY KEY,
    filePathSrc TEXT NOT NULL,
    filePathDst TEXT NOT NULL,
    album TEXT,
    tipo TEXT,
    copied INTEGER NOT NULL DEFAULT 0
);
```

### Tabella: DST (Destination)
```sql
CREATE TABLE DST (
    hash TEXT NOT NULL PRIMARY KEY,
    data_acquisizione TEXT
);
```

### Tabella: APP_STATE
```sql
CREATE TABLE APP_STATE (
    key TEXT NOT NULL PRIMARY KEY,
    value TEXT
);
```

## Migration Safety

✅ **Tutte le migrazioni sono safe:**
- Usano `ALTER TABLE ... ADD COLUMN` (non distruttive)
- Aggiungono colonne alla fine (non cambiano indici)
- Forniscono DEFAULT VALUES per righe esistenti
- Verificano se la colonna esiste prima di aggiungerla

✅ **Zero data loss:**
- Dati esistenti non vengono modificati
- Solo nuove colonne vuote vengono aggiunte
- Backup non necessario (ma consigliato per cautela)

## When to Upgrade

| Scenario | Azione |
|----------|--------|
| **Nuovo database** | Automatico, nulla da fare |
| **Vecchio database, prima volta con nuova app** | Automatico (app aggiorna al primo run) |
| **Vuoi verificare schema prima di usare** | Esegui `upgrade-database.ps1` |
| **Profilo molto vecchio (pre-thumbnail)** | Esegui manualmente il tool di upgrade |
| **Crei nuova cartella di test** | Ricrea database da zero |

## Troubleshooting

### Errore: "Database is locked"
L'app sta usando il database. Chiudila prima di eseguire l'upgrade manuale.

```powershell
# Chiudi tutte le istanze
Get-Process | Where-Object Name -like "*GestioneFoto*" | Stop-Process -Force
# Poi esegui upgrade
.\upgrade-database.ps1
```

### Errore: "Database disk image is corrupted"
Il database è corrotto. Ricrea da zero:
```powershell
Remove-Item "test-archives\test-scan.db"
# Esegui scansione normalmente
```

### Upgrade script non trovato
Assicurati di eseguire da root della repo:
```powershell
cd G:\GestioneFoto
.\upgrade-database.ps1
```

## Future Migrations

Quando vengono aggiunte nuove feature che richiedono colonne DB:

1. **Nel codice** (HashDbWriter.cs):
   ```csharp
   private static void EnsureNewFeatureColumn(SqliteConnection conn)
   {
       // Verifica e aggiunge la colonna
   }
   ```

2. **In EnsureSchema()**:
   ```csharp
   public void EnsureSchema(string databasePath)
   {
       // ... CREATE TABLE ...
       EnsureNewFeatureColumn(conn);  // ← Aggiunto
   }
   ```

3. **Lo script di upgrade automaticamente considererà la nuova colonna.**

Per ogni nuova colonna, aggiungere la relativa verifica sia in `HashDbWriter.EnsureSchema()` sia nel tool standalone `DatabaseUpgradeTool`.
