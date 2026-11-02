using System.Diagnostics;
using System.Text.Json;
using GestioneFoto.App.Models;

namespace GestioneFoto.App.Services;

public sealed class ScannerProcessCoordinator
{
    private readonly object _processLock = new();
    private Process? _process;
    private bool _paused;

    public void Pause()
    {
        SetPaused(true);
    }

    public void Resume()
    {
        SetPaused(false);
    }

    private void SetPaused(bool paused)
    {
        lock (_processLock)
        {
            _paused = paused;
            SendCommandLocked(paused ? "pause" : "resume");
        }
    }

    public async Task RunAsync(ProfileConfig profile, IProgress<ProgressInfo> progress, CancellationToken cancellationToken)
    {
        var scannerPath = ResolveScannerPath();
        if (!File.Exists(scannerPath))
        {
            throw new FileNotFoundException(
                $"Scanner autonomo non trovato: {scannerPath}. Compilare o pubblicare anche GestioneFoto.Scanner.",
                scannerPath);
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = scannerPath,
                Arguments = "--coordinated",
                WorkingDirectory = Environment.CurrentDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Impossibile avviare lo scanner: {scannerPath}");
        }

        try
        {
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(profile));
            await process.StandardInput.FlushAsync();

            lock (_processLock)
            {
                _process = process;
                if (_paused)
                {
                    SendCommandLocked("pause");
                }
            }

            var outputTask = ReadProgressAsync(process.StandardOutput, progress);
            var errorTask = process.StandardError.ReadToEndAsync();
            using var registration = cancellationToken.Register(() => SendCommand("cancel"));

            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                await StopProcessAsync(process);
                throw;
            }

            await outputTask;
            var errorOutput = await errorTask;
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (process.ExitCode != 0)
            {
                var details = string.IsNullOrWhiteSpace(errorOutput)
                    ? $"Lo scanner è terminato con codice {process.ExitCode}."
                    : errorOutput.Trim();
                throw new InvalidOperationException(details);
            }
        }
        finally
        {
            lock (_processLock)
            {
                if (ReferenceEquals(_process, process))
                {
                    _process = null;
                }
            }

            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException) when (process.HasExited)
                {
                }

                await process.WaitForExitAsync();
            }
        }
    }

    private void SendCommand(string command)
    {
        lock (_processLock)
        {
            SendCommandLocked(command);
        }
    }

    private void SendCommandLocked(string command)
    {
        if (_process is not { HasExited: false } process)
        {
            return;
        }

        try
        {
            process.StandardInput.WriteLine(command);
            process.StandardInput.Flush();
        }
        catch (IOException) when (process.HasExited)
        {
        }
    }

    private static async Task ReadProgressAsync(StreamReader output, IProgress<ProgressInfo> progress)
    {
        while (await output.ReadLineAsync() is { } line)
        {
            var info = JsonSerializer.Deserialize<ProgressInfo>(line, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (info is null)
            {
                throw new InvalidDataException("Lo scanner ha restituito un messaggio di avanzamento vuoto.");
            }

            progress.Report(info);
        }
    }

    private static async Task StopProcessAsync(Process process)
    {
        var exitTask = process.WaitForExitAsync();
        var completed = await Task.WhenAny(exitTask, Task.Delay(TimeSpan.FromSeconds(5)));
        if (completed != exitTask && !process.HasExited)
        {
            process.Kill(entireProcessTree: true);
        }

        await exitTask;
    }

    private static string ResolveScannerPath()
    {
        var appOutputDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        var besideApplication = Path.Combine(appOutputDirectory.FullName, "GestioneFoto.Scanner.exe");
        if (File.Exists(besideApplication))
        {
            return besideApplication;
        }

        var configuration = appOutputDirectory.Parent?.Name;
        var targetFramework = appOutputDirectory.Name;
        var sourceDirectory = appOutputDirectory.Parent?.Parent?.Parent?.Parent;
        if (!string.IsNullOrWhiteSpace(configuration) &&
            sourceDirectory is not null)
        {
            return Path.Combine(
                sourceDirectory.FullName,
                "GestioneFoto.Scanner",
                "bin",
                configuration,
                targetFramework,
                "GestioneFoto.Scanner.exe");
        }

        return besideApplication;
    }
}
