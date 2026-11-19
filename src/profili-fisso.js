import { getProfilesDirectory, listProfiles, saveProfile, loadProfile } from './services/profileManager.js';

import path from 'path';
import fs from 'node:fs';

const profilo_giuliano = {
  name: "giuliano",
  archive: "archive-giuliano-2.db",
  sourceFolders: [ "E:\\MediaBackup","F:\\TarPhoto\\Giuliano","H:\\DeTarPhoto","H:\\Immagini","H:\\IsoPhoto","K:\\","N:\\Photo\\Giuliano"],
  destDir: "H:\\ArchivioFoto",
  scanDir: "scan",
  backupPath: "giuliano\\backup",
  albumPath: "giuliano\\album",
  batchSize: 100,
  minSize: 0,
  verbose: 1,
  simulazione: true,
  force: false,
  formato: "YYYYMMDD",
  filters: null,
  orderby: null,
  sellist: null,
  casiParticolari: {
    "2005-06-15T00:00:00.000Z": ["p1014430.jpg", "P1015578.jpg", "P1015579.jpg",
      "P1015580.jpg", "P1015585.jpg", "P1015587.jpg", "P1015589.jpg",
      "P1015590.jpg", "P1015591.jpg", "P1015592.jpg", "P1015599.jpg",
      "P1015601.jpg", "P1015603.jpg", "P1015604.jpg", "P1015607.jpg",
      "P1015673.jpg", "P1015913.jpg", "P1012865.jpg", "P1015764.jpg",
      "P1015914.jpg", "P1015917.jpg", "P1012867.jpg", "P1014392.jpg",
      "P1014396.jpg", "P1014397.jpg", "P1015669.jpg", "P1015670.jpg",
      "P1015671.jpg", "P1015672.jpg", "P1015775.jpg", "P1015776.jpg"],
    "1996-01-01T00:00:00.000Z": ["1609109454880.jpg", "1609226641201.jpg", "1609421577439.jpg", "1609421577794.jpg",
      "1609421577991.jpg", "1609441861792.jpg", "1609441861999.jpg", "1609441862149.jpg", "1609441862491.jpg",
      "1609442065295.jpg", "1609448134764.jpg", "bruco.jpg", "effects(1).jpg", "effects(5).jpg",
      "magazine-unlock-01-2.3.5592-_08b1dd9da00af89800.jpg", "magazine-unlock-01-2.3.5592-_1d6f605067045dd815.jpg",
      "magazine-unlock-01-2.3.5592-_4d9e49d8d536cc0813.jpg", "magazine-unlock-01-2.3.5592-_578208c7bcfd1e11e2.jpg",
      "magazine-unlock-01-2.3.5592-_5e8c96ef8bb6899d79.jpg", "magazine-unlock-01-2.3.5592-_83a225a10921dcfa75.jpg",
      "magazine-unlock-01-2.3.5592-_a44f37549d12601062.jpg", "magazine-unlock-01-2.3.5592-_bb9ca14f93f11615ae.jpg",
      "magazine-unlock-01-2.3.5592-_f2a178930ab30d61b1.jpg", "magazine-unlock-01-2.3.5592-_fb92fff73055c1282b.jpg",
      "magazine-unlock-01-2.3.5662-_2fc2414582bfb348bb.jpg", "magazine-unlock-01-2.3.5662-_82d1e4607cd218ed21.jpg",
      "magazine-unlock-01-2.3.5662-_8e465518b1f85f87f5.jpg", "magazine-unlock-01-2.3.5662-_a249982c2ba43113b6.jpg",
      "magazine-unlock-01-2.3.5662-_a669af6b8111860144.jpg", "magazine-unlock-01-2.3.5662-_af76dfa82e16468ade.jpg",
      "magazine-unlock-01-2.3.5662-_c8d3a3a8ee5971c162.jpg", "magazine-unlock-01-2.3.5662-_d5837d550f5bf86e7a.jpg",
      "magazine-unlock-01-2.3.5662-_d5ff5916a2467f6e23.jpg", "magazine-unlock-01-2.3.5662-_ef9f5df17ffa31391e.jpg"
    ],
    "2005-01-04T00:00:00.000Z": ["p1010022.mov", "p1010023.mov", "p1010024.mov", "p1010155.mov", "p1010163.mov"],
    "2008-08-01T00:00:00.000Z": ["p1014193.jpg", "p1014194.jpg", "p1014195.jpg", "P1014193.jpg", "P1014194.jpg", "P1014195.jpg"],
    "1998-02-23T00:00:00.000Z": ["Image02.tif", "Image03.tif", "Image04.tif", "Image0502.tif",
      "Image0503.tif", "Image0504.tif", "Image0505.tif", "Image103.tif", "Image104.tif",
      "Image105.tif", "Image106.tif", "Image201.tif", "Image203.tif", "Image204.tif",
      "Image301.tif", "Image302.tif", "Image303.tif", "Image304.tif", "Image402.tif",
      "Image403.tif", "Image404.tif", "Image405.tif", "Image04.tif", "Image304.tif",
      "Image402.tif", "Image403.tif", "Image404.jpg", "Image404.tif", "Image405.jpg", "negativo-01.png"],
    "2006-12-25T00:00:00.000Z": ["festa natale 032.avi", "festa natale 033.avi"],
    "2007-09-06T00:00:00.000Z": ["p1010156.jpg", "p1010157.jpg",
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
  sourceFolders: ["N:\\Photo\\Roberto"],
  destDir: "C:\\ArchivioFoto",
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
  orderby: null,
  sellist: null,
  extensions: [".jpg", ".png", ".tif", ".jpeg", ".nef", ".tiff", ".JPG", ".pdf", '.JPG',
    ".avi", ".mov", ".mpg", ".gif", ".wmv", ".3gp", ".mp4", ".m4v", ".bts", ".MOV", ".bmp"]
}
await saveProfile(profilo_giuliano);
await saveProfile(profilo_roberto);

