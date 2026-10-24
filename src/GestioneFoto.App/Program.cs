using GestioneFoto.App.UI;

namespace GestioneFoto.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var startupProfile = ResolveStartupProfile(args);
        var openViewerOnStart = ResolveOpenViewerFlag(args);
        Application.Run(new MainForm(startupProfile, openViewerOnStart));
    }

    private static bool ResolveOpenViewerFlag(string[] args)
    {
        foreach (var raw in args)
        {
            var arg = raw?.Trim();
            if (string.IsNullOrWhiteSpace(arg))
            {
                continue;
            }

            if (string.Equals(arg, "--view-db", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "/view-db", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--visualizza-db", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "/visualizza-db", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ResolveStartupProfile(string[] args)
    {
        if (args.Length == 0)
        {
            return null;
        }

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i]?.Trim();
            if (string.IsNullOrWhiteSpace(arg))
            {
                continue;
            }

            if (arg.StartsWith("--profile=", StringComparison.OrdinalIgnoreCase))
            {
                return arg[10..].Trim();
            }

            if (arg.StartsWith("/profile:", StringComparison.OrdinalIgnoreCase))
            {
                return arg[9..].Trim();
            }

            if (string.Equals(arg, "--profile", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "-p", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                {
                    return args[i + 1].Trim();
                }
            }
        }

        return args.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim();
    }
}
