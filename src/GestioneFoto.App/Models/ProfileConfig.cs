namespace GestioneFoto.App.Models;

public sealed class ProfileConfig
{
    public required string ProfileName { get; init; }
    public string? UserName { get; init; }
    public required string IncludePath { get; init; }
    public required string DatabasePath { get; init; }
    public required string DestinationRoot { get; init; }
    public required string DestinationTemplate { get; init; }
    public string? AlbumDestination { get; init; }
    public required string[] Extensions { get; init; }
    public required string[] ExcludePaths { get; init; }
    public required long MinSize { get; init; }
    public string? ArchiveTempPath { get; init; }
}
