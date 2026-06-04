using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Tlp;

public readonly record struct TlpPoint(double Pos, double Value);

public sealed class TuneLabTempo
{
    [JsonPropertyName("pos")] public double Pos { get; set; }
    [JsonPropertyName("bpm")] public double Bpm { get; set; }
}

public sealed class TuneLabTimeSignature
{
    [JsonPropertyName("numerator")] public int Numerator { get; set; }
    [JsonPropertyName("denominator")] public int Denominator { get; set; }
    [JsonPropertyName("barIndex")] public int BarIndex { get; set; }
}

public sealed class TuneLabVoice
{
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("id")] public string Id { get; set; } = "";
}

public sealed class TuneLabAutomation
{
    [JsonPropertyName("default")] public double Default { get; set; }
    [JsonPropertyName("values")][JsonConverter(typeof(TlpFlatPointsConverter))] public List<TlpPoint> Values { get; set; } = new();
}

public sealed class TuneLabNote
{
    [JsonPropertyName("pos")] public double Pos { get; set; }
    [JsonPropertyName("dur")] public double Dur { get; set; }
    [JsonPropertyName("pitch")] public int Pitch { get; set; }
    [JsonPropertyName("lyric")] public string Lyric { get; set; } = "";
    [JsonPropertyName("pronunciation")] public string? Pronunciation { get; set; }
}

public sealed class TuneLabVibrato
{
    [JsonPropertyName("pos")] public double Pos { get; set; }
    [JsonPropertyName("dur")] public double Dur { get; set; }
    [JsonPropertyName("frequency")] public double Frequency { get; set; }
    [JsonPropertyName("amplitude")] public double Amplitude { get; set; }
    [JsonPropertyName("phase")] public double Phase { get; set; }
    [JsonPropertyName("attack")] public double Attack { get; set; }
    [JsonPropertyName("release")] public double Release { get; set; }
    [JsonPropertyName("affectedAutomations")] public Dictionary<string, double> AffectedAutomations { get; set; } = new();
}

public abstract class TuneLabPart
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("pos")] public double Pos { get; set; }
    [JsonPropertyName("dur")] public double Dur { get; set; }
}

public sealed class TuneLabMidiPart : TuneLabPart
{
    [JsonPropertyName("type")] public string Type => "midi";
    [JsonPropertyName("gain")] public double? Gain { get; set; } = 0.0;
    [JsonPropertyName("voice")] public TuneLabVoice Voice { get; set; } = new();
    [JsonPropertyName("notes")] public List<TuneLabNote> Notes { get; set; } = new();
    [JsonPropertyName("automations")] public Dictionary<string, TuneLabAutomation> Automations { get; set; } = new();
    [JsonPropertyName("pitch")][JsonConverter(typeof(TlpPointGroupsConverter))] public List<List<TlpPoint>> Pitch { get; set; } = new();
    [JsonPropertyName("vibratos")] public List<TuneLabVibrato> Vibratos { get; set; } = new();
}

public sealed class TuneLabAudioPart : TuneLabPart
{
    [JsonPropertyName("type")] public string Type => "audio";
    [JsonPropertyName("path")] public string Path { get; set; } = "";
}

public sealed class TuneLabTrack
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("gain")] public double Gain { get; set; }
    [JsonPropertyName("pan")] public double Pan { get; set; }
    [JsonPropertyName("mute")] public bool Mute { get; set; }
    [JsonPropertyName("solo")] public bool Solo { get; set; }
    [JsonPropertyName("color")] public string Color { get; set; } = "";
    [JsonPropertyName("asRefer")] public bool? AsRefer { get; set; } = false;
    [JsonPropertyName("parts")][JsonConverter(typeof(TlpPartListConverter))] public List<TuneLabPart> Parts { get; set; } = new();
}

public sealed class TuneLabProject
{
    [JsonPropertyName("version")] public int Version { get; set; }
    [JsonPropertyName("tempos")] public List<TuneLabTempo> Tempos { get; set; } = new();
    [JsonPropertyName("timeSignatures")] public List<TuneLabTimeSignature> TimeSignatures { get; set; } = new();
    [JsonPropertyName("tracks")] public List<TuneLabTrack> Tracks { get; set; } = new();
}
