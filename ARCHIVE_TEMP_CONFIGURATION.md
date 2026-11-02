# Configurazione Profile - Opzioni Avanzate

## Descrizione Campi

### Campo: `ArchiveTempPath`
**Tipo**: String (percorso directory)  
**Default**: Vuoto (usa %TEMP%)  
**Descrizione**: Specifica una cartella alternativa dove decomprimere temporaneamente i file da archivi ZIP, TAR.GZ, TGZ durante la scansione.

### Quando Usare `ArchiveTempPath`

#### Scenario 1: %TEMP% è pieno o quasi pieno
Se la partizione TEMP non ha spazio sufficiente:

```
ArchiveTempPath=D:\temp-archives
```

L'app userà `D:\temp-archives` per le estrazioni temporanee.

#### Scenario 2: Velocità (SSD vs HDD)
Se vuoi che le estrazioni avvengano su un disco più veloce:

```
ArchiveTempPath=D:\nvme-temp
```

#### Scenario 3: Rete/Cloud
Se la cartella sorgente è in rete e la TEMP è lenta:

```
ArchiveTempPath=C:\local-temp
```

Usa un disco locale (SSD preferibilmente) per velocità.

## Funzionamento Tecnico

### Senza ArchiveTempPath (Default)
```
%TEMP%
  ↓
GestioneFoto_a1b2c3d4/
  ├─ image1.jpg (estrai → analizza → cancella)
  ├─ image2.jpg (estrai → analizza → cancella)
  └─ ... (ripeti per ogni file nell'archivio)
  ↓
[Directory cancellata quando archivio completato]
```

### Con ArchiveTempPath Configurato
```
D:\custom-temp
  ↓
GestioneFoto_a1b2c3d4/
  ├─ image1.jpg (estrai → analizza → cancella)
  ├─ image2.jpg (estrai → analizza → cancella)
  └─ ... (ripeti per ogni file nell'archivio)
  ↓
[Directory cancellata quando archivio completato]
```

## Controllo Spazio Disponibile

L'app automaticamente:

1. **Verifica** lo spazio disponibile sul disco prima di estrarre ogni file
2. **Richiede** 110% della dimensione del file (buffer di sicurezza)
3. **Salta silenziosamente** i file se non c'è spazio
4. **Continua** con il prossimo file

### Esempio
- File archivio: 100 MB
- Spazio richiesto: 110 MB (buffer)
- Spazio disponibile: 50 MB
- ➜ **File saltato**, scansione continua con il prossimo

## Configurazione Esempio

```
# Profile con estrazione in cartella custom
ProfileName=Large-Archives
IncludePath=G:\archivi-grandi
DatabasePath=H:\db\scan.db
DestinationRoot=H:\output
ArchiveTempPath=D:\estrazione-temp
Extensions=jpg,png,tiff
MinSize=1000000
```

Se `D:\estrazione-temp` non è accessibile, l'app **fallback automatico** a %TEMP%.

Se nemmeno %TEMP% ha spazio, i file vengono saltati senza errore (resilienza).

## Pulizia Automatica

La directory temporanea viene **sempre ripulita**:
- ✅ Dopo ogni file processato (cancellazione singolo file)
- ✅ Dopo l'archivio completato (cancellazione directory intera)
- ✅ Se la scansione fallisce (pulizia nel finally block)
- ✅ Se l'app viene chiusa forzatamente (Windows pulisce %TEMP% al riavvio)

Non rimangono file orfani sul disco.
