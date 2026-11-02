# How to Test Archive Scanning and Thumbnail Feature

This guide walks you through testing the archive scanning and thumbnail generation functionality.

## Prerequisites

- Built application: `bin\GestioneFoto.App.exe`
- Test archives: Already created in `test-archives\archives\`
- Test profile: `test-archives\test.profile`
- Test verifier tool: Built at `test-archives\ArchiveScanTest\bin\Release\net8.0-windows\ArchiveScanTest.exe`

## Test Archives Available

1. **archive1.zip** - ZIP archive containing 2 test images
2. **archive2.tar.gz** - TAR.GZ archive containing 1 test image  
3. **archive3.tgz** - TGZ archive containing 1 test image

Total expected: **4 image files**

## Step 0 (OPTIONAL): Upgrade Existing Database

If you have an **existing database from before the thumbnail feature**, you need to upgrade it:

```powershell
.\upgrade-database.ps1 -DbPath "test-archives\test-scan.db"
```

Or if you want to start fresh, simply delete the old database:

```powershell
Remove-Item test-archives\test-scan.db -ErrorAction SilentlyContinue
```

**Note**: The application will automatically add missing columns (like THUMBNAIL) when it opens a database, but it's good practice to upgrade explicitly first.

## Step 1: Open the Application with Test Profile

```powershell
bin\GestioneFoto.App.exe --profile=test-archives\test.profile
```

This will:
- Load the "Test-Archives" profile
- Point to `test-archives\archives` directory
- Create/update database at `test-archives\test-scan.db`
- Set destination for copies at `test-archives\output`

## Step 2: Run the Scan

1. When the app opens, you should see:
   - Profile name: "Test-Archives"
   - Include path: "G:\GestioneFoto\test-archives\archives"
   - Database path: "G:\GestioneFoto\test-archives\test-scan.db"

2. Click the **"Scansione"** button (green button on the left)

3. Watch the scan progress in the status area

4. When complete, you should see:
   - Status message showing number of files found
   - "Scansione completata" message

## Step 3: Verify with the Test Tool

After the scan completes and you close the app, run the verification tool:

```powershell
test-archives\ArchiveScanTest\bin\Release\net8.0-windows\ArchiveScanTest.exe
```

### Expected Output

The verification tool should display:

```
=== Archive Scan Verification ===

Database found: G:\GestioneFoto\test-archives\test-scan.db

=== Schema ===
SRC columns: ..., THUMBNAIL, ...
Has THUMBNAIL: YES

=== Records ===
Total records: 4
Records with thumbnail: 4
Records from archives (with |): 4

=== Files ===
  <path>|image1.jpg                  HASH1... [thumb:Yes]
  <path>|image2.jpg                  HASH2... [thumb:Yes]
  <path>|image3.jpg                  HASH3... [thumb:Yes]
  <path>|image4.jpg                  HASH4... [thumb:Yes]
```

**Key verification points:**
- [ ] Database is created
- [ ] Schema includes THUMBNAIL column
- [ ] 4 records are in the database
- [ ] All 4 records have thumbnails (thumb:Yes)
- [ ] All 4 records have "|" in path (indicating they came from archives)

## Step 4: Visual Verification - View Thumbnails in Database Viewer

Back in the app (or open a new instance with the same profile):

1. Click **"Visualizza Database"** button

2. The Database Viewer window opens

3. Click on any file record in the list

4. In the preview panel on the right, you should see the thumbnail image displayed

**What to verify:**
- [ ] Each record shows a thumbnail in the preview panel
- [ ] Thumbnails are properly scaled and readable
- [ ] No errors when clicking through records

## Troubleshooting

### Database not created
- Ensure you clicked "Scansione" button
- Check that the profile includes the correct archive directory
- Verify test archives exist in `test-archives\archives\`

### No thumbnail displayed
- Verify the test images in archives are valid JPG/PNG/GIF formats
- Check that the database schema includes the THUMBNAIL column
- Ensure records show "Yes" in the verification tool output

### "File not found" error in Database Viewer
This is **expected** for archive files. The preview should fall back to displaying the stored thumbnail instead.

## What This Tests

✓ **Archive Format Support**: ZIP, TAR.GZ, TGZ files are properly scanned  
✓ **Virtual Path Handling**: Files from archives are tracked with "path|file" format  
✓ **Thumbnail Generation**: Thumbnails are created during scan for each image  
✓ **Thumbnail Storage**: BLOB column properly stores thumbnail data  
✓ **Thumbnail Display**: UI displays thumbnails without re-extracting archives  
✓ **Database Schema Evolution**: Existing databases automatically get thumbnail column  

## Next Steps

After successful verification:

1. **Copy Workflow**: Test that files from archives are correctly extracted during the copy phase
2. **Large Archives**: Test with larger archive files to verify performance
3. **Mixed Content**: Test with directories containing both regular files and archives
