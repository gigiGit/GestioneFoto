# GestioneFoto.App

GestioneFoto e' una applicazione desktop C# (WinForms) pensata per facilitare il backup di foto/video da telefono e fotocamera.

## A cosa serve

1. acquisire i nuovi file da una cartella di appoggio (`IncludePath`);
2. organizzare i file copiati nel backup (`DestinationRoot`) secondo data;
3. evitare doppie elaborazioni inutili grazie a un codice identificativo univoco del file + tracking su database SQLite;
4. mantenere una vista per album (`AlbumDestination`) tramite collegamenti, senza duplicare spazio disco.

## Workflow operativo

1. `Scansione`: individua solo file mai caricati prima.
2. `Visualizza`: controllo dati e modifiche manuali (es. data acquisizione, album).
3. `Copia`: copia solo i file non ancora copiati.
4. `Rigenera Album`: ricrea i collegamenti album in qualsiasi momento.

## Gestione profili

Da GUI puoi:

1. selezionare un profilo esistente;
2. modificarne i campi;
3. salvare sullo stesso file o con `Salva come`.

I profili sono in `config/*.profile`.

## Documentazione

1. Guida operativa per utente non tecnico: [GUIDA_UTENTE.md](GUIDA_UTENTE.md)
2. Panoramica tecnica completa: [DOCUMENTAZIONE_COMPLETA.md](DOCUMENTAZIONE_COMPLETA.md)
3. Prerequisiti e inizializzazione ambiente di sviluppo: [prerequisit.txt](prerequisit.txt)

## Note

L'interfaccia operativa e' GestioneFoto.App (WinForms C#).

La GUI avvia `GestioneFoto.Scanner.exe` come processo separato: lo scanner
elabora i file in parallelo e serializza le scritture SQLite. Pausa, ripresa e
stop della scansione sono inoltrati al processo. Per eseguire una scansione
autonoma:

```powershell
.\GestioneFoto.Scanner.exe --profile .\config\Giuliano.profile --max-parallelism 8
```

La modalita' autonoma aggiorna la tabella `SRC`; la GUI coordina inoltre la
preparazione automatica successiva alla scansione. Lo script
`setup\Build-AppAndMsi.ps1` pubblica entrambi gli eseguibili insieme.

Per inizializzare una workstation Windows appena configurata, esegui dalla
radice del repository:

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\Inizializza-Progetto.ps1
```