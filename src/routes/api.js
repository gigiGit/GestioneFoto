
import express from 'express';
const router = express.Router();
import fs from 'fs';
import path from 'path';
import { scanSource } from '../services/scanner.js';
import { readSomeRecords,  updateArchiveRecord ,readBatchRecords} from '../services/scan-archive.js';
const profili = {};
// API di verifica server
router.get('/health', (req, res) => {
    res.json({
        success: true,
        message: 'Server operativo',
        version: '1.0.0'
    });
});

// Endpoint predisposto per future API
router.get('/status', (req, res) => {
    res.json({
        success: true,
        message: 'API disponibili'
    });
});

import {
    createDefaultProfile,
    getProfile,
    listProfiles,
    loadProfile,
    saveProfile
} from '../services/profileManager.js';



const archiveByProfile = new Map();
let records = [];

router.get("/profiles", async (req, res) => {
    console.log("/profiles")
    const defaultProf = createDefaultProfile();
    const profiliName = [];
    listProfiles().then(profiliFile => {
        profiliFile.forEach(profiloFile => {
            const Jprofilo = fs.readFileSync(path.join(profiloFile.parentPath, profiloFile.name));
            const profilo = JSON.parse(Jprofilo);
            profiliName.push(profilo.name);
            profili[profilo.name] = { ...defaultProf, ...profilo };
        });
        res.json(profiliName);
    });
});
router.get("/profiles/load/:name", async (req, res) => {
    const name = req.params.name;
    console.log("profilo richiesto: ", name)
    try {
        res.json(await loadProfile(name));
    } catch (error) {
        res.json({ error: error.message });
    }
});

const PROFILE_NAME_PATTERN = /^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$/;
const PROFILE_PATH_FIELDS = ["sourceFolders", "destDir", "scanDir", "archive", "backupPath", "albumPath"];

// Restituisce { profile } normalizzato oppure { error }
function parseProfile(body) {
    if (!body || typeof body !== "object") return { error: "Profilo non valido" };
    const name = typeof body.name === "string" ? body.name.trim() : "";
    if (!PROFILE_NAME_PATTERN.test(name)) return { error: "Nome profilo non valido" };

    const profile = { name };
    for (const field of PROFILE_PATH_FIELDS) {
        const value = typeof body[field] === "string" ? body[field].trim() : "";
        if (!value) return { error: `Campo ${field} obbligatorio` };
        profile[field] = value;
    }

    const batchSize = Number(body.batchSize);
    if (!Number.isInteger(batchSize) || batchSize < 1 || batchSize > 10000) return { error: "batchSize non valido" };
    const minSize = Number(body.minSize);
    if (!Number.isInteger(minSize) || minSize < 0) return { error: "minSize non valido" };
    profile.batchSize = batchSize;
    profile.minSize = minSize;
    profile.verbose = Boolean(body.verbose);

    const extensions = Array.isArray(body.extensions) ? body.extensions : String(body.extensions ?? "").split(",");
    profile.extensions = extensions
        .map(ext => String(ext).trim().toLowerCase())
        .filter(Boolean)
        .map(ext => (ext.startsWith(".") ? ext : "." + ext));
    if (profile.extensions.length === 0) return { error: "Indicare almeno un'estensione" };

    return { profile };
}
router.post("/profiles/save", async (req, res) => {
    const profile = req.body;
    console.log("profilo", profile);
    saveProfile(profile).then(() => res.json({ success: true }));
});
router.post("/profiles", async (req, res) => {
    const { profile, error } = parseProfile(req.body);
    if (error) return res.status(400).json({ error });
    try {
        const existing = (await loadProfile(profile.name)) ?? {};
        const saved = await saveProfile({ ...existing, ...profile });
        profili[saved.name] = { ...createDefaultProfile(), ...saved };
        if (saved.archive) {
            try {
                await loadProfileArchive(saved.name, saved.archive);
            } catch (error) {
                console.error(`Errore caricamento archivio ${saved.name}:`, error);
                records = [];
            }
        }
        res.json(saved);
    } catch (err) {
        console.error("/profiles save:", err);
        res.status(500).json({ error: "Errore durante il salvataggio del profilo" });
    }
});


var Runner = false;
router.post("/jobs/scan", async (req, res) => {
    const profile = req.body ?? {};
    console.log("Starting scan for profile:", profile);
    if (Runner) return res.status(409).json({ error: "Scansione già in corso" });
    Runner = true;
    scanSource(profile)
        .then(() => {
            res.json({ success: true });
            console.log(`Scan completed for profile: ${profile.name}`);
        })
        .catch(err => {
            console.error("Errore durante la scansione:", err);
            res.status(500).json({ error: "Errore durante la scansione" });
        })
        .finally(() => {
            Runner = false;
        });
});

const ORDER_BY_COLUMNS = new Set(["dt_valida", "name", "size"]);
const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;

function parseRecordFilters(query) {
    const filters = {};
    const text = value => {
        if (typeof value === "string") return value.trim();
        if (typeof value === "number" && Number.isFinite(value)) return String(value);
        return "";
    };

    if (text(query.name)) filters.name = text(query.name);
    if (text(query.ext)) {
        const extension = text(query.ext).toLowerCase();
        filters.ext = extension.startsWith(".") ? extension : `.${extension}`;
    }
    if (text(query.album)) filters.album = text(query.album);
    if (DATE_PATTERN.test(text(query.dateFrom))) filters.dateFrom = text(query.dateFrom);
    if (DATE_PATTERN.test(text(query.dateTo))) filters.dateTo = text(query.dateTo);

    const minSize = Number(text(query.minSize));
    if (text(query.minSize) && Number.isInteger(minSize) && minSize >= 0) filters.minSize = minSize;

    if (text(query.valido) === "0" || text(query.valido) === "1") filters.valido = Number(query.valido);
    if ([true, 1, "1", "true", "on"].includes(query.duplicates)) filters.duplicates = true;

    filters.orderBy = ORDER_BY_COLUMNS.has(text(query.orderBy)) ? text(query.orderBy) : "dt_valida";
    return filters;
}

function recordDate(value) {
    if (value instanceof Date) {
        return Number.isNaN(value.getTime()) ? "" : value.toISOString().slice(0, 10);
    }
    if (typeof value !== "string") return "";
    const text = value.trim();
    const isoDate = /^(\d{4}-\d{2}-\d{2})/.exec(text);
    if (isoDate) return isoDate[1];
    const exifDate = /^(\d{4})[:/](\d{2})[:/](\d{2})/.exec(text);
    if (exifDate) return `${exifDate[1]}-${exifDate[2]}-${exifDate[3]}`;
    const parsedDate = new Date(text);
    return Number.isNaN(parsedDate.getTime()) ? "" : parsedDate.toISOString().slice(0, 10);
}


let pathFile = {};

async function loadFiles(destDir, archive, filters) {
    console.log("loadFiles called with destDir:", destDir, "archive:", archive, "filters:", filters);
    const archivePath = path.join(destDir, archive);
    let filtri = "1=1";
    if (filters !== null && typeof filters === "object") {
        if (filters.name) filtri += " and name like '%" + filters.name + "%' ";
        if (filters.ext) filtri += " and ext = '" + filters.ext + "' ";
        if (filters.album) filtri += " and album like '%" + filters.album + "%' ";
        if (filters.dateFrom) filtri += " and dt_valida >= '" + filters.dateFrom + "' ";
        if (filters.dateTo) filtri += " and dt_valida <= '" + filters.dateTo + "' ";
        if (filters.minSize) filtri += " and size >= " + filters.minSize + " ";
        if (filters.valido !== undefined) filtri += " and valido = " + filters.valido + " ";
    }
    console.log("Filtri SQL generati:", filtri);
    if (records == null || records.length === 0) {
        fs.accessSync(archivePath);
        records = await readSomeRecords(archivePath, filtri, filters.orderBy);
    }
}
router.post("/preview/list", async (req, res) => {
    console.log("/preview/list called with body:", req.body);
    const { destDir, archive } = req.body.parametri;
    const filters = req.body.filters;
    try {
        await loadFiles(destDir, archive, filters);
        console.log("/preview/list: called", records.length);
        res.json(records.map(file => file.fullname));
    } catch (error) {
        console.error("/preview/list:", error);
        res.status(500).json({ error: "Errore durante la ricerca" });
    }
});

router.post("/records/update", async (req, res) => {
    const profileName = typeof req.body?.profileName === "string" ? req.body.profileName : "";
    const fullname = typeof req.body?.fullname === "string" ? req.body.fullname : "";
    const dtValida = req.body?.dtValida;
    const valido = req.body?.valido;
    if (!profileName || !fullname || typeof valido !== "boolean") {
        return res.status(400).json({ error: "profileName, fullname e valido sono obbligatori" });
    }
    if (dtValida !== null && (typeof dtValida !== "string" || Number.isNaN(Date.parse(dtValida)))) {
        return res.status(400).json({ error: "Data valida non corretta" });
    }

    //await waitForScans(profileName);
    const archivePath = archiveByProfile.get(profileName);
    if (!archivePath) return res.status(404).json({ error: "Archivio del profilo non disponibile" });

    const updatePromise = updateArchiveRecord(archivePath, {
        fullname,
        dtValida: dtValida ? new Date(dtValida).toISOString() : null,
        valido
    });
    try {
        const record = await updatePromise;
        //await reloadArchiveProfiles(archivePath);
        const duplicateCount = record.hash
            ? getCachedRecords(profileName).filter(candidate => candidate.hash === record.hash).length
            : 0;
        res.json({ success: true, record: { ...record, duplicateCount } });
    } catch (error) {
        console.error("Errore aggiornamento record:", error);
        res.status(500).json({ error: error.message });
    } finally {

    }
});

router.get("/records", async (req, res) => {
    try {
        const profileName = typeof req.query.profileName === "string" ? req.query.profileName : "";
        //await waitForScans(profileName);
        searchArchive(parseRecordFilters(req.query), profileName)
            .then(result => {
                records = result;
                console.log("/records: called", records.length);
                res.json(records);
            });
    } catch (error) {
        console.error("/records:", error);
        res.status(500).json({ error: "Errore durante la ricerca" });
    }
});

router.post("/records/detail", async (req, res) => {
    const fullname = typeof req.body.fullname === "string" ? req.body.fullname : "";
    try {
        console.log("/records/detail: called for", fullname);
        res.json(records.find(f => f.fullname === fullname));
    } catch (error) {
        console.error("/records/detail:", error);
        res.status(500).json({ error: "Errore durante la lettura del record" });
    }
});

router.post("/jobs/runner", async (req, res) => {
    const parametri = req.body ?? {};
    console.log(req.body);
    if (Runner) {
        res.status(409).json({ error: "Attività già in corso per questo profilo" });
        return;
    }
    Runner = true;
    readBatchRecords(parametri.runner,"single_files", parametri.profile)
        .then(result => res.json({ success: true, count: result.count, errorCount: result.errorCount }))
        .catch(error => res.status(500).json({ error: error.message }))
        .finally(() => { Runner = false; });
});

import os from 'os';

router.get('/folders', async (req, res) => {
    const targetPath =req.query.path ? path.resolve(req.query.path) :    os.homedir();
    console.log("Navigazione in", targetPath);
  try {
    // Parte dalla home dell'utente se non è specificato un percorso
    const entries =  fs.readdirSync(targetPath, { withFileTypes: true });

    const folders = entries
      .filter(entry => entry.isDirectory() && !entry.name.startsWith('.')) // Solo cartelle non nascoste
      .map(entry => ({
        name: entry.name,
        fullPath: path.join(targetPath, entry.name)
      }))
      .sort((a, b) => a.name.localeCompare(b.name));
    console.log("Lista:", folders);
    res.json({
      currentPath: targetPath,
      folderName: path.basename(targetPath) || targetPath,
      parentPath: path.dirname(targetPath),
      folders
    });
  } catch (err) {
     console.error('Errore lettura cartella:', err);
    res.status(500).json({ error: `Errore: ${err.code || err.message}` });
  }
});

export default router;