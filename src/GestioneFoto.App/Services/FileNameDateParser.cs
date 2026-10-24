using System.Globalization;
using System.Text.RegularExpressions;

namespace GestioneFoto.App.Services;

public static class FileNameDateParser
{
    private static readonly string[] DefaultPatternTexts =
    [
        // Esempi: 20200527_183045, 20200527-183045
        @"(?<!\d)(?<y>\d{4})(?<m>\d{2})(?<d>\d{2})[ _\-]?(?<hh>\d{2})(?<mm>\d{2})(?<ss>\d{2})(?!\d)",
        // Esempi: 2020-05-27 18-30-45, 2020_05_27T18:30, 2020.05.27-18.30.45
        @"(?<!\d)(?<y>\d{4})[-_\.](?<m>\d{2})[-_\.](?<d>\d{2})[ T_\-](?<hh>\d{2})[:\-\._](?<mm>\d{2})(?:[:\-\._](?<ss>\d{2}))?(?!\d)",
        // Esempi: VID-20200527-WA0000, IMG_20240531
        @"(?<!\d)(?<y>\d{4})(?<m>\d{2})(?<d>\d{2})(?!\d)",
        // Esempi: 2020-05-27, 2020_05_27
        @"(?<!\d)(?<y>\d{4})[-_\.](?<m>\d{2})[-_\.](?<d>\d{2})(?!\d)"
    ];

    private const string PatternFileName = "filename-date-patterns.txt";
    private static readonly Lazy<IReadOnlyList<Regex>> DatePatterns = new(LoadPatterns);

    public static DateTime? TryReadDateFromPath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        var fileName = Path.GetFileNameWithoutExtension(filePath);
        return TryReadDateFromFileName(fileName);
    }

    public static DateTime? TryReadDateFromFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        foreach (var pattern in DatePatterns.Value)
        {
            var match = pattern.Match(fileName);
            if (!match.Success)
            {
                continue;
            }

            if (!TryGetInt(match, "y", out var year) ||
                !TryGetInt(match, "m", out var month) ||
                !TryGetInt(match, "d", out var day))
            {
                continue;
            }

            var hour = 0;
            var minute = 0;
            var second = 0;
            _ = TryGetInt(match, "hh", out hour);
            _ = TryGetInt(match, "mm", out minute);
            _ = TryGetInt(match, "ss", out second);

            try
            {
                return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified);
            }
            catch (ArgumentOutOfRangeException)
            {
                continue;
            }
        }

        return null;
    }

    private static bool TryGetInt(Match match, string groupName, out int value)
    {
        value = 0;
        var group = match.Groups[groupName];
        if (!group.Success || string.IsNullOrWhiteSpace(group.Value))
        {
            return false;
        }

        return int.TryParse(group.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static IReadOnlyList<Regex> LoadPatterns()
    {
        var texts = new List<string>();

        // 1) Pattern personalizzati da file config
        var configPath = ResolvePatternConfigPath();
        if (!string.IsNullOrWhiteSpace(configPath) && File.Exists(configPath))
        {
            foreach (var rawLine in File.ReadLines(configPath))
            {
                var line = rawLine?.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                {
                    continue;
                }

                texts.Add(line);
            }
        }

        // 2) Fallback ai pattern di default se il file e' assente o vuoto
        if (texts.Count == 0)
        {
            texts.AddRange(DefaultPatternTexts);
        }

        var compiled = new List<Regex>();
        foreach (var text in texts)
        {
            try
            {
                compiled.Add(new Regex(text, RegexOptions.Compiled | RegexOptions.CultureInvariant));
            }
            catch
            {
                // Ignora righe non valide per non bloccare l'app.
            }
        }

        if (compiled.Count == 0)
        {
            foreach (var text in DefaultPatternTexts)
            {
                compiled.Add(new Regex(text, RegexOptions.Compiled | RegexOptions.CultureInvariant));
            }
        }

        return compiled;
    }

    private static string? ResolvePatternConfigPath()
    {
        // Priorita' 1: cartella corrente (sviluppo da repo)
        var currentCandidate = Path.Combine(Environment.CurrentDirectory, "config", PatternFileName);
        if (File.Exists(currentCandidate))
        {
            return currentCandidate;
        }

        // Priorita' 2: risalita dalla cartella bin fino alla root con "config"
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "config", PatternFileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }
}