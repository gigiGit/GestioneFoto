from pathlib import Path

from profili import scegli_profilo


scelta = scegli_profilo()

if scelta["profilo"] is None:
    exit()

profilo = scelta["profilo"]

print("Profilo:", profilo)

#profilo["reload"] = True
print("Configurazione caricata correttamente:",profilo.get("name"))
print("Rilettura Destinazione:", profilo.get("reload",False))


from libreria import elabora_tutto
elabora_tutto(profilo)