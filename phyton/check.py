import os
import re
import zipfile
import tarfile
import hashlib
from io import BytesIO
from pathlib import Path
from PIL import Image, ExifTags
import json
from datetime import datetime, timezone
import struct

import zipfile
import json
import warnings

import json
import subprocess
import tempfile
import os
from datetime import datetime


import json
import subprocess
import tempfile
import os

from pathlib import Path
from datetime import datetime



FFPROBE = r"C:\tools\ffmpeg\bin\ffprobe.exe"

def estrai_data_video(nome_file):
    percorso_temp = None

    try:
        with tempfile.NamedTemporaryFile(
            suffix=Path(nome_file).suffix,
            delete=False
        ) as temp:
            percorso_temp = temp.name

            with open(nome_file, "rb") as f:
                while chunk := f.read(1024 * 1024):
                    temp.write(chunk)

        risultato = subprocess.run(
            [
                "ffprobe",
                "-v", "quiet",
                "-show_format",
                "-show_streams",
                "-of", "json",
                percorso_temp
            ],
            capture_output=True,
            text=True,
            check=True
        )

        metadati = json.loads(risultato.stdout)

        # 1. Priorità alla data della traccia video
        for stream in metadati.get("streams", []):
            if stream.get("codec_type") == "video":
                tags = stream.get("tags", {})
                valore = tags.get("creation_time")

                if valore:
                    dt = datetime.fromisoformat(
                        valore.replace("Z", "+00:00")
                    )

                    print(f"[DATA VIDEO] {dt}")
                    return dt.replace(tzinfo=None)

        # 2. In alternativa, data del contenitore MOV/MP4
        tags = metadati.get("format", {}).get("tags", {})
        valore = tags.get("creation_time")

        if valore:
            dt = datetime.fromisoformat(
                valore.replace("Z", "+00:00")
            )

            print(f"[DATA CONTENITORE] {dt}")
            return dt.replace(tzinfo=None)

    except Exception as e:
        print(
            f"[ATTENZIONE] Impossibile estrarre data video "
            f"{nome_file}: {e}"
        )

    finally:
        if percorso_temp and os.path.exists(percorso_temp):
            os.unlink(percorso_temp)

    return None


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

print("Configurazione caricata correttamente")
print("Casi particolari preparati correttamente",CASI_PARTICOLARI)
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
                print("EXIF data:", exif)
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
def calcola_hash_e_intestazione(stream):
    hasher = hashlib.sha256()
    intestazione = bytearray()
    while chunk := stream.read(65536):
        hasher.update(chunk)
        if len(intestazione) < 131072:
            intestazione.extend(chunk)
    return hasher.hexdigest(), bytes(intestazione)


def ottieni_data_migliore(nome_file, timestamp_fallback, primi_bytes):
    nome = Path(nome_file).name.lower()
    ext = Path(nome_file).suffix.lower()
    print("Nome file:", nome_file)
    print("Estensione:", ext)
    # 1. Priorità alla data EXIF
    if ext in ESTENSIONI_IMMAGINI and primi_bytes:
        dt_exif = estrai_data_exif(primi_bytes)
        if dt_exif:
            return dt_exif
    # 2. Se è un video, proviamo a estrarre la data dai metadati
    if ext in [".mov", ".mp4", ".avi", ".mkv"] :
        dt_video = estrai_data_video(nome_file)
        if dt_video:
            return dt_video
    return None
def elabora_file(nome_file, timestamp_fallback):
    with open(nome_file, "rb") as f_in:
        file_hash, intestazione = calcola_hash_e_intestazione(f_in)
    data_migliore = ottieni_data_migliore(nome_file, timestamp_fallback, intestazione)
    print("Data migliore per", nome_file, ":", data_migliore)
    print("Hash file per", nome_file, ":", file_hash)
    return
for sorgente in ["L:\\"]:
        if not os.path.exists(sorgente):
            print(f"[SALTATO] Percorso non trovato: {sorgente}")
            continue
        print(f"Scansione della cartella: {sorgente}")
        visitati=0
        for root, dirs, files in os.walk(sorgente):
            print(f"Scansione della cartella: {root}",end="")
            for file in files:
                percorso_file = os.path.join(root, file)
                visitati += 1
            print(f"Totale file visitati: {visitati}")
#elabora_file("C:\\ArchivioFoto\\Giuliano\\2026\\11\\27\\MOVIE(10).mp4", datetime.now())
from pillow_heif import open_heif
from pillow_heif import register_heif_opener
register_heif_opener()
#heif = open_heif("C:\\ArchivioFoto\\Lollo\\2026\\11\\27\\IMG_3730(1).HEIC")

#print(heif.info)

from pathlib import Path
from libreria import processa_elemento
percorso = r"C:\ArchivioFoto\Lollo\2026\11\27\IMG_3730(1).HEIC"
#ottieni_data_migliore(percorso, datetime.now(), None,None)
processa_elemento(percorso, [], "C:\\tmp", "C:\\tmp", None, None)
