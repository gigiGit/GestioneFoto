import { scanSource } from './services/scanner.js';
import { getProfilesDirectory, listProfiles, saveProfile, loadProfile } from './services/profileManager.js';
import { readBatchRecords, initDB, logMemoria, query } from "./services/scan-archive.js";
import { setAll, casiParticolari, calcolaDHashHex, changeFileDate, analisiDest, linkAlbum, analisiExif, analisiThumbnail, copia, analisiDate, analisiAlbum, hashFile } from "./services/runner.js";

import path from 'path';
import fs from 'fs';

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
  const noHash = query(archivePath, "select * from raw_files where hash is null", []);
  console.log("Records with null hash:", noHash);
  if(noHash.length>0) {
    var modifiedCount = await readBatchRecords(archivePath, "single_files", profile,
      (record) => {
        return hashFile(record, profile);
      }
    );
  }
  logMemoria("after hash");
  profile.verbose = 1;
  var modifiedCount = await readBatchRecords(archivePath, "single_files", profile,
    (record) => {
      return casiParticolari(record, profile);
    }
  );
  profile.verbose = 0;
  var modifiedCount = await readBatchRecords(archivePath, "single_files", profile,
    (record) => {
      return setAll(record, profile);
    }
  );
  logMemoria("after setAll");
}
  var modifiedCount = await readBatchRecords(archivePath, "single_files", profile,
    (record) => {
      return analisiDest(record, profile);
    }
  );
  logMemoria("after analisiDest");
  const rows = query(archivePath, "select count(*) from single_files where destPath is null", []);
  console.log("Rows with null destPath:", rows);
  const daCopiare = query(archivePath, "select count(*) from V_copy ", []);
  console.log("File da copiare:", daCopiare);
  var modifiedCount = await readBatchRecords(archivePath, "single_files", profile,
    (record) => {
      return copia(record, profile);
    }
  );
  console.log("Copia completed:", modifiedCount);
  logMemoria("Fine");

if(false) {
  const stream = fs.createWriteStream(`copia-${profile.name}.bat`, { encoding: 'utf8' });
  stream.write('@echo off\n');
  let destPath = query(archivePath, "select distinct destPath from single_files where destPath is not null order by destPath", []);
  stream.write(destPath.map(dp => `mkdir "${path.join(profile.destDir, profile.backupPath, dp.destPath)}"`).join('\n') + '\n');
  logMemoria("after destPath");

  destPath = null;
  logMemoria("after azzeramento destPath");
  const immagini = query(archivePath, "select * from single_files where destPath is not null order by size", []);
  logMemoria("after immagini");
  immagini.forEach((file) => {
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
