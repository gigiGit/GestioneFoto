@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Setup-Iniziale.ps1" %*
if errorlevel 1 (
  echo Setup non completato. Vedi messaggi sopra.
  exit /b 1
)
echo Setup completato con successo.
