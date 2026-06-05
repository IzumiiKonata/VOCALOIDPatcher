using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.LibreSvip.Model;

namespace VOCALOIDPatcher.Formats.LibreSvip;

public sealed class RawSingingTrack
{
    public string Title { get; init; } = "";
    public List<Note> Notes { get; init; } = new();
    public List<PitchBendData> PitchData { get; init; } = new();
    public List<int> PartOffsets { get; init; } = new();
}

public sealed class RawExport
{
    public List<SongTempo> Tempos { get; init; } = new();
    public List<TimeSignature> TimeSignatures { get; init; } = new();
    public List<RawSingingTrack> Tracks { get; init; } = new();

    public bool HasNotes => Tracks.Any(t => t.Notes.Count > 0);
}
