import { app, BrowserWindow } from 'electron';
import path from 'path';

// Opzionale: passa il percorso scrivibile per il DB se il tuo server legge process.env.DB_PATH
//process.env.DB_PATH = path.join(app.getPath('userData'), 'database.sqlite');

// Avvia direttamente il tuo server esistente
import { app as serverApp } from "./server.js"

function createWindow() {
  const mainWindow = new BrowserWindow({
    width: 1280,
    height: 800,
    autoHideMenuBar: true, // Nasconde la barra dei menu classica (File, Modifica...)
    webPreferences: {
      nodeIntegration: false,
      contextIsolation: true
    }
  });

  // Sostituisci 3000 con la porta usata dal tuo src/server.js
  mainWindow.loadURL('http://localhost:3080');
}

app.whenReady().then(() => {
  // Attende mezzo secondo per dare tempo al server e a SQLite di avviarsi
  setTimeout(createWindow, 500);
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});
