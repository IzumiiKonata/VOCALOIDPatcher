using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Vpr;

public enum VprLanguage
{
    Japanese = 0,
    English = 1,
    Korean = 2,
    Spanish = 3,
    SimplifiedChinese = 4,
}

public sealed class VprPoint
{
    [JsonPropertyName("pos")] public int Pos { get; set; }
    [JsonPropertyName("value")] public JsonElement? Value { get; set; }
}

public sealed class VprOutPoint
{
    [JsonPropertyName("pos")] public int Pos { get; set; }
    [JsonPropertyName("value")] public double Value { get; set; }
}

public sealed class VprTimeSig
{
    [JsonPropertyName("bar")] public int Bar { get; set; }
    [JsonPropertyName("denom")] public int Denom { get; set; } = 4;
    [JsonPropertyName("numer")] public int Numer { get; set; } = 4;
}

public sealed class VprEnabled
{
    [JsonPropertyName("isEnabled")] public bool? IsEnabled { get; set; } = true;
}

public sealed class VprGlobal
{
    [JsonPropertyName("isEnabled")] public bool? IsEnabled { get; set; } = true;
    [JsonPropertyName("value")] public int? Value { get; set; } = 12000;
}

public sealed class VprRegion
{
    [JsonPropertyName("isEnabled")] public bool? IsEnabled { get; set; } = true;
    [JsonPropertyName("begin")] public double? Begin { get; set; } = 0;
    [JsonPropertyName("end")] public double? End { get; set; } = 7680;
}

public sealed class VprVersion
{
    [JsonPropertyName("major")] public int Major { get; set; } = 5;
    [JsonPropertyName("minor")] public int Minor { get; set; }
    [JsonPropertyName("revision")] public int Revision { get; set; }
}

public sealed class VprWeight
{
    [JsonPropertyName("pre")] public int? Pre { get; set; } = 64;
    [JsonPropertyName("post")] public int? Post { get; set; } = 64;
}

public sealed class VprSingingSkill
{
    [JsonPropertyName("duration")] public int? Duration { get; set; } = 158;
    [JsonPropertyName("weight")] public VprWeight? Weight { get; set; } = new();
}

public sealed class VprVibrato
{
    [JsonPropertyName("type")] public int? TypeValue { get; set; } = 0;
    [JsonPropertyName("duration")] public int? Duration { get; set; } = 0;
    [JsonPropertyName("depths")] public List<VprPoint>? Depths { get; set; }
    [JsonPropertyName("rates")] public List<VprPoint>? Rates { get; set; }
}

public sealed class VprExp
{
    [JsonPropertyName("opening")] public int? Opening { get; set; } = 127;
    [JsonPropertyName("accent")] public int? Accent { get; set; }
    [JsonPropertyName("decay")] public int? Decay { get; set; }
    [JsonPropertyName("bendDepth")] public int? BendDepth { get; set; }
    [JsonPropertyName("bendLength")] public int? BendLength { get; set; }
}

public sealed class VprNote
{
    [JsonPropertyName("langID")] public int? LangId { get; set; }
    [JsonPropertyName("pos")] public int Pos { get; set; }
    [JsonPropertyName("duration")] public int? Duration { get; set; }
    [JsonPropertyName("number")] public int Number { get; set; }
    [JsonPropertyName("lyric")] public string Lyric { get; set; } = "";
    [JsonPropertyName("phoneme")] public string? Phoneme { get; set; }
    [JsonPropertyName("isProtected")] public bool? IsProtected { get; set; }
    [JsonPropertyName("exp")] public VprExp? Exp { get; set; }
    [JsonPropertyName("singingSkill")] public VprSingingSkill? SingingSkill { get; set; }
    [JsonPropertyName("vibrato")] public VprVibrato? Vibrato { get; set; }
    [JsonPropertyName("velocity")] public int Velocity { get; set; } = 64;
    [JsonPropertyName("isAiVibratoEnabled")] public bool? IsAiVibratoEnabled { get; set; }
    [JsonPropertyName("directPitches")] public List<VprPoint>? DirectPitches { get; set; }
    [JsonPropertyName("phonemePositions")] public List<VprBasePos>? PhonemePositions { get; set; }
}

public sealed class VprBasePos
{
    [JsonPropertyName("pos")] public int Pos { get; set; }
}

public sealed class VprControllers
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("events")] public List<VprPoint> Events { get; set; } = new();
}

public sealed class VprOutControllers
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("events")] public List<VprOutPoint> Events { get; set; } = new();
}

public sealed class VprAutomation
{
    [JsonPropertyName("isFolded")] public bool? IsFolded { get; set; } = true;
    [JsonPropertyName("height")] public double? Height { get; set; } = 0;
    [JsonPropertyName("events")] public List<VprOutPoint> Events { get; set; } = new();
}

public sealed class VprTempo
{
    [JsonPropertyName("isFolded")] public bool? IsFolded { get; set; } = true;
    [JsonPropertyName("events")] public List<VprOutPoint> Events { get; set; } = new();
    [JsonPropertyName("global")] public VprGlobal? Global { get; set; } = new();
    [JsonPropertyName("height")] public double? Height { get; set; } = 0;
    [JsonPropertyName("ara")] public VprEnabled? Ara { get; set; }
}

public sealed class VprTimeSigs
{
    [JsonPropertyName("isFolded")] public bool? IsFolded { get; set; } = true;
    [JsonPropertyName("events")] public List<VprTimeSig> Events { get; set; } = new();
}

public sealed class VprMasterTrack
{
    [JsonPropertyName("loop")] public VprRegion Loop { get; set; } = new();
    [JsonPropertyName("samplingRate")] public int SamplingRate { get; set; } = 44100;
    [JsonPropertyName("tempo")] public VprTempo Tempo { get; set; } = new();
    [JsonPropertyName("timeSig")] public VprTimeSigs TimeSig { get; set; } = new();
    [JsonPropertyName("volume")] public VprAutomation Volume { get; set; } = new();
    [JsonPropertyName("mainTuning")] public double MainTuning { get; set; } = 440;
}

public sealed class VprVoice
{
    [JsonPropertyName("compID")] public string? CompId { get; set; }
    [JsonPropertyName("langID")] public int? LangId { get; set; }
}

public sealed class VprLangId
{
    [JsonPropertyName("langID")] public int? LangId { get; set; }
}

public sealed class VprAIVoice
{
    [JsonPropertyName("compID")] public string? CompId { get; set; }
    [JsonPropertyName("langIDs")] public List<VprLangId> LangIds { get; set; } = new();
}

public sealed class VprVoices
{
    [JsonPropertyName("compID")] public string? CompId { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
}

public sealed class VprEffects
{
    [JsonPropertyName("isFolded")] public bool? IsFolded { get; set; } = true;
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("isBypassed")] public bool? IsBypassed { get; set; }
}

public sealed class VprVoicePart
{
    [JsonPropertyName("pos")] public int Pos { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; } = "";
    [JsonPropertyName("duration")] public int? Duration { get; set; }
    [JsonPropertyName("notes")] public List<VprNote> Notes { get; set; } = new();
    [JsonPropertyName("voice")] public VprVoice? Voice { get; set; }
    [JsonPropertyName("secondaryVoice")] public VprVoice? SecondaryVoice { get; set; }
    [JsonPropertyName("aiVoice")] public VprAIVoice? AiVoice { get; set; }
    [JsonPropertyName("controllers")] public List<VprOutControllers>? Controllers { get; set; }
    [JsonPropertyName("stylePresetID")] public string? StylePresetId { get; set; }
    [JsonPropertyName("styleName")] public string? StyleName { get; set; } = "No Effect";
}

public sealed class VprWav
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("originalName")] public string? OriginalName { get; set; }
}

public sealed class VprWavPart
{
    [JsonPropertyName("pos")] public int Pos { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; } = "";
    [JsonPropertyName("region")] public VprRegion? Region { get; set; }
    [JsonPropertyName("wav")] public VprWav? Wav { get; set; }
}

public sealed class VprRawTrack
{
    [JsonPropertyName("type")] public int Type { get; set; }
    [JsonPropertyName("isFolded")] public bool? IsFolded { get; set; }
    [JsonPropertyName("busNo")] public int? BusNo { get; set; }
    [JsonPropertyName("color")] public int? Color { get; set; }
    [JsonPropertyName("height")] public double? Height { get; set; }
    [JsonPropertyName("isMuted")] public bool IsMuted { get; set; }
    [JsonPropertyName("isSoloMode")] public bool IsSoloMode { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("panpot")] public JsonElement? Panpot { get; set; }
    [JsonPropertyName("volume")] public JsonElement? Volume { get; set; }
    [JsonPropertyName("parts")] public List<JsonElement>? Parts { get; set; }
}

public sealed class VprProject
{
    [JsonPropertyName("masterTrack")] public VprMasterTrack MasterTrack { get; set; } = new();
    [JsonPropertyName("title")] public string? Title { get; set; } = "";
    [JsonPropertyName("tracks")] public List<VprRawTrack> Tracks { get; set; } = new();
    [JsonPropertyName("vender")] public string Vender { get; set; } = "Yamaha Corporation";
    [JsonPropertyName("version")] public VprVersion? Version { get; set; } = new();
    [JsonPropertyName("voices")] public List<VprVoices> Voices { get; set; } = new();
}

public sealed class VprOutVoiceTrack
{
    [JsonPropertyName("type")] public int Type { get; set; }
    [JsonPropertyName("isFolded")] public bool? IsFolded { get; set; } = true;
    [JsonPropertyName("busNo")] public int? BusNo { get; set; } = 0;
    [JsonPropertyName("color")] public int? Color { get; set; } = 0;
    [JsonPropertyName("height")] public double? Height { get; set; } = 0;
    [JsonPropertyName("isMuted")] public bool IsMuted { get; set; }
    [JsonPropertyName("isSoloMode")] public bool IsSoloMode { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; } = "";
    [JsonPropertyName("panpot")] public VprAutomation Panpot { get; set; } = new();
    [JsonPropertyName("volume")] public VprAutomation Volume { get; set; } = new();
    [JsonPropertyName("parts")] public List<VprVoicePart> Parts { get; set; } = new();
}

public sealed class VprOutAudioTrack
{
    [JsonPropertyName("type")] public int Type { get; set; } = 1;
    [JsonPropertyName("isFolded")] public bool? IsFolded { get; set; } = true;
    [JsonPropertyName("busNo")] public int? BusNo { get; set; } = 0;
    [JsonPropertyName("color")] public int? Color { get; set; } = 0;
    [JsonPropertyName("height")] public double? Height { get; set; } = 0;
    [JsonPropertyName("isMuted")] public bool IsMuted { get; set; }
    [JsonPropertyName("isSoloMode")] public bool IsSoloMode { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; } = "";
    [JsonPropertyName("panpot")] public VprAutomation Panpot { get; set; } = new();
    [JsonPropertyName("volume")] public VprAutomation Volume { get; set; } = new();
    [JsonPropertyName("parts")] public List<VprWavPart> Parts { get; set; } = new();
}

public sealed class VprOutProject
{
    [JsonPropertyName("masterTrack")] public VprMasterTrack MasterTrack { get; set; } = new();
    [JsonPropertyName("title")] public string? Title { get; set; } = "";
    [JsonPropertyName("tracks")] public List<object> Tracks { get; set; } = new();
    [JsonPropertyName("vender")] public string Vender { get; set; } = "Yamaha Corporation";
    [JsonPropertyName("version")] public VprVersion? Version { get; set; } = new();
    [JsonPropertyName("voices")] public List<VprVoices> Voices { get; set; } = new();
}
