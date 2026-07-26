using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Svp;

public sealed class SVMeter
{
    [JsonPropertyName("index")] public int Index { get; set; }
    [JsonPropertyName("numerator")] public int Numerator { get; set; } = 4;
    [JsonPropertyName("denominator")] public int Denominator { get; set; } = 4;
}

public sealed class SVTempo
{
    [JsonPropertyName("bpm")] public double Bpm { get; set; }
    [JsonPropertyName("position")] public long Position { get; set; }
}

public sealed class SVTime
{
    [JsonPropertyName("meter")] public List<SVMeter> Meter { get; set; } = new();
    [JsonPropertyName("tempo")] public List<SVTempo> Tempo { get; set; } = new();
}

public sealed class SVNoteAttributes
{
    [JsonPropertyName("tF0Offset")] public double? TF0Offset { get; set; }
    [JsonPropertyName("tF0Left")] public double? TF0Left { get; set; }
    [JsonPropertyName("tF0Right")] public double? TF0Right { get; set; }
    [JsonPropertyName("dF0Left")] public double? DF0Left { get; set; }
    [JsonPropertyName("dF0Right")] public double? DF0Right { get; set; }
    [JsonPropertyName("tF0VbrStart")] public double? TF0VbrStart { get; set; }
    [JsonPropertyName("tF0VbrLeft")] public double? TF0VbrLeft { get; set; }
    [JsonPropertyName("tF0VbrRight")] public double? TF0VbrRight { get; set; }
    [JsonPropertyName("dF0Vbr")] public double? DF0Vbr { get; set; }
    [JsonPropertyName("fF0Vbr")] public double? FF0Vbr { get; set; }
    [JsonPropertyName("pF0Vbr")] public double? PF0Vbr { get; set; }
    [JsonPropertyName("paramLoudness")] public double? ParamLoudness { get; set; }
    [JsonPropertyName("paramBreathiness")] public double? ParamBreathiness { get; set; }
    [JsonPropertyName("paramGender")] public double? ParamGender { get; set; }
    [JsonPropertyName("paramTension")] public double? ParamTension { get; set; }

    [JsonIgnore] public double TransitionOffset => TF0Offset ?? 0.0;
    [JsonIgnore] public double PortamentoLeft => TF0Left ?? 0.07;
    [JsonIgnore] public double PortamentoRight => TF0Right ?? 0.07;
    [JsonIgnore] public double DepthLeft => DF0Left ?? 0.15;
    [JsonIgnore] public double DepthRight => DF0Right ?? 0.15;
    [JsonIgnore] public double VibratoStart => TF0VbrStart ?? 0.25;
    [JsonIgnore] public double VibratoLeft => TF0VbrLeft ?? 0.2;
    [JsonIgnore] public double VibratoRight => TF0VbrRight ?? 0.2;
    [JsonIgnore] public double VibratoDepth => DF0Vbr ?? 1.0;
    [JsonIgnore] public double VibratoFrequency => FF0Vbr ?? 5.5;
    [JsonIgnore] public double VibratoPhase => PF0Vbr ?? 0.0;
}

public sealed class SVNote
{
    [JsonPropertyName("onset")] public long Onset { get; set; }
    [JsonPropertyName("duration")] public long Duration { get; set; }
    [JsonPropertyName("lyrics")] public string Lyrics { get; set; } = "";
    [JsonPropertyName("phonemes")] public string Phonemes { get; set; } = "";
    [JsonPropertyName("pitch")] public int Pitch { get; set; }
    [JsonPropertyName("instantMode")] public bool? InstantMode { get; set; }
    [JsonPropertyName("attributes")] public SVNoteAttributes Attributes { get; set; } = new();
    [JsonPropertyName("systemAttributes")] public SVNoteAttributes? SystemAttributes { get; set; }
}

public readonly record struct SvParamPoint(long Offset, double Value);

public sealed class SVParamCurve
{
    [JsonPropertyName("mode")] public string Mode { get; set; } = "linear";
    [JsonPropertyName("points")][JsonConverter(typeof(SvpPointsConverter))] public List<SvParamPoint> Points { get; set; } = new();
}

public sealed class SVParameters
{
    [JsonPropertyName("pitchDelta")] public SVParamCurve PitchDelta { get; set; } = new();
    [JsonPropertyName("vibratoEnv")] public SVParamCurve VibratoEnv { get; set; } = new();
    [JsonPropertyName("loudness")] public SVParamCurve Loudness { get; set; } = new();
    [JsonPropertyName("tension")] public SVParamCurve Tension { get; set; } = new();
    [JsonPropertyName("breathiness")] public SVParamCurve Breathiness { get; set; } = new();
    [JsonPropertyName("gender")] public SVParamCurve Gender { get; set; } = new();
}

public sealed class SVPitchControl
{
    [JsonPropertyName("pos")] public long Pos { get; set; }
    [JsonPropertyName("pitch")] public double Pitch { get; set; }
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "curve";
    [JsonPropertyName("points")][JsonConverter(typeof(SvpPointsConverter))] public List<SvParamPoint> Points { get; set; } = new();
}

public sealed class SVGroup
{
    [JsonPropertyName("name")] public string Name { get; set; } = "main";
    [JsonPropertyName("uuid")] public string Uuid { get; set; } = "";
    [JsonPropertyName("notes")] public List<SVNote> Notes { get; set; } = new();
    [JsonPropertyName("parameters")] public SVParameters Parameters { get; set; } = new();
    [JsonPropertyName("pitchControls")] public List<SVPitchControl> PitchControls { get; set; } = new();
}

public sealed class SVDatabase
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("languageOverride")] public string? LanguageOverride { get; set; }
    [JsonPropertyName("phonesetOverride")] public string? PhonesetOverride { get; set; }
}

public sealed class SVAudio
{
    [JsonPropertyName("filename")] public string Filename { get; set; } = "";
    [JsonPropertyName("duration")] public double Duration { get; set; }
}

public sealed class SVRef
{
    [JsonPropertyName("audio")] public SVAudio? Audio { get; set; }
    [JsonPropertyName("blickOffset")] public long BlickOffset { get; set; }
    [JsonPropertyName("pitchOffset")] public int PitchOffset { get; set; }
    [JsonPropertyName("database")] public SVDatabase Database { get; set; } = new();
    [JsonPropertyName("voice")] public SVNoteAttributes Voice { get; set; } = new();
    [JsonPropertyName("groupID")] public string GroupId { get; set; } = "";
    [JsonPropertyName("isInstrumental")] public bool IsInstrumental { get; set; }
    [JsonPropertyName("systemPitchDelta")] public SVParamCurve SystemPitchDelta { get; set; } = new();
}

public sealed class SVMixer
{
    [JsonPropertyName("pan")] public double Pan { get; set; }
    [JsonPropertyName("mute")] public bool Mute { get; set; }
    [JsonPropertyName("solo")] public bool Solo { get; set; }
    [JsonPropertyName("gainDecibel")] public double GainDecibel { get; set; }
}

public sealed class SVTrack
{
    [JsonPropertyName("name")] public string Name { get; set; } = "Track 1";
    [JsonPropertyName("dispColor")] public string DispColor { get; set; } = "ff7db235";
    [JsonPropertyName("mixer")] public SVMixer Mixer { get; set; } = new();
    [JsonPropertyName("mainGroup")] public SVGroup MainGroup { get; set; } = new();
    [JsonPropertyName("mainRef")] public SVRef MainRef { get; set; } = new();
    [JsonPropertyName("groups")] public List<SVRef> Groups { get; set; } = new();
    [JsonPropertyName("renderEnabled")] public bool RenderEnabled { get; set; } = true;
}

public sealed class SVProject
{
    [JsonPropertyName("version")] public int Version { get; set; } = 100;
    [JsonPropertyName("time")] public SVTime Time { get; set; } = new();
    [JsonPropertyName("library")] public List<SVGroup> Library { get; set; } = new();
    [JsonPropertyName("tracks")] public List<SVTrack> Tracks { get; set; } = new();
    [JsonPropertyName("instantModeEnabled")] public bool? InstantModeEnabled { get; set; }
}
