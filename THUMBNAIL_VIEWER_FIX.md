# Database Viewer Thumbnail Fix

## Problema

Quando si apriva il Database Viewer, l'applicazione si bloccava con errore:

```
System.ArgumentException: Column 'Thumbnail' does not belong to table SourceRows.
   at GestioneFoto.App.UI.DatabaseViewerForm.ToSourceFileRow(DataRowView rowView)
```

## Causa Root

Il metodo `ToSourceFileRow()` cercava di accedere alla colonna `Thumbnail` nel DataTable, ma:

1. ✗ La colonna non era stata aggiunta a `InitializeTableSchema()`
2. ✗ Il valore thumbnail non veniva passato quando si aggiungevano righe con `_rowsTable.Rows.Add()`

## Soluzione Implementata

### Fix #1: Aggiungi colonna al schema (linea 376)

```csharp
private void InitializeTableSchema()
{
    // ... altre colonne ...
    _rowsTable.Columns.Add("Thumbnail", typeof(byte[]));  // ← AGGIUNTO
}
```

### Fix #2: Aggiungi thumbnail quando si popola la tabella (linea 413)

```csharp
foreach (var row in loadedRows)
{
    _rowsTable.Rows.Add(
        row.Hash,
        row.FilePathSrc,
        // ... altre colonne ...
        row.Thumbnail is not null ? (object)row.Thumbnail : DBNull.Value);  // ← AGGIUNTO
}
```

## Flusso di Dati

Prima (❌ Broken):
```
Database
  ↓ (query SELECT ... s.thumbnail)
ReadRowsFromDb()
  ↓ (row.Thumbnail = byte[] data)
LoadRowsAsync()
  ↓ (foreach row, ma thumbnail non passato)
_rowsTable.Rows.Add()  ← Colonna mancante nel DataTable!
  ↓
ToSourceFileRow()
  ↓ (prova ad accedere: row.Field<byte[]>("Thumbnail"))
CRASH! 💥
```

Dopo (✅ Fixed):
```
Database
  ↓ (query SELECT ... s.thumbnail)
ReadRowsFromDb()
  ↓ (row.Thumbnail = byte[] data)
LoadRowsAsync()
  ↓ (foreach row, thumbnail passato)
_rowsTable.Rows.Add(..., row.Thumbnail)  ← Colonna presente!
  ↓
GridBindingSource
  ↓
ToSourceFileRow()
  ↓ (accede: row.Field<byte[]>("Thumbnail"))
✓ Funziona! 
  ↓
UpdatePreview()
  ↓ (mostra thumbnail)
```

## File Modificati

- **DatabaseViewerForm.cs**
  - Linea 376: Aggiunta colonna `Thumbnail` in `InitializeTableSchema()`
  - Linea 413: Aggiunto parametro `thumbnail` in `Rows.Add()`

## Testing

```powershell
# Esegui nuovamente il test
.\bin\GestioneFoto.App.exe --profile=test-archives\test.profile

# Clicca "Scansione"
# Clicca "Visualizza Database"
# Seleziona un file → deve mostrare il thumbnail senza crash
```

## Verifica del Fix

✓ Colonna THUMBNAIL nel DataTable  
✓ Dati thumbnail caricati da database  
✓ ToSourceFileRow() accede alla colonna senza errore  
✓ UpdatePreview() mostra thumbnail dall'oggetto SourceFileRow  
✓ UI non si blocca più  

## Note Tecniche

- **Tipo colonna**: `byte[]` per dati BLOB
- **Gestione null**: `row.Thumbnail is not null ? (object)row.Thumbnail : DBNull.Value`
- **Compatibilità**: Funziona con database nuovi e vecchi (colonna aggiunta automaticamente se mancante)
