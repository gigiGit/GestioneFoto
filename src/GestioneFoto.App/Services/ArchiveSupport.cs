using System.Formats.Tar;
using System.IO.Compression;

namespace GestioneFoto.App.Services;

/// <summary>
/// Supporto per file contenuti in archivi (zip, tar, tar.gz, tgz).
/// Un file interno è identificato dal percorso virtuale "percorso\archivio.zip|cartella/file.jpg".
/// </summary>
public static class ArchiveSupport
{
    public const char Separator = '|';

    private static readonly string[] ArchiveSuffixes = { ".zip", ".tar", ".tar.gz", ".tgz" };

    public static bool IsArchive(string path)
    {
        return ArchiveSuffixes.Any(s => path.EndsWith(s, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsVirtualPath(string path) => path.Contains(Separator);

    public static string BuildVirtualPath(string archivePath, string entryName)
        => archivePath + Separator + entryName.Replace('\\', '/');

    public static (string Archive, string Entry) Split(string virtualPath)
    {
        var idx = virtualPath.IndexOf(Separator);
        return (virtualPath[..idx], virtualPath[(idx + 1)..]);
    }

    public static bool Exists(string path)
    {
        return IsVirtualPath(path) ? File.Exists(Split(path).Archive) : File.Exists(path);
    }

    public static DateTime GetLastWriteTime(string path)
    {
        return IsVirtualPath(path) ? File.GetLastWriteTime(Split(path).Archive) : File.GetLastWriteTime(path);
    }

    public sealed class Entry
    {
        public required string Name { get; init; }
        public required long Length { get; init; }
        public required DateTime LastWriteTime { get; init; }
        public required Func<Stream> Open { get; init; }
    }

    /// <summary>Enumera le voci-file dell'archivio; lo stream di Open è valido solo durante l'iterazione.</summary>
    public static IEnumerable<Entry> Enumerate(string archivePath)
    {
        if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var entry in EnumerateZip(archivePath))
            {
                yield return entry;
            }

            yield break;
        }

        // Supporta .tar, .tar.gz, .tgz
        foreach (var entry in EnumerateTar(archivePath))
        {
            yield return entry;
        }
    }

    private static IEnumerable<Entry> EnumerateZip(string archivePath)
    {
        using var zip = ZipFile.OpenRead(archivePath);
        foreach (var e in zip.Entries)
        {
            if (string.IsNullOrEmpty(e.Name))
            {
                continue;
            }

            var entry = e;
            yield return new Entry
            {
                Name = entry.FullName,
                Length = entry.Length,
                LastWriteTime = entry.LastWriteTime.LocalDateTime,
                Open = entry.Open
            };
        }
    }

    private static IEnumerable<Entry> EnumerateTar(string archivePath)
    {
        using var fileStream = File.OpenRead(archivePath);
        Stream readStream = fileStream;

        // Determina se è compresso con gzip
        var isGzipped = archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
                        archivePath.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase);

        if (isGzipped)
        {
            readStream = new GZipStream(fileStream, CompressionMode.Decompress);
        }

        try
        {
            using var tar = new TarReader(readStream);
            TarEntry? te;

            while ((te = tar.GetNextEntry()) is not null)
            {
                if (te.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile))
                {
                    continue;
                }

                if (te.DataStream is null || te.Length == 0)
                {
                    continue;
                }

                var current = te;
                yield return new Entry
                {
                    Name = current.Name,
                    Length = current.Length,
                    LastWriteTime = current.ModificationTime.LocalDateTime,
                    Open = () => current.DataStream!
                };
            }
        }
        finally
        {
            if (isGzipped)
            {
                readStream.Dispose();
            }
        }
    }


    public static void ExtractTo(string virtualPath, string destinationPath)
    {
        var (archive, entryName) = Split(virtualPath);
        foreach (var e in Enumerate(archive))
        {
            if (!string.Equals(e.Name.Replace('\\', '/'), entryName, StringComparison.Ordinal))
            {
                continue;
            }

            using var input = e.Open();
            using var output = File.Create(destinationPath);
            input.CopyTo(output);
            return;
        }

        throw new FileNotFoundException("Voce non trovata nell'archivio.", virtualPath);
    }
}
