using System.Security.Cryptography;
using GestioneFoto.App.Models;

namespace GestioneFoto.App.Services;

public sealed class ScanCoordinator
{
    private readonly ExifDateReader _exifReader;
    private readonly HashDbWriter _dbWriter;

    public ScanCoordinator(ExifDateReader exifReader, HashDbWriter dbWriter)
    {
        _exifReader = exifReader;
        _dbWriter = dbWriter;
    }

    public Task RunAsync(ProfileConfig profile, PauseController pause, IProgress<ProgressInfo> progress, CancellationToken ct)
    {
        return Task.Run(() => Run(profile, pause, progress, ct), ct);
    }

    private void Run(ProfileConfig profile, PauseController pause, IProgress<ProgressInfo> progress, CancellationToken ct)
    {
        _dbWriter.EnsureSchema(profile.DatabasePath);
        var scanSession = _dbWriter.StartNewScanSession(profile.DatabasePath);

        progress.Report(new ProgressInfo
        {
            Phase = "Scan",
            Message = $"Ricerca file (sessione {scanSession})...",
            Processed = 0,
            Total = 0
        });

        var candidateFiles = EnumerateCandidateFiles(profile);
        var trackedFiles = _dbWriter.LoadTrackedSourceFiles(profile.DatabasePath);
        var files = candidateFiles
            .Where(file => !trackedFiles.Contains(file))
            .ToArray();
        var total = files.Length;

        progress.Report(new ProgressInfo
        {
            Phase = "Scan",
            Message = $"Trovati {candidateFiles.Length} file, nuovi da analizzare: {total}",
            Processed = 0,
            Total = total
        });

        for (var i = 0; i < total; i++)
        {
            ct.ThrowIfCancellationRequested();
            pause.Wait(ct);

            var filePath = files[i];
            ProcessFile(profile.DatabasePath, filePath, scanSession);

            var processed = i + 1;
            progress.Report(new ProgressInfo
            {
                Phase = "Process",
                Message = $"Analisi {processed} di {total}",
                Processed = processed,
                Total = total
            });
        }

        progress.Report(new ProgressInfo
        {
            Phase = "Done",
            Message = "Operazione completata.",
            Processed = total,
            Total = total
        });
    }

    private string[] EnumerateCandidateFiles(ProfileConfig profile)
    {
        var extSet = new HashSet<string>(profile.Extensions, StringComparer.OrdinalIgnoreCase);
        var excludes = profile.ExcludePaths.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        var result = new List<string>();

        foreach (var file in EnumerateFilesSafe(profile.IncludePath))
        {
            try
            {
                var info = new FileInfo(file);
                if (HasAnyAttribute(info.Attributes, FileAttributes.System | FileAttributes.Hidden | FileAttributes.ReparsePoint))
                {
                    continue;
                }

                if (!extSet.Contains(info.Extension.TrimStart('.')))
                {
                    continue;
                }

                if (info.Length < profile.MinSize)
                {
                    continue;
                }

                if (excludes.Any(e => file.StartsWith(e, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                result.Add(file);
            }
            catch
            {
                continue;
            }
        }

        return result.ToArray();
    }

    private static IEnumerable<string> EnumerateFilesSafe(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            IEnumerable<string> subDirs;
            try
            {
                subDirs = Directory.EnumerateDirectories(current);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }

            foreach (var dir in subDirs)
            {
                try
                {
                    var attrs = File.GetAttributes(dir);
                    if (HasAnyAttribute(attrs, FileAttributes.System | FileAttributes.Hidden | FileAttributes.ReparsePoint))
                    {
                        continue;
                    }

                    pending.Push(dir);
                }
                catch
                {
                    continue;
                }
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(current);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
            }
        }
    }

    private static bool HasAnyAttribute(FileAttributes value, FileAttributes mask)
    {
        return (value & mask) != 0;
    }

    private void ProcessFile(string databasePath, string filePath, int scanSession)
    {
        var info = new FileInfo(filePath);

        using var sha = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hashBytes = sha.ComputeHash(stream);
        var hash = Convert.ToHexString(hashBytes);

        if (_dbWriter.ExistsSourceEntry(databasePath, hash, filePath))
        {
            return;
        }

        var exifDate = _exifReader.TryRead(filePath);
        var nameDate = FileNameDateParser.TryReadDateFromPath(info.Name);
        var fallback = info.CreationTime < info.LastWriteTime ? info.CreationTime : info.LastWriteTime;

        _dbWriter.InsertFile(databasePath, hash, filePath, info.Length, exifDate, nameDate, fallback, scanSession);
    }
}
