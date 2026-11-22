import fs from 'node:fs';
import path from 'node:path';
import { argv } from 'node:process';
function cercaFile(dir, risultati) {
  const elementi = fs.readdirSync(dir);
  for (const elemento of elementi) {
    const percorsoCompleto = path.join(dir, elemento);
    if (fs.statSync(percorsoCompleto).isDirectory()) {
      // console.log("Ricerca in " + percorsoCompleto)
      cercaFile(percorsoCompleto, risultati);
    } else {
      if (!risultati) risultati = {};
      risultati[elemento.toLowerCase()] = (risultati[elemento.toLowerCase()] || 0) + 1;
    }
  }
}

// Esempio d'uso
const risultati = {};
//cercaFile('E:\\',risultati);
//fs.writeFileSync('risultati.json', JSON.stringify(risultati, null, 2));

const lista = JSON.parse(fs.readFileSync('risultati.json', 'utf-8'));
const src = fs.readFileSync('D:\\Photo\\LG.txt', 'utf-8').split("\n");

src.forEach((f) => {

  const pname = f;
  const ext = path.extname(pname);
  if (ext !== ".json") {
    const nome = path.basename(pname.trim());
    if (!lista[nome.toLowerCase()]) console.log("Non trovato:", nome);

  }

});


