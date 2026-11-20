import path from "node:path";
import fs from "node:fs/promises";

import Database from "better-sqlite3";
const COLONNE = { raw_files: [], single_files: [] };

export function logMemoria(etichetta = '') {
  const mem = process.memoryUsage();
  const toMB = (bytes) => (bytes / 1024 / 1024).toFixed(2) + ' MB';

  console.log(`[MEMORIA ${etichetta}]`, {
    //totaleProcesso_RSS: toMB(mem.rss),        // Memoria fisica totale (JS + C++ SQLite)
    heapJS_Usato: toMB(mem.heapUsed),         // Oggetti e array JS (es. pendingUpdates)
    heapJS_Allocato: toMB(mem.heapTotal),     // Spazio totale riservato da V8
    //memoriaEsterna_C: toMB(mem.external)      // Buffer e strutture C++
  });
}

export function initDB(archivePath) {
  const db = new Database(path.resolve(archivePath));
  const RAW_FILES = `CREATE TABLE IF NOT EXISTS raw_files (
    hash TEXT,
    disco TEXT,
    fullname TEXT,
    name TEXT,
    ext TEXT,
    size INTEGER,
    createdAt TEXT,
    albums TEXT,
    valido Integer,
    primary key (fullname)
  )`;
  const SINGLE_FILES = `CREATE TABLE IF NOT EXISTS single_files (
    hash TEXT,
    disco TEXT,
    fullname TEXT,
    name TEXT,
    ext TEXT,
    dt_valida TEXT,
    size INTEGER,
    createdAt TEXT,
    exifCreateDate TEXT,
    exifDateTimeOriginal TEXT,
    exifGPSDateStamp TEXT,
    exifModifyDate TEXT,
    thumbnail TEXT,
    albums TEXT,
    valido Integer,
    stato  integer,
    destPath TEXT,
    primary key (fullname)
  )`;
const V_COPY=`create view if not exists v_copy select f.* from single_files f
join raw_files r on r.hash=f.hash
where f.destPath is not null`;
  try {
    //db.prepare("drop table if exists raw_files").run();
    db.prepare(SQL).run();
    db.prepare(VSQL).run();
    COLONNE.raw_files = db.prepare("PRAGMA table_info(raw_files)").all().map(col => col.name);
    COLONNE.single_files = db.prepare("PRAGMA table_info(single_files)").all().map(col => col.name);
    if(COLONNE.raw_files.indexOf("disco") === -1) {
      COLONNE.raw_files.push("disco");
      db.prepare("alter table raw_files add column disco TEXT").run();
      console.log("Updated raw_files with disco column");
    }
    if(COLONNE.single_files.indexOf("disco") === -1) {
      COLONNE.single_files.push("disco");
      db.prepare("alter table single_files add column disco TEXT").run(); 
      console.log("Updated single_files with disco column");
    }
    if(COLONNE.single_files.indexOf("stato") === -1) {
      COLONNE.single_files.push("stato");
      db.prepare("alter table single_files add column stato INTEGER").run(); 
      console.log("Aggiornato single_files con colonna STATO");
    }
    db.prepare("drop view if exists v_files").run();
    db.prepare(`create view if not exists v_files as 
                select ${COLONNE.raw_files.join(", ")} from raw_files
                union 
                select ${COLONNE.raw_files.join(", ")} from single_files;`).run();
    db.prepare("drop view if exists v_copy").run();
    db.prepare(V_COPY).run();
  } catch (error) {
    throw new Error(`Unable to initialize DB: ${error.message}`, { cause: error });
  }
  console.log("Columns raw files:", COLONNE.raw_files);
  console.log("Columns single files:", COLONNE.single_files);
}


export function insertFiles(archivePath, files) {
  const columns = COLONNE.raw_files;
  const placeholders = columns.map(() => "?").join(", ");
  const db = new Database(path.resolve(archivePath));
  db.prepare("BEGIN TRANSACTION").run();
  const stmt1 = db.prepare(`insert into raw_files (${columns.join(", ")}) VALUES (${placeholders})`);
  const stmt2 = db.prepare(`update raw_files set ${columns.map(column => `${column}=?`).join(", ")} where fullname=?`);
  let values;
  try {
    for (const file of files) {
      /*const result = stmt2.run([...columns.map(column => column === "albums" && Array.isArray(file.albums)
        ? JSON.stringify(file.albums)
        : file[column] ?? null), file.fullname]);
      if (result.changes === 0)
      */ {
        values = columns.map(column => column === "albums" && Array.isArray(file.albums)
          ? JSON.stringify(file.albums)
          : file[column] ?? null);
        stmt1.run(values);
      }
    }
    db.prepare("COMMIT").run();
  } catch (error) {
    for (let i = 0; i < values.length; i++) {
      const v = values[i];
      if (
        v !== null &&
        typeof v !== "string" &&
        typeof v !== "number" &&
        typeof v !== "bigint" &&
        !Buffer.isBuffer(v)
      ) {
        console.log(
          `Colonna ${columns[i]} contiene valore non valido:`,
          v,
          `(${typeof v})`
        );
      }
    }
    db.prepare("ROLLBACK").run();
    throw error;
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


export async function readBatchRecords(archivePath, TABELLA, options, callback) {
  const orderby = options?.orderby || null;
  const filters = options?.filters || null;
  const sellist = options?.sellist || '*';
  const batchSize = options?.batchSize || 100;

  console.log("Order by:", orderby);
  console.log("Filters:", filters);
  console.log("Sellist:", sellist);
  const db = new Database(path.resolve(archivePath));
  if (COLONNE.raw_files.length === 0) COLONNE.raw_files = db.prepare("PRAGMA table_info(raw_files)").all().map(col => col.name);
  if (COLONNE.single_files.length === 0) COLONNE.single_files = db.prepare("PRAGMA table_info(single_files)").all().map(col => col.name);

  const Assignement = COLONNE[TABELLA].map(column => `${column}=?`);
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
    const where = ` WHERE ${conditions.length ? conditions.join(" AND ") : " 1=1"}`;
    const SQL = `SELECT ${sellist} FROM ${TABELLA} ${where} `;
    let offset = 0;
    const UPDSQL = `UPDATE ${TABELLA} SET ${Assignement.join(", ")} WHERE fullname = ?`;
    console.log("Executing SQL:", SQL, "with values:", values, "batchSize:", batchSize);
    console.log("   update SQL:", UPDSQL);

    const stmtUpd = db.prepare(UPDSQL);
    const stmt = db.prepare(SQL);
    let modifiedRows = [];
    const rows = stmt.all(values)
    for (const row of rows) {
      //for (const row of stmt.iterate(values)) {
      const record = mapArchivedRecord(row);
      const result = await callback(record,options);
      if (result) {
        modifiedRows.push(record);
      }
      if (modifiedRows.length > batchSize) {
        for (const file of modifiedRows) {
          const uvalues = COLONNE[TABELLA].map(column => column === "albums" ? JSON.stringify(file.albums) : file[column]);
          stmtUpd.run([...uvalues, file.fullname]);
        }
        modifiedRowsCount += modifiedRows.length;
        modifiedRows = [];
      }
    }
    if (modifiedRows.length > 0) {
      for (const file of modifiedRows) {
        const uvalues = COLONNE[TABELLA].map(column => column === "albums" ? JSON.stringify(file.albums) : file[column]);
        stmtUpd.run([...uvalues, file.fullname]);
      }
      modifiedRowsCount += modifiedRows.length;
      modifiedRows = [];
    }
  } catch (error) {
    console.log(error);
    throw error;
  } finally {
    db.close();
  }
  return modifiedRowsCount;
}

export function consolidaDB(archivePath) {
  const db = new Database(archivePath);
  if (COLONNE.raw_files.length === 0) COLONNE.raw_files = db.prepare("PRAGMA table_info(raw_files)").all().map(col => col.name);
  if (COLONNE.single_files.length === 0) COLONNE.single_files = db.prepare("PRAGMA table_info(single_files)").all().map(col => col.name);

  try {
    const stmt = `
    INSERT INTO single_files (${COLONNE["raw_files"].join(", ")})
      SELECT ${COLONNE["raw_files"].join(", ")}
    FROM raw_files WHERE hash NOT IN (SELECT hash FROM single_files)`;
    //console.log("Executing consolidation SQL:", stmt);
    db.prepare(stmt).run();
    //db.prepare("delete from raw_files where hash in (select hash from single_files)").run();
  } finally {
    db.close();
  }
}


export function readSomeRecords(archivePath, TABELLA, options = {}) {
  const orderby = options?.orderby || null;
  const filters = options?.filters || null;
  const sellist = options?.sellist || '*';
  const batchSize = options?.batchSize || 100;
  //console.log("Lettura record dall'archivio", archivePath);
  const db = new Database(path.resolve(archivePath));
  const WHERE = filters ? "where " + filters : "";
  const ORDERBY = orderby ? "order by " + orderby : "";
  const SQL = `SELECT ${sellist} FROM ${TABELLA} ${WHERE} ${ORDERBY}`
  const stmt = db.prepare(SQL);
  let records = [];
  try {
    records = stmt.all();
    console.log("Record letti:", records.length, `SQL: "${SQL}"`);
  } catch (error) {
    console.error(error);
  } finally {
    db.close();
  }
  return records.map(mapArchivedRecord);
}
export function mapValues(TABELLA, object) {
  return COLONNE[TABELLA].map(column => column.name === "albums" ? JSON.stringify(object[column]) : object[column]);
}

export function query(archivePath, SQL, values) {
  const db = new Database(path.resolve(archivePath));
  let result = [];
  try {
    const stmt = db.prepare(SQL);
    result = stmt.all(values);
  } catch (error) {
    console.error(error);
  } finally {
    db.close();
  }
  return result;
}
export function updateArchiveRecord(archivePath, file) {
  // da implementare
  if(!file.fullname) return false;
  const arr = Object.keys(file).map(key => {
    if (key !== "fullname") 
      return key;
  });

  //const db = new Database(path.resolve(archivePath));

  try {
    const columns = COLONNE["single_files"];
    const setClause = arr.map(col => `${col} = ?`).join(", ");
    const values = arr.map(col => file[col]);
    const SQL = `UPDATE single_files SET ${setClause} WHERE fullname = ?`;
    values.push(file.fullname);
    console.log("Columns to update:", arr);
    console.log("SQL:", SQL);
    //db.prepare(SQL).run(values);
  } catch (error) {
    console.error(error);
  } finally {
    //db.close();
  }
}