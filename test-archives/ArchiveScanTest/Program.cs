using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== Archive Scan Verification ===\n");

        var dbPath = "G:\\GestioneFoto\\test-archives\\test-scan.db";
        var archiveDir = "G:\\GestioneFoto\\test-archives\\archives";

        if (!File.Exists(dbPath))
        {
            Console.WriteLine($"ERROR: Database not found at {dbPath}");
            Console.WriteLine("\nTo run the test:");
            Console.WriteLine("  1. Open the app: bin\\GestioneFoto.App.exe --profile=test-archives\\test.profile");
            Console.WriteLine("  2. Click 'Scansione' button");
            Console.WriteLine("  3. Wait for scan to complete");
            Console.WriteLine("  4. Close the app");
            Console.WriteLine("  5. Run this verification again");
            return;
        }

        Console.WriteLine($"Database found: {dbPath}\n");

        try
        {
            using var conn = new SQLiteConnection($"Data Source={dbPath}");
            conn.Open();

            // Check table structure
            Console.WriteLine("=== Schema ===");
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA table_info(SRC)";
                using var reader = cmd.ExecuteReader();
                var columns = new List<string>();
                while (reader.Read())
                    columns.Add((string)reader["name"]);
                Console.WriteLine($"SRC columns: {string.Join(", ", columns)}");
                Console.WriteLine($"Has THUMBNAIL: {(columns.Contains("THUMBNAIL") ? "YES" : "NO")}");
            }

            // Count records
            Console.WriteLine("\n=== Records ===");
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM SRC";
                var count = (long)cmd.ExecuteScalar();
                Console.WriteLine($"Total records: {count}");

                if (count > 0)
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM SRC WHERE THUMBNAIL IS NOT NULL";
                    var thumbCount = (long)cmd.ExecuteScalar();
                    Console.WriteLine($"Records with thumbnail: {thumbCount}");

                    cmd.CommandText = "SELECT COUNT(*) FROM SRC WHERE FILE LIKE '%|%'";
                    var archiveRecords = (long)cmd.ExecuteScalar();
                    Console.WriteLine($"Records from archives (with |): {archiveRecords}");

                    // List files
                    Console.WriteLine("\n=== Files ===");
                    cmd.CommandText = @"
                        SELECT 
                            SUBSTR(FILE, 1, 50) as File,
                            SUBSTR(HASH, 1, 8) as Hash,
                            CASE WHEN THUMBNAIL IS NOT NULL THEN 'Yes' ELSE 'No' END as Thumb
                        FROM SRC
                        ORDER BY FILE";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var file = (string)reader["File"];
                        var hash = (string)reader["Hash"];
                        var thumb = (string)reader["Thumb"];
                        Console.WriteLine($"  {file,-50} {hash}... [thumb:{thumb}]");
                    }
                }
            }

            Console.WriteLine("\n=== Archive Contents Verification ===");
            Console.WriteLine($"Archives directory: {archiveDir}");
            foreach (var archiveFile in Directory.GetFiles(archiveDir))
            {
                var filename = Path.GetFileName(archiveFile);
                Console.WriteLine($"  • {filename}");
            }

            Console.WriteLine("\n✓ Test completed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
        }
    }
}
