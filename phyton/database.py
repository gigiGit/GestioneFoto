
import sqlite3
from pathlib import Path

debug = False
def inizializza_database(percorso_db):
    conn = sqlite3.connect(percorso_db)

    conn.execute("""
        CREATE TABLE IF NOT EXISTS file_originali (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            hash TEXT NOT NULL,
            percorso_originale TEXT NOT NULL,
            nome_file TEXT,
            cartella_album TEXT,
            sorgente TEXT,
            UNIQUE(hash, percorso_originale)
        )
    """)

    conn.execute("""
        CREATE INDEX IF NOT EXISTS idx_hash
        ON file_originali(hash)
    """)

    conn.commit()
    return conn


def registra_file(conn, hash_file, percorso_originale, sorgente=None):

    percorso = Path(percorso_originale)

    cursor = conn.execute("""
        INSERT OR IGNORE INTO file_originali
        (hash, percorso_originale, nome_file,
         cartella_album, sorgente)
        VALUES (?, ?, ?, ?, ?)
    """, (
        hash_file,
        str(percorso_originale),
        percorso.name,
        percorso.parent.name,
        sorgente
    ))

    conn.commit()
    if 'debug' in globals() and debug:
        print(
            f"[DATABASE] Hash: {hash_file[:12]} | "
            f"File: {percorso_originale} | "
            f"Inserito: {cursor.rowcount}"
        )
        print(
            "[DATABASE] Record presenti:",
            conn.execute(
                "SELECT COUNT(*) FROM file_originali"
            ).fetchone()[0]
        )