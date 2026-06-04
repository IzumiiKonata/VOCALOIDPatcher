using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ufdata;

public sealed class UFNotes
{
    [JsonPropertyName("key")] public int Key { get; set; }
    [JsonPropertyName("tickOn")] public int TickOn { get; set; }
    [JsonPropertyName("tickOff")] public int TickOff { get; set; }
    [JsonPropertyName("lyric")] public string Lyric { get; set; } = "";
}

public sealed class UFPitch
{
    [JsonPropertyName("ticks")] public List<int> Ticks { get; set; } = new();
    [JsonPropertyName("values")] public List<double?> Values { get; set; } = new();
    [JsonPropertyName("isAbsolute")] public bool IsAbsolute { get; set; }
}

public sealed class UFTempos
{
    [JsonPropertyName("tickPosition")] public int TickPosition { get; set; }
    [JsonPropertyName("bpm")] public double Bpm { get; set; }
}

public sealed class UFTimeSignatures
{
    [JsonPropertyName("measurePosition")] public int MeasurePosition { get; set; }
    [JsonPropertyName("numerator")] public int Numerator { get; set; } = 4;
    [JsonPropertyName("denominator")] public int Denominator { get; set; } = 4;
}

public sealed class UFTracks
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("notes")] public List<UFNotes> Notes { get; set; } = new();
    [JsonPropertyName("pitch")] public UFPitch? Pitch { get; set; }
}

public sealed class UFProject
{
    [JsonPropertyName("name")] public string Name { get; set; } = "export";
    [JsonPropertyName("tracks")] public List<UFTracks> Tracks { get; set; } = new();
    [JsonPropertyName("timeSignatures")] public List<UFTimeSignatures> TimeSignatures { get; set; } = new();
    [JsonPropertyName("tempos")] public List<UFTempos> Tempos { get; set; } = new();
    [JsonPropertyName("measurePrefix")] public int MeasurePrefix { get; set; }
}

public sealed class UFData
{
    [JsonPropertyName("formatVersion")] public int FormatVersion { get; set; } = 1;
    [JsonPropertyName("project")] public UFProject Project { get; set; } = new();
}
