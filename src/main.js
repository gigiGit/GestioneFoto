import { analisiFromArchive,scanSource,analisiByName} from './services/scanner.js';
import {getProfilesDirectory,listProfiles,saveProfile,loadProfile} from './services/profileManager.js';

const profilo_giuliano = {
  name: "giuliano",
  archive: "archive-giuliano.db",
  sourceFolders: "E:\\",
  destDir: "C:\\ArchivioFoto",
  scanDir: "scan",
  backupPath: "backup\\giuliano",
  albumPath: "album\\giuliano",
  batchSize: 500,
  minSize: 0,
  verbose: 1,
  simulazione: true,
  force:false,
  formato: "YYYYMMDD",
  filters: "valido=1",
  extensions: [".jpg", ".png", ".tif", ".jpeg", ".nef", ".tiff", ".JPG", ".pdf",'.JPG',
    ".avi", ".mov", ".mpg", ".gif", ".wmv", ".3gp", ".mp4", ".m4v", ".bts", ".MOV", ".bmp"]
}
const profilo_roberto = {
  name: "roberto",
  archive: "archive-roberto.db",
  sourceFolders: "F:\\Photo\\Roberto",
  destDir: "C:\\ArchivioFoto",
  scanDir: "scan",
  backupPath: "backup\\roberto",
  albumPath: "album\\roberto",
  batchSize: 500,
  minSize: 0,
  verbose: 1,
  simulazione: true,
  force:false,
  formato: "YYYYMMDD",
  filters: null,
  extensions: [".jpg", ".png", ".tif", ".jpeg", ".nef", ".tiff", ".JPG", ".pdf",'.JPG',
    ".avi", ".mov", ".mpg", ".gif", ".wmv", ".3gp", ".mp4", ".m4v", ".bts", ".MOV", ".bmp"]
}
await saveProfile(profilo_giuliano);
//await saveProfile(profilo_roberto);

//const profile_dir=getProfilesDirectory();
//console.log(profile_dir);
//console.log("lista:",await listProfiles());
const profile=await loadProfile("giuliano");
//console.log(profile);

const job = "analisiDest";
await analisiFromArchive(job, profile).then(result => console.log("Job:", job, "\nFiles Elaborati:", result.count, "\nErrori:", result.errorCount,"\nConteggio:",result.files.length,"\nReadTime:", result.readTime,"\nScanTime:", result.scanTime,"\nTotalTime:", result.TotalTime));
//await analisiByName(profile);
