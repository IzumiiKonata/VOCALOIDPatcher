namespace VOCALOIDPatcher.Formats.LibreSvip;

public enum ExportPhase
{
    Reading,
    Pitch,
    Generating,
    Writing,
}

public readonly struct ExportProgress
{
    public ExportPhase Phase { get; init; }
    public int Current { get; init; }
    public int Total { get; init; }
    public string? Arg { get; init; }
}
