//import { analisiFromMemory, analisiFromDb, scanSource } from './services/scanner.js';
import { getProfilesDirectory, listProfiles, saveProfile, loadProfile } from './services/profileManager.js';
import {  readBatchRecords2, readBatchRecords } from "./services/scan-archive.js";
import { setAll, casiParticolari, calcolaDHashHex, changeFileDate, analisiDest, linkAlbum, analisiExif, analisiThumbnail, copia, analisiDate, analisiAlbum, hashFile } from "./services/runner.js";

import path from 'path';
import fs from 'node:fs';

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
    //totaleProcesso_RSS: toMB(mem.rss),        // Memoria fisica totale (JS + C++ SQLite)
    heapJS_Usato: toMB(mem.heapUsed),         // Oggetti e array JS (es. pendingUpdates)
    heapJS_Allocato: toMB(mem.heapTotal) //,     // Spazio totale riservato da V8
    //memoriaEsterna_C: toMB(mem.external)      // Buffer e strutture C++
  });
}
/*
await scanSource(profile)
  .then(result => {
    console.log("Scan completed:", result.length);
  });
  */
var cnt = 0;
logMemoria("before readBatchRecords");
var modifiedCount = await readBatchRecords(archivePath, profile,
  async (record,count) => {
    cnt++;
    if (cnt % 100 === 0)
      logMemoria(`Nel batch ${cnt}/${count}`);
    const res = await setAll(record, profile);
    return res[0] || res[1] || res[2] || res[3] || res[4];
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
