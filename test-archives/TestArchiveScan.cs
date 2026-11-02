// Test program to verify archive scanning and thumbnail generation
// Compile: csc.exe /reference:bin\GestioneFoto.App.dll TestArchiveScan.cs
// Run: dotnet run --project TestArchiveScan.csproj

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Data.SQLite;
using GestioneFoto.App.Services;
using GestioneFoto.App.Data;

class TestArchiveScan
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== Archive Scan Test ===\n");

        var profilePath = "G:\\GestioneFoto\\test-archives\\test.profile";
        var archivesDir = "G:\\GestioneFoto\\test-archives\\archives";
        var dbPath = "G:\\GestioneFoto\\test-archives\\test-scan.db";

        // Load profile
        var profile = LoadProfile(profilePath);
        Console.WriteLine($"Profile: {profile["ProfileName"]}");
        Console.WriteLine($"Archives dir: {archivesDir}");
        Console.WriteLine($"Database: {dbPath}\n");

        // Delete old database
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
            Console.WriteLine("Deleted old database.");
        }

        // Prepare scan coordinator
        var writer = new HashDbWriter(dbPath, "test-app");
        var scanner = new ScanCoordinator(writer);

        // Scan archives
        Console.WriteLine("Starting scan...");
        var stats = scanner.Scan(
            directories: new[] { archivesDir },
            extensions: profile["Extensions"].Split(',').Select(e => e.Trim()).ToArray(),
            minSize: long.Parse(profile["MinSize"])
        );

        Console.WriteLine($"\nScan completed:");
        Console.WriteLine($"  Files found: {stats.FilesFound}");
        Console.WriteLine($"  Files analyzed: {stats.FilesAnalyzed}");
        Console.WriteLine($"  Errors: {stats.ErrorCount}");

        // Verify database
        Console.WriteLine("\nVerifying database contents...");
        VerifyDatabase(dbPath);
    }

    static Dictionary<string, string> LoadProfile(string path)
    {
        var profile = new Dictionary<string, string>();
        foreach (var line in File.ReadAllLines(path))
        {
            var parts = line.Split('=');
            if (parts.Length == 2)
                profile[parts[0].Trim()] = parts[1].Trim();
        }
        return profile;
    }

    static void VerifyDatabase(string dbPath)
    {
        if (!File.Exists(dbPath))
        {
            Console.WriteLine("ERROR: Database not created!");
            return;
        }

        using var conn = new SQLiteConnection($"Data Source={dbPath}");
        conn.Open();

        // Check SRC table structure
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA table_info(SRC)";
        using var reader = cmd.ExecuteReader();
        var columns = new List<string>();
        while (reader.Read())
            columns.Add((string)reader["name"]);

        Console.WriteLine($"SRC table columns: {string.Join(", ", columns)}");
        Console.WriteLine($"Has THUMBNAIL column: {columns.Contains("THUMBNAIL")}");

        // Count records and thumbnails
        cmd.CommandText = "SELECT COUNT(*) FROM SRC";
        var recordCount = (long)cmd.ExecuteScalar();
        Console.WriteLine($"Total records: {recordCount}");

        cmd.CommandText = "SELECT COUNT(*) FROM SRC WHERE THUMBNAIL IS NOT NULL";
        var thumbnailCount = (long)cmd.ExecuteScalar();
        Console.WriteLine($"Records with thumbnail: {thumbnailCount}");

        // List files
        if (recordCount > 0)
        {
            Console.WriteLine("\nFiles in database:");
            cmd.CommandText = "SELECT FILE, HASH, THUMBNAIL IS NOT NULL as HasThumbnail FROM SRC LIMIT 10";
            using var reader2 = cmd.ExecuteReader();
            while (reader2.Read())
            {
                var file = (string)reader2["FILE"];
                var hash = (string)reader2["HASH"];
                var hasThumbnail = (long)reader2["HasThumbnail"] == 1;
                Console.WriteLine($"  {file} (hash: {hash.Substring(0, 8)}..., thumbnail: {hasThumbnail})");
            }
        }
    }
}
