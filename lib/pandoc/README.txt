Pandoc si installa al di fuori del repository tramite winget:

    winget install --id JohnMacFarlane.Pandoc --exact

Oppure esegui dalla radice del repository:

    powershell -ExecutionPolicy Bypass -File .\setup\Inizializza-Progetto.ps1

Per aggiornare le guide HTML, esegui:

    powershell -ExecutionPolicy Bypass -File .\lib\converti-help.ps1

Lo script usa prima un'eventuale copia locale ignorata da Git e poi `pandoc.exe`
presente nel PATH.