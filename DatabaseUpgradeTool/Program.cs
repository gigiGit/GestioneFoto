namespace DatabaseUpgradeTool;

using System;
using System.Data.SQLite;
using System.IO;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("=== GestioneFoto Database Upgrade Tool ===\n");
            Console.WriteLine("Usage: DatabaseUpgradeTool.exe <database-path>\n");
            Console.WriteLine("This tool upgrades an existing database to the latest SRC and HASH schema");
            Console.WriteLine("by adding missing scan metadata, thumbnail, and copy-tracking columns.\n");
            Console.WriteLine("Example:");
            Console.WriteLine("  DatabaseUpgradeTool.exe G:\\GestioneFoto\\test-archives\\test-scan.db\n");
            return;
        }

        var dbPath = args[0];

        if (!File.Exists(dbPath))
        {
            Console.WriteLine($"ERROR: Database not found: {dbPath}");
            return;
        }

        Console.WriteLine($"=== Database Upgrade ===\n");
        Console.WriteLine($"Database: {dbPath}");
        Console.WriteLine($"Size: {(new FileInfo(dbPath)).Length} bytes\n");

        try
        {
            UpgradeDatabase(dbPath);
            Console.WriteLine("\n✓ Upgrade completed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n✗ ERROR: {ex.Message}");
        }
    }

    static void UpgradeDatabase(string dbPath)
    {
        using var conn = new SQLiteConnection($"Data Source={dbPath}");
        conn.Open();

        Console.WriteLine("Checking schema...\n");

        // Check THUMBNAIL column
        Console.WriteLine("Checking SRC table:");
        if (!ColumnExists(conn, "SRC", "thumbnail"))
        {
            Console.WriteLine("  [MISSING] THUMBNAIL column");
            Console.WriteLine("  Adding THUMBNAIL column...");
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "ALTER TABLE SRC ADD COLUMN thumbnail BLOB;";
            cmd.ExecuteNonQuery();
            Console.WriteLine("  ✓ THUMBNAIL column added");
        }
        else
        {
            Console.WriteLine("  ✓ THUMBNAIL column exists");
        }

        foreach (var column in new[] { "dtExifOriginal", "dtExifDigitized", "dtExifDateTime", "dtAcqOverride", "dtFolderName", "dtCreation", "absolutePath" })
        {
            if (!ColumnExists(conn, "SRC", column))
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"ALTER TABLE SRC ADD COLUMN {column} TEXT;";
                cmd.ExecuteNonQuery();
                Console.WriteLine($"  ✓ {column} column added");
            }
            else
            {
                Console.WriteLine($"  ✓ {column} column exists");
            }
        }

        using (var view = conn.CreateCommand())
        {
            view.CommandText = """
                DROP VIEW IF EXISTS V_HASH_ACQUISITION_DATE;

                CREATE VIEW V_HASH_ACQUISITION_DATE AS
                WITH candidates AS (
                    SELECT
                        hash,
                        coalesce(
                            nullif(trim(dtAcqOverride), ''),
                            nullif(trim(dtExifOriginal), ''),
                            nullif(trim(dtExifDigitized), ''),
                            nullif(trim(dtExifDateTime), ''),
                            nullif(trim(dtExif), ''),
                            nullif(trim(dtFileName), ''),
                            nullif(trim(dtFolderName), ''),
                            nullif(trim(dtCreation), ''),
                            nullif(trim(dtFile), '')
                        ) AS data_acquisizione,
                        CASE
                            WHEN trim(coalesce(dtAcqOverride, '')) <> '' THEN 'Modifica manuale'
                            WHEN trim(coalesce(dtExifOriginal, '')) <> '' THEN 'EXIF Original'
                            WHEN trim(coalesce(dtExifDigitized, '')) <> '' THEN 'EXIF Digitized'
                            WHEN trim(coalesce(dtExifDateTime, '')) <> '' THEN 'EXIF DateTime'
                            WHEN trim(coalesce(dtExif, '')) <> '' THEN 'EXIF'
                            WHEN trim(coalesce(dtFileName, '')) <> '' THEN 'Nome file'
                            WHEN trim(coalesce(dtFolderName, '')) <> '' THEN 'Cartella'
                            WHEN trim(coalesce(dtCreation, '')) <> '' THEN 'Creazione file'
                            WHEN trim(coalesce(dtFile, '')) <> '' THEN 'Data file'
                        END AS data_source,
                        CASE
                            WHEN trim(coalesce(dtAcqOverride, '')) <> '' THEN 0
                            WHEN trim(coalesce(dtExifOriginal, '')) <> '' THEN 1
                            WHEN trim(coalesce(dtExifDigitized, '')) <> '' THEN 2
                            WHEN trim(coalesce(dtExifDateTime, '')) <> '' THEN 3
                            WHEN trim(coalesce(dtExif, '')) <> '' THEN 4
                            WHEN trim(coalesce(dtFileName, '')) <> '' THEN 5
                            WHEN trim(coalesce(dtFolderName, '')) <> '' THEN 6
                            WHEN trim(coalesce(dtCreation, '')) <> '' THEN 7
                            WHEN trim(coalesce(dtFile, '')) <> '' THEN 8
                            ELSE 9
                        END AS date_priority,
                        file AS source_file
                    FROM SRC
                ),
                ranked AS (
                    SELECT
                        *,
                        row_number() OVER (
                            PARTITION BY hash
                            ORDER BY date_priority, data_acquisizione, source_file
                        ) AS rn
                    FROM candidates
                )
                SELECT hash, data_acquisizione, data_source, source_file
                FROM ranked
                WHERE rn = 1;
                """;
            view.ExecuteNonQuery();
        }
        Console.WriteLine("  ✓ V_HASH_ACQUISITION_DATE view created");

        // Check scanSession column
        if (!ColumnExists(conn, "SRC", "scanSession"))
        {
            Console.WriteLine("  [MISSING] scanSession column");
            Console.WriteLine("  Adding scanSession column...");
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "ALTER TABLE SRC ADD COLUMN scanSession INTEGER NOT NULL DEFAULT 0;";
            cmd.ExecuteNonQuery();
            Console.WriteLine("  ✓ scanSession column added");
        }
        else
        {
            Console.WriteLine("  ✓ scanSession column exists");
        }

        Console.WriteLine("\nChecking HASH table:");
        if (!ColumnExists(conn, "HASH", "copied"))
        {
            Console.WriteLine("  [MISSING] copied column");
            Console.WriteLine("  Adding copied column...");
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "ALTER TABLE HASH ADD COLUMN copied INTEGER NOT NULL DEFAULT 0;";
            cmd.ExecuteNonQuery();
            Console.WriteLine("  ✓ copied column added");
        }
        else
        {
            Console.WriteLine("  ✓ copied column exists");
        }

        // Verify tables exist
        Console.WriteLine("\nVerifying tables:");
        var tables = new[] { "SRC", "DST", "HASH", "APP_STATE" };
        foreach (var table in tables)
        {
            if (TableExists(conn, table))
            {
                Console.WriteLine($"  ✓ {table} table exists");
            }
            else
            {
                Console.WriteLine($"  [MISSING] {table} table - skipping");
            }
        }

        // Show statistics
        Console.WriteLine("\nDatabase statistics:");
        ShowTableStats(conn, "SRC");
        ShowTableStats(conn, "DST");
        ShowTableStats(conn, "HASH");

        conn.Close();
    }

    static bool ColumnExists(SQLiteConnection conn, string table, string column)
    {
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info({table});";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var colName = reader.GetString(1);
                if (string.Equals(colName, column, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    static bool TableExists(SQLiteConnection conn, string table)
    {
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM {table} LIMIT 1;";
            cmd.ExecuteScalar();
            return true;
        }
        catch
        {
            return false;
        }
    }

    static void ShowTableStats(SQLiteConnection conn, string table)
    {
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM {table};";
            var count = (long)cmd.ExecuteScalar();
            Console.WriteLine($"  {table}: {count} rows");
        }
        catch
        {
            Console.WriteLine($"  {table}: [error reading]");
        }
    }
}
