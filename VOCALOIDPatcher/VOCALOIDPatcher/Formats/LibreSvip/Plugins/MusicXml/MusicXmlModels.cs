using VOCALOIDPatcher.Formats.LibreSvip.Model;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.MusicXml;

public enum MXmlNoteType
{
    Begin = 1,
    Middle = 2,
    End = 3,
    Single = 4,
}

public sealed class KeyTick
{
    public int Tick { get; set; }
    public SongTempo? Tempo { get; set; }
    public Note? NoteStart { get; set; }
    public Note? NoteEnd { get; set; }
}

public sealed class MXmlMeasureContent
{
    public int Duration { get; set; }
    public Note? Note { get; set; }
    public MXmlNoteType? NoteType { get; set; }
    public double? Bpm { get; set; }

    public static MXmlMeasureContent WithTempo(double bpm) =>
        new() { Duration = 0, Bpm = bpm };

    public static MXmlMeasureContent WithRest(int duration) =>
        new() { Duration = duration };

    public static MXmlMeasureContent WithNote(int duration, Note note, MXmlNoteType noteType) =>
        new() { Duration = duration, Note = note, NoteType = noteType };
}

public sealed class MXmlMeasure
{
    public int TickStart { get; set; }
    public int Length { get; set; }
    public TimeSignature? TimeSignature { get; set; }
    public System.Collections.Generic.List<MXmlMeasureContent> Contents { get; set; } = new();
}
