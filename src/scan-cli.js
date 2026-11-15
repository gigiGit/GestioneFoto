import { analisiFromArchive,scanSource} from './services/scanner.js';

const profilo_giuliano = {
  archive: "archive-giuliano.db",
  sourceFolders: "E:\\",
  destDir: "F:\\ArchivioFoto",
  scanDir: "scan",
  backupPath: "backup\\giuliano",
  albumPath: "album\\giuliano",
  batchSize: 500,
  minSize: 0,
  verbose: 0,
  simulazione: true,
  force:false,
  formato: "YYYYMMDD",
  filters: null,
  extensions: [".jpg", ".png", ".tif", ".jpeg", ".nef", ".tiff", ".JPG", ".pdf",'.JPG',
    ".avi", ".mov", ".mpg", ".gif", ".wmv", ".3gp", ".mp4", ".m4v", ".bts", ".MOV", ".bmp"]
}
const profilo_roberto = {
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
const profilo = profilo_roberto;
//await scanSource(profilo).then(result => console.log("Scan completed. Files found:", result.length));
//const job = "changeFileDate";
//await analisiFromArchive(job, profilo).then(result => console.log("Job:", job, "\nFiles Elaborati:", result.count, "\nErrori:", result.errorCount,"\nConteggio:",result.files.length,"\nReadTime:", result.readTime,"\nScanTime:", result.scanTime,"\nTotalTime:", result.TotalTime));

for( const job of ["hash", "thumbnail", "exif","analisiDate", "analisiAlbum", "copia", "changeFileDate"]) {
  console.log("---------------------------------------------");
  await analisiFromArchive(job, profilo).then(result => console.log("Job:", job, "\nFiles Elaborati:", result.count, "\nErrori:", result.errorCount,"\nConteggio:",result.files.length,"\nReadTime:", result.readTime,"\nScanTime:", result.scanTime,"\nTotalTime:", result.TotalTime));
} 

