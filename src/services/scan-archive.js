import path from "node:path";
import fs from "node:fs/promises";

import Database from "better-sqlite3";
const IMAGE_EXTENSIONS = new Set([
  ".jpg", ".jpeg", ".tif", ".tiff", ".nef", ".png", ".webp", ".heic", ".heif", ".avif"
]);
const INSERT_SQL = `INSERT INTO raw_files (
  hash,dhash, fullname, name, ext, dt_valida, size, createdAt,
  exifCreateDate, exifDateTimeOriginal, exifGPSDateStamp, exifModifyDate, thumbnail, albums,valido,destPath
) VALUES (?,?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`;

function run(db, sql, values = []) {
  return new Promise((resolve, reject) => {
    db.run(sql, values, function (error) {
      if (error) reject(error);
      else resolve(this.changes);
    });
  });
}

async function upsertFiles(db, files) {
  const columns = [
    "hash", "dhash", "fullname", "name", "ext", "dt_valida", "size", "createdAt",
    "exifCreateDate", "exifDateTimeOriginal", "exifGPSDateStamp", "exifModifyDate",
    "thumbnail", "albums", "valido", "destPath"
  ];
  const assignments = columns.map(column => `${column} = COALESCE(?, ${column})`).join(", ");
  const placeholders = columns.map(() => "?").join(", ");
  await run(db, "BEGIN IMMEDIATE");
  try {
    for (const file of files) {
      const values = columns.map(column => column === "albums" && Array.isArray(file.albums)
        ? JSON.stringify(file.albums)
        : file[column] ?? null);
      const changes = await run(db, `UPDATE raw_files SET ${assignments} WHERE fullname = ?`, [...values, file.fullname]);
      if (changes === 0) {
        await run(db, `INSERT INTO raw_files (${columns.join(", ")}) VALUES (${placeholders})`, values);
      }
    }
    await run(db, "COMMIT");
  } catch (error) {
    await run(db, "ROLLBACK");
    throw error;
  }
}

function updateFiles(db, files) {
  const assignments = columns.map(column => `${column} = COALESCE(?, ${column})`).join(", ");
  try {
    const stmt = db.prepare(`UPDATE raw_files SET ${assignments} WHERE fullname = ?`);
    for (const file of files) {
      const values = columns.map(column => column === "albums" && Array.isArray(file.albums)
        ? JSON.stringify(file.albums)
        : file[column] ?? null);
      stmt.run([...values, file.fullname]);
    }
  } catch (error) {
    throw error;
  }
}

function writeArchiveFiles(databasePath, files) {
  const db = new Database(databasePath);
  try {
    initDB(db);
    upsertFiles(db, files);
  } finally {
    db.close();
  }
}

function deserializeAlbums(value) {
  if (value === null || value === undefined || value === "") return [];
  let albums = [];
  try {
    albums = JSON.parse(value);
  } catch (error) {

  }
  if (!Array.isArray(albums) || albums.some(album => typeof album !== "string")) {
    throw new TypeError("Invalid albums value in archive record");
  }
  return albums;
}

function mapArchivedRecord(record) {
  if (!record.albums) return record;
  else return { ...record, albums: deserializeAlbums(record.albums) };
}

export function readBatchRecords(archivePath, options, callback) {
  const orderby = options?.orderby || null;
  const filters = options?.filters || null;
  const sellist = options?.sellist || '*';
  const batchSize = options?.batchSize || 100;

  console.log("Order by:", orderby);
  console.log("Filters:", filters);
  console.log("Sellist:", sellist);
  const db = new Database(path.resolve(archivePath));
  let modifiedRowsCount = 0;
  try {
    const conditions = [];
    const values = [];
    const text = value => typeof value === "string" ? value.trim() : "";
    if (filters) {
      if (text(filters.name)) {
        conditions.push("instr(lower(COALESCE(name, '')), lower(?)) > 0");
        values.push(text(filters.name));
      }
      if (text(filters.ext)) {
        const extension = text(filters.ext);
        conditions.push("lower(ext) = lower(?)");
        values.push(extension.startsWith(".") ? extension : `.${extension}`);
      }
    }
    const where = ` WHERE rowid> ? and rowid < ? and ${conditions.length ? conditions.join(" AND ") : " 1=1"}`;
    const SQL = `SELECT ${sellist} FROM raw_files ${where} `;


    let offset = 0;

    console.log("Executing SQL:", SQL, "with values:", [offset, offset + batchSize, ...values], "batchSize:", batchSize);

    const stmt = db.prepare(SQL);
    while (true) {
      const rows = stmt.all([offset, offset + batchSize, ...values]);
      if (rows.length === 0) break;
      const modifiedRows = [];
      //console.log(rows);
      for (const row of rows) {
        const record = mapArchivedRecord(row);
        const result = callback(record, modifiedRows.length);
        if (result) {
          modifiedRows.push(record);
        }
      }
      if (modifiedRows.length > 0) {
        updateFiles(db, modifiedRows);
        modifiedRowsCount += modifiedRows.length;
      }
      offset += batchSize;
    }
  } finally {
    db.close();
  }
  return modifiedRowsCount;
}

const columns = [
  "hash", "fullname", "name", "ext", "dt_valida", "size", "createdAt",
  "exifCreateDate", "exifDateTimeOriginal", "exifGPSDateStamp", "exifModifyDate",
  "thumbnail", "albums", "valido", "destPath"
];
export async function readBatchRecords2(archivePath, options, callback) {
  const orderby = options?.orderby || null;
  const filters = options?.filters || null;
  const sellist = options?.sellist || '*';
  const batchSize = options?.batchSize || 100;

  console.log("Order by:", orderby);
  console.log("Filters:", filters);
  console.log("Sellist:", sellist);
  const db = new Database(path.resolve(archivePath));
  const Assignement = columns.map(column => `${column}=?`);
  let modifiedRowsCount = 0;
  let uvalues=null;
  try {
    const conditions = [];
    const values = [];
    const text = value => typeof value === "string" ? value.trim() : "";
    if (filters) {
      if (text(filters.name)) {
        conditions.push("instr(lower(COALESCE(name, '')), lower(?)) > 0");
        values.push(text(filters.name));
      }
      if (text(filters.ext)) {
        const extension = text(filters.ext);
        conditions.push("lower(ext) = lower(?)");
        values.push(extension.startsWith(".") ? extension : `.${extension}`);
      }
    }
    const where = ` WHERE ${conditions.length ? conditions.join(" AND ") : " 1=1"}`;
    const SQL = `SELECT ${sellist} FROM raw_files ${where} `;


    let offset = 0;
    
    const UPDSQL=`UPDATE raw_files SET ${Assignement.join(", ")} WHERE fullname = ?`;
    console.log("Executing SQL:", SQL, "with values:", values, "batchSize:", batchSize);
    console.log("   update SQL:", UPDSQL);

    const stmtUpd = db.prepare(UPDSQL);
    const stmt = db.prepare(SQL);
    let modifiedRows = [];
    const rows = stmt.all(values)
    for (const row of rows) {
    //for (const row of stmt.iterate(values)) {
      const record = mapArchivedRecord(row);
      const result = await callback(record, modifiedRows.length);
      if (result) {
        modifiedRows.push(record);
      }
      if (modifiedRows.length > batchSize) {
        for (const file of modifiedRows) {
          uvalues = columns.map(column => column === "albums" ? JSON.stringify(file.albums) : file[column]);
          uvalues.push(file.fullname);
          stmtUpd.run(uvalues);
        }
        modifiedRowsCount += modifiedRows.length;
        modifiedRows = [];
      }
    }
    if (modifiedRows.length > batchSize) {
      const Assignement = columns.map(column => `${column}=?`);
      const stmtUpd = db.prepare(`UPDATE raw_files SET ${Assignement.join(", ")} WHERE fullname = ?`);
      for (const file of modifiedRows) {
        const uvalues = columns.map(column => column === "albums" ? JSON.stringify(file.albums) : file[column]);
        stmtUpd.run([...uvalues, file.fullname]);
      }
      modifiedRowsCount += modifiedRows.length;
      modifiedRows = [];
    }
  }catch(error){
    console.log(error);
    console.log(uvalues);
    throw error;
  } finally {
    db.close();
  }
  return modifiedRowsCount;
}

