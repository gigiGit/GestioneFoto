
import fs from 'node:fs';
import path from 'node:path';
import fsp from "node:fs/promises";
import { fileURLToPath } from 'node:url';
import os from 'node:os';
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Directory contenente i profili
const PROFILES_DIR = path.resolve(
    __dirname,
    '../data/profiles'
);
const profiloDefault={
      archive: "archive",
  sourceFolders: path.join(os.homedir(), "Pictures"),
  destDir: "C:\\ArchivioFoto",
  scanDir: "scan",
  backupPath: "backup",
  albumPath: "album",
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
export function getProfilesDirectory() {
  return process.env.GESTIONEFOTO_PROFILES_DIR
    ? path.resolve(process.env.GESTIONEFOTO_PROFILES_DIR)
    : path.join(process.env.USERPROFILE || require("node:os").homedir(), ".gestione-foto", "profiles");
}
// --------------------------------------------------
// Inizializzazione directory
// --------------------------------------------------

async function ensureProfilesDirectory() {
    await fsp.mkdir(getProfilesDirectory(), { recursive: true });
}

// --------------------------------------------------
// Crea profilo predefinito
// --------------------------------------------------

export function createDefaultProfile(id = 'default') {
    profiloDefault.id = id;
    profiloDefault.createdAt = new Date().toISOString();
    profiloDefault.updatedAt = new Date().toISOString();
    return profiloDefault;
}

// --------------------------------------------------
// Recupera un profilo
// --------------------------------------------------

export async function getProfile(id) {
    return await loadProfile(id);
}

// --------------------------------------------------
// Elenca tutti i profili
// --------------------------------------------------

export async function listProfiles() {
 const profilesDir = getProfilesDirectory();
 console.log("Directory: ",profilesDir)
  try {
    const files = await fsp.readdir(profilesDir, { withFileTypes: true });
    return files;
  } catch (error) {
    if (error.code === "ENOENT") return [];
    throw error;
  }

}

// --------------------------------------------------
// Carica un profilo dal disco
// --------------------------------------------------

export async function loadProfile(id) {

    validateProfileId(id);

    await ensureProfilesDirectory();

    const filePath = getProfilePath(id);

    try {

        const content = await fsp.readFile(filePath, 'utf8');

        return JSON.parse(content);

    } catch (error) {

        if (error.code === 'ENOENT') {
            return null;
        }

        throw error;
    }
}

// --------------------------------------------------
// Salva un profilo sul disco
// --------------------------------------------------

export async function saveProfile(profile) {

    if (!profile || typeof profile !== 'object') {
        throw new Error('Profilo non valido');
    }
    
    validateProfileId(profile.name);

    await ensureProfilesDirectory();

    const existing = await loadProfile(profile.name);

    const now = new Date().toISOString();

    const profileToSave = {
        ...profile,
        createdAt: existing?.createdAt || profile.createdAt || now,
        updatedAt: now
    };

    const filePath = getProfilePath(profile.name);

    await fsp.writeFile(
        filePath,
        JSON.stringify(profileToSave, null, 4),
        'utf8'
    );

    return profileToSave;
}

// --------------------------------------------------
// Funzioni interne
// --------------------------------------------------

function validateProfileId(id) {

    if (
        typeof id !== 'string' ||
        !/^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$/.test(id)
    ) {
        throw new Error('Identificativo profilo non valido');
    }
}

function getProfilePath(id) {
    validateProfileId(id);
    return path.join(getProfilesDirectory(), `${id}.json`);
}
