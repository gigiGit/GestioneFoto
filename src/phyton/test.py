from libreria import (
    elabora_tutto,
    estrai_data_video,
    prepara_casi_particolari,
    carica_configurazione_json
)

FILE_CONFIG_JSON = "giuliano.json"


profilo = carica_configurazione_json(FILE_CONFIG_JSON)
ESTENSIONI_IMMAGINI = {
    ext.lower() for ext in profilo.get("extensions", [])
}


CASI_PARTICOLARI = prepara_casi_particolari(  profilo.get("casiParticolari", {}))
FORMATO_CARTELLE = profilo.get("formato", "%Y/%m/%d")

print("Configurazione caricata correttamente")
print("Casi particolari preparati correttamente",CASI_PARTICOLARI)
print("Formato cartelle:", FORMATO_CARTELLE)

elabora_tutto(profilo)
