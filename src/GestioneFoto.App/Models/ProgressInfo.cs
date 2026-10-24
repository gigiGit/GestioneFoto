namespace GestioneFoto.App.Models;

public sealed class ProgressInfo
{
    public required string Phase { get; init; }
    public required string Message { get; init; }
    public int Processed { get; init; }
    public int Total { get; init; }

    public int Percent => Total <= 0 ? 0 : (int)Math.Clamp((Processed * 100.0) / Total, 0, 100);
}
