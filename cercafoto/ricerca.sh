#!/bin/bash
UTENTE=Patty
NOME=DESKTOP-5BRNMGV
SRC=${1-/mnt/m}
DST=${2-/mnt/k/MediaBackup/$UTENTE/$NOME}
TDT=$(date +"%d.%m.%Y.%H.%M.%S")
FILE_HASH=$DST/hashes.txt
FILE_CSV=$DST/media.csv
FILE_LOG=$DST/log/$TDT.log
FILE_ERR=$DST/log/$TDT.log

EXTS="jpg jpeg png gif bmp tif tiff webp mp4 avi mov mkv wmv flv webm m4v 3gp mpg mpeg nef m2ts"

gestisci(){
	local SRC="$1"
	# Riceve in input il file da analizzare
	# ne calcola l'hash
	# lo confronta con gli altri hash già analizzati
	# se già trattato lo segnala (nel log) scrive il record im FILE_CSV e ritorna
	# altrimenti:
	# ne estrae i metadati
	# recupera la data di acquisizione
	# se non esiste recupera la data di ultima modifica
	# se non esiste usa la data del file
	# Inizializza le variabili Anno e Mese con la data recuperata
	# crea la cartella $DST/Anno/Mese
    # Copia il file nella cartella appena creata impostando come data del file la data estratta dai metadati
	# Logga l'operazione nei file:
	#    FILE_LOG : operazione eseguita sul file (Copia o Skip e ragione)
	#    FILE_ERR : eventuale errore nella gestione del file
	#    FILE_CSV : record contenente le informazioni di File Originale, HASH, File Copiato
}


#for E in $EXTS
#do
#	find $SRC \( -path  $SRC'/found.*' -o -path $SRC'/*RECYCLE*' -o -path $SRC'/System Volume Information' \) -prune -o -iname '*.'$E -print -exec gestisci {} \;
#done

GCNT=0
for E in $EXTS
do
	CNT=$(find $SRC \( -path  $SRC'/found.*' -o -path $SRC'/*RECYCLE*' -o -path $SRC'/System Volume Information' \) -prune -o -iname '*.'$E -print | wc -l)
	echo "$E : $CNT"
	GCNT=$(($GCNT+$CNT))
done
echo "Totale : $GCNT"
