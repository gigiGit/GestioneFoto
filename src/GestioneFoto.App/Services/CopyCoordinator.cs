using GestioneFoto.App.Models;
using Microsoft.Data.Sqlite;
using System.Diagnostics;
using System.Globalization;

namespace GestioneFoto.App.Services;

public sealed class CopyExecutionResult
{
    public int Copied { get; init; }
    public int SkippedMissingSource { get; init; }
    public int Failed { get; init; }
    public int AlbumLinksCreated { get; init; }
    public int AlbumLinksSkipped { get; init; }
    public int AlbumLinksFailed { get; init; }
}

public sealed class AlbumRebuildResult
{
    public int ProcessedRows { get; init; }
    public int MissingDestination { get; init; }
    public int AlbumLinksCreated { get; init; }
    public int AlbumLinksSkipped { get; init; }
    public int AlbumLinksFailed { get; init; }
}

public sealed class AlbumLinkResult
{
    public int Created { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
}

public sealed class CopyCoordinator
{
    private readonly HashDbWriter _dbWriter;

    public CopyCoordinator(HashDbWriter dbWriter)
    {
        _dbWriter = dbWriter;
    }

    public Task<HashPreparationResult> PrepareHashAsync(ProfileConfig profile, IProgress<ProgressInfo> progress, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            _dbWriter.EnsureSchema(profile.DatabasePath);

            var currentScanSession = _dbWriter.GetCurrentScanSession(profile.DatabasePath);
            var lastPreparedScanSession = _dbWriter.GetLastPreparedScanSession(profile.DatabasePath);

            if (currentScanSession <= 0)
            {
                throw new InvalidOperationException("Eseguire prima una scansione.");
            }

            if (currentScanSession <= lastPreparedScanSession)
            {
                throw new InvalidOperationException("Prepara gia' eseguita per l'ultima scansione. Eseguire una nuova scansione.");
            }

            progress.Report(new ProgressInfo
            {
                Phase = "Hash",
                Message = $"Preparazione tabella HASH per scansione {currentScanSession}...",
                Processed = 0,
                Total = 1
            });

            var result = _dbWriter.PrepareHashFromScanSession(
                profile.DatabasePath,
                profile.DestinationRoot,
                profile.DestinationTemplate,
                currentScanSession,
                (processed, total, message) =>
                {
                    progress.Report(new ProgressInfo
                    {
                        Phase = "HashProgress",
                        Message = message,
                        Processed = processed,
                        Total = total
                    });
                });

            _dbWriter.SetLastPreparedScanSession(profile.DatabasePath, currentScanSession);

            foreach (var message in result.DuplicateMessages)
            {
                progress.Report(new ProgressInfo
                {
                    Phase = "HashDuplicate",
                    Message = message,
                    Processed = 1,
                    Total = 1
                });
            }

            progress.Report(new ProgressInfo
            {
                Phase = "Hash",
                Message = $"HASH aggiornata con {result.Inserted} record della scansione {currentScanSession}. Gia' presenti: {result.SkippedExisting}. Duplicati scartati: {result.SkippedDuplicates}.",
                Processed = 1,
                Total = 1
            });

            return result;
        }, ct);
    }

    public Task<CopyExecutionResult> CopyFromHashAsync(ProfileConfig profile, PauseController pause, IProgress<ProgressInfo> progress, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            _dbWriter.EnsureSchema(profile.DatabasePath);

            var rows = LoadCopyRows(profile.DatabasePath);
            var total = rows.Count;
            var copied = 0;
            var skippedMissingSource = 0;
            var failed = 0;
            var albumLinksCreated = 0;
            var albumLinksSkipped = 0;
            var albumLinksFailed = 0;

            progress.Report(new ProgressInfo
            {
                Phase = "Copy",
                Message = $"Record HASH da copiare: {total}",
                Processed = 0,
                Total = total
            });

            for (var i = 0; i < rows.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                pause.Wait(ct);

                var row = rows[i];
                try
                {
                    if (!ArchiveSupport.Exists(row.FilePathSrc))
                    {
                        skippedMissingSource++;
                        progress.Report(new ProgressInfo
                        {
                            Phase = "CopySkipped",
                            Message = $"Sorgente non trovato: {row.FilePathSrc}",
                            Processed = i + 1,
                            Total = total
                        });
                        continue;
                    }

                    var destinationPath = BuildDestinationPath(profile, row.FilePathSrc, row.AcquisitionDate);

                    var destinationDirectory = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrWhiteSpace(destinationDirectory))
                    {
                        Directory.CreateDirectory(destinationDirectory);
                    }

                    if (ArchiveSupport.IsVirtualPath(row.FilePathSrc))
                    {
                        ArchiveSupport.ExtractTo(row.FilePathSrc, destinationPath);
                    }
                    else
                    {
                        File.Copy(row.FilePathSrc, destinationPath, true);
                    }

                    if (row.AcquisitionDate.HasValue)
                    {
                        File.SetCreationTime(destinationPath, row.AcquisitionDate.Value);
                        File.SetLastWriteTime(destinationPath, row.AcquisitionDate.Value);
                    }

                    MarkAsCopied(profile.DatabasePath, row.Hash, destinationPath);

                    var albumLinkResult = CreateAlbumLinks(profile.AlbumDestination, row.Album, destinationPath, row.Hash);
                    albumLinksCreated += albumLinkResult.Created;
                    albumLinksSkipped += albumLinkResult.Skipped;
                    albumLinksFailed += albumLinkResult.Failed;

                    copied++;
                }
                catch
                {
                    failed++;
                }

                progress.Report(new ProgressInfo
                {
                    Phase = "CopyProgress",
                    Message = $"Copia {i + 1} di {total}",
                    Processed = i + 1,
                    Total = total
                });
            }

            progress.Report(new ProgressInfo
            {
                Phase = "Copy",
                Message = $"Copia completata. Copiati: {copied}, sorgenti mancanti: {skippedMissingSource}, errori: {failed}, link album creati: {albumLinksCreated}, link album saltati: {albumLinksSkipped}, link album falliti: {albumLinksFailed}.",
                Processed = total,
                Total = total
            });

            return new CopyExecutionResult
            {
                Copied = copied,
                SkippedMissingSource = skippedMissingSource,
                Failed = failed,
                AlbumLinksCreated = albumLinksCreated,
                AlbumLinksSkipped = albumLinksSkipped,
                AlbumLinksFailed = albumLinksFailed
            };
        }, ct);
    }

    public Task<AlbumRebuildResult> RebuildAlbumLinksAsync(ProfileConfig profile, IProgress<ProgressInfo> progress, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            _dbWriter.EnsureSchema(profile.DatabasePath);

            var rows = LoadAlbumRows(profile.DatabasePath);
            var total = rows.Count;
            var missingDestination = 0;
            var albumLinksCreated = 0;
            var albumLinksSkipped = 0;
            var albumLinksFailed = 0;

            progress.Report(new ProgressInfo
            {
                Phase = "AlbumRebuild",
                Message = $"Rigenerazione link album su {total} record copiati.",
                Processed = 0,
                Total = total
            });

            for (var i = 0; i < rows.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var row = rows[i];

                if (string.IsNullOrWhiteSpace(row.FilePathDst) || !File.Exists(row.FilePathDst))
                {
                    missingDestination++;
                    progress.Report(new ProgressInfo
                    {
                        Phase = "AlbumRebuildMissingDestination",
                        Message = $"Destinazione non trovata per hash {row.Hash}: {row.FilePathDst}",
                        Processed = i + 1,
                        Total = total
                    });
                    continue;
                }

                var linkResult = CreateAlbumLinks(profile.AlbumDestination, row.Album, row.FilePathDst, row.Hash);
                albumLinksCreated += linkResult.Created;
                albumLinksSkipped += linkResult.Skipped;
                albumLinksFailed += linkResult.Failed;

                progress.Report(new ProgressInfo
                {
                    Phase = "AlbumRebuildProgress",
                    Message = $"Rigenerazione album {i + 1} di {total}",
                    Processed = i + 1,
                    Total = total
                });
            }

            progress.Report(new ProgressInfo
            {
                Phase = "AlbumRebuild",
                Message = $"Rigenerazione album completata. Record: {total}, destinazioni mancanti: {missingDestination}, link creati: {albumLinksCreated}, link saltati: {albumLinksSkipped}, link falliti: {albumLinksFailed}.",
                Processed = total,
                Total = total
            });

            return new AlbumRebuildResult
            {
                ProcessedRows = total,
                MissingDestination = missingDestination,
                AlbumLinksCreated = albumLinksCreated,
                AlbumLinksSkipped = albumLinksSkipped,
                AlbumLinksFailed = albumLinksFailed
            };
        }, ct);
    }

    private static List<CopyRow> LoadCopyRows(string databasePath)
    {
        using var conn = new SqliteConnection($"Data Source={databasePath}");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT
                h.hash,
                h.filePathSrc,
                h.filePathDst,
                v.data_acquisizione,
                h.album
            FROM HASH h
            LEFT JOIN V_HASH_ACQUISITION_DATE v
                ON v.hash = h.hash
            WHERE coalesce(h.copied, 0) = 0
            ORDER BY h.filePathDst, h.filePathSrc;
            """;

        var rows = new List<CopyRow>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new CopyRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                TryParseAcquisitionDate(reader.IsDBNull(3) ? null : reader.GetString(3)),
                reader.IsDBNull(4) ? null : reader.GetString(4)));
        }

        return rows;
    }

    private static List<AlbumRow> LoadAlbumRows(string databasePath)
    {
        using var conn = new SqliteConnection($"Data Source={databasePath}");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT
                h.hash,
                h.filePathDst,
                h.album
            FROM HASH h
            WHERE coalesce(h.copied, 0) = 1
              AND trim(coalesce(h.album, '')) <> ''
            ORDER BY h.filePathDst;
            """;

        var rows = new List<AlbumRow>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new AlbumRow(
                reader.GetString(0),
                reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2)));
        }

        return rows;
    }

    private static DateTime? TryParseAcquisitionDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var formats = new[] { "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd" };
        if (DateTime.TryParseExact(raw, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            return dt;
        }

        return DateTime.TryParse(raw, out dt) ? dt : null;
    }

    private static string BuildDestinationPath(ProfileConfig profile, string sourcePath, DateTime? acquisitionDate)
    {
        var fileName = Path.GetFileName(sourcePath);
        var relativeFolder = acquisitionDate.HasValue
            ? BuildDestinationRelativeFolder(profile.DestinationTemplate, acquisitionDate.Value)
            : string.Empty;
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

    private static void MarkAsCopied(string databasePath, string hash, string destinationPath)
    {
        using var conn = new SqliteConnection($"Data Source={databasePath}");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE HASH
            SET copied = 1,
                filePathDst = $dst
            WHERE hash = $hash;
            """;
        cmd.Parameters.AddWithValue("$hash", hash);
        cmd.Parameters.AddWithValue("$dst", destinationPath);
        cmd.ExecuteNonQuery();
    }

    private static AlbumLinkResult CreateAlbumLinks(string? albumDestination, string? rawAlbum, string destinationPath, string hash)
    {
        var result = new AlbumLinkResult();

        if (string.IsNullOrWhiteSpace(albumDestination) ||
            string.IsNullOrWhiteSpace(rawAlbum) ||
            string.IsNullOrWhiteSpace(destinationPath) ||
            !File.Exists(destinationPath))
        {
            return result;
        }

        Directory.CreateDirectory(albumDestination);
        var fileName = Path.GetFileName(destinationPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return result;
        }

        foreach (var album in ParseAlbumValues(rawAlbum))
        {
            try
            {
                var safeAlbum = SanitizeFolderSegment(album);
                if (string.IsNullOrWhiteSpace(safeAlbum))
                {
                    result.Skipped++;
                    continue;
                }

                var albumFolder = Path.Combine(albumDestination, safeAlbum);
                Directory.CreateDirectory(albumFolder);

                var linkPath = Path.Combine(albumFolder, fileName);
                if (File.Exists(linkPath))
                {
                    result.Skipped++;
                    continue;
                }

                CreateWindowsLink(linkPath, destinationPath);
                result.Created++;
            }
            catch
            {
                result.Failed++;
            }
        }

        return result;
    }

    private static IEnumerable<string> ParseAlbumValues(string rawAlbum)
    {
        return rawAlbum
            .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static string SanitizeFolderSegment(string segment)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = segment
            .Select(ch => invalid.Contains(ch) ? '_' : ch)
            .ToArray();

        return new string(chars).Trim().TrimEnd('.');
    }

    private static void CreateWindowsLink(string linkPath, string targetPath)
    {
        if (TryCreateMkLink(linkPath, targetPath, hardLink: true))
        {
            return;
        }

        if (TryCreateMkLink(linkPath, targetPath, hardLink: false))
        {
            return;
        }

        throw new IOException($"Impossibile creare link: {linkPath} -> {targetPath}");
    }

    private static bool TryCreateMkLink(string linkPath, string targetPath, bool hardLink)
    {
        var mode = hardLink ? "/H " : string.Empty;
        var args = $"/c mklink {mode}\"{linkPath}\" \"{targetPath}\"";

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = args,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        process.Start();
        process.WaitForExit();
        return process.ExitCode == 0;
    }

    private sealed record CopyRow(string Hash, string FilePathSrc, string FilePathDst, DateTime? AcquisitionDate, string? Album);
    private sealed record AlbumRow(string Hash, string FilePathDst, string? Album);
}
