import tkinter as tk
from tkinter import ttk

import json
from pathlib import Path
from datetime import datetime


CARTELLA_PROFILI = Path("profili")

def prepara_casi_particolari(casi,risultato):
    
    for data_iso, nomi_file in casi.items():
        data = datetime.fromisoformat(
            data_iso.replace("Z", "+00:00")
        ).replace(tzinfo=None)
        for nome in nomi_file:
            risultato[nome.lower()] = data
    return 



def scegli_profilo():
    profili = carica_profili()

    finestra = tk.Tk()
    finestra.title("GestioneFoto3")
    finestra.geometry("420x230")
    finestra.resizable(False, False)

    risultato = {"profilo": None}

    ttk.Label(
        finestra,
        text="Seleziona il profilo di archiviazione",
        font=("Segoe UI", 12, "bold")
    ).pack(pady=20)

    selezione = tk.StringVar()

    combo = ttk.Combobox(
        finestra,
        textvariable=selezione,
        values=list(profili.keys()),
        state="readonly",
        width=35
    )
    combo.pack(pady=10)

    if profili:
        combo.current(0)

    def conferma():
        nome = selezione.get()
        if nome:
            risultato["profilo"] = profili[nome]
            risultato["nome"] = nome
            finestra.destroy()

    ttk.Button(
        finestra,
        text="Avvia",
        command=conferma
    ).pack(pady=20)

    finestra.mainloop()

    return risultato


def carica_profili():
    profili = {}

    CARTELLA_PROFILI.mkdir(parents=True, exist_ok=True)

    for file in sorted(CARTELLA_PROFILI.glob("*.json")):
        try:
            with open(file, "r", encoding="utf-8") as f:
                configurazione = json.load(f)

            profili[file.stem] = configurazione

        except (json.JSONDecodeError, OSError) as e:
            print(f"Errore nel profilo {file.name}: {e}")

    return profili
