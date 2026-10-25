# CercaFoto per Linux

#!/bin/bash

# Estensioni supportate per immagini
EXTENSIONS=("*.jpg" "*.jpeg" "*.png" "*.gif" "*.bmp" "*.tiff" "*.webp")

# Cartella di destinazione
USERNAME=$(whoami)
SCRIPT_DIR=$(dirname "$0")
DEST_BASE="$SCRIPT_DIR/../MediaBackup/$USERNAME"

# File per tracciare hash già copiati
HASH_FILE="$DEST_BASE/hashes.txt"

mkdir -p "$DEST_BASE"
touch "$HASH_FILE"

# Funzione per ottenere hash
get_hash() {
    md5sum "$1" | awk '{print $1}'
}

# Funzione per copiare file
copy_file() {
    local src="$1"
    local hash=$(get_hash "$src")

    if grep -q "$hash" "$HASH_FILE"; then
        echo "Duplicato saltato: $src"
        return
    fi

    echo "$hash" >> "$HASH_FILE"

    # Ottieni data modifica (YYYY-MM-DD)
    local mtime=$(stat -c %y "$src" | cut -d' ' -f1)
    local year=$(echo "$mtime" | cut -d'-' -f1)
    local month=$(echo "$mtime" | cut -d'-' -f2)

    local dest_dir="$DEST_BASE/$year/$month"
    mkdir -p "$dest_dir"

    local filename=$(basename "$src")
    local dest_path="$dest_dir/$filename"

    # Gestisci conflitti di nome
    local counter=1
    local base="${filename%.*}"
    local ext="${filename##*.}"
    while [ -f "$dest_path" ]; do
        dest_path="$dest_dir/${base}_$counter.$ext"
        ((counter++))
    done

    cp "$src" "$dest_path"
    echo "Copiato: $src -> $dest_path"
}

# Scansiona il filesystem (partendo da /home e /media per sicurezza)
echo "Iniziando scansione..."
for ext in "${EXTENSIONS[@]}"; do
    find /home /media -type f -iname "$ext" 2>/dev/null | while read -r file; do
        copy_file "$file"
    done
done

echo "Scansione completata."