using System.Text.Json;
using GestioneFoto.App.Config;
using GestioneFoto.App.Models;
using GestioneFoto.App.Services;

var maxParallelism = Math.Clamp(Environment.ProcessorCount, 2, 8);
string? profilePath = null;
var coordinated = false;

for (var index = 0; index < args.Length; index++)
{
    switch (args[index])
    {
        case "--profile" when index + 1 < args.Length:
            profilePath = args[++index];
            break;
        case "--max-parallelism" when index + 1 < args.Length &&
                                      int.TryParse(args[++index], out var parsedParallelism) &&
                                      parsedParallelism > 0:
            maxParallelism = parsedParallelism;
            break;
        case "--coordinated":
            coordinated = true;
            break;
        case "--help":
        case "-h":
            Console.WriteLine("GestioneFoto.Scanner --profile <file.profile> [--max-parallelism <numero>]");
            Console.WriteLine("Internal coordinator mode: --coordinated (reads profile JSON and controls from stdin)");
            return 0;
        default:
            Console.Error.WriteLine($"Argomento non valido: {args[index]}");
            return 1;
    }
}

if (coordinated == (profilePath is not null))
{
    Console.Error.WriteLine("Specificare --profile oppure --coordinated.");
    return 1;
}

ProfileConfig profile;
if (coordinated)
{
    var profileJson = await Console.In.ReadLineAsync();
    if (string.IsNullOrWhiteSpace(profileJson))
    {
        Console.Error.WriteLine("Il coordinatore non ha fornito il profilo.");
        return 1;
    }

    profile = JsonSerializer.Deserialize<ProfileConfig>(profileJson)
        ?? throw new InvalidDataException("Profilo coordinato non valido.");
}
else
{
    profile = ProfileLoader.Load(profilePath!);
}

using var cancellation = new CancellationTokenSource();
var pause = new PauseController();

if (coordinated)
{
    _ = Task.Run(async () =>
    {
        while (await Console.In.ReadLineAsync() is { } command)
        {
            switch (command)
            {
                case "pause":
                    pause.Pause();
                    break;
                case "resume":
                    pause.Resume();
                    break;
                case "cancel":
                    cancellation.Cancel();
                    return;
            }
        }

        cancellation.Cancel();
    });
}

try
{
    var coordinator = new ScanCoordinator(new ExifDateReader(), new HashDbWriter(), maxParallelism);
    IProgress<ProgressInfo> progress = new InlineProgress<ProgressInfo>(
        info => Console.WriteLine(JsonSerializer.Serialize(info)));
    await coordinator.RunAsync(profile, pause, progress, cancellation.Token);
    return 0;
}
catch (OperationCanceledException)
{
    return 2;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value) => report(value);
}
