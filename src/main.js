import { analisiFromArchive, scanSource, analisiByName } from './services/scanner.js';
import { getProfilesDirectory, listProfiles, saveProfile, loadProfile } from './services/profileManager.js';

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
  verbose: 0,
  simulazione: true,
  force: false,
  formato: "YYYYMMDD",
  filters: null,
  casiParticolari: {
    "2014-09-06T00:00:00.000Z": ["p1010156.jpg", "p1010157.jpg",
      "p1010158.jpg", "p1010159.jpg", "p1010160.jpg", "p1010161.jpg", "p1010162.jpg",
      "p1010164.jpg", "p1010165.jpg", "p1010166.jpg", "p1010167.jpg", "p1010168.jpg", "p1010169.jpg",
      "p1010170.jpg", "p1010171.jpg", "p1010172.mov", "p1010173.mov", "p1010174.mov"],
    "2011-06-01T00:00:00.000Z": ["IMG_2145.jpg", "IMG_2146.jpg", "IMG_2147.jpg", "IMG_2148.jpg", "IMG_2149.jpg", "IMG_2150.jpg", "IMG_2151.jpg", "IMG_2152.jpg", "IMG_2153.jpg", "IMG_2145.1.jpg", "IMG_2146.1.jpg", "IMG_2147.1.jpg", "IMG_2148.1.jpg", "IMG_2149.1.jpg", "IMG_2150.1.jpg", "IMG_2151.1.jpg", "IMG_2152.1.jpg", "IMG_2153.1.jpg", "img_2145.1.jpg", "img_2146.1.jpg", "img_2147.1.jpg", "img_2148.1.jpg", "img_2149.1.jpg", "img_2150.1.jpg", "img_2151.1.jpg", "img_2152.1.jpg", "img_2153.1.jpg"]
  },
  extensions: [".jpg", ".png", ".tif", ".jpeg", ".nef", ".tiff", ".JPG", ".pdf", '.JPG',
    ".avi", ".mov", ".mpg", ".gif", ".wmv", ".3gp", ".mp4", ".m4v", ".bts", ".MOV", ".bmp"]
}
const profilo_roberto = {
  name: "roberto",
  archive: "archive-roberto.db",
  sourceFolders: "F:\\Photo\\Roberto",
  destDir: "F:\\ArchivioFoto",
  scanDir: "scan",
  backupPath: "backup\\roberto",
  albumPath: "album\\roberto",
  batchSize: 500,
  minSize: 0,
  verbose: 1,
  simulazione: true,
  force: false,
  formato: "YYYYMMDD",
  filters: null,
  extensions: [".jpg", ".png", ".tif", ".jpeg", ".nef", ".tiff", ".JPG", ".pdf", '.JPG',
    ".avi", ".mov", ".mpg", ".gif", ".wmv", ".3gp", ".mp4", ".m4v", ".bts", ".MOV", ".bmp"]
}
await saveProfile(profilo_giuliano);
//await saveProfile(profilo_roberto);
//const profile_dir=getProfilesDirectory();
//console.log(profile_dir);
//console.log("lista:",await listProfiles());
const profile = await loadProfile("giuliano");
//console.log(profile);

const job = "analisiDate";
await analisiFromArchive(job, profile).then(result => console.log("Job:", job, "\nFiles Elaborati:", result.count, "\nErrori:", result.errorCount, "\nConteggio:", result.files.length, "\nReadTime:", result.readTime, "\nScanTime:", result.scanTime, "\nTotalTime:", result.TotalTime));
//await analisiByName(profile);
