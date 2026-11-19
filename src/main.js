import { scanSource } from './services/scanner.js';
import { getProfilesDirectory, listProfiles, saveProfile, loadProfile } from './services/profileManager.js';
import { readBatchRecords, consolidaDB, initDB, logMemoria, query } from "./services/scan-archive.js";
import { setAll, casiParticolari, calcolaDHashHex, changeFileDate, analisiDest, linkAlbum, analisiExif, analisiThumbnail, copia, analisiDate, analisiAlbum, hashFile } from "./services/runner.js";

import path from 'path';
import fs from 'fs';
//const profile_dir=getProfilesDirectory();
//console.log(profile_dir);
//console.log("lista:",await listProfiles());
const profile = await loadProfile("giuliano");

const archivePath = path.join(profile.destDir, profile.archive);
console.log(profile);

if (true) {
  logMemoria("Inizio");
  initDB(archivePath);
  logMemoria("before scan");
  await scanSource(profile)
    .then(result => {
      console.log("Scan completed:", result.length);
    });
  /// Non serve più consolidaDB(archivePath);
  logMemoria("after scan");
  var modifiedCount = await readBatchRecords(archivePath, "single_files", profile,
    (record) => {
      return hashFile(record, profile);
    }
  );
  logMemoria("after hash");
  var modifiedCount = await readBatchRecords(archivePath, "single_files", profile,
    (record) => {
      return setAll(record, profile);
    }
  );
  logMemoria("after setAll");
  var modifiedCount = await readBatchRecords(archivePath, "single_files", profile,
    (record) => {
      return analisiDest(record, profile);
    }
  );
  logMemoria("after analisiDest");
  const rows = query(archivePath, "select count(*) from single_files where destPath is null", []);
  console.log("Rows with null destPath:", rows);
  logMemoria("Fine");
  const stream = fs.createWriteStream('copia-giuliano.bat', { encoding: 'utf8' });
  stream.write('@echo off\n');

  let destPath = query(archivePath, "select distinct destPath from single_files where destPath is not null order by destPath", []);
  stream.write(destPath.map(dp => `mkdir "${path.join(profile.destDir, profile.backupPath, dp.destPath)}"`).join('\n') + '\n');
  logMemoria("after destPath");

  destPath = null;
  logMemoria("after azzeramento destPath");

  const immagini = query(archivePath, "select * from single_files where destPath is not null order by size", []);
  logMemoria("after immagini");
  immagini.forEach((file) => {
    if (file.fullname === "\\1961\\01-Gennaio\\07\\giuliano-germanna(1).png") {
      file.name = "giuliano-germanna_1.png";
      file.valido = 1;
    }
    if (file.fullname === "\\1961\\01-Gennaio\\07\\giuliano-germanna(3).png") {
      file.name = "giuliano-germanna_3.png";
      file.valido = 1;
    }

    const riga2 = file.valido ?
      `copy "${path.join(file.disco ? file.disco + ":" : "", file.fullname)}" "${path.join(profile.destDir, profile.backupPath, file.destPath, file.name)}"` :
      `REM copy "${path.join(file.disco ? file.disco + ":" : "", file.fullname)}" "${path.join(profile.destDir, profile.backupPath, file.destPath, file.name)}"`;

    stream.write(riga2 + '\n');
  });
  stream.end(); // Chiude lo stream quando hai finito
  stream.on('finish', () => {
    console.log('Scrittura completata!');
  });
  stream.on('error', (err) => {
    console.error('Errore durante la scrittura:', err);
  });
}
if (false) {
  logMemoria("before scan");
  await scanSource(profile)
    .then(result => {
      console.log("Scan completed:", result.length);
    });
  logMemoria("after scan");
  var modifiedCount = await readBatchRecords(archivePath, "raw_files", profile,
    (record) => {
      return hashFile(record, null, profile);
    }
  );
  logMemoria("after hash");
  consolidaDB(archivePath);
  logMemoria("after purge");

  var modifiedCount = await readBatchRecords(archivePath, "single_files", profile,
    async (record) => {
      return await setAll(record, profile);
    }
  );

  var modifiedCount = await readBatchRecords(archivePath, "single_files", profile,
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