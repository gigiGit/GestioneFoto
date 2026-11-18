import { analisiFromMemory, analisiFromDb, scanSource } from './services/scanner.js';
import { getProfilesDirectory, listProfiles, saveProfile, loadProfile } from './services/profileManager.js';
import { writeArchiveFiles, readSomeRecords, archiveScanFiles, readBatchRecords } from "./services/scan-archive.cjs";
import { setAll, casiParticolari, calcolaDHashHex, changeFileDate, analisiDest, linkAlbum, analisiExif, analisiThumbnail, copia, analisiDate, analisiAlbum, hashFile } from "./services/runner.js";

import path from 'path';
import fs from 'node:fs';

//const profile_dir=getProfilesDirectory();
//console.log(profile_dir);
//console.log("lista:",await listProfiles());
const profile = await loadProfile("giuliano");
const archivePath = path.join(profile.destDir, profile.archive);
//console.log(profile);

//const job = "analisiDate";
/////// Test
//await readBatchRecords(archivePath, {sellist: profile.sellist, orderby: profile.orderby, filters: profile.filters}, (record) => {
//  console.log("Batch record:", record);
//});
//////  Fine Test
function logMemoria(etichetta = '') {
  const mem = process.memoryUsage();
  const toMB = (bytes) => (bytes / 1024 / 1024).toFixed(2) + ' MB';

  console.log(`[MEMORIA ${etichetta}]`, {
    totaleProcesso_RSS: toMB(mem.rss),        // Memoria fisica totale (JS + C++ SQLite)
    heapJS_Usato: toMB(mem.heapUsed),         // Oggetti e array JS (es. pendingUpdates)
    heapJS_Allocato: toMB(mem.heapTotal),     // Spazio totale riservato da V8
    memoriaEsterna_C: toMB(mem.external)      // Buffer e strutture C++
  });
}
if (true) {
  await scanSource(profile)
    .then(result => {
      console.log("Scan completed:", result.length);
    });
  logMemoria("before readBatchRecords");
  var modifiedCount = await readBatchRecords(archivePath, profile,
    async (record) => {
      const res = await setAll(record, profile);
      return res[0] || res[1] || res[2] || res[3] || res[4];
      //return hashFile(record, null, profile);
      //return casiParticolari(record, null, profile);
      //return analisiExif(record, null, profile);
      // return analisiThumbnail(record, null, profile);
      //return analisiDate(record, null, profile);
      // return analisiDest(record, null, profile);
    }
  );
  console.log("Modified count:", modifiedCount);
  var modifiedCount = await readBatchRecords(archivePath, profile,
    async (record) => {
      return analisiDest(record, null, profile);
    }
  );
  console.log("Modified count:", modifiedCount);
  logMemoria("after readBatchRecords");
}
/*
const listByName = {};
function custom(file) {
  if (!listByName[file.name]) listByName[file.name] = { list: [], best: file };
  listByName[file.name].list.push(file);
  if (file.size > listByName[file.name].best.size) listByName[file.name].best = file;
  return false;
}
const job = "analisiDate";
await analisiFromArchive(job, profile).then(
  result => {
    console.log("Job:", job,
      "\nFiles Elaborati:", result.count,
      "\nErrori:", result.errorCount,
      "\nConteggio:", result.files.length,
      "\nReadTime:", result.readTime,
      "\nScanTime:", result.scanTime,
      "\nTotalTime:", result.TotalTime);
    
  });
await analisiFromArchive("analisiDest", profile).then((result) => {
  console.log("Job: analisiDest",
    "\nFiles Elaborati:", result.count,
    "\nErrori:", result.errorCount,
    "\nConteggio:", result.files.length,
    "\nReadTime:", result.readTime,
    "\nScanTime:", result.scanTime,
    "\nTotalTime:", result.TotalTime);
  

  const stream = fs.createWriteStream('copia-roberto.bat', { encoding: 'utf8' });
  stream.write('@echo off\n');
  result.files.sort((a, b) => a.size-b.size).forEach((file) => {
    if(file.fullname==="E:\\1961\\01-Gennaio\\07\\giuliano-germanna(1).png"){
      file.name="giuliano-germanna_1.png";
      file.valido=1;
    }
    if(file.fullname==="E:\\1961\\01-Gennaio\\07\\giuliano-germanna(3).png"){
      file.name="giuliano-germanna_3.png";
      file.valido=1;
    }
    const riga1=`mkdir "${path.join(profile.destDir, profile.backupPath, file.destPath)}"`;
    const riga2 = file.valido ?
      `copy "${file.fullname}" "${path.join(profile.destDir, profile.backupPath,file.destPath, file.name)}"` :
      `REM copy "${file.fullname}" "${path.join(profile.destDir,profile.backupPath, file.destPath, file.name)}"`;
    stream.write(riga1 + '\n');
    stream.write(riga2 + '\n');
  });

  stream.end(); // Chiude lo stream quando hai finito

  stream.on('finish', () => {
    console.log('Scrittura completata!');
  });

  stream.on('error', (err) => {
    console.error('Errore durante la scrittura:', err);
  });




})
*/

/*
const toUpdate = [];
for (const name in listByName) {
  if (listByName[name].list.length > 1) {
    for (const f of listByName[name].list) {
      f.valido = 0;
      toUpdate.push(f);
    }
    listByName[name].best.valido = 1;
    toUpdate.push(listByName[name].best);
    console.log(`Duplicate name found for file: ${name} -> ${listByName[name].list.map(f => f.name+"-"+f.valido+"-"+f.size).join(", ")}`);
  }else{
    listByName[name].best.valido = 1;
    toUpdate.push(listByName[name].best);
  }
}
const archivePath = path.join(profile.destDir, profile.archive);
console.log("Updating archive with files:", toUpdate.length);
await writeArchiveFiles(archivePath, toUpdate);

//await analisiByName(profile);
*/