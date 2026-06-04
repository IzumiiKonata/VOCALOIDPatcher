using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.S5p;

public readonly record struct S5pPoint(double Offset, double Value);

public sealed class S5pMeterItem
{
    [JsonPropertyName("measure")] public int Measure { get; set; }
    [JsonPropertyName("beatPerMeasure")] public int BeatPerMeasure { get; set; } = 4;
    [JsonPropertyName("beatGranularity")] public int BeatGranularity { get; set; } = 4;
}

public sealed class S5pTempoItem
{
    [JsonPropertyName("position")] public long Position { get; set; }
    [JsonPropertyName("beatPerMinute")] public double BeatPerMinute { get; set; } = 120.0;
}

public sealed class S5pDbDefaults
{
    [JsonPropertyName("lyric")] public string? Lyric { get; set; } = "la";
    [JsonPropertyName("breathiness")] public double? Breathiness { get; set; } = 0.0;
    [JsonPropertyName("gender")] public double? Gender { get; set; } = 0.0;
    [JsonPropertyName("tension")] public double? Tension { get; set; } = 0.0;
    [JsonPropertyName("dF0Vbr")] public double DF0Vbr { get; set; } = 0.025;
    [JsonPropertyName("pF0Vbr")] public double PF0Vbr { get; set; } = 0.0;
    [JsonPropertyName("tF0VbrLeft")] public double TF0VbrLeft { get; set; } = 0.15;
    [JsonPropertyName("tF0VbrRight")] public double TF0VbrRight { get; set; } = 0.15;
    [JsonPropertyName("tF0VbrStart")] public double TF0VbrStart { get; set; } = 0.25;
    [JsonPropertyName("fF0Vbr")] public double FF0Vbr { get; set; } = 5.5;
    [JsonPropertyName("tF0Left")] public double TF0Left { get; set; } = 0.0;
    [JsonPropertyName("tF0Right")] public double TF0Right { get; set; } = 0.0;
    [JsonPropertyName("dF0Left")] public double DF0Left { get; set; } = 0.0;
    [JsonPropertyName("dF0Right")] public double DF0Right { get; set; } = 0.0;
    [JsonPropertyName("dF0Jitter")] public double DF0Jitter { get; set; } = 1.0;
}

public sealed class S5pNote
{
    [JsonPropertyName("lyric")] public string Lyric { get; set; } = "";
    [JsonPropertyName("onset")] public long Onset { get; set; }
    [JsonPropertyName("duration")] public long Duration { get; set; }
    [JsonPropertyName("comment")] public string Comment { get; set; } = "";
    [JsonPropertyName("pitch")] public int Pitch { get; set; }
    [JsonPropertyName("dF0Vbr")] public double? DF0Vbr { get; set; }
    [JsonPropertyName("pF0Vbr")] public double? PF0Vbr { get; set; }
    [JsonPropertyName("tF0VbrLeft")] public double? TF0VbrLeft { get; set; }
    [JsonPropertyName("tF0VbrRight")] public double? TF0VbrRight { get; set; }
    [JsonPropertyName("tF0VbrStart")] public double? TF0VbrStart { get; set; }
    [JsonPropertyName("fF0Vbr")] public double? FF0Vbr { get; set; }
    [JsonPropertyName("tF0Left")] public double? TF0Left { get; set; }
    [JsonPropertyName("tF0Right")] public double? TF0Right { get; set; }
    [JsonPropertyName("dF0Left")] public double? DF0Left { get; set; }
    [JsonPropertyName("dF0Right")] public double? DF0Right { get; set; }
    [JsonPropertyName("dF0Jitter")] public double? DF0Jitter { get; set; }
    [JsonPropertyName("tF0Offset")] public double? TF0Offset { get; set; }
    [JsonPropertyName("tNoteOffset")] public double? TNoteOffset { get; set; }
    [JsonPropertyName("tSylOnset")] public double? TSylOnset { get; set; }
    [JsonPropertyName("tSylCoda")] public double? TSylCoda { get; set; }
    [JsonPropertyName("wSylNucleus")] public double? WSylNucleus { get; set; }
    [JsonPropertyName("sublib")] public string? Sublib { get; set; }
}

public sealed class S5pTrackMixer
{
    [JsonPropertyName("gainDecibel")] public double GainDecibel { get; set; } = 0.0;
    [JsonPropertyName("pan")] public double Pan { get; set; } = 0.0;
    [JsonPropertyName("muted")] public bool Muted { get; set; }
    [JsonPropertyName("solo")] public bool Solo { get; set; }
    [JsonPropertyName("engineOn")] public bool EngineOn { get; set; } = true;
    [JsonPropertyName("display")] public bool Display { get; set; } = true;
}

public sealed class S5pParameters
{
    [JsonPropertyName("interval")] public long Interval { get; set; } = 5512500;
    [JsonPropertyName("pitchDelta")][JsonConverter(typeof(S5pPointsConverter))] public List<S5pPoint> PitchDelta { get; set; } = new();
    [JsonPropertyName("vibratoEnv")][JsonConverter(typeof(S5pPointsConverter))] public List<S5pPoint> VibratoEnv { get; set; } = new();
    [JsonPropertyName("loudness")][JsonConverter(typeof(S5pPointsConverter))] public List<S5pPoint> Loudness { get; set; } = new();
    [JsonPropertyName("tension")][JsonConverter(typeof(S5pPointsConverter))] public List<S5pPoint> Tension { get; set; } = new();
    [JsonPropertyName("breathiness")][JsonConverter(typeof(S5pPointsConverter))] public List<S5pPoint> Breathiness { get; set; } = new();
    [JsonPropertyName("voicing")][JsonConverter(typeof(S5pPointsConverter))] public List<S5pPoint> Voicing { get; set; } = new();
    [JsonPropertyName("gender")][JsonConverter(typeof(S5pPointsConverter))] public List<S5pPoint> Gender { get; set; } = new();
}

public sealed class S5pTrack
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("dbName")] public string DbName { get; set; } = "";
    [JsonPropertyName("color")] public string Color { get; set; } = "15e879";
    [JsonPropertyName("displayOrder")] public int DisplayOrder { get; set; }
    [JsonPropertyName("dbDefaults")] public S5pDbDefaults DbDefaults { get; set; } = new();
    [JsonPropertyName("notes")] public List<S5pNote?> Notes { get; set; } = new();
    [JsonPropertyName("mixer")] public S5pTrackMixer Mixer { get; set; } = new();
    [JsonPropertyName("parameters")] public S5pParameters Parameters { get; set; } = new();
}

public sealed class S5pInstrumental
{
    [JsonPropertyName("filename")] public string Filename { get; set; } = "";
    [JsonPropertyName("offset")] public double Offset { get; set; }
}

public sealed class S5pMixer
{
    [JsonPropertyName("gainInstrumentalDecibel")] public double GainInstrumentalDecibel { get; set; }
    [JsonPropertyName("gainVocalMasterDecibel")] public double GainVocalMasterDecibel { get; set; }
    [JsonPropertyName("instrumentalMuted")] public bool InstrumentalMuted { get; set; }
    [JsonPropertyName("vocalMasterMuted")] public bool VocalMasterMuted { get; set; }
}

public sealed class S5pProject
{
    [JsonPropertyName("version")] public int Version { get; set; } = 7;
    [JsonPropertyName("meter")] public List<S5pMeterItem> Meter { get; set; } = new();
    [JsonPropertyName("tempo")] public List<S5pTempoItem> Tempo { get; set; } = new();
    [JsonPropertyName("tracks")] public List<S5pTrack> Tracks { get; set; } = new();
    [JsonPropertyName("instrumental")] public S5pInstrumental Instrumental { get; set; } = new();
    [JsonPropertyName("mixer")] public S5pMixer Mixer { get; set; } = new();
}
