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
                dtFileName TEXT,
                dtFile TEXT,
                scanSession INTEGER NOT NULL DEFAULT 0,
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
                    hash,
                    coalesce(min(dtExif), min(dtFileName), min(dtFile))
                FROM SRC
                GROUP BY hash;
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

    public void InsertFile(string databasePath, string hash, string filePath, long size, DateTime? exifDate, DateTime? nameDate, DateTime fallbackDate, int scanSession)
    {
        using var conn = new SqliteConnection($"Data Source={databasePath}");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO SRC(hash, file, size, dtExif, dtFileName, dtFile, scanSession)
            VALUES($hash, $file, $size, $dtExif, $dtFileName, $dtFile, $scanSession);
            """;

        cmd.Parameters.AddWithValue("$hash", hash);
        cmd.Parameters.AddWithValue("$file", filePath);
        cmd.Parameters.AddWithValue("$size", size);
        cmd.Parameters.AddWithValue("$dtExif", (object?)exifDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$dtFileName", (object?)nameDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$dtFile", fallbackDate.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("$scanSession", scanSession);

        cmd.ExecuteNonQuery();
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
                        hash,
                        file,
                        coalesce(dtExif, dtFileName, dtFile) AS data_acquisizione,
                        row_number() OVER (
                            PARTITION BY hash
                            ORDER BY
                                CASE
                                    WHEN dtExif IS NOT NULL THEN 0
                                    WHEN dtFileName IS NOT NULL THEN 1
                                    ELSE 2
                                END,
                                coalesce(dtExif, dtFileName, dtFile),
                                file
                        ) AS rn
                    FROM SRC
                    WHERE scanSession = $scanSession
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
            var acqDate = TryParseAcqDate(row.AcqDateRaw)
                ?? File.GetLastWriteTime(row.FilePathSrc);

            var relativeFolder = BuildDestinationRelativeFolder(destinationTemplate, acqDate);
            var filePathDst = Path.Combine(destinationRoot, relativeFolder, fileName);

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
