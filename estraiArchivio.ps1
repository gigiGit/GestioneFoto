<#
.SYNOPSIS
Estrae uno o piu archivi TGZ in una cartella.

.DESCRIPTION
Accetta percorsi e caratteri jolly. I file omonimi non vengono sovrascritti:
sono salvati nelle sottocartelle duplicati-1, duplicati-2 e successive,
accanto alla cartella in cui sarebbero stati estratti.
Per ogni jolly gli archivi sono elaborati in ordine alfabetico; i percorsi
espliciti mantengono l'ordine indicato.

Richiede .NET SDK 8 e tar.exe. Gli archivi vengono verificati prima
dell'estrazione. Il lettore TAR viene compilato in una cartella temporanea e
rimosso al termine. I collegamenti rigidi vengono estratti come copie;
i collegamenti simbolici interni richiedono i permessi di Windows. I file
speciali e i collegamenti simbolici senza destinazione non sono supportati.

.PARAMETER Archivi
Percorsi degli archivi o espressioni con caratteri jolly.

.PARAMETER Destinazione
Cartella di destinazione, creata se non esiste. Si puo usare l'alias -d.

.EXAMPLE
.\estraiArchivio.ps1 *.tgz -d C:\tmp

.EXAMPLE
.\estraiArchivio.ps1 "C:\backup\*.tgz" "D:\altri\foto.tgz" -d "C:\foto estratte"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0, ValueFromRemainingArguments = $true)]
    [ValidateNotNullOrEmpty()]
    [string[]]$Archivi,

    [Parameter(Mandatory = $true)]
    [Alias('d')]
    [ValidateNotNullOrEmpty()]
    [string]$Destinazione
)

$ErrorActionPreference = 'Stop'

$dotnet = Get-Command -Name dotnet.exe -CommandType Application -ErrorAction SilentlyContinue |
    Select-Object -First 1
if (-not $dotnet) {
    throw 'Per eseguire lo script e necessario .NET SDK 8.'
}
$tar = Get-Command -Name tar.exe -CommandType Application -ErrorAction SilentlyContinue |
    Select-Object -First 1
if (-not $tar) {
    throw 'Per verificare gli archivi e necessario tar.exe.'
}
$versioneTar = @(& $tar.Source --version)
if ($LASTEXITCODE -ne 0) {
    throw "Impossibile eseguire '$($tar.Source)'."
}
$tarGnu = $versioneTar.Count -gt 0 -and $versioneTar[0] -like 'tar (GNU tar)*'

$percorsi = [System.Collections.Generic.List[string]]::new()
$giaAggiunti = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase
)

foreach ($archivio in $Archivi) {
    if (Test-Path -LiteralPath $archivio) {
        $risolti = @(Resolve-Path -LiteralPath $archivio)
    } else {
        $risolti = @(Resolve-Path -Path $archivio | Sort-Object -Property ProviderPath)
    }

    if ($risolti.Count -eq 0) {
        throw "Nessun archivio trovato per '$archivio'."
    }

    foreach ($percorso in $risolti) {
        if ($percorso.Provider.Name -ne 'FileSystem') {
            throw "Il percorso '$percorso' non appartiene al file system."
        }

        $file = Get-Item -LiteralPath $percorso.ProviderPath -Force
        if ($file -isnot [System.IO.FileInfo]) {
            throw "Il percorso '$percorso' non e un file."
        }

        if ($giaAggiunti.Add($file.FullName)) {
            $percorsi.Add($file.FullName)
        }
    }
}

if (Test-Path -LiteralPath $Destinazione) {
    $cartella = Get-Item -LiteralPath $Destinazione -Force
} else {
    $cartella = New-Item -Path $Destinazione -ItemType Directory -Force
}

if ($cartella -isnot [System.IO.DirectoryInfo]) {
    throw "La destinazione '$Destinazione' non e una cartella del file system."
}
if (($cartella.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
    throw "La destinazione '$Destinazione' non puo essere un collegamento."
}

$progetto = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
'@

$sorgente = @'
using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Text.Json;

internal static class Program
{
    private sealed record Collegamento(
        string Nome, string Destinazione, string Target, bool Rigido, bool TargetFisico,
        Dictionary<string, string> Estratti);

    private static int Main(string[] args)
    {
        try
        {
            using JsonDocument richiesta = JsonDocument.Parse(File.ReadAllText(args[0]));
            string destinazione = Path.GetFullPath(
                richiesta.RootElement.GetProperty("Destinazione").GetString()
                ?? throw new InvalidDataException("Destinazione mancante."));
            string tarExe = richiesta.RootElement.GetProperty("Tar").GetString()
                ?? throw new InvalidDataException("Percorso di tar.exe mancante.");
            bool tarGnu = richiesta.RootElement.GetProperty("TarGnu").GetBoolean();

            FileAttributes attributi = File.GetAttributes(destinazione);
            if ((attributi & FileAttributes.Directory) == 0 ||
                (attributi & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"Destinazione non valida: '{destinazione}'.");
            }

            var riservati = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var collegamenti = new List<Collegamento>();
            foreach (JsonElement elemento in richiesta.RootElement.GetProperty("Archivi").EnumerateArray())
            {
                string archivio = elemento.GetString()
                    ?? throw new InvalidDataException("Percorso di archivio mancante.");
                Console.WriteLine($"Estrazione: {archivio}");
                VerificaArchivio(tarExe, tarGnu, archivio);
                EstraiArchivio(archivio, destinazione, riservati, collegamenti);
            }

            CreaCollegamenti(destinazione, collegamenti);
            Console.WriteLine("Estrazione completata.");
            return 0;
        }
        catch (Exception errore) when (errore is IOException ||
                                      errore is UnauthorizedAccessException ||
                                      errore is InvalidDataException ||
                                      errore is ArgumentException ||
                                      errore is NotSupportedException ||
                                      errore is System.ComponentModel.Win32Exception ||
                                      errore is JsonException)
        {
            Console.Error.WriteLine($"Errore: {errore.Message}");
            return 1;
        }
    }

    private static void VerificaArchivio(string tarExe, bool tarGnu, string archivio)
    {
        var avvio = new ProcessStartInfo(tarExe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        if (tarGnu)
        {
            avvio.ArgumentList.Add("--force-local");
            string cartellaTar = Path.GetDirectoryName(tarExe)!;
            avvio.Environment["PATH"] = cartellaTar + Path.PathSeparator + avvio.Environment["PATH"];
        }
        avvio.ArgumentList.Add("-tzf");
        avvio.ArgumentList.Add(archivio);

        using Process processo = Process.Start(avvio)
            ?? throw new IOException("Impossibile avviare tar.exe.");
        Task<string> errori = processo.StandardError.ReadToEndAsync();
        processo.StandardOutput.BaseStream.CopyTo(Stream.Null);
        processo.WaitForExit();
        string messaggio = errori.GetAwaiter().GetResult().Trim();

        if (processo.ExitCode != 0)
        {
            throw new InvalidDataException(
                $"Verifica fallita per '{archivio}' (tar.exe: {processo.ExitCode}): {messaggio}");
        }
    }

    private static void EstraiArchivio(
        string archivio, string radice, HashSet<string> riservati, List<Collegamento> collegamenti)
    {
        var fileEstratti = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using FileStream file = File.OpenRead(archivio);
        if (file.ReadByte() != 0x1f || file.ReadByte() != 0x8b)
        {
            throw new InvalidDataException($"L'archivio '{archivio}' non e un file gzip.");
        }
        file.Position = 0;

        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using (var tar = new TarReader(gzip, leaveOpen: true))
        {
            TarEntry? voce;
            while ((voce = tar.GetNextEntry()) is not null)
            {
                if (voce.EntryType == TarEntryType.GlobalExtendedAttributes)
                {
                    continue;
                }

                string percorso = PercorsoVoce(radice, voce.Name);
                switch (voce.EntryType)
                {
                    case TarEntryType.Directory:
                    case TarEntryType.DirectoryList:
                        CreaCartella(radice, percorso, riservati);
                        fileEstratti[percorso] = percorso;
                        break;

                    case TarEntryType.RegularFile:
                    case TarEntryType.V7RegularFile:
                    case TarEntryType.ContiguousFile:
                    {
                        if (percorso.Equals(radice, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidDataException($"Nome file non valido: '{voce.Name}'.");
                        }
                        string cartella = Path.GetDirectoryName(percorso)!;
                        CreaCartella(radice, cartella, riservati);
                        string destinazione = PercorsoDisponibile(cartella, Path.GetFileName(percorso), riservati);
                        ScriviFile(destinazione, voce.DataStream, voce.Length, voce.ModificationTime.UtcDateTime);
                        fileEstratti[percorso] = destinazione;
                        SegnalaDuplicato(radice, percorso, destinazione);
                        break;
                    }

                    case TarEntryType.HardLink:
                    {
                        if (percorso.Equals(radice, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidDataException($"Nome file non valido: '{voce.Name}'.");
                        }
                        string originale = PercorsoVoce(radice, voce.LinkName);
                        bool targetFisico = fileEstratti.TryGetValue(originale, out string? sorgente);
                        string cartella = Path.GetDirectoryName(percorso)!;
                        CreaCartella(radice, cartella, riservati);
                        string destinazione = PercorsoDisponibile(cartella, Path.GetFileName(percorso), riservati);
                        riservati.Add(destinazione);
                        collegamenti.Add(new Collegamento(
                            voce.Name, destinazione, sorgente ?? originale, true, targetFisico, fileEstratti));
                        fileEstratti[percorso] = destinazione;
                        SegnalaDuplicato(radice, percorso, destinazione);
                        break;
                    }

                    case TarEntryType.SymbolicLink:
                    {
                        if (percorso.Equals(radice, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidDataException($"Nome collegamento non valido: '{voce.Name}'.");
                        }
                        string target = PercorsoTarget(radice, percorso, voce.LinkName);
                        string cartella = Path.GetDirectoryName(percorso)!;
                        CreaCartella(radice, cartella, riservati);
                        string destinazione = PercorsoDisponibile(cartella, Path.GetFileName(percorso), riservati);
                        riservati.Add(destinazione);
                        fileEstratti[percorso] = destinazione;
                        collegamenti.Add(new Collegamento(
                            voce.Name, destinazione, target, false, false, fileEstratti));
                        SegnalaDuplicato(radice, percorso, destinazione);
                        break;
                    }

                    default:
                        throw new InvalidDataException(
                            $"Tipo di voce non supportato ({voce.EntryType}): '{voce.Name}'.");
                }
            }
        }

        gzip.CopyTo(Stream.Null);
    }

    private static string PercorsoVoce(string radice, string nome)
    {
        if (string.IsNullOrEmpty(nome))
        {
            throw new InvalidDataException("L'archivio contiene un nome vuoto.");
        }

        string relativo = nome.Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(relativo))
        {
            throw new InvalidDataException($"Percorso assoluto non consentito: '{nome}'.");
        }

        VerificaParti(relativo, nome, consentiPadre: false);
        string percorso = Path.GetFullPath(Path.Combine(radice, relativo));
        VerificaRadice(radice, percorso, nome);
        return percorso;
    }

    private static string PercorsoTarget(string radice, string collegamento, string nome)
    {
        if (string.IsNullOrEmpty(nome))
        {
            throw new InvalidDataException($"Destinazione mancante per '{collegamento}'.");
        }

        string relativo = nome.Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(relativo))
        {
            throw new InvalidDataException($"Destinazione assoluta non consentita: '{nome}'.");
        }
        VerificaParti(relativo, nome, consentiPadre: true);
        string percorso = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(collegamento)!, relativo));
        VerificaRadice(radice, percorso, nome);
        return percorso;
    }

    private static void VerificaParti(string relativo, string nome, bool consentiPadre)
    {
        foreach (string parte in relativo.Split(Path.DirectorySeparatorChar))
        {
            if (parte.Length == 0 || parte == "." || (consentiPadre && parte == ".."))
            {
                continue;
            }
            if (parte == ".." || parte.EndsWith(' ') || parte.EndsWith('.') ||
                parte.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new InvalidDataException($"Percorso non consentito: '{nome}'.");
            }
        }
    }

    private static void VerificaRadice(string radice, string percorso, string nome)
    {
        string verifica = Path.GetRelativePath(radice, percorso);
        if (verifica == ".." ||
            verifica.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            Path.IsPathRooted(verifica))
        {
            throw new InvalidDataException($"Percorso fuori dalla destinazione: '{nome}'.");
        }
    }

    private static void CreaCartella(string radice, string cartella, HashSet<string> riservati)
    {
        string relativo = Path.GetRelativePath(radice, cartella);
        if (relativo == ".")
        {
            return;
        }
        if (relativo == ".." ||
            relativo.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            Path.IsPathRooted(relativo))
        {
            throw new InvalidDataException($"Cartella fuori dalla destinazione: '{cartella}'.");
        }

        string corrente = radice;
        foreach (string parte in relativo.Split(Path.DirectorySeparatorChar))
        {
            corrente = Path.Combine(corrente, parte);
            if (riservati.Contains(corrente))
            {
                throw new IOException($"La cartella '{corrente}' e occupata da un collegamento.");
            }
            if (!Esiste(corrente))
            {
                Directory.CreateDirectory(corrente);
            }

            FileAttributes attributi = File.GetAttributes(corrente);
            if ((attributi & FileAttributes.Directory) == 0 ||
                (attributi & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"La cartella '{corrente}' e occupata o e un collegamento.");
            }
        }
    }

    private static string PercorsoDisponibile(string cartella, string nome, HashSet<string> riservati)
    {
        string percorso = Path.Combine(cartella, nome);
        if (!riservati.Contains(percorso) && !Esiste(percorso))
        {
            return percorso;
        }

        for (int numero = 1; numero < int.MaxValue; numero++)
        {
            string duplicati = Path.Combine(cartella, $"duplicati-{numero}");
            if (riservati.Contains(duplicati))
            {
                continue;
            }
            if (!Esiste(duplicati))
            {
                Directory.CreateDirectory(duplicati);
            }

            FileAttributes attributi = File.GetAttributes(duplicati);
            if ((attributi & FileAttributes.Directory) == 0 ||
                (attributi & FileAttributes.ReparsePoint) != 0)
            {
                continue;
            }

            string candidato = Path.Combine(duplicati, nome);
            if (!riservati.Contains(candidato) && !Esiste(candidato))
            {
                return candidato;
            }
        }

        throw new IOException($"Nessun percorso disponibile per '{percorso}'.");
    }

    private static void CreaCollegamenti(string radice, List<Collegamento> collegamenti)
    {
        var perPercorso = collegamenti.ToDictionary(
            collegamento => collegamento.Destinazione, StringComparer.OrdinalIgnoreCase);
        var creati = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var inCorso = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Collegamento collegamento in collegamenti)
        {
            CreaCollegamento(radice, collegamento, perPercorso, creati, inCorso);
        }
    }

    private static void CreaCollegamento(
        string radice, Collegamento collegamento, Dictionary<string, Collegamento> perPercorso,
        HashSet<string> creati, HashSet<string> inCorso)
    {
        if (creati.Contains(collegamento.Destinazione))
        {
            return;
        }
        if (!inCorso.Add(collegamento.Destinazione))
        {
            throw new InvalidDataException($"Ciclo di collegamenti: '{collegamento.Nome}'.");
        }

        try
        {
            string target = !collegamento.TargetFisico &&
                collegamento.Estratti.TryGetValue(collegamento.Target, out string? estratto)
                    ? estratto : collegamento.Target;
            string relativo = Path.GetRelativePath(radice, target);
            string corrente = radice;
            if (relativo != ".")
            {
                foreach (string parte in relativo.Split(Path.DirectorySeparatorChar))
                {
                    corrente = Path.Combine(corrente, parte);
                    if (perPercorso.TryGetValue(corrente, out Collegamento? dipendenza))
                    {
                        CreaCollegamento(radice, dipendenza, perPercorso, creati, inCorso);
                    }
                    if (Esiste(corrente) &&
                        (File.GetAttributes(corrente) & FileAttributes.ReparsePoint) != 0 &&
                        !creati.Contains(corrente))
                    {
                        throw new InvalidDataException(
                            $"Destinazione di collegamento non sicura: '{collegamento.Nome}'.");
                    }
                }
            }

            if (collegamento.Rigido && File.Exists(target))
            {
                using FileStream contenuto = File.OpenRead(target);
                ScriviFile(collegamento.Destinazione, contenuto, contenuto.Length, File.GetLastWriteTimeUtc(target));
            }
            else if (!collegamento.Rigido && Directory.Exists(target))
            {
                string targetRelativo = Path.GetRelativePath(
                    Path.GetDirectoryName(collegamento.Destinazione)!, target);
                Directory.CreateSymbolicLink(collegamento.Destinazione, targetRelativo);
            }
            else if (!collegamento.Rigido && File.Exists(target))
            {
                string targetRelativo = Path.GetRelativePath(
                    Path.GetDirectoryName(collegamento.Destinazione)!, target);
                File.CreateSymbolicLink(collegamento.Destinazione, targetRelativo);
            }
            else
            {
                throw new InvalidDataException(
                    $"Destinazione del collegamento assente o non valida: '{collegamento.Nome}'.");
            }
            creati.Add(collegamento.Destinazione);
        }
        finally
        {
            inCorso.Remove(collegamento.Destinazione);
        }
    }

    private static bool Esiste(string percorso)
    {
        try
        {
            File.GetAttributes(percorso);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    private static void ScriviFile(string destinazione, Stream? contenuto, long lunghezza, DateTime modifica)
    {
        if (contenuto is null && lunghezza != 0)
        {
            throw new InvalidDataException($"Contenuto mancante per '{destinazione}'.");
        }

        string temporaneo = Path.Combine(
            Path.GetDirectoryName(destinazione)!, ".estrai-" + Guid.NewGuid().ToString("N") + ".tmp");
        bool creato = false;
        try
        {
            using (var output = new FileStream(temporaneo, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                creato = true;
                contenuto?.CopyTo(output);
                if (output.Length != lunghezza)
                {
                    throw new InvalidDataException($"Contenuto incompleto per '{destinazione}'.");
                }
            }

            File.SetLastWriteTimeUtc(temporaneo, modifica);
            File.Move(temporaneo, destinazione);
            creato = false;
        }
        finally
        {
            if (creato)
            {
                File.Delete(temporaneo);
            }
        }
    }

    private static void SegnalaDuplicato(string radice, string previsto, string effettivo)
    {
        if (!previsto.Equals(effettivo, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Duplicato: {Path.GetRelativePath(radice, effettivo)}");
        }
    }
}
'@

$temporanea = Join-Path ([System.IO.Path]::GetTempPath()) ('estraiArchivio-' + [guid]::NewGuid().ToString('N'))
New-Item -Path $temporanea -ItemType Directory -ErrorAction Stop | Out-Null

try {
    $utf8 = [System.Text.UTF8Encoding]::new($false)
    $progettoPath = Join-Path $temporanea 'Estrattore.csproj'
    $sorgentePath = Join-Path $temporanea 'Program.cs'
    $richiestaPath = Join-Path $temporanea 'richiesta.json'
    $outputPath = Join-Path $temporanea 'output'

    [System.IO.File]::WriteAllText($progettoPath, $progetto, $utf8)
    [System.IO.File]::WriteAllText($sorgentePath, $sorgente, $utf8)
    $richiesta = @{
        Destinazione = $cartella.FullName
        Archivi = @($percorsi.ToArray())
        Tar = $tar.Source
        TarGnu = $tarGnu
    } | ConvertTo-Json -Compress
    [System.IO.File]::WriteAllText($richiestaPath, $richiesta, $utf8)

    Push-Location -LiteralPath $temporanea
    try {
        & $dotnet.Source build $progettoPath --configuration Release --output $outputPath --nologo --verbosity quiet
        if ($LASTEXITCODE -ne 0) {
            throw 'Impossibile preparare il lettore TAR. Verificare che .NET SDK 8 sia installato.'
        }

        & $dotnet.Source (Join-Path $outputPath 'Estrattore.dll') $richiestaPath
        if ($LASTEXITCODE -ne 0) {
            throw "Estrazione interrotta (codice $LASTEXITCODE)."
        }
    } finally {
        Pop-Location
    }
} finally {
    Remove-Item -LiteralPath $temporanea -Recurse -Force
}
