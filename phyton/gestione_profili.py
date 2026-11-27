
import json
import tkinter as tk
from tkinter import ttk, messagebox, filedialog, simpledialog
from pathlib import Path


# ============================================================
# CONFIGURAZIONE
# ============================================================

CARTELLA_PROFILI = (
    Path(__file__).resolve().parent.parent / "profili"
)


# ============================================================
# FUNZIONI JSON
# ============================================================

def carica_profili():
    """
    Carica tutti i profili JSON presenti nella cartella profili.

    Restituisce un dizionario:
        {
            "Personale": {...},
            "Famiglia": {...}
        }
    """

    CARTELLA_PROFILI.mkdir(parents=True, exist_ok=True)

    profili = {}

    for file_json in sorted(CARTELLA_PROFILI.glob("*.json")):

        try:
            with file_json.open("r", encoding="utf-8") as file:
                dati = json.load(file)

            if not isinstance(dati, dict):
                print(
                    f"[ATTENZIONE] Profilo ignorato: "
                    f"{file_json.name} non contiene un oggetto JSON."
                )
                continue

            profili[file_json.stem] = dati

        except (json.JSONDecodeError, OSError) as errore:
            messagebox.showwarning(
                "Errore caricamento profilo",
                f"Impossibile leggere {file_json.name}:\n{errore}"
            )

    return profili


def salva_json(nome_file, dati):
    """
    Salva un profilo JSON.
    """

    CARTELLA_PROFILI.mkdir(parents=True, exist_ok=True)

    percorso = CARTELLA_PROFILI / f"{nome_file}.json"

    try:
        with percorso.open("w", encoding="utf-8") as file:
            json.dump(
                dati,
                file,
                ensure_ascii=False,
                indent=4
            )

        return True

    except (OSError, TypeError, ValueError) as errore:
        messagebox.showerror(
            "Errore salvataggio",
            f"Impossibile salvare il profilo:\n{errore}"
        )
        return False


def formatta_valore(valore):
    """
    Converte qualsiasi valore JSON in una stringa leggibile.
    """

    if valore is None:
        return "null"

    if isinstance(valore, bool):
        return "true" if valore else "false"

    if isinstance(valore, (dict, list)):
        return json.dumps(
            valore,
            ensure_ascii=False,
            indent=4
        )

    return str(valore)


def interpreta_valore(testo):
    """
    Converte il testo inserito nell'editor nel tipo JSON corretto.

    Esempi:
        true       -> bool
        123        -> int
        12.5       -> float
        null       -> None
        [1, 2]     -> list
        {"a": 1}   -> dict
        testo      -> string
    """

    testo = testo.strip()

    try:
        return json.loads(testo)

    except json.JSONDecodeError:
        # Se non è JSON valido, lo considera una stringa.
        return testo


# ============================================================
# EDITOR DINAMICO ATTRIBUTI
# ============================================================

class EditorAttributi(tk.Toplevel):

    def __init__(self, parent, dati, titolo="Modifica profilo"):

        super().__init__(parent)

        self.title(titolo)
        self.geometry("850x600")
        self.minsize(650, 400)

        self.parent = parent

        # Copia indipendente dei dati.
        self.dati = json.loads(
            json.dumps(dati, ensure_ascii=False)
        )

        self.risultato = None

        self.crea_interfaccia()
        self.carica_attributi()

        self.transient(parent)
        self.grab_set()

    # --------------------------------------------------------

    def crea_interfaccia(self):

        contenitore = ttk.Frame(self, padding=10)
        contenitore.pack(fill="both", expand=True)

        ttk.Label(
            contenitore,
            text="Attributi del profilo",
            font=("Arial", 12, "bold")
        ).pack(anchor="w", pady=(0, 8))

        # Tabella attributi
        frame_tabella = ttk.Frame(contenitore)
        frame_tabella.pack(fill="both", expand=True)

        self.tabella = ttk.Treeview(
            frame_tabella,
            columns=("attributo", "valore", "tipo"),
            show="headings",
            selectmode="browse"
        )

        self.tabella.heading(
            "attributo",
            text="Attributo"
        )

        self.tabella.heading(
            "valore",
            text="Valore"
        )

        self.tabella.heading(
            "tipo",
            text="Tipo"
        )

        self.tabella.column(
            "attributo",
            width=200,
            anchor="w"
        )

        self.tabella.column(
            "valore",
            width=450,
            anchor="w"
        )

        self.tabella.column(
            "tipo",
            width=100,
            anchor="center"
        )

        scrollbar = ttk.Scrollbar(
            frame_tabella,
            orient="vertical",
            command=self.tabella.yview
        )

        self.tabella.configure(
            yscrollcommand=scrollbar.set
        )

        self.tabella.pack(
            side="left",
            fill="both",
            expand=True
        )

        scrollbar.pack(
            side="right",
            fill="y"
        )

        self.tabella.bind(
            "<Double-1>",
            self.modifica_attributo
        )

        # Pulsanti gestione attributi
        frame_pulsanti = ttk.Frame(contenitore)
        frame_pulsanti.pack(fill="x", pady=10)

        ttk.Button(
            frame_pulsanti,
            text="Aggiungi",
            command=self.aggiungi_attributo
        ).pack(side="left", padx=3)

        ttk.Button(
            frame_pulsanti,
            text="Modifica",
            command=self.modifica_attributo
        ).pack(side="left", padx=3)

        ttk.Button(
            frame_pulsanti,
            text="Elimina",
            command=self.elimina_attributo
        ).pack(side="left", padx=3)

        ttk.Button(
            frame_pulsanti,
            text="Modifica JSON completo",
            command=self.modifica_json_completo
        ).pack(side="left", padx=3)

        # Pulsanti conferma
        frame_conferma = ttk.Frame(contenitore)
        frame_conferma.pack(fill="x", pady=(10, 0))

        ttk.Button(
            frame_conferma,
            text="Salva",
            command=self.conferma
        ).pack(side="right", padx=3)

        ttk.Button(
            frame_conferma,
            text="Annulla",
            command=self.destroy
        ).pack(side="right", padx=3)

    # --------------------------------------------------------

    def carica_attributi(self):

        for elemento in self.tabella.get_children():
            self.tabella.delete(elemento)

        for attributo, valore in self.dati.items():

            if isinstance(valore, bool):
                tipo = "booleano"

            elif valore is None:
                tipo = "null"

            elif isinstance(valore, dict):
                tipo = "oggetto"

            elif isinstance(valore, list):
                tipo = "lista"

            elif isinstance(valore, int):
                tipo = "intero"

            elif isinstance(valore, float):
                tipo = "decimale"

            else:
                tipo = "stringa"

            self.tabella.insert(
                "",
                "end",
                iid=attributo,
                values=(
                    attributo,
                    formatta_valore(valore),
                    tipo
                )
            )

    # --------------------------------------------------------

    def aggiungi_attributo(self):

        nome = simpledialog.askstring(
            "Nuovo attributo",
            "Nome dell'attributo:",
            parent=self
        )

        if nome is None:
            return

        nome = nome.strip()

        if not nome:
            messagebox.showwarning(
                "Attenzione",
                "Il nome dell'attributo non può essere vuoto."
            )
            return

        if nome in self.dati:
            messagebox.showwarning(
                "Attenzione",
                "Questo attributo esiste già."
            )
            return

        self.apri_editor_valore(nome, None, nuovo=True)

    # --------------------------------------------------------

    def modifica_attributo(self, evento=None):

        selezione = self.tabella.selection()

        if not selezione:
            return

        nome = selezione[0]

        valore = self.dati[nome]

        self.apri_editor_valore(
            nome,
            valore,
            nuovo=False
        )

    # --------------------------------------------------------

    def apri_editor_valore(self, nome, valore, nuovo=False):

        finestra = tk.Toplevel(self)

        finestra.title(
            "Nuovo attributo" if nuovo else f"Modifica: {nome}"
        )

        finestra.geometry("600x400")
        finestra.transient(self)
        finestra.grab_set()

        frame = ttk.Frame(finestra, padding=10)
        frame.pack(fill="both", expand=True)

        ttk.Label(
            frame,
            text="Nome attributo:"
        ).pack(anchor="w")

        campo_nome = ttk.Entry(frame)
        campo_nome.pack(fill="x", pady=(0, 10))
        campo_nome.insert(0, nome)

        ttk.Label(
            frame,
            text="Valore (testo o JSON):"
        ).pack(anchor="w")

        campo_valore = tk.Text(
            frame,
            height=12,
            wrap="word"
        )

        campo_valore.pack(
            fill="both",
            expand=True,
            pady=(0, 10)
        )

        if not nuovo:
            campo_valore.insert(
                "1.0",
                formatta_valore(valore)
            )

        def salva_valore():

            nuovo_nome = campo_nome.get().strip()

            if not nuovo_nome:
                messagebox.showwarning(
                    "Attenzione",
                    "Inserire il nome dell'attributo.",
                    parent=finestra
                )
                return

            if (
                nuovo_nome != nome
                and nuovo_nome in self.dati
            ):
                messagebox.showwarning(
                    "Attenzione",
                    "Esiste già un attributo con questo nome.",
                    parent=finestra
                )
                return

            testo = campo_valore.get(
                "1.0",
                "end-1c"
            )

            nuovo_valore = interpreta_valore(testo)

            if not nuovo:
                del self.dati[nome]

            self.dati[nuovo_nome] = nuovo_valore

            self.carica_attributi()

            finestra.destroy()

        frame_pulsanti = ttk.Frame(frame)
        frame_pulsanti.pack(fill="x")

        ttk.Button(
            frame_pulsanti,
            text="Salva",
            command=salva_valore
        ).pack(side="right", padx=3)

        ttk.Button(
            frame_pulsanti,
            text="Annulla",
            command=finestra.destroy
        ).pack(side="right", padx=3)

    # --------------------------------------------------------

    def elimina_attributo(self):

        selezione = self.tabella.selection()

        if not selezione:
            return

        nome = selezione[0]

        conferma = messagebox.askyesno(
            "Conferma eliminazione",
            f"Eliminare l'attributo '{nome}'?",
            parent=self
        )

        if conferma:
            del self.dati[nome]
            self.carica_attributi()

    # --------------------------------------------------------

    def modifica_json_completo(self):

        finestra = tk.Toplevel(self)

        finestra.title("Modifica JSON completo")
        finestra.geometry("750x550")
        finestra.transient(self)
        finestra.grab_set()

        frame = ttk.Frame(finestra, padding=10)
        frame.pack(fill="both", expand=True)

        testo = tk.Text(
            frame,
            wrap="none",
            font=("Consolas", 10)
        )

        testo.pack(fill="both", expand=True)

        testo.insert(
            "1.0",
            json.dumps(
                self.dati,
                ensure_ascii=False,
                indent=4
            )
        )

        def salva_json_completo():

            contenuto = testo.get("1.0", "end-1c")

            try:
                nuovo_dati = json.loads(contenuto)

                if not isinstance(nuovo_dati, dict):
                    raise ValueError(
                        "Il JSON principale deve essere un oggetto."
                    )

                self.dati = nuovo_dati
                self.carica_attributi()

                finestra.destroy()

            except (json.JSONDecodeError, ValueError) as errore:
                messagebox.showerror(
                    "JSON non valido",
                    str(errore),
                    parent=finestra
                )

        ttk.Button(
            frame,
            text="Applica modifiche",
            command=salva_json_completo
        ).pack(anchor="e", pady=8)

    # --------------------------------------------------------

    def conferma(self):

        self.risultato = self.dati
        self.destroy()


# ============================================================
# GESTIONE PROFILI
# ============================================================

class GestioneProfili:

    def __init__(self, root=None):

        self.root = root
        self.finestra = (
            tk.Toplevel(root) if root else tk.Tk()
        )

        self.finestra.title("Gestione profili")
        self.finestra.geometry("1050x650")
        self.finestra.minsize(800, 500)

        self.profili = {}
        self.nomi_profili = []
        self.profilo_selezionato = None
        self.risultato = None

        self.crea_interfaccia()
        self.aggiorna_elenco()

        self.finestra.protocol(
            "WM_DELETE_WINDOW",
            self.annulla
        )

    # --------------------------------------------------------

    def crea_interfaccia(self):

        principale = ttk.Frame(
            self.finestra,
            padding=10
        )

        principale.pack(
            fill="both",
            expand=True
        )

        # Pannello sinistro
        sinistra = ttk.Frame(principale)
        sinistra.pack(
            side="left",
            fill="y",
            padx=(0, 10)
        )

        ttk.Label(
            sinistra,
            text="Profili disponibili",
            font=("Arial", 11, "bold")
        ).pack(anchor="w", pady=(0, 5))

        self.elenco = tk.Listbox(
            sinistra,
            width=30,
            height=25,
            exportselection=False
        )

        self.elenco.pack(
            fill="y",
            expand=True
        )

        self.elenco.bind(
            "<<ListboxSelect>>",
            self.seleziona
        )

        ttk.Button(
            sinistra,
            text="Nuovo profilo",
            command=self.nuovo
        ).pack(fill="x", pady=(8, 2))

        ttk.Button(
            sinistra,
            text="Modifica profilo",
            command=self.modifica
        ).pack(fill="x", pady=2)

        ttk.Button(
            sinistra,
            text="Elimina profilo",
            command=self.elimina
        ).pack(fill="x", pady=2)

        ttk.Button(
            sinistra,
            text="Aggiorna elenco",
            command=self.aggiorna_elenco
        ).pack(fill="x", pady=2)

        # Pannello destro
        destra = ttk.Frame(principale)
        destra.pack(
            side="left",
            fill="both",
            expand=True
        )

        ttk.Label(
            destra,
            text="Attributi del profilo selezionato",
            font=("Arial", 11, "bold")
        ).pack(anchor="w", pady=(0, 5))

        frame_tabella = ttk.Frame(destra)
        frame_tabella.pack(
            fill="both",
            expand=True
        )

        self.tabella_attributi = ttk.Treeview(
            frame_tabella,
            columns=("attributo", "valore"),
            show="headings"
        )

        self.tabella_attributi.heading(
            "attributo",
            text="Attributo"
        )

        self.tabella_attributi.heading(
            "valore",
            text="Valore"
        )

        self.tabella_attributi.column(
            "attributo",
            width=200,
            anchor="w"
        )

        self.tabella_attributi.column(
            "valore",
            width=500,
            anchor="w"
        )

        scrollbar = ttk.Scrollbar(
            frame_tabella,
            orient="vertical",
            command=self.tabella_attributi.yview
        )

        self.tabella_attributi.configure(
            yscrollcommand=scrollbar.set
        )

        self.tabella_attributi.pack(
            side="left",
            fill="both",
            expand=True
        )

        scrollbar.pack(
            side="right",
            fill="y"
        )

        # Pulsanti inferiori
        frame_inferiore = ttk.Frame(principale)
        frame_inferiore.pack(
            fill="x",
            pady=(10, 0)
        )

        ttk.Button(
            frame_inferiore,
            text="Utilizza profilo selezionato",
            command=self.conferma
        ).pack(side="right", padx=3)

        ttk.Button(
            frame_inferiore,
            text="Annulla",
            command=self.annulla
        ).pack(side="right", padx=3)

    # --------------------------------------------------------

    def aggiorna_elenco(self):

        selezione_precedente = self.profilo_selezionato

        self.profili = carica_profili()
        self.nomi_profili = []

        self.elenco.delete(0, tk.END)

        for nome_file, dati in self.profili.items():

            nome_visualizzato = dati.get(
                "nome",
                nome_file
            )

            self.nomi_profili.append(nome_file)

            self.elenco.insert(
                tk.END,
                str(nome_visualizzato)
            )

        self.svuota_dettagli()

        self.profilo_selezionato = None

        if selezione_precedente in self.nomi_profili:
            indice = self.nomi_profili.index(
                selezione_precedente
            )

            self.elenco.selection_set(indice)
            self.elenco.activate(indice)
            self.mostra_profilo(selezione_precedente)

    # --------------------------------------------------------

    def svuota_dettagli(self):

        for elemento in self.tabella_attributi.get_children():
            self.tabella_attributi.delete(elemento)

    # --------------------------------------------------------

    def mostra_profilo(self, nome_file):

        self.svuota_dettagli()

        dati = self.profili.get(nome_file, {})

        self.profilo_selezionato = nome_file

        for attributo, valore in dati.items():

            self.tabella_attributi.insert(
                "",
                "end",
                values=(
                    attributo,
                    formatta_valore(valore)
                )
            )

    # --------------------------------------------------------

    def seleziona(self, evento=None):

        selezione = self.elenco.curselection()

        if not selezione:
            return

        indice = selezione[0]

        nome_file = self.nomi_profili[indice]

        self.mostra_profilo(nome_file)

    # --------------------------------------------------------

    def nuovo(self):

        nome_file = simpledialog.askstring(
            "Nuovo profilo",
            "Nome del file del profilo:",
            parent=self.finestra
        )

        if nome_file is None:
            return

        nome_file = nome_file.strip()

        if not nome_file:
            return

        if (
            Path(nome_file).name != nome_file
            or nome_file in (".", "..")
            or "/" in nome_file
            or "\\" in nome_file
        ):
            messagebox.showwarning(
                "Nome non valido",
                "Inserire un nome file valido, senza percorsi.",
                parent=self.finestra
            )
            return

        if (CARTELLA_PROFILI / f"{nome_file}.json").exists():
            messagebox.showwarning(
                "Profilo esistente",
                "Esiste già un profilo con questo nome.",
                parent=self.finestra
            )
            return

        editor = EditorAttributi(
            self.finestra,
            {"nome": nome_file},
            "Nuovo profilo"
        )

        self.finestra.wait_window(editor)

        if editor.risultato is None:
            return

        if salva_json(nome_file, editor.risultato):
            self.aggiorna_elenco()

            if nome_file in self.nomi_profili:
                indice = self.nomi_profili.index(nome_file)
                self.elenco.selection_set(indice)
                self.elenco.activate(indice)
                self.mostra_profilo(nome_file)

    # --------------------------------------------------------

    def modifica(self):

        nome_file = self.profilo_selezionato

        if not nome_file:
            messagebox.showwarning(
                "Attenzione",
                "Selezionare prima un profilo.",
                parent=self.finestra
            )
            return

        dati = self.profili[nome_file]

        editor = EditorAttributi(
            self.finestra,
            dati,
            f"Modifica profilo: {nome_file}"
        )

        self.finestra.wait_window(editor)

        if editor.risultato is None:
            return

        if salva_json(nome_file, editor.risultato):
            self.aggiorna_elenco()

            if nome_file in self.nomi_profili:
                indice = self.nomi_profili.index(nome_file)
                self.elenco.selection_set(indice)
                self.elenco.activate(indice)
                self.mostra_profilo(nome_file)

    # --------------------------------------------------------

    def elimina(self):

        nome_file = self.profilo_selezionato

        if not nome_file:
            messagebox.showwarning(
                "Attenzione",
                "Selezionare prima un profilo.",
                parent=self.finestra
            )
            return

        conferma = messagebox.askyesno(
            "Conferma eliminazione",
            f"Eliminare definitivamente il profilo '{nome_file}'?",
            parent=self.finestra
        )

        if not conferma:
            return

        percorso = CARTELLA_PROFILI / f"{nome_file}.json"

        try:
            percorso.unlink()

            self.profilo_selezionato = None
            self.aggiorna_elenco()

        except OSError as errore:
            messagebox.showerror(
                "Errore eliminazione",
                str(errore),
                parent=self.finestra
            )

    # --------------------------------------------------------

    def conferma(self):

        if not self.profilo_selezionato:
            messagebox.showwarning(
                "Attenzione",
                "Selezionare un profilo da utilizzare.",
                parent=self.finestra
            )
            return

        self.risultato = self.profili[
            self.profilo_selezionato
        ]

        self.finestra.destroy()

    # --------------------------------------------------------

    def annulla(self):

        self.risultato = None
        self.finestra.destroy()


# ============================================================
# FUNZIONE PUBBLICA
# ============================================================

def scegli_profilo():

    gestione = GestioneProfili()

    gestione.finestra.mainloop()

    return gestione.risultato
