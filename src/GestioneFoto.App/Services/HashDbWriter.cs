using Microsoft.Data.Sqlite;
using System.Globalization;

namespace GestioneFoto.App.Services;

public sealed class HashPreparationResult
{
    public int Inserted { get; init; }
    public int SkippedDuplicates { get; init; }
    public int SkippedExisting { get; init; }
    public IReadOnlyList<string> DuplicateMessages { get; init; } = Array.Empty<string>();
}

public sealed class HashDbWriter
{
    public void EnsureSchema(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? ".");

        using var conn = new SqliteConnection($"Data Source={databasePath}");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS SRC (
                hash TEXT NOT NULL,
                file TEXT NOT NULL,
                size INTEGER,
                dtExif TEXT,
                dtExifOriginal TEXT,
                dtExifDigitized TEXT,
                dtExifDateTime TEXT,
                dtAcqOverride TEXT,
                dtFileName TEXT,
                dtFolderName TEXT,
                dtFile TEXT,
                dtCreation TEXT,
                absolutePath TEXT,
                scanSession INTEGER NOT NULL DEFAULT 0,
                thumbnail BLOB,
                PRIMARY KEY (hash, file)
            );

            CREATE TABLE IF NOT EXISTS DST (
                hash TEXT NOT NULL PRIMARY KEY,
                data_acquisizione TEXT
            );

            CREATE TABLE IF NOT EXISTS HASH (
                hash TEXT NOT NULL PRIMARY KEY,
                filePathSrc TEXT NOT NULL,
                filePathDst TEXT NOT NULL,
                album TEXT,
                tipo TEXT,
                copied INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS APP_STATE (
                key TEXT NOT NULL PRIMARY KEY,
                value TEXT
            );
            """;
        cmd.ExecuteNonQuery();

        EnsureSrcScanSessionColumn(conn);
        EnsureHashCopiedColumn(conn);
        EnsureSrcThumbnailColumn(conn);
        EnsureSrcColumn(conn, "dtExifOriginal", "TEXT");
        EnsureSrcColumn(conn, "dtExifDigitized", "TEXT");
        EnsureSrcColumn(conn, "dtExifDateTime", "TEXT");
        EnsureSrcColumn(conn, "dtAcqOverride", "TEXT");
        EnsureSrcColumn(conn, "dtFolderName", "TEXT");
        EnsureSrcColumn(conn, "dtCreation", "TEXT");
        EnsureSrcColumn(conn, "absolutePath", "TEXT");
        EnsureAcquisitionDateView(conn);
    }

    private static void EnsureAcquisitionDateView(SqliteConnection conn)
    {
        using (var check = conn.CreateCommand())
        {
            check.CommandText = "SELECT sql FROM sqlite_master WHERE type = 'view' AND name = 'V_HASH_ACQUISITION_DATE';";
            var existingSql = check.ExecuteScalar()?.ToString();
            if (existingSql?.Contains("dtFolderName", StringComparison.OrdinalIgnoreCase) != true)
            {
                using var drop = conn.CreateCommand();
                drop.CommandText = "DROP VIEW IF EXISTS V_HASH_ACQUISITION_DATE;";
                drop.ExecuteNonQuery();
            }
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE VIEW IF NOT EXISTS V_HASH_ACQUISITION_DATE AS
            WITH candidates AS (
                SELECT
                    hash,
                    coalesce(
                        nullif(trim(dtAcqOverride), ''),
                        nullif(trim(dtExifOriginal), ''),
                        nullif(trim(dtExifDigitized), ''),
                        nullif(trim(dtExifDateTime), ''),
                        nullif(trim(dtExif), ''),
                        nullif(trim(dtFileName), ''),
                        nullif(trim(dtFolderName), ''),
                        nullif(trim(dtCreation), ''),
                        nullif(trim(dtFile), '')
                    ) AS data_acquisizione,
                    CASE
                        WHEN trim(coalesce(dtAcqOverride, '')) <> '' THEN 'Modifica manuale'
                        WHEN trim(coalesce(dtExifOriginal, '')) <> '' THEN 'EXIF Original'
                        WHEN trim(coalesce(dtExifDigitized, '')) <> '' THEN 'EXIF Digitized'
                        WHEN trim(coalesce(dtExifDateTime, '')) <> '' THEN 'EXIF DateTime'
                        WHEN trim(coalesce(dtExif, '')) <> '' THEN 'EXIF'
                        WHEN trim(coalesce(dtFileName, '')) <> '' THEN 'Nome file'
                        WHEN trim(coalesce(dtFolderName, '')) <> '' THEN 'Cartella'
                        WHEN trim(coalesce(dtCreation, '')) <> '' THEN 'Creazione file'
                        WHEN trim(coalesce(dtFile, '')) <> '' THEN 'Data file'
                    END AS data_source,
                    CASE
                        WHEN trim(coalesce(dtAcqOverride, '')) <> '' THEN 0
                        WHEN trim(coalesce(dtExifOriginal, '')) <> '' THEN 1
                        WHEN trim(coalesce(dtExifDigitized, '')) <> '' THEN 2
                        WHEN trim(coalesce(dtExifDateTime, '')) <> '' THEN 3
                        WHEN trim(coalesce(dtExif, '')) <> '' THEN 4
                        WHEN trim(coalesce(dtFileName, '')) <> '' THEN 5
                        WHEN trim(coalesce(dtFolderName, '')) <> '' THEN 6
                        WHEN trim(coalesce(dtCreation, '')) <> '' THEN 7
                        WHEN trim(coalesce(dtFile, '')) <> '' THEN 8
                        ELSE 9
                    END AS date_priority,
                    file AS source_file
                FROM SRC
            ),
            ranked AS (
                SELECT
                    *,
                    row_number() OVER (
                        PARTITION BY hash
                        ORDER BY date_priority, data_acquisizione, source_file
                    ) AS rn
                FROM candidates
            )
            SELECT hash, data_acquisizione, data_source, source_file
            FROM ranked
            WHERE rn = 1;
            """;
        cmd.ExecuteNonQuery();
    }

    private static void EnsureSrcColumn(SqliteConnection conn, string columnName, string columnType)
    {
        using var check = conn.CreateCommand();
        check.CommandText = "PRAGMA table_info(SRC);";

        using (var reader = check.ExecuteReader())
        {
            while (reader.Read())
            {
                if (!reader.IsDBNull(1) &&
                    string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
        }

        using var alter = conn.CreateCommand();
        alter.CommandText = $"ALTER TABLE SRC ADD COLUMN {columnName} {columnType};";
        alter.ExecuteNonQuery();
    }

    private static void EnsureSrcThumbnailColumn(SqliteConnection conn)
    {
        using var check = conn.CreateCommand();
        check.CommandText = "PRAGMA table_info(SRC);";

        var hasThumbnail = false;
        using (var reader = check.ExecuteReader())
        {
            while (reader.Read())
            {
                if (!reader.IsDBNull(1) && string.Equals(reader.GetString(1), "thumbnail", StringComparison.OrdinalIgnoreCase))
                {
                    hasThumbnail = true;
                    break;
                }
            }
        }

        if (!hasThumbnail)
        {
            using var alter = conn.CreateCommand();
            alter.CommandText = "ALTER TABLE SRC ADD COLUMN thumbnail BLOB;";
            alter.ExecuteNonQuery();
        }
    }


    private static void EnsureSrcScanSessionColumn(SqliteConnection conn)
    {
        using var check = conn.CreateCommand();
        check.CommandText = "PRAGMA table_info(SRC);";

        var hasScanSession = false;
        using (var reader = check.ExecuteReader())
        {
            while (reader.Read())
            {
                if (!reader.IsDBNull(1) && string.Equals(reader.GetString(1), "scanSession", StringComparison.OrdinalIgnoreCase))
                {
                    hasScanSession = true;
                    break;
                }
            }
        }

        if (!hasScanSession)
        {
            using var alter = conn.CreateCommand();
            alter.CommandText = "ALTER TABLE SRC ADD COLUMN scanSession INTEGER NOT NULL DEFAULT 0;";
            alter.ExecuteNonQuery();
        }
    }

    private static void EnsureHashCopiedColumn(SqliteConnection conn)
    {
        using var check = conn.CreateCommand();
        check.CommandText = "PRAGMA table_info(HASH);";

        var hasCopied = false;
        using (var reader = check.ExecuteReader())
        {
            while (reader.Read())
            {
                if (!reader.IsDBNull(1) && string.Equals(reader.GetString(1), "copied", StringComparison.OrdinalIgnoreCase))
                {
                    hasCopied = true;
                    break;
                }
            }
        }

        if (!hasCopied)
        {
            using var alter = conn.CreateCommand();
            alter.CommandText = "ALTER TABLE HASH ADD COLUMN copied INTEGER NOT NULL DEFAULT 0;";
            alter.ExecuteNonQuery();
        }
    }

    public HashSet<string> LoadTrackedSourceFiles(string databasePath)
    {
        using var conn = new SqliteConnection($"Data Source={databasePath}");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT file FROM SRC;";

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (!reader.IsDBNull(0))
            {
                result.Add(reader.GetString(0));
            }
        }

        return result;
    }

    public bool ExistsSourceEntry(string databasePath, string hash, string filePath)
    {
        using var conn = new SqliteConnection($"Data Source={databasePath}");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM SRC WHERE hash = $hash AND file = $file LIMIT 1;";
        cmd.Parameters.AddWithValue("$hash", hash);
        cmd.Parameters.AddWithValue("$file", filePath);

        return cmd.ExecuteScalar() is not null;
    }

    public int PrepareDstFromSrc(string databasePath)
    {
        using var conn = new SqliteConnection($"Data Source={databasePath}");
        conn.Open();

        using var tx = conn.BeginTransaction();

        using (var clear = conn.CreateCommand())
        {
            clear.Transaction = tx;
            clear.CommandText = "DELETE FROM DST;";
            clear.ExecuteNonQuery();
        }

        using (var fill = conn.CreateCommand())
        {
            fill.Transaction = tx;
            fill.CommandText = """
                INSERT INTO DST(hash, data_acquisizione)
                SELECT
                    hash, data_acquisizione
                FROM V_HASH_ACQUISITION_DATE;
                """;
            var rows = fill.ExecuteNonQuery();
            tx.Commit();
            return rows;
        }
    }

    public int StartNewScanSession(string databasePath)
    {
        EnsureSchema(databasePath);
        var current = GetCurrentScanSession(databasePath);
        var next = current + 1;
        SetState(databasePath, "currentScanSession", next.ToString(CultureInfo.InvariantCulture));
        return next;
    }

    public int GetCurrentScanSession(string databasePath)
    {
        EnsureSchema(databasePath);
        return GetStateInt(databasePath, "currentScanSession");
    }

    public int GetLastPreparedScanSession(string databasePath)
    {
        EnsureSchema(databasePath);
        return GetStateInt(databasePath, "lastPreparedScanSession");
    }

    public void SetLastPreparedScanSession(string databasePath, int scanSession)
    {
        EnsureSchema(databasePath);
        SetState(databasePath, "lastPreparedScanSession", scanSession.ToString(CultureInfo.InvariantCulture));
    }

    public void InsertFile(string databasePath, string hash, string filePath, long size, ExifDates exifDates, DateTime? nameDate, DateTime? folderDate, DateTime fallbackDate, DateTime? creationDate, string absolutePath, int scanSession, byte[]? thumbnail = null)
    {
        using var conn = new SqliteConnection($"Data Source={databasePath}");
        conn.Open();

        using var cmd = conn.CreateCommand();
        ConfigureInsertCommand(cmd);
        SetInsertValues(cmd, hash, filePath, size, exifDates, nameDate, folderDate, fallbackDate, creationDate, absolutePath, scanSession, thumbnail);
        cmd.ExecuteNonQuery();
    }

    public void InsertFile(string databasePath, string hash, string filePath, long size, DateTime? exifDate, DateTime? nameDate, DateTime fallbackDate, int scanSession, byte[]? thumbnail = null)
    {
        string absolutePath;
        if (ArchiveSupport.IsVirtualPath(filePath))
        {
            var (archive, entry) = ArchiveSupport.Split(filePath);
            absolutePath = ArchiveSupport.BuildVirtualPath(Path.GetFullPath(archive), entry);
        }
        else
        {
            absolutePath = Path.GetFullPath(filePath);
        }

        InsertFile(
            databasePath,
            hash,
            filePath,
            size,
            new ExifDates(exifDate, null, null),
            nameDate,
            null,
            fallbackDate,
            null,
            absolutePath,
            scanSession,
            thumbnail);
    }

    public ScanBatchWriter OpenScanBatchWriter(string databasePath)
    {
        return new ScanBatchWriter(databasePath);
    }

    private static void ConfigureInsertCommand(SqliteCommand cmd)
    {
        cmd.CommandText = """
            INSERT OR IGNORE INTO SRC(
                hash, file, size, dtExif, dtExifOriginal, dtExifDigitized, dtExifDateTime,
                dtFileName, dtFolderName, dtFile, dtCreation, absolutePath, scanSession, thumbnail)
            VALUES(
                $hash, $file, $size, $dtExif, $dtExifOriginal, $dtExifDigitized, $dtExifDateTime,
                $dtFileName, $dtFolderName, $dtFile, $dtCreation, $absolutePath, $scanSession, $thumbnail);
            """;
        cmd.Parameters.Add("$hash", SqliteType.Text);
        cmd.Parameters.Add("$file", SqliteType.Text);
        cmd.Parameters.Add("$size", SqliteType.Integer);
        cmd.Parameters.Add("$dtExif", SqliteType.Text);
        cmd.Parameters.Add("$dtExifOriginal", SqliteType.Text);
        cmd.Parameters.Add("$dtExifDigitized", SqliteType.Text);
        cmd.Parameters.Add("$dtExifDateTime", SqliteType.Text);
        cmd.Parameters.Add("$dtFileName", SqliteType.Text);
        cmd.Parameters.Add("$dtFolderName", SqliteType.Text);
        cmd.Parameters.Add("$dtFile", SqliteType.Text);
        cmd.Parameters.Add("$dtCreation", SqliteType.Text);
        cmd.Parameters.Add("$absolutePath", SqliteType.Text);
        cmd.Parameters.Add("$scanSession", SqliteType.Integer);
        cmd.Parameters.Add("$thumbnail", SqliteType.Blob);
    }

    private static void SetInsertValues(
        SqliteCommand cmd,
        string hash,
        string filePath,
        long size,
        ExifDates exifDates,
        DateTime? nameDate,
        DateTime? folderDate,
        DateTime fallbackDate,
        DateTime? creationDate,
        string absolutePath,
        int scanSession,
        byte[]? thumbnail)
    {
        cmd.Parameters["$hash"].Value = hash;
        cmd.Parameters["$file"].Value = filePath;
        cmd.Parameters["$size"].Value = size;
        cmd.Parameters["$dtExif"].Value = DbDate(exifDates.Preferred);
        cmd.Parameters["$dtExifOriginal"].Value = DbDate(exifDates.Original);
        cmd.Parameters["$dtExifDigitized"].Value = DbDate(exifDates.Digitized);
        cmd.Parameters["$dtExifDateTime"].Value = DbDate(exifDates.Generic);
        cmd.Parameters["$dtFileName"].Value = DbDate(nameDate);
        cmd.Parameters["$dtFolderName"].Value = DbDate(folderDate);
        cmd.Parameters["$dtFile"].Value = DbDate(fallbackDate);
        cmd.Parameters["$dtCreation"].Value = DbDate(creationDate);
        cmd.Parameters["$absolutePath"].Value = absolutePath;
        cmd.Parameters["$scanSession"].Value = scanSession;
        cmd.Parameters["$thumbnail"].Value = (object?)thumbnail ?? DBNull.Value;
    }

    private static object DbDate(DateTime? date)
    {
        return date?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? (object)DBNull.Value;
    }

    public sealed class ScanBatchWriter : IDisposable
    {
        private const int BatchSize = 256;
        private readonly SqliteConnection _connection;
        private SqliteTransaction? _transaction;
        private SqliteCommand? _command;
        private SqliteCommand? _metadataCommand;
        private int _pendingRows;
        private bool _disposed;

        internal ScanBatchWriter(string databasePath)
        {
            _connection = new SqliteConnection($"Data Source={databasePath}");
            _connection.Open();
            try
            {
                StartBatch();
            }
            catch
            {
                _connection.Dispose();
                throw;
            }
        }

        public void InsertFile(
            string hash,
            string filePath,
            long size,
            ExifDates exifDates,
            DateTime? nameDate,
            DateTime? folderDate,
            DateTime fallbackDate,
            DateTime? creationDate,
            string absolutePath,
            int scanSession,
            byte[]? thumbnail = null)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var command = _command ?? throw new InvalidOperationException("La transazione di scansione non è attiva.");
            SetInsertValues(command, hash, filePath, size, exifDates, nameDate, folderDate, fallbackDate, creationDate, absolutePath, scanSession, thumbnail);
            command.ExecuteNonQuery();

            if (++_pendingRows >= BatchSize)
            {
                CommitBatch();
                StartBatch();
            }
        }

        public bool InsertFileBasic(
            string hash,
            string filePath,
            long size,
            DateTime? nameDate,
            DateTime? folderDate,
            DateTime fallbackDate,
            DateTime? creationDate,
            string absolutePath,
            int scanSession)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var command = _command ?? throw new InvalidOperationException("La transazione di scansione non è attiva.");
            SetInsertValues(
                command,
                hash,
                filePath,
                size,
                default,
                nameDate,
                folderDate,
                fallbackDate,
                creationDate,
                absolutePath,
                scanSession,
                null);
            var inserted = command.ExecuteNonQuery() > 0;
            if (inserted)
            {
                CompleteRow();
            }

            return inserted;
        }

        public void UpdateMetadata(string hash, string filePath, ExifDates exifDates, byte[]? thumbnail)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var command = _metadataCommand ?? throw new InvalidOperationException("La transazione di scansione non è attiva.");
            command.Parameters["$dtExif"].Value = DbDate(exifDates.Preferred);
            command.Parameters["$dtExifOriginal"].Value = DbDate(exifDates.Original);
            command.Parameters["$dtExifDigitized"].Value = DbDate(exifDates.Digitized);
            command.Parameters["$dtExifDateTime"].Value = DbDate(exifDates.Generic);
            command.Parameters["$thumbnail"].Value = (object?)thumbnail ?? DBNull.Value;
            command.Parameters["$hash"].Value = hash;
            command.Parameters["$file"].Value = filePath;
            if (command.ExecuteNonQuery() > 0)
            {
                CompleteRow();
            }
        }

        private void CompleteRow()
        {
            if (++_pendingRows >= BatchSize)
            {
                CommitBatch();
                StartBatch();
            }
        }

        private void StartBatch()
        {
            _transaction = _connection.BeginTransaction();
            _command = _connection.CreateCommand();
            _command.Transaction = _transaction;
            ConfigureInsertCommand(_command);
            _command.Prepare();

            _metadataCommand = _connection.CreateCommand();
            _metadataCommand.Transaction = _transaction;
            _metadataCommand.CommandText = """
                UPDATE SRC
                SET
                    dtExif = coalesce(nullif(trim(dtExif), ''), $dtExif),
                    dtExifOriginal = coalesce(nullif(trim(dtExifOriginal), ''), $dtExifOriginal),
                    dtExifDigitized = coalesce(nullif(trim(dtExifDigitized), ''), $dtExifDigitized),
                    dtExifDateTime = coalesce(nullif(trim(dtExifDateTime), ''), $dtExifDateTime),
                    thumbnail = coalesce(thumbnail, $thumbnail)
                WHERE hash = $hash AND file = $file;
                """;
            _metadataCommand.Parameters.Add("$dtExif", SqliteType.Text);
            _metadataCommand.Parameters.Add("$dtExifOriginal", SqliteType.Text);
            _metadataCommand.Parameters.Add("$dtExifDigitized", SqliteType.Text);
            _metadataCommand.Parameters.Add("$dtExifDateTime", SqliteType.Text);
            _metadataCommand.Parameters.Add("$thumbnail", SqliteType.Blob);
            _metadataCommand.Parameters.Add("$hash", SqliteType.Text);
            _metadataCommand.Parameters.Add("$file", SqliteType.Text);
            _metadataCommand.Prepare();
        }

        private void CommitBatch()
        {
            _command?.Dispose();
            _command = null;
            _metadataCommand?.Dispose();
            _metadataCommand = null;
            _transaction?.Commit();
            _transaction?.Dispose();
            _transaction = null;
            _pendingRows = 0;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                _command?.Dispose();
                _metadataCommand?.Dispose();
                if (_pendingRows > 0)
                {
                    _transaction?.Commit();
                }
                else
                {
                    _transaction?.Rollback();
                }
            }
            finally
            {
                _transaction?.Dispose();
                _connection.Dispose();
                _disposed = true;
            }
        }
    }

    public HashPreparationResult PrepareHashFromScanSession(
        string databasePath,
        string destinationRoot,
        string destinationTemplate,
        int scanSession,
        Action<int, int, string>? progressCallback = null)
    {
        using var conn = new SqliteConnection($"Data Source={databasePath}");
        conn.Open();

        using var tx = conn.BeginTransaction();

        var selectedRows = new List<(string Hash, string FilePathSrc, string? AcqDateRaw, bool AlreadyInHash)>();
        var albumMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        using (var albumSelect = conn.CreateCommand())
        {
            albumSelect.Transaction = tx;
            albumSelect.CommandText = "SELECT hash, file FROM SRC;";

            using var albumReader = albumSelect.ExecuteReader();
            while (albumReader.Read())
            {
                var hash = albumReader.GetString(0);
                var filePath = albumReader.GetString(1);
                var parentFolder = Directory.GetParent(filePath)?.Name;

                if (string.IsNullOrWhiteSpace(parentFolder))
                {
                    continue;
                }

                if (!albumMap.TryGetValue(hash, out var folders))
                {
                    folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    albumMap[hash] = folders;
                }

                folders.Add(parentFolder);
            }
        }

        using (var select = conn.CreateCommand())
        {
            select.Transaction = tx;
            select.CommandText = """
                WITH ranked AS (
                    SELECT
                        s.hash,
                        s.file,
                        v.data_acquisizione,
                        row_number() OVER (
                            PARTITION BY s.hash
                            ORDER BY s.file
                        ) AS rn
                    FROM SRC s
                    INNER JOIN V_HASH_ACQUISITION_DATE v
                        ON v.hash = s.hash
                    WHERE s.scanSession = $scanSession
                )
                SELECT
                    ranked.hash,
                    ranked.file,
                    ranked.data_acquisizione,
                    CASE WHEN h.hash IS NULL THEN 0 ELSE 1 END AS already_in_hash
                FROM ranked
                LEFT JOIN HASH h
                    ON h.hash = ranked.hash
                WHERE ranked.rn = 1;
                """;
            select.Parameters.AddWithValue("$scanSession", scanSession);

            using var reader = select.ExecuteReader();
            while (reader.Read())
            {
                var hash = reader.GetString(0);
                var filePathSrc = reader.GetString(1);
                var acqDateRaw = reader.IsDBNull(2) ? null : reader.GetString(2);
                var alreadyInHash = !reader.IsDBNull(3) && reader.GetInt64(3) != 0;
                selectedRows.Add((hash, filePathSrc, acqDateRaw, alreadyInHash));
            }
        }

        var inserted = 0;
        var skippedDuplicates = 0;
        var skippedExisting = 0;
        var duplicateMessages = new List<string>();
        var destinationMap = new Dictionary<string, (string Hash, string FilePathSrc)>(StringComparer.OrdinalIgnoreCase);
        var totalCandidates = selectedRows.Count;

        using (var existingDest = conn.CreateCommand())
        {
            existingDest.Transaction = tx;
            existingDest.CommandText = "SELECT hash, filePathSrc, filePathDst FROM HASH;";
            using var reader = existingDest.ExecuteReader();
            while (reader.Read())
            {
                if (reader.IsDBNull(2))
                {
                    continue;
                }

                var pathDst = reader.GetString(2);
                if (string.IsNullOrWhiteSpace(pathDst))
                {
                    continue;
                }

                destinationMap[pathDst] = (
                    reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                    reader.IsDBNull(1) ? string.Empty : reader.GetString(1));
            }
        }

        progressCallback?.Invoke(0, totalCandidates, $"Selezionati {totalCandidates} hash della scansione {scanSession}...");

        for (var i = 0; i < selectedRows.Count; i++)
        {
            var row = selectedRows[i];

            if (row.AlreadyInHash)
            {
                skippedExisting++;
                progressCallback?.Invoke(i + 1, totalCandidates, $"Preparazione copia {i + 1} di {totalCandidates}");
                continue;
            }

            var fileName = Path.GetFileName(row.FilePathSrc);
            var acqDate = TryParseAcqDate(row.AcqDateRaw);
            var relativeFolder = acqDate.HasValue
                ? BuildDestinationRelativeFolder(destinationTemplate, acqDate.Value)
                : string.Empty;
            var filePathDst = string.IsNullOrWhiteSpace(relativeFolder)
                ? Path.Combine(destinationRoot, fileName)
                : Path.Combine(destinationRoot, relativeFolder, fileName);

            var album = albumMap.TryGetValue(row.Hash, out var foldersForHash)
                ? string.Join(',', foldersForHash.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                : string.Empty;
            var tipo = ResolveMediaTypeByExtension(Path.GetExtension(row.FilePathSrc));

            if (destinationMap.TryGetValue(filePathDst, out var existing))
            {
                skippedDuplicates++;
                duplicateMessages.Add(
                    $"Duplicazione rilevata: dst='{filePathDst}' già assegnato ad hash='{existing.Hash}' src='{existing.FilePathSrc}'. " +
                    $"Scartato hash='{row.Hash}' src='{row.FilePathSrc}'.");
                progressCallback?.Invoke(i + 1, totalCandidates, $"Preparazione copia {i + 1} di {totalCandidates}");
                continue;
            }

            destinationMap[filePathDst] = (row.Hash, row.FilePathSrc);

            using var insert = conn.CreateCommand();
            insert.Transaction = tx;
            insert.CommandText = """
                INSERT OR IGNORE INTO HASH(hash, filePathSrc, filePathDst, album, tipo, copied)
                VALUES($hash, $filePathSrc, $filePathDst, $album, $tipo, 0);
                """;

            insert.Parameters.AddWithValue("$hash", row.Hash);
            insert.Parameters.AddWithValue("$filePathSrc", row.FilePathSrc);
            insert.Parameters.AddWithValue("$filePathDst", filePathDst);
            insert.Parameters.AddWithValue("$album", album);
            insert.Parameters.AddWithValue("$tipo", tipo);
            insert.ExecuteNonQuery();

            inserted++;
            progressCallback?.Invoke(i + 1, totalCandidates, $"Preparazione copia {i + 1} di {totalCandidates}");
        }

        tx.Commit();
        return new HashPreparationResult
        {
            Inserted = inserted,
            SkippedDuplicates = skippedDuplicates,
            SkippedExisting = skippedExisting,
            DuplicateMessages = duplicateMessages
        };
    }

    private static int GetStateInt(string databasePath, string key)
    {
        using var conn = new SqliteConnection($"Data Source={databasePath}");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM APP_STATE WHERE key = $key LIMIT 1;";
        cmd.Parameters.AddWithValue("$key", key);
        var value = cmd.ExecuteScalar()?.ToString();

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static void SetState(string databasePath, string key, string value)
    {
        using var conn = new SqliteConnection($"Data Source={databasePath}");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO APP_STATE(key, value)
            VALUES($key, $value)
            ON CONFLICT(key) DO UPDATE SET
                value = excluded.value;
            """;
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$value", value);
        cmd.ExecuteNonQuery();
    }

    private static DateTime? TryParseAcqDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var formats = new[]
        {
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm",
            "yyyy-MM-dd"
        };

        if (DateTime.TryParseExact(raw, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            return dt;
        }

        if (DateTime.TryParse(raw, out dt))
        {
            return dt;
        }

        return null;
    }

    private static string BuildDestinationRelativeFolder(string template, DateTime date)
    {
        var normalized = (template ?? string.Empty)
            .Replace('\\', '/')
            .Trim('/');

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var replaced = normalized
            .Replace("YYYY", date.ToString("yyyy", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("MM", date.ToString("MM", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("DD", date.ToString("dd", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);

        var parts = replaced
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length == 0 ? string.Empty : Path.Combine(parts);
    }

    private static string ResolveMediaTypeByExtension(string extension)
    {
        var normalized = extension.Trim().TrimStart('.').ToLowerInvariant();
        var videoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "mp4", "avi", "mov", "mkv", "wmv", "flv", "webm", "m4v", "3gp", "mpg", "mpeg", "m2ts"
        };

        return videoExtensions.Contains(normalized) ? "Video" : "Immagine";
    }
}
