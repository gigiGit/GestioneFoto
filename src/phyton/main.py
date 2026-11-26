import os
import re
import zipfile
import tarfile
import hashlib
from io import BytesIO
from datetime import datetime, timezone
from pathlib import Path
import json
import struct
import warnings
from PIL import Image, ExifTags

Image.MAX_IMAGE_PIXELS = 200_000_000
FILE_CONFIG = "config.ini"
FILE_CONFIG_JSON = "giuliano.json"
FILE_DA_IGNORARE = {'thumbs.db', '.ds_store', 'desktop.ini', 'indice_hash.txt'}
ESTENSIONI_DA_IGNORARE = {'.ctg', '.tmp', '.ini', '.db', '.json'}
#ESTENSIONI_IMMAGINI = {'.jpg', '.jpeg', '.tiff', '.webp', '.dng', '.nef', '.cr2', '.arw'}
ESTENSIONI_TAR = ('.tar', '.tar.gz', '.tgz')

def prepara_casi_particolari(casi):
    risultato = {}

    for data_iso, nomi_file in casi.items():
        data = datetime.fromisoformat(
            data_iso.replace("Z", "+00:00")
        ).replace(tzinfo=None)

        for nome in nomi_file:
            risultato[nome.lower()] = data

    return risultato



def carica_configurazione_json(percorso_config):
    config_path = Path(percorso_config)
    with config_path.open("r", encoding="utf-8") as f:
        profilo = json.load(f)
        return  profilo

profilo = carica_configurazione_json(FILE_CONFIG_JSON)
ESTENSIONI_IMMAGINI = {
    ext.lower() for ext in profilo.get("extensions", [])
}
SORGENTI = profilo.get("sourceFolders", [])
DESTINAZIONE = profilo.get("destDir")

if not DESTINAZIONE or not SORGENTI:
    raise ValueError("Configurazione JSON incompleta")

CASI_PARTICOLARI = prepara_casi_particolari(  profilo.get("casiParticolari", {}))
FORMATO_CARTELLE = profilo.get("formato", "%Y/%m/%d")

def carica_configurazione(percorso_config):
    if not os.path.exists(percorso_config):
        with open(percorso_config, "w", encoding="utf-8") as f:
            f.write("[IMPOSTAZIONI]\n")
            f.write("destinazione = C:\\Percorso\\Destinazione\\Ordinata\n\n")
            f.write("[SORGENTI]\n")
            f.write("# Inserisci un percorso per riga (cartelle, schede SD, singoli ZIP o TAR)\n")
            f.write("E:\\DCIM\n")
            f.write("D:\\Backup_Vecchi\\Archivi_Foto\n")
        print(f"Creato file '{percorso_config}' di esempio. Compilalo con i tuoi percorsi e rilancia lo script.")
        exit()

    destinazione = ""
    sorgenti = []
    sezione_corrente = None

    with open(percorso_config, "r", encoding="utf-8") as f:
        for riga in f:
            riga_pulita = riga.strip()
            if not riga_pulita or riga_pulita.startswith(("#", ";")):
                continue
            if riga_pulita.startswith("[") and riga_pulita.endswith("]"):
                sezione_corrente = riga_pulita[1:-1].strip().upper()
                continue

            if sezione_corrente == "IMPOSTAZIONI" and "=" in riga_pulita:
                chiave, valore = riga_pulita.split("=", 1)
                if chiave.strip().lower() == "destinazione":
                    destinazione = valore.strip().strip('"')
            elif sezione_corrente == "SORGENTI":
                sorgenti.append(riga_pulita.strip('"'))

    if not destinazione or not sorgenti:
        print(f"[ERRORE] Specifica sia 'destinazione' che almeno una sorgente in '{percorso_config}'.")
        exit(1)

    return destinazione, sorgenti

def calcola_solo_hash_file(percorso_file):
    hasher = hashlib.sha256()
    with open(percorso_file, "rb") as f:
        while chunk := f.read(65536):
            hasher.update(chunk)
    return hasher.hexdigest()

def sincronizza_indice_destinazione(cartella_dest, percorso_indice):
    hash_visti = set()
    if os.path.exists(percorso_indice):
        with open(percorso_indice, "r", encoding="utf-8") as f:
            hash_visti = set(line.strip() for line in f if line.strip())

    nuovi_indicizzati = 0
    print("Verifica dei file già presenti nella cartella di destinazione...")
    
    with open(percorso_indice, "a", encoding="utf-8") as f_indice:
        for root, _, files in os.walk(cartella_dest):
            for file in files:
                if file.lower() in FILE_DA_IGNORARE:
                    continue
                percorso_completo = os.path.join(root, file)
                file_hash = calcola_solo_hash_file(percorso_completo)
                
                if file_hash not in hash_visti:
                    hash_visti.add(file_hash)
                    f_indice.write(file_hash + "\n")
                    nuovi_indicizzati += 1
                    print(f"\rNuovo file indicizzato: {percorso_completo}", end="", flush=True)

    if nuovi_indicizzati > 0:
        print(f"Aggiunti all'indice {nuovi_indicizzati} file preesistenti trovati nella destinazione.")
    
    return hash_visti

def salva_nuovo_hash(percorso_indice, file_hash):
    with open(percorso_indice, "a", encoding="utf-8") as f:
        f.write(file_hash + "\n")

from datetime import datetime
from io import BytesIO
from PIL import Image, ExifTags


def estrai_data_exif(dati_bytes):
    try:
        with warnings.catch_warnings():
            warnings.filterwarnings(
                "ignore",
                message=r"Tag data larger than file.*",
                category=UserWarning,
                module=r"PIL\.TiffImagePlugin"
            )

            with Image.open(BytesIO(dati_bytes)) as img:
                exif = img.getexif()

                if not exif:
                    return None

                exif_ifd = exif.get_ifd(ExifTags.IFD.Exif)

                candidati = [
                    exif_ifd.get(36867),
                    exif_ifd.get(36868),
                    exif.get(306)
                ]

                for valore in candidati:
                    if valore and isinstance(valore, str):
                        pulito = valore.replace("\x00", "").strip()[:19]

                        try:
                            return datetime.strptime(
                                pulito, "%Y:%m:%d %H:%M:%S"
                            )
                        except ValueError:
                            continue

    except Exception:
        pass

    return None

def estrai_data_da_nome(nome_file):
    match = re.search(r'(199\d|20[0-3]\d)[-._]?(0[1-9]|1[0-2])[-._]?(0[1-9]|[12]\d|3[01])', nome_file)
    if match:
        anno, mese, giorno = int(match.group(1)), int(match.group(2)), int(match.group(3))
        try:
            return datetime(anno, mese, giorno)
        except ValueError:
            pass
    return None


def ottieni_data_migliore(nome_file, timestamp_fallback, primi_bytes):
    nome = Path(nome_file).name.lower()
    if nome in CASI_PARTICOLARI:
        return CASI_PARTICOLARI[nome]
    
    ext = Path(nome_file).suffix.lower()

    # 1. Priorità alla data EXIF
    if ext in ESTENSIONI_IMMAGINI and primi_bytes:
        dt_exif = estrai_data_exif(primi_bytes)
        if dt_exif:
            return dt_exif
    # 2. Data ricavata dal nome del file
    dt_nome = estrai_data_da_nome(Path(nome_file).name)
    if dt_nome:
        return dt_nome
    # 3. Data dell'archivio o del file system
    #if isinstance(timestamp_fallback, datetime):
    #    return timestamp_fallback    
    #return datetime.fromtimestamp(timestamp_fallback)
    
    # 4. Se nessuna delle precedenti ha funzionato, usa la data corrente come fallback
    # in questo modo, si potranno gestire come casi particolari i file senza data valida
    return datetime.now()


def calcola_hash_e_intestazione(stream):
    hasher = hashlib.sha256()
    intestazione = bytearray()
    while chunk := stream.read(65536):
        hasher.update(chunk)
        if len(intestazione) < 131072:
            intestazione.extend(chunk)
    return hasher.hexdigest(), bytes(intestazione)

def scrivi_file_unico(stream, nome_file, dt, cartella_dest):
    # Crea il percorso usando il formato definito nel JSON
    percorso_relativo = dt.strftime(FORMATO_CARTELLE)

    cartella_data = Path(cartella_dest) / percorso_relativo
    cartella_data.mkdir(parents=True, exist_ok=True)

    percorso_finale = cartella_data / Path(nome_file).name

    # Evita di sovrascrivere file con lo stesso nome
    contatore = 1

    while percorso_finale.exists():
        stem = Path(nome_file).stem
        suffix = Path(nome_file).suffix

        percorso_finale = cartella_data / f"{stem}_{contatore}{suffix}"
        contatore += 1

    # Copia fisicamente il file
    with open(percorso_finale, "wb") as f_out:
        while chunk := stream.read(65536):
            f_out.write(chunk)

    # Imposta la data di accesso e modifica sulla data di scatto
    timestamp_scatto = dt.timestamp()

    os.utime(
        percorso_finale,
        (timestamp_scatto, timestamp_scatto)
    )

def da_ignorare(nome_file):
    p = Path(nome_file)
    return p.name.lower() in FILE_DA_IGNORARE or p.suffix.lower() in ESTENSIONI_DA_IGNORARE
def get_real_timestamp(info):
  # Cerca il campo extra UNIX timestamp (Header ID 0x5455)
  extra = info.extra
  pos = 0
  while pos + 4 <= len(extra):
    header_id, data_size = struct.unpack('<HH', extra[pos : pos + 4])
    if header_id == 0x5455 and data_size >= 5:
      flags = extra[pos + 4]
      if flags & 1:  # Bit 0 indica la presenza del mtime (ultima modifica)
        mtime = struct.unpack('<I', extra[pos + 5 : pos + 9])[0]
        return datetime.fromtimestamp(mtime, tz=timezone.utc)
    pos += 4 + data_size

def processa_elemento(percorso_completo, hash_visti, percorso_indice, cartella_dest, stats):
    nome_file = os.path.basename(percorso_completo)
    nome_lower = nome_file.lower()

    if nome_lower.endswith(".zip"):
        try:
            with zipfile.ZipFile(percorso_completo, 'r') as z:
                for info in z.infolist():
                    if info.is_dir() or da_ignorare(info.filename):
                        continue
                    with z.open(info) as f_in:
                        file_hash, intestazione = calcola_hash_e_intestazione(f_in)
                    
                    if file_hash in hash_visti:
                        stats['duplicati'] += 1
                        continue

                    dt_archivio =  get_real_timestamp(info)
                    if dt_archivio is None:
                        dt_archivio = datetime(*info.date_time)
                    dt_finale = ottieni_data_migliore(info.filename, dt_archivio, intestazione)
                    
                    with z.open(info) as f_in:
                        scrivi_file_unico(f_in, info.filename, dt_finale, cartella_dest)
                    
                    hash_visti.add(file_hash)
                    salva_nuovo_hash(percorso_indice, file_hash)
                    stats['salvati'] += 1
        except zipfile.BadZipFile as e:
            print(f"[ATTENZIONE] ZIP corrotto o non valido saltato: {percorso_completo}")
            print(f"[ERRORE] {percorso_completo} -> Motivo: {e}")

    elif nome_lower.endswith(ESTENSIONI_TAR):
        try:
            print(f"Elaborazione TAR: {percorso_completo}")
            with tarfile.open(percorso_completo, 'r:*') as t:
                for member in t.getmembers():
                    if not member.isfile() or da_ignorare(member.name):
                        continue
                    f_in = t.extractfile(member)
                    if not f_in:
                        continue
                    file_hash, intestazione = calcola_hash_e_intestazione(f_in)
                    f_in.close()

                    if file_hash in hash_visti:
                        stats['duplicati'] += 1
                        continue

                    dt_finale = ottieni_data_migliore(member.name, member.mtime, intestazione)
                    
                    f_in = t.extractfile(member)
                    if f_in:
                        scrivi_file_unico(f_in, member.name, dt_finale, cartella_dest)
                        f_in.close()
                        hash_visti.add(file_hash)
                        salva_nuovo_hash(percorso_indice, file_hash)
                        stats['salvati'] += 1
        except tarfile.TarError:
            print(f"[ATTENZIONE] TAR corrotto o non valido saltato: {percorso_completo}")

    else:
        if da_ignorare(nome_file):
            return
        with open(percorso_completo, "rb") as f_in:
            file_hash, intestazione = calcola_hash_e_intestazione(f_in)
        
        if file_hash in hash_visti:
            stats['duplicati'] += 1
            return

        dt_fs = os.path.getmtime(percorso_completo)
        dt_finale = ottieni_data_migliore(nome_file, dt_fs, intestazione)

        with open(percorso_completo, "rb") as f_in:
            scrivi_file_unico(f_in, nome_file, dt_finale, cartella_dest)
        
        hash_visti.add(file_hash)
        salva_nuovo_hash(percorso_indice, file_hash)
        stats['salvati'] += 1


def elabora_tutto():
    cartella_destinazione = DESTINAZIONE
    sorgenti = SORGENTI

    Path(cartella_destinazione).mkdir(parents=True, exist_ok=True)
    percorso_indice = os.path.join(
        cartella_destinazione, "indice_hash.txt"
    )

    hash_visti = sincronizza_indice_destinazione(
        cartella_destinazione, percorso_indice
    )

    stats = {'salvati': 0, 'duplicati': 0}

    print(f"Totale impronte univoche in archivio: {len(hash_visti)}\n")

    for sorgente in sorgenti:
        if not os.path.exists(sorgente):
            print(f"[SALTATO] Percorso non trovato: {sorgente}")
            continue

        print(f"Scansione in corso: {sorgente} ...")

        if os.path.isfile(sorgente):
            processa_elemento(
                sorgente, hash_visti, percorso_indice,
                cartella_destinazione, stats
            )
        else:
            for root, _, files in os.walk(sorgente):
                for file in files:
                    percorso_completo = os.path.join(root, file)
                    processa_elemento(
                        percorso_completo, hash_visti,
                        percorso_indice, cartella_destinazione, stats
                    )

    print(
        f"\nCompletato! Nuovi file salvati: {stats['salvati']} "
        f"| Duplicati ignorati: {stats['duplicati']}"
    )


if __name__ == "__main__":
    elabora_tutto()
