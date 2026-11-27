from pathlib import Path


from libreria import scegli_profilo, elabora_tutto

scelta = scegli_profilo()

if scelta["profilo"] is None:
    exit()

profilo = scelta["profilo"]

print("Profilo:", profilo)

#profilo["reload"] = True
print("Configurazione caricata correttamente:",profilo.get("name"))
print("Rilettura Destinazione:", profilo.get("reload",False))
elabora_tutto(profilo)