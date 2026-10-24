using GestioneFoto.App.Models;
using Microsoft.Data.Sqlite;
using System.Globalization;

namespace GestioneFoto.App.Services;

public sealed class ExifRecalculationResult
{
    public int TotalCandidates { get; init; }
    public int SrcUpdated { get; init; }
    public int HashUpdated { get; init; }
    public int MovedCopiedFiles { get; init; }
    public int MissingSourceFiles { get; init; }
    public int MissingCopiedDestinations { get; init; }
    public int SkippedNoExif { get; init; }
    public int Errors { get; init; }
}

public sealed class UtilityCoordinator
{
    private readonly HashDbWriter _dbWriter;
    private readonly ExifDateReader _exifReader;

    public UtilityCoordinator(HashDbWriter dbWriter, ExifDateReader exifReader)
    {
        _dbWriter = dbWriter;
        _exifReader = exifReader;
    }

    public Task<ExifRecalculationResult> RecalculateMissingExifAsync(ProfileConfig profile, IProgress<ProgressInfo> progress, CancellationToken ct)
    {
        return Task.Run(() => RecalculateMissingExif(profile, progress, ct), ct);
    }

    private ExifRecalculationResult RecalculateMissingExif(ProfileConfig profile, IProgress<ProgressInfo> progress, CancellationToken ct)
    {
        _dbWriter.EnsureSchema(profile.DatabasePath);

        using var conn = new SqliteConnection($"Data Source={profile.DatabasePath}");
        conn.Open();

        var rows = LoadCandidates(conn);
        var total = rows.Count;
        var srcUpdated = 0;
        var hashUpdated = 0;
        var movedCopiedFiles = 0;
        var missingSourceFiles = 0;
        var missingCopiedDestinations = 0;
        var skippedNoExif = 0;
        var errors = 0;

        progress.Report(new ProgressInfo
        {
            Phase = "UtilityExif",
            Message = $"Record SRC con dtExif mancante: {total}",
            Processed = 0,
            Total = total
        });

        for (var i = 0; i < rows.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var row = rows[i];

            try
            {
                if (!File.Exists(row.FilePath))
                {
                    missingSourceFiles++;
                    ReportProgress(progress, i + 1, total, "Sorgente non trovato");
                    continue;
                }

                var exifDate = _exifReader.TryRead(row.FilePath);
                var fileNameDate = FileNameDateParser.TryReadDateFromPath(row.FilePath);
                var effectiveDate = exifDate ?? fileNameDate;

                if (!effectiveDate.HasValue)
                {
                    skippedNoExif++;
                    ReportProgress(progress, i + 1, total, "Nessuna data recuperabile (EXIF o nome file)");
                    continue;
                }

                var oldAcqDate = FirstAvailableDate(row.DtExif, row.DtFileName, row.DtFile);
                var dayChanged = !oldAcqDate.HasValue || oldAcqDate.Value.Date != effectiveDate.Value.Date;

                if (exifDate.HasValue)
                {
                    UpdateSrcExif(conn, row.Hash, row.FilePath, exifDate.Value);
                    srcUpdated++;
                }
                else if (fileNameDate.HasValue)
                {
                    UpdateSrcFileName(conn, row.Hash, row.FilePath, fileNameDate.Value);
                    srcUpdated++;
                }

                if (!string.IsNullOrWhiteSpace(row.HashFilePathSrc) &&
                    string.Equals(row.HashFilePathSrc, row.FilePath, StringComparison.OrdinalIgnoreCase))
                {
                    if (row.Copied == 1)
                    {
                        if (string.IsNullOrWhiteSpace(row.HashFilePathDst) || !File.Exists(row.HashFilePathDst))
                        {
                            missingCopiedDestinations++;
                        }
                        else
                        {
                            var currentTarget = row.HashFilePathDst;
                            var finalTarget = currentTarget;

                            if (dayChanged)
                            {
                                var newTarget = BuildDestinationPath(profile, row.HashFilePathSrc, effectiveDate.Value);
                                if (!PathsEqual(currentTarget, newTarget))
                                {
                                    var targetDirectory = Path.GetDirectoryName(newTarget);
                                    if (!string.IsNullOrWhiteSpace(targetDirectory))
                                    {
                                        Directory.CreateDirectory(targetDirectory);
                                    }

                                    finalTarget = EnsureUniquePath(newTarget);
                                    File.Move(currentTarget, finalTarget);
                                    movedCopiedFiles++;
                                }

                                UpdateHashDestination(conn, row.Hash, finalTarget);
                                hashUpdated++;
                            }

                            // Allinea sempre i timestamp del file gia' copiato alla data acquisizione effettiva.
                            File.SetCreationTime(finalTarget, effectiveDate.Value);
                            File.SetLastWriteTime(finalTarget, effectiveDate.Value);
                        }
                    }
                    else
                    {
                        var newPlannedPath = BuildDestinationPath(profile, row.HashFilePathSrc, effectiveDate.Value);
                        if (!PathsEqual(row.HashFilePathDst, newPlannedPath))
                        {
                            UpdateHashDestination(conn, row.Hash, newPlannedPath);
                            hashUpdated++;
                        }
                    }
                }

                ReportProgress(progress, i + 1, total, "Ricalcolo data acquisizione in corso");
            }
            catch
            {
                errors++;
                ReportProgress(progress, i + 1, total, "Errore durante il ricalcolo della data");
            }
        }

        progress.Report(new ProgressInfo
        {
            Phase = "UtilityExif",
            Message = "Ricalcolo dtExif completato.",
            Processed = total,
            Total = total
        });

        return new ExifRecalculationResult
        {
            TotalCandidates = total,
            SrcUpdated = srcUpdated,
            HashUpdated = hashUpdated,
            MovedCopiedFiles = movedCopiedFiles,
            MissingSourceFiles = missingSourceFiles,
            MissingCopiedDestinations = missingCopiedDestinations,
            SkippedNoExif = skippedNoExif,
            Errors = errors
        };
    }

    private static List<ExifCandidateRow> LoadCandidates(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT
                s.hash,
                s.file,
                s.dtExif,
                s.dtFileName,
                s.dtFile,
                h.filePathSrc,
                h.filePathDst,
                coalesce(h.copied, 0)
            FROM SRC s
            LEFT JOIN HASH h
                ON h.hash = s.hash
            WHERE trim(coalesce(s.dtExif, '')) = ''
            ORDER BY s.file;
            """;

        var rows = new List<ExifCandidateRow>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new ExifCandidateRow(
                Hash: reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                FilePath: reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                DtExif: reader.IsDBNull(2) ? null : reader.GetString(2),
                DtFileName: reader.IsDBNull(3) ? null : reader.GetString(3),
                DtFile: reader.IsDBNull(4) ? null : reader.GetString(4),
                HashFilePathSrc: reader.IsDBNull(5) ? null : reader.GetString(5),
                HashFilePathDst: reader.IsDBNull(6) ? null : reader.GetString(6),
                Copied: reader.IsDBNull(7) ? 0 : Convert.ToInt32(reader.GetInt64(7), CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    private static void UpdateSrcExif(SqliteConnection conn, string hash, string filePath, DateTime exifDate)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE SRC
            SET dtExif = $dtExif
            WHERE hash = $hash
              AND file = $file;
            """;
        cmd.Parameters.AddWithValue("$dtExif", exifDate.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$hash", hash);
        cmd.Parameters.AddWithValue("$file", filePath);
        cmd.ExecuteNonQuery();
    }

    private static void UpdateSrcFileName(SqliteConnection conn, string hash, string filePath, DateTime fileNameDate)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE SRC
            SET dtFileName = $dtFileName
            WHERE hash = $hash
              AND file = $file;
            """;
        cmd.Parameters.AddWithValue("$dtFileName", fileNameDate.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$hash", hash);
        cmd.Parameters.AddWithValue("$file", filePath);
        cmd.ExecuteNonQuery();
    }

    private static void UpdateHashDestination(SqliteConnection conn, string hash, string destinationPath)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE HASH
            SET filePathDst = $dst
            WHERE hash = $hash;
            """;
        cmd.Parameters.AddWithValue("$hash", hash);
        cmd.Parameters.AddWithValue("$dst", destinationPath);
        cmd.ExecuteNonQuery();
    }

    private static DateTime? FirstAvailableDate(string? dtExif, string? dtFileName, string? dtFile)
    {
        return TryParseDate(dtExif) ?? TryParseDate(dtFileName) ?? TryParseDate(dtFile);
    }

    private static DateTime? TryParseDate(string? raw)
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

        return DateTime.TryParse(raw, out dt) ? dt : null;
    }

    private static string BuildDestinationPath(ProfileConfig profile, string sourcePath, DateTime acquisitionDate)
    {
        var fileName = Path.GetFileName(sourcePath);
        var relativeFolder = BuildDestinationRelativeFolder(profile.DestinationTemplate, acquisitionDate);
        return string.IsNullOrWhiteSpace(relativeFolder)
            ? Path.Combine(profile.DestinationRoot, fileName)
            : Path.Combine(profile.DestinationRoot, relativeFolder, fileName);
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

        var parts = replaced.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? string.Empty : Path.Combine(parts);
    }

    private static void ReportProgress(IProgress<ProgressInfo> progress, int processed, int total, string message)
    {
        progress.Report(new ProgressInfo
        {
            Phase = "UtilityExifProgress",
            Message = message,
            Processed = processed,
            Total = total
        });
    }

    private static bool PathsEqual(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureUniquePath(string desiredPath)
    {
        if (!File.Exists(desiredPath))
        {
            return desiredPath;
        }

        var directory = Path.GetDirectoryName(desiredPath) ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(desiredPath);
        var extension = Path.GetExtension(desiredPath);

        var index = 1;
        string candidate;
        do
        {
            candidate = Path.Combine(directory, $"{fileName}_{index}{extension}");
            index++;
        }
        while (File.Exists(candidate));

        return candidate;
    }

    private sealed record ExifCandidateRow(
        string Hash,
        string FilePath,
        string? DtExif,
        string? DtFileName,
        string? DtFile,
        string? HashFilePathSrc,
        string? HashFilePathDst,
        int Copied);
}
