using GestioneFoto.App.Models;

namespace GestioneFoto.App.Config;

public static class ProfileLoader
{
    public static ProfileConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Profilo non trovato.", path);
        }

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
            {
                continue;
            }

            var idx = line.IndexOf('=');
            if (idx <= 0)
            {
                continue;
            }

            var key = line[..idx].Trim();
            var value = line[(idx + 1)..].Trim();
            map[key] = value;
        }

        return new ProfileConfig
        {
            ProfileName = GetRequired(map, "ProfileName", Path.GetFileNameWithoutExtension(path)),
            UserName = map.TryGetValue("UserName", out var userName) ? userName : null,
            IncludePath = GetRequired(map, "IncludePath"),
            DatabasePath = GetRequired(map, "DatabasePath"),
            DestinationRoot = GetRequired(map, "DestinationRoot"),
            DestinationTemplate = GetRequired(map, "DestinationTemplate"),
            AlbumDestination = map.TryGetValue("AlbumDestination", out var albumDestination) ? albumDestination : null,
            Extensions = SplitList(GetRequired(map, "Extensions")),
            ExcludePaths = SplitPaths(map.TryGetValue("ExcludePaths", out var ex) ? ex : string.Empty),
            MinSize = long.TryParse(map.TryGetValue("MinSize", out var ms) ? ms : "0", out var minSize) ? minSize : 0,
            ArchiveTempPath = map.TryGetValue("ArchiveTempPath", out var archiveTempPath) ? archiveTempPath : null
        };
    }

    private static string GetRequired(Dictionary<string, string> map, string key, string? fallback = null)
    {
        if (map.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (!string.IsNullOrWhiteSpace(fallback))
        {
            return fallback;
        }

        throw new InvalidOperationException($"Chiave mancante nel profilo: {key}");
    }

    private static string[] SplitList(string raw)
    {
        return raw
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.TrimStart('.').ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] SplitPaths(string raw)
    {
        return raw
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
