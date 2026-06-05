using System.Collections.Generic;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Dv;

public sealed class DvPoint
{
    public int X { get; set; }
    public int Y { get; set; }

    public DvPoint() { }
    public DvPoint(int x, int y) { X = x; Y = y; }
}

public sealed class DvTempo
{
    public int Position { get; set; }
    public int Bpm { get; set; }
}

public sealed class DvTimeSignature
{
    public int MeasurePosition { get; set; }
    public int Numerator { get; set; } = 4;
    public int Denominator { get; set; } = 4;
}

public sealed class DvNoteParameter
{
    public List<DvPoint> AmplitudePoints { get; set; } = new();
    public List<DvPoint> FrequencyPoints { get; set; } = new();
    public List<DvPoint> VibratoPoints { get; set; } = new();
}

public sealed class DvPhoneme
{
    public sbyte Unknown1 { get; set; }
    public float ConsonantRate { get; set; }
    public sbyte VowelModified { get; set; }
    public float Medial { get; set; }
    public float Rime { get; set; }
    public float Ending { get; set; }
}

public sealed class DvNote
{
    public int Start { get; set; }
    public int Length { get; set; }
    public int Key { get; set; }
    public int Vibrato { get; set; }
    public string Phoneme { get; set; } = "";
    public string Word { get; set; } = "";
    public byte Padding1 { get; set; }
    public DvNoteParameter NoteVibratoData { get; set; } = new();
    public List<float> Unknown { get; set; } = new();
    public DvPhoneme? Phonemes { get; set; }
    public int? BenDepth { get; set; }
    public int? BenLength { get; set; }
    public int? PorTail { get; set; }
    public int? PorHead { get; set; }
    public int? Timbre { get; set; }
    public string? CrossLyric { get; set; }
    public int? CrossTimbre { get; set; }
}

public sealed class DvSegment
{
    public int Start { get; set; }
    public int Length { get; set; }
    public string Name { get; set; } = "";
    public string SingerName { get; set; } = "";
    public List<DvNote> Notes { get; set; } = new();
    public List<DvPoint> VolumeData { get; set; } = new();
    public List<DvPoint> PitchData { get; set; } = new();
    public List<DvPoint> BreathData { get; set; } = new();
    public List<DvPoint>? Ext3Data { get; set; }
    public List<DvPoint>? Ext5Data { get; set; }
    public List<DvPoint>? Ext6Data { get; set; }
    public List<DvPoint>? Ext7Data { get; set; }
}

public sealed class DvSingingTrack
{
    public string Name { get; set; } = "";
    public byte Mute { get; set; }
    public byte Solo { get; set; }
    public int Volume { get; set; }
    public int Balance { get; set; }
    public List<DvSegment> Segments { get; set; } = new();
}

public sealed class DvAudioInfo
{
    public int Start { get; set; }
    public int Length { get; set; }
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
}

public sealed class DvAudioTrack
{
    public string Name { get; set; } = "";
    public byte Mute { get; set; }
    public byte Solo { get; set; }
    public int Volume { get; set; }
    public int Balance { get; set; }
    public List<DvAudioInfo> Infos { get; set; } = new();
}

public enum DvTrackType
{
    Singing = 0,
    Audio = 1,
}

public sealed class DvTrack
{
    public DvTrackType TrackType { get; set; }
    public DvSingingTrack? SingingTrack { get; set; }
    public DvAudioTrack? AudioTrack { get; set; }
}

public sealed class DvInnerProject
{
    public HashSet<string> Features { get; set; } = new();
    public List<DvTempo> Tempos { get; set; } = new();
    public List<DvTimeSignature> TimeSignatures { get; set; } = new();
    public List<DvTrack> Tracks { get; set; } = new();
}

public sealed class DvProject
{
    public int Version { get; set; }
    public DvInnerProject InnerProject { get; set; } = new();
}
