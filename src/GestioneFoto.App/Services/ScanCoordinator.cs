using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using GestioneFoto.App.Models;

namespace GestioneFoto.App.Services;

public sealed class ScanCoordinator
{
    private readonly ExifDateReader _exifReader;
    private readonly HashDbWriter _dbWriter;
    private readonly ThumbnailGenerator _thumbnailGenerator;
    private readonly List<string> _archives = new();
    private readonly int _maxDegreeOfParallelism;

    private sealed record MetadataWorkItem(string Hash, string FilePath, string? ArchiveEntryName, int? ArchiveEntryIndex = null);

    public ScanCoordinator(ExifDateReader exifReader, HashDbWriter dbWriter, int? maxDegreeOfParallelism = null)
    {
        _exifReader = exifReader;
        _dbWriter = dbWriter;
        _thumbnailGenerator = new ThumbnailGenerator();
        _maxDegreeOfParallelism = maxDegreeOfParallelism ?? Math.Clamp(Environment.ProcessorCount, 2, 8);
        if (_maxDegreeOfParallelism < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism));
        }
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

        var files = EnumerateCandidateFiles(profile);
        var pendingMetadata = new ConcurrentBag<MetadataWorkItem>();
        using var scanWriter = _dbWriter.OpenScanBatchWriter(profile.DatabasePath);

        progress.Report(new ProgressInfo
        {
            Phase = "Hash",
            Message = $"Fase 1/2: calcolo hash e registrazione dati rapidi. File: {files.Length}; archivi: {_archives.Count}.",
            Processed = 0,
            Total = files.Length,
            CurrentFile = files.FirstOrDefault()
        });

        var processedFiles = 0;
        var lastReportedFiles = 0;
        var lastProgressTimestamp = Stopwatch.GetTimestamp();
        var progressLock = new object();
        Parallel.ForEach(
            files,
            new ParallelOptions
            {
                CancellationToken = ct,
                MaxDegreeOfParallelism = _maxDegreeOfParallelism
            },
            filePath =>
        {
            pause.Wait(ct);
            ReportPhase1Progress(progress, progressLock, ref lastReportedFiles, ref lastProgressTimestamp,
                processedFiles, files.Length, filePath, "Calcolo hash");
            var item = ProcessFileHash(filePath, scanWriter, scanSession, ct);
            if (item is not null)
            {
                pendingMetadata.Add(item);
            }

            var processed = Interlocked.Increment(ref processedFiles);
            ReportPhase1Progress(progress, progressLock, ref lastReportedFiles, ref lastProgressTimestamp,
                processed, files.Length, filePath, "Hash registrati");
        });

        var extSet = new HashSet<string>(profile.Extensions, StringComparer.OrdinalIgnoreCase);
        for (var a = 0; a < _archives.Count; a++)
        {
            ct.ThrowIfCancellationRequested();
            pause.Wait(ct);

            var archive = _archives[a];
            progress.Report(new ProgressInfo
            {
                Phase = "HashArchive",
                Message = $"Fase 1/2: hash archivio {a + 1} di {_archives.Count}: {archive}",
                Processed = a,
                Total = _archives.Count,
                CurrentFile = archive
            });

            try
            {
                ProcessArchiveHashes(
                    archive,
                    a,
                    _archives.Count,
                    extSet,
                    profile.MinSize,
                    scanWriter,
                    scanSession,
                    pendingMetadata,
                    pause,
                    progress,
                    ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                progress.Report(new ProgressInfo
                {
                    Phase = "ArchiveError",
                    Message = $"Archivio non leggibile {archive}: {ex.Message}",
                    Processed = a + 1,
                    Total = _archives.Count
                });
            }
        }

        var metadataItems = pendingMetadata.ToArray();
        var fileMetadataItems = metadataItems.Where(item => item.ArchiveEntryName is null).ToArray();
        var archiveMetadataItems = metadataItems.Where(item => item.ArchiveEntryName is not null).ToArray();
        var metadataTotal = metadataItems.Length;
        var metadataProcessed = 0;
        progress.Report(new ProgressInfo
        {
            Phase = "Metadata",
            Message = $"Fase 2/2: recupero EXIF e thumbnail. File da arricchire: {metadataTotal}.",
            Processed = 0,
            Total = metadataTotal,
            CurrentFile = metadataItems.FirstOrDefault()?.FilePath
        });

        var metadataProgressLock = new object();
        var lastMetadataReport = Stopwatch.GetTimestamp();
        Parallel.ForEach(
            fileMetadataItems,
            new ParallelOptions
            {
                CancellationToken = ct,
                MaxDegreeOfParallelism = _maxDegreeOfParallelism
            },
            item =>
            {
                pause.Wait(ct);
                var exifDates = _exifReader.ReadDates(item.FilePath);
                var thumbnail = _thumbnailGenerator.TryGenerate(item.FilePath);
                ct.ThrowIfCancellationRequested();
                lock (scanWriter)
                {
                    scanWriter.UpdateMetadata(item.Hash, item.FilePath, exifDates, thumbnail);
                }

                ReportMetadataProgress(progress, metadataProgressLock, ref metadataProcessed, ref lastMetadataReport,
                    metadataTotal, item.FilePath);
            });

        var archiveGroups = archiveMetadataItems
            .GroupBy(item => ArchiveSupport.Split(item.FilePath).Archive, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        for (var archiveIndex = 0; archiveIndex < archiveGroups.Length; archiveIndex++)
        {
            ct.ThrowIfCancellationRequested();
            pause.Wait(ct);
            var archiveGroup = archiveGroups[archiveIndex];
            try
            {
                ProcessArchiveMetadata(
                    profile,
                    archiveGroup.Key,
                    archiveGroup.ToArray(),
                    scanWriter,
                    pause,
                    progress,
                    metadataProgressLock,
                    ref metadataProcessed,
                    ref lastMetadataReport,
                    metadataTotal,
                    ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                progress.Report(new ProgressInfo
                {
                    Phase = "MetadataError",
                    Message = $"Metadati non letti dall'archivio {archiveGroup.Key}: {ex.Message}",
                    Processed = metadataProcessed,
                    Total = metadataTotal,
                    CurrentFile = archiveGroup.Key
                });
            }
        }

        progress.Report(new ProgressInfo
        {
            Phase = "Done",
            Message = "Operazione completata.",
            Processed = metadataProcessed,
            Total = metadataTotal
        });
    }

    private static void ReportPhase1Progress(
        IProgress<ProgressInfo> progress,
        object progressLock,
        ref int lastReportedCount,
        ref long lastProgressTimestamp,
        int processed,
        int total,
        string currentFile,
        string status)
    {
        lock (progressLock)
        {
            var elapsed = Stopwatch.GetElapsedTime(lastProgressTimestamp);
            if (processed != 0 &&
                processed != total &&
                processed - lastReportedCount < 100 &&
                elapsed < TimeSpan.FromMilliseconds(250))
            {
                return;
            }

            lastReportedCount = processed;
            lastProgressTimestamp = Stopwatch.GetTimestamp();
            progress.Report(new ProgressInfo
            {
                Phase = "Hash",
                Message = $"Fase 1/2: {status} {processed} di {total}",
                Processed = processed,
                Total = total,
                CurrentFile = currentFile
            });
        }
    }

    private static void ReportMetadataProgress(
        IProgress<ProgressInfo> progress,
        object progressLock,
        ref int processed,
        ref long lastProgressTimestamp,
        int total,
        string currentFile)
    {
        var count = Interlocked.Increment(ref processed);
        lock (progressLock)
        {
            var elapsed = Stopwatch.GetElapsedTime(lastProgressTimestamp);
            if (count != total &&
                count % 100 != 0 &&
                elapsed < TimeSpan.FromMilliseconds(250))
            {
                return;
            }

            lastProgressTimestamp = Stopwatch.GetTimestamp();
            progress.Report(new ProgressInfo
            {
                Phase = "Metadata",
                Message = $"Fase 2/2: EXIF e thumbnail {count} di {total}",
                Processed = count,
                Total = total,
                CurrentFile = currentFile
            });
        }
    }

    private string[] EnumerateCandidateFiles(ProfileConfig profile)
    {
        var extSet = new HashSet<string>(profile.Extensions, StringComparer.OrdinalIgnoreCase);
        var excludes = profile.ExcludePaths.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        var result = new List<string>();
        _archives.Clear();

        foreach (var file in EnumerateFilesSafe(profile.IncludePath))
        {
            try
            {
                var info = new FileInfo(file);
                if (HasAnyAttribute(info.Attributes, FileAttributes.System | FileAttributes.Hidden | FileAttributes.ReparsePoint))
                {
                    continue;
                }

                if (ArchiveSupport.IsArchive(file))
                {
                    if (!excludes.Any(e => file.StartsWith(e, StringComparison.OrdinalIgnoreCase)))
                    {
                        _archives.Add(Path.GetFullPath(file));
                    }

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

                result.Add(Path.GetFullPath(file));
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

    private void ProcessArchiveHashes(
        string archivePath,
        int archiveIndex,
        int archiveCount,
        HashSet<string> extSet,
        long minSize,
        HashDbWriter.ScanBatchWriter scanWriter,
        int scanSession,
        ConcurrentBag<MetadataWorkItem> pendingMetadata,
        PauseController pause,
        IProgress<ProgressInfo> progress,
        CancellationToken ct)
    {
        archivePath = Path.GetFullPath(archivePath);
        var archiveEntryIndex = 0;
        foreach (var entry in ArchiveSupport.Enumerate(archivePath))
        {
            ct.ThrowIfCancellationRequested();
            pause.Wait(ct);

            var currentEntryIndex = archiveEntryIndex++;
            var name = entry.Name.Replace('\\', '/');
            var fileName = name[(name.LastIndexOf('/') + 1)..];
            var ext = Path.GetExtension(fileName).TrimStart('.');
            if (!extSet.Contains(ext) || entry.Length < minSize)
            {
                continue;
            }

            var virtualPath = ArchiveSupport.BuildVirtualPath(archivePath, name);
            progress.Report(new ProgressInfo
            {
                Phase = "HashArchive",
                Message = $"Fase 1/2: hash archivio {archiveIndex + 1} di {archiveCount}",
                Processed = archiveIndex,
                Total = archiveCount,
                CurrentFile = virtualPath
            });

            string hash;
            using (var input = entry.Open())
            {
                hash = Convert.ToHexString(SHA256.HashData(input));
            }

            ct.ThrowIfCancellationRequested();
            var nameDate = FileNameDateParser.TryReadDateFromPath(fileName);
            var folderDate = FileNameDateParser.TryReadDateFromParentFolders(virtualPath);
            var inserted = false;
            lock (scanWriter)
            {
                inserted = scanWriter.InsertFileBasic(
                    hash,
                    virtualPath,
                    entry.Length,
                    nameDate,
                    folderDate,
                    entry.LastWriteTime,
                    null,
                    virtualPath,
                    scanSession);
            }

            if (inserted)
            {
                pendingMetadata.Add(new MetadataWorkItem(hash, virtualPath, name, currentEntryIndex));
            }
        }
    }

    private void ProcessArchiveMetadata(
        ProfileConfig profile,
        string archivePath,
        MetadataWorkItem[] items,
        HashDbWriter.ScanBatchWriter scanWriter,
        PauseController pause,
        IProgress<ProgressInfo> progress,
        object progressLock,
        ref int metadataProcessed,
        ref long lastProgressTimestamp,
        int metadataTotal,
        CancellationToken ct)
    {
        var pendingByName = items
            .GroupBy(item => item.ArchiveEntryName!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => new Queue<MetadataWorkItem>(group.OrderBy(item => item.ArchiveEntryIndex)),
                StringComparer.Ordinal);
        var baseTempDir = string.IsNullOrWhiteSpace(profile.ArchiveTempPath)
            ? Path.GetTempPath()
            : profile.ArchiveTempPath;
        try
        {
            Directory.CreateDirectory(baseTempDir);
        }
        catch (Exception ex) when (
            (ex is IOException or UnauthorizedAccessException) &&
            !string.Equals(baseTempDir, Path.GetTempPath(), StringComparison.OrdinalIgnoreCase))
        {
            baseTempDir = Path.GetTempPath();
            Directory.CreateDirectory(baseTempDir);
        }

        var tempDir = Path.Combine(baseTempDir, "GestioneFoto_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);

        try
        {
            foreach (var entry in ArchiveSupport.Enumerate(archivePath))
            {
                ct.ThrowIfCancellationRequested();
                pause.Wait(ct);
                var name = entry.Name.Replace('\\', '/');
                if (!pendingByName.TryGetValue(name, out var itemsForName) || itemsForName.Count == 0)
                {
                    continue;
                }

                var item = itemsForName.Dequeue();
                if (itemsForName.Count == 0)
                {
                    pendingByName.Remove(name);
                }

                progress.Report(new ProgressInfo
                {
                    Phase = "Metadata",
                    Message = "Fase 2/2: recupero EXIF e thumbnail dall'archivio",
                    Processed = metadataProcessed,
                    Total = metadataTotal,
                    CurrentFile = item.FilePath
                });

                var extension = Path.GetExtension(name);
                var tempFile = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + extension);
                try
                {
                    using (var input = entry.Open())
                    using (var output = File.Create(tempFile))
                    {
                        input.CopyTo(output);
                    }

                    var exifDates = _exifReader.ReadDates(tempFile);
                    var thumbnail = _thumbnailGenerator.TryGenerate(tempFile);
                    ct.ThrowIfCancellationRequested();
                    lock (scanWriter)
                    {
                        scanWriter.UpdateMetadata(item.Hash, item.FilePath, exifDates, thumbnail);
                    }

                    ReportMetadataProgress(
                        progress,
                        progressLock,
                        ref metadataProcessed,
                        ref lastProgressTimestamp,
                        metadataTotal,
                        item.FilePath);
                }
                finally
                {
                    File.Delete(tempFile);
                }
            }

            var missingItems = pendingByName.Values.Sum(queue => queue.Count);
            if (missingItems > 0)
            {
                throw new InvalidDataException(
                    $"Nell'archivio {archivePath} non sono state trovate {missingItems} voci da arricchire.");
            }
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private MetadataWorkItem? ProcessFileHash(string filePath, HashDbWriter.ScanBatchWriter scanWriter, int scanSession, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var absolutePath = Path.GetFullPath(filePath);
        var info = new FileInfo(absolutePath);
        info.Refresh();
        var size = info.Length;
        var creationDate = info.CreationTime;
        var fallbackDate = creationDate < info.LastWriteTime ? creationDate : info.LastWriteTime;
        var nameDate = FileNameDateParser.TryReadDateFromPath(absolutePath);
        var folderDate = FileNameDateParser.TryReadDateFromParentFolders(absolutePath);

        string hash;
        using (var stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.SequentialScan))
        {
            hash = Convert.ToHexString(SHA256.HashData(stream));
        }

        ct.ThrowIfCancellationRequested();
        bool inserted;
        lock (scanWriter)
        {
            inserted = scanWriter.InsertFileBasic(
                hash,
                absolutePath,
                size,
                nameDate,
                folderDate,
                fallbackDate,
                creationDate,
                absolutePath,
                scanSession);
        }

        return inserted ? new MetadataWorkItem(hash, absolutePath, null) : null;
    }
}
