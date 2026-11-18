import path from "node:path";
import exifr from "exifr";
import sharp from "sharp";
import fs from "node:fs";
import { createHash } from "node:crypto";
import { createReadStream } from "node:fs";
export async function setAll(record,profile) {
  const tasks = [
    hashFile(record, null, profile),
    casiParticolari(record, null, profile),
    analisiExif(record, null, profile),
    analisiThumbnail(record, null, profile),
    analisiDate(record, null, profile),
  ];
  return await  Promise.all(tasks);
}

export async function casiParticolari(file, pendingFiles, option) {
  let mod = false;
  if (option.casiParticolari) {
    for (const date in option.casiParticolari) {
      for (const f of option.casiParticolari[date]) {
        if (file.name === f) {
          file.exifCreateDate = date;
          file.exifDateTimeOriginal = date;
          file.exifModifyDate = date;
          file.dt_valida = date;
          file.dt_created = date;
          mod=true;
        }
      }
    }
  }
  return mod;
}
export async function calcolaDHashHex(file, pendingFiles=[], { force = false, destDir, backupPath, formato, verbose }) {
    const filePath = file.fullname;
    try {
        const { data } = await sharp(filePath)
            .rotate()
            .resize(9, 8, { fit: 'fill' })
            .grayscale()
            .raw()
            .toBuffer({ resolveWithObject: true });

        let bin = '';
        for (let y = 0; y < 8; y++) {
            for (let x = 0; x < 8; x++) {
                bin += data[y * 9 + x] > data[y * 9 + (x + 1)] ? '1' : '0';
            }
        }

        // Converte i 64 bit in 16 caratteri esadecimali usando BigInt
        file.dhash = BigInt('0b' + bin).toString(16).padStart(16, '0');
        //pendingFiles.push(file);
        return true;
    } catch (error) {
        console.error(`Error calculating dHash for file ${filePath}:`, error);
        return false;
    }
}
export async function changeFileDate(file, pendingFiles=[], { force = false, destDir, backupPath, formato, verbose }) {
    if (!file.valido && !file.dt_valida) return false;
    const data = new Date(file.dt_valida)
    const destPath = path.join(destDir, backupPath, file.destPath, file.name);
    if (!fs.existsSync(destPath)) return false;
    try {
        if (verbose > 1) console.log(`Changing file date for ${destPath}:`, data);
        fs.utimesSync(destPath, data, data);
        file.createdAt = file.dt_valida;
        //pendingFiles.push(file);
        return true;
    } catch (error) {
        console.error(`Error changing file date for ${destPath}:`, error);
        return false;
    }
}

export async function copia(file, pendingFiles=[], { force = false, destDir, backupPath, formato, verbose }) {
    if (!file.valido && !file.dt_valida) return false;
    const destPath = path.join(destDir, backupPath, file.destPath, file.name);

    if (fs.existsSync(destPath)) {
        return false;
    }
    try {
        if (verbose > 1) console.log(`Copying file ${file.fullname} to ${destPath}`);
        fs.mkdirSync(path.dirname(destPath), { recursive: true });
        fs.copyFileSync(file.fullname, destPath);
        return true;
    } catch (error) {
        console.error(`Error copying file ${file.fullname} to ${destPath}:`, error);
        return false;
    }
}

export async function analisiDest(file, pendingFiles=[], { force = false, destDir, backupPath, formato, verbose }) {
    if (!file.valido && !file.dt_valida) return false;
    const data = new Date(file.dt_valida)
    const anno = data.getFullYear();
    const mese = data.toLocaleString('it-IT', { month: 'long' }); // "novembre" (usa 'short' per "nov")
    const giorno = String(data.getDate()).padStart(2, '0');
    const dataFormattata = formato === "YYYYMMDD" ? `${anno}${path.sep}${mese}${path.sep}${giorno}` : `${anno}${path.sep}${mese}`;
    const destinationDir = dataFormattata;
    const destPath = path.join(destDir, backupPath, destinationDir, file.name);
    //if (file.destPath) return false;
    file.destPath = destinationDir;
    //pendingFiles.push(file);
    return true;
    // Implement the copy functionality here
    /*    try {
            if (verbose > 1) console.log(`Copying file ${file.fullname} to ${destPath}`);
            fs.mkdirSync(path.dirname(destPath), { recursive: true });
            fs.copyFileSync(file.fullname, destPath);
            file.destPath = destinationDir;
            pendingFiles.push(file);
            return true;
        } catch (error) {
            console.error(`Error copying file ${file.fullname} to ${destPath}:`, error);
            return false;
        }
    }*/
}
export async function hashFile(file, pendingFiles=[], { force = false }) {
    if (!force && file.hash) return false;
    try {
        const Hash = await new Promise((resolve, reject) => {
            const hash = createHash("sha256");
            const stream = createReadStream(file.fullname);
            stream.on("error", reject);
            stream.on("data", chunk => hash.update(chunk));
            stream.on("end", () => resolve(hash.digest("hex")));
        });
        file.hash = Hash;
        //pendingFiles.push(file);
        return true;
    } catch (error) {
        console.error(`Error hashing file ${file.fullname}:`, error);
        return false;
    }
}

export async function linkAlbum(file, pendingFiles=[], { verbose = 0, destDir, backupPath, albumPath }) {
    if (!file.valido) return false;
    const srcPath = path.join(destDir, backupPath, file.destPath, file.name);
    if (fs.existsSync(srcPath)) {
        for (const album of file.albums) {
            const destPath = path.join(destDir, albumPath, album, file.name);
            fs.mkdirSync(path.dirname(destPath), { recursive: true });
            if (fs.existsSync(destPath)) continue;
            try {
                if (verbose > 1) console.log(`Linking file ${srcPath} to ${destPath}`);
                fs.symlinkSync(srcPath, destPath);
            } catch (error) {
                console.error(`Error linking file ${srcPath} to ${destPath}:`, error);
                return false;
            }
        }
        return true;
    } else {
        console.warn(`Source file does not exist: ${srcPath}`);
        return false;
    }
}


export async function analisiExif(file, pendingFiles=[], parametri) {
    if((file.exifCreateDate || file.exifDateTimeOriginal || file.exifGPSDateStamp || file.exifModifyDate)) return false;
    const unsupportedExtensions = new Set([".mpg", ".pdf", ".m4v", ".wmv", ".mp4", ".mov", ".avi", ".mkv", ".gif", ".3gp", ".bmp"]);
    const ext = file.ext.toLowerCase();
    if (unsupportedExtensions.has(ext)) return false;

    const { force = false } = parametri;
    if (!force    ) {
        return false;
    }
    try {
        const exifData = await exifr.parse(file.fullname) ?? null;
        file.exifCreateDate = exifData?.CreateDate ? new Date(exifData.CreateDate).toISOString() : null;
        file.exifDateTimeOriginal = exifData?.DateTimeOriginal ? new Date(exifData.DateTimeOriginal).toISOString() : null;
        file.exifGPSDateStamp = exifData?.GPSDateStamp ? new Date(exifData.GPSDateStamp).toISOString() : file.exifCreateDate;
        file.exifModifyDate = exifData?.ModifyDate ? new Date(exifData.ModifyDate).toISOString() : null;
        if (parametri.verbose > 1) console.log(`EXIF data for ${file.fullname}:`, exifData);
        //pendingFiles.push(file);
        return true;
    } catch (error) {
        console.error(`Error reading EXIF data for ${file.fullname}:`, error);
        file.exifCreateDate = null;
        file.exifDateTimeOriginal = null;
        file.exifGPSDateStamp = null;
        file.exifModifyDate = null;
        //pendingFiles.push(file);
        return false;
    }
}


export async function analisiThumbnail(file, pendingFiles=[], parametri) {
    const supportedExtensions = new Set([
        ".jpg", ".jpeg", ".tif", ".tiff", ".nef", ".png", ".webp", ".heic", ".heif", ".avif"
    ]);
    const THUMBNAIL_SIZE = 400;
    const THUMBNAIL_VERSION = 2;

    const { force = false, verbose } = parametri;
    const extension = path.extname(file.fullname).toLowerCase();
    if (!supportedExtensions.has(extension)) return false;
    if (!force && file.thumbnail)
        return false;
    try {
        const thumbnail = await sharp(file.fullname)
            .rotate()
            .resize({ width: THUMBNAIL_SIZE, fit: "inside", withoutEnlargement: true })
            .jpeg({ quality: 80 })
            .toBuffer();
        file.thumbnail = `data:image/jpeg;base64,${thumbnail.toString("base64")}`;
        file.valido = true;
        if (verbose > 1) console.log(`Generated thumbnail for ${file.fullname}`);
        //pendingFiles.push(file);
        return true;
    } catch (error) {
       // console.error(`Error generating thumbnail for ${file.fullname}:`, error);
       // produciamo una miniatura di default in caso di errore
        file.thumbnail = "data:image/jpeg;base64,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";
        file.valido = false;
        //pendingFiles.push(file);
        return true;
    }
}
export function toLocalISOString(date) {
    const offset = -date.getTimezoneOffset();
    const sign = offset >= 0 ? '+' : '-';
    const pad = (num) => String(Math.floor(Math.abs(num))).padStart(2, '0');

    const localDate = new Date(date.getTime() + offset * 60 * 1000);
    try {
        return localDate.toISOString().slice(0, -1) + `Z`; //${sign}${pad(offset / 60)}:${pad(offset % 60)}
    } catch {
        return null;
    }
}

export async function analisiAlbum(file, pendingFiles=[], { force = false }) {
    if (!(force || file.albums === null || file.albums === undefined)) return false;

    try {
        const normalizedPath = path.normalize(file.fullname);
        const dirs = normalizedPath.split(path.sep);
        file.albums = dirs.slice(1, -1);
        //pendingFiles.push(file);
        return true;
    } catch (error) {
        console.error(`Error generating Albums for ${file.fullname}:`, error);
        return false;
    }
}

export async function analisiDate(file, pendingFiles=[], { force = false, verbose = 0 }) {
    if(file.dt_valida) return false;
    if (verbose > 1) console.log(`Starting analisiDate for ${file.fullname}`);
    try {
        let bestTry = file.exifDateTimeOriginal ?? file.exifCreateDate ?? file.exifGPSDateStamp ?? file.exifModifyDate;
        if (!(bestTry > "1900" && bestTry < "2030")) {
            bestTry = null;
        }
        if (verbose > 1) console.log(`EXIF Best try for ${file.fullname}: ${bestTry}`);
        var dateFrom = "exif";
        if (bestTry === null || bestTry === undefined) {
            bestTry = recuperaDataDaNome(file);
            if (verbose > 1) console.log(`recuperaDataDaNome Best try for ${file.fullname}: ${bestTry}`);
            if (bestTry > "1900" && bestTry < "2030") {
                var dateFrom = "Nome";
            } else {
                bestTry = null;
            }
        }
        if (bestTry === null) {
            bestTry = file.createdAt;
            var dateFrom = "fs";
        }
        file.dt_valida = toLocalISOString(new Date(bestTry));
        file.dateFrom = dateFrom;
        //pendingFiles.push(file);
        return true;
    } catch (error) {
        console.error(`Error generating analisiDate for ${file.fullname}:`, error);
        file.dt_valida = null;
        return true
    }

    return false;
}

export function recuperaDataDaNome(file) {
    function isOraValid(txt) {
        return Number(txt) ? Number(txt) % 60 : "00";
    }
    const filename = file.name.toLowerCase();
    //console.log(`Checking filename: ${filename}`);
    if (filename.startsWith("fotoscandiano")) {
        return "1950-01-01T00:00:00.000Z";
    }
    if (filename.startsWith("giuliano")) {
        return "1961-01-07T00:00:00.000Z";
    }
    var patt1 = [] ;
    var patt6 = [/(\d{4})-(\d{2})-(\d{2})_(\d{2})-(\d{2})-(\d{2})/, /(\d{4})-(\d{2})-(\d{2})_(\d{2})(\d{2})(\d{2})/, /(\d{4})(\d{2})(\d{2})_(\d{2})(\d{2})(\d{2})/, /(\d{4})(\d{2})(\d{2})_(\d{2})-(\d{2})-(\d{2})/,
        /screenshot_(\d{4})(\d{2})(\d{2})-(\d{2})(\d{2})(\d{2})/, /vid_(\d{4})(\d{2})(\d{2})_(\d{2})(\d{2})(\d{2})/,
        /vid-(\d{4})(\d{2})(\d{2})-(\d{2})(\d{2})(\d{2})/,
        /vid(\d{4})(\d{2})(\d{2})(\d{2})(\d{2})(\d{2})/,/(\d{4})(\d{2})(\d{2})(\d{2})(\d{2})(\d{2}).jpg/,
        /img.(\d{4})_(\d{2})_(\d{2}).(\d{2})_(\d{2})_(\d{2})/];
    var patt3 = [/img-(\d{4})(\d{2})(\d{2})-wa\d{4}\.jpg/,
        /vid-(\d{4})(\d{2})(\d{2})-wa/
    ];

    for (const p of patt1) {
        if (filename.match(p)) {
            var dateArray = filename.match(p);
            if (dateArray.length < 2) continue;
            var date = new Date(Number(dateArray[1]));
            return toLocalISOString(date);
        }
    }
    for (const p of patt6) {
        if (filename.match(p)) {
            var dateArray = filename.match(p);
            if (dateArray.length < 7) continue;
            //console.log(`Filename matches pattern: ${file.fullname} ${p}`);
            var date = `${dateArray[1]}-${dateArray[2]}-${dateArray[3]} ${dateArray[4]}:${dateArray[5]}:${dateArray[6]}`;
            return toLocalISOString(new Date(date));
        }
    }
    for (const p of patt3) {
        if (filename.match(p)) {
            var dateArray = filename.match(p);
            if (dateArray.length < 4) continue;
            var date = `${dateArray[1]}-${dateArray[2]}-${dateArray[3]} 00:00:00`;
            //console.log(`Filename matches pattern: ${file.fullname} ${date}`);
            return toLocalISOString(new Date(date));
        }
    }
    if (file.albums && file.albums[0]) {
        return file.albums[0]
            .replace("Gennaio", "01")
            .replace("Febbraio", "02")
            .replace("Marzo", "03")
            .replace("Aprile", "04")
            .replace("Maggio", "05")
            .replace("Giugno", "06")
            .replace("Luglio", "07")
            .replace("Agosto", "08")
            .replace("Settembre", "09")
            .replace("Ottobre", "10")
            .replace("Novembre", "11")
            .replace("Dicembre", "12") + "T00:00:00.000Z";
    }


    if (filename >= "1900" && filename < "2030") {
        // Formato YYYY-MM-DD_HH-MM-SS
        var date = filename.slice(0, 4) + "-" + filename.slice(5, 7) + "-" + filename.slice(8, 10) + " " + isOraValid(filename.slice(11, 13)) + ":" + isOraValid(filename.slice(14, 16)) + ":" + isOraValid(filename.slice(16, 18));
        //console.log(`1 Trying date from filename: ${date}`, filename);
        try {
            return toLocalISOString(new Date(date));
        } catch {
            // Formato YYYY-MM-DD_HHMMSS senza trattini tra ore, minuti e secondi
            var date = filename.slice(0, 4) + "-" + filename.slice(5, 7) + "-" + filename.slice(8, 10) + " " + isOraValid(filename.slice(11, 13)) + ":" + isOraValid(filename.slice(13, 15)) + ":" + isOraValid(filename.slice(15, 17));
            //console.log(`2 Trying date from filename: ${date}`, filename);
            try {

                return toLocalISOString(new Date(date));
            } catch {
                // Formato YYYYMMDD_HHMMSS senza trattini tra data e ora
                var date = filename.slice(0, 4) + "-" + filename.slice(4, 6) + "-" + filename.slice(6, 8) + " " + isOraValid(filename.slice(9, 11)) + ":" + isOraValid(filename.slice(11, 13)) + ":" + isOraValid(filename.slice(13, 15));
                //print(`3 Trying date from filename: ${date}`, filename);
                try {
                    return toLocalISOString(new Date(date));
                } catch {
                    // Formato YYYYMMDD_HHMMSS senza trattini tra data e ora, con eventuale underscore tra data e ora
                    var date = filename.slice(0, 4) + "-" + filename.slice(4, 6) + "-" + filename.slice(6, 8) + " " + isOraValid(filename.slice(9, 11)) + ":" + isOraValid(filename.slice(11, 13)) + ":" + isOraValid(filename.slice(13, 15));
                    try {
                        //print(`Trying date from filename: ${date}`, filename);
                        return toLocalISOString(new Date(date));
                    } catch {
                        //print(`Failed to parse date from filename: ${date}`, filename);
                        return null;
                    }
                }
            }
        }
    }
    if (filename.startsWith("img-") && filename >= "img-1900" && filename < "img-2030") {
        var date = filename.slice(4, 8) + "-" + filename.slice(8, 10) + "-" + filename.slice(10, 12) + " " + isOraValid(filename.slice(13, 15)) + ":" + isOraValid(filename.slice(15, 17)) + ":" + isOraValid(filename.slice(17, 19));
        try {
            //print(`Trying date from filename: ${date}`, filename);
            return toLocalISOString(new Date(date));
        } catch {
            //print(`Failed to parse date from filename: ${date}`, filename);
            return null;
        }
    }
    if (filename.startsWith("img_") && filename >= "img_1900" && filename < "img_2030") {
        var date = filename.slice(4, 8) + "-" + filename.slice(8, 10) + "-" + filename.slice(10, 12) + " " + isOraValid(filename.slice(13, 15)) + ":" + isOraValid(filename.slice(15, 17)) + ":" + isOraValid(filename.slice(17, 19));
        try {
            //print(`Trying date from filename: ${date}`, filename);
            return toLocalISOString(new Date(date));
        } catch {
            //print(`Failed to parse date from filename: ${date}`, filename);
            return null;
        }
    }

    if (filename.startsWith("vid-") && filename >= "vid-1900" && filename < "vid-2030") {
        var date = filename.slice(4, 8) + "-" + filename.slice(8, 10) + "-" + filename.slice(10, 12) + " " + isOraValid(filename.slice(13, 15)) + ":" + isOraValid(filename.slice(15, 17)) + ":" + isOraValid(filename.slice(17, 19));
        try {
            //print(`Trying date from filename: ${date}`, filename);
            return toLocalISOString(new Date(date));
        } catch {
            //print(`Failed to parse date from filename: ${date}`, filename);
            return null;
        }
    }
    if (filename.startsWith("vid_") && filename >= "vid_1900" && filename < "vid_2030") {
        var date = filename.slice(4, 8) + "-" + filename.slice(8, 10) + "-" + filename.slice(10, 12) + " " + isOraValid(filename.slice(13, 15)) + ":" + isOraValid(filename.slice(15, 17)) + ":" + isOraValid(filename.slice(17, 19));
        try {
            //print(`Trying date from filename: ${date}`, filename);
            return toLocalISOString(new Date(date));
        } catch {
            //print(`Failed to parse date from filename: ${date}`, filename);
            return null;
        }
    }

    if (filename.startsWith("vid") && filename >= "vid1900" && filename < "vid2030") {
        var date = filename.slice(3, 7) + "-" + filename.slice(7, 9) + "-" + filename.slice(9, 11) + " " + isOraValid(filename.slice(11, 13)) + ":" + isOraValid(filename.slice(13, 15)) + ":" + isOraValid(filename.slice(15, 17));
        try {
            //print(`Trying date from filename: ${date}`, filename);
            return toLocalISOString(new Date(date));
        } catch {
            //print(`Failed to parse date from filename: ${date}`, filename);
            return null;
        }
    }
    if (filename.startsWith("screenshot_") && filename >= "screenshot_1900" && filename < "screenshot_2030") {
        var date = filename.slice(11, 15) + "-" + filename.slice(15, 17) + "-" + filename.slice(17, 19) + " " + isOraValid(filename.slice(20, 22)) + ":" + isOraValid(filename.slice(22, 24)) + ":" + isOraValid(filename.slice(24, 26));
        try {
            //print(`Trying date from filename: ${date}`, filename);
            return toLocalISOString(new Date(date));
        } catch {
            //print(`Failed to parse date from filename: ${date}`, filename);
            return null;
        }
    }
}

