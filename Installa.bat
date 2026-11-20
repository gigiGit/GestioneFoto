winget source update
REM winget uninstall --id OpenJS.NodeJS
winget install --id OpenJS.NodeJS --exact --version 24.10.0
npm install better-sqlite3 exifr express sharp
npm install bmp-js  node-gyp rebuild 

npm run build
pause
npm run main