from libreria import (
    elabora_tutto,
    carica_configurazione_json
)

FILE_CONFIG_JSON = r"profili\giuliano.json"
profilo = carica_configurazione_json(FILE_CONFIG_JSON)
#profilo["reload"] = True
print("Configurazione caricata correttamente:",profilo.get("name"))
print("Rilettura Destinazione:", profilo.get("reload",False))
elabora_tutto(profilo)
