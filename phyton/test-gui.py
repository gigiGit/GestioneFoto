
from gestione_profili import scegli_profilo
from libreria import elabora_tutto


if __name__ == "__main__":

    profilo = scegli_profilo()

    if profilo is None:
        print("Operazione annullata.")
        raise SystemExit(0)

    print("Profilo selezionato:", profilo["name"])

    elabora_tutto(profilo)
