using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Aisp;

public sealed class AISNote
{
    [JsonPropertyName("s")] public int Start { get; set; }
    [JsonPropertyName("l")] public int Length { get; set; }
    [JsonPropertyName("m")] public int MidiNo { get; set; }
    [JsonPropertyName("ly")] public string? Lyric { get; set; }
    [JsonPropertyName("py")] public string? Pinyin { get; set; }
    [JsonPropertyName("vel")] public int? Vel { get; set; } = 50;
    [JsonPropertyName("tri")] public bool? Triple { get; set; } = false;
    [JsonPropertyName("pit")][JsonConverter(typeof(AisPitConverter))] public List<double> Pit { get; set; } = new();
    [JsonPropertyName("bc")] public int? Bc { get; set; } = 0;
    [JsonPropertyName("bj")] public int? Bj { get; set; } = 0;
    [JsonPropertyName("bq")] public int? Bq { get; set; } = 0;
}

public sealed class AISSingVoicePattern
{
    [JsonPropertyName("uid")] public int? Uid { get; set; }
    [JsonPropertyName("s")] public int Start { get; set; }
    [JsonPropertyName("l")] public int? Length { get; set; }
    [JsonPropertyName("n")] public List<AISNote> Notes { get; set; } = new();
}

public sealed class AISAudioPattern
{
    [JsonPropertyName("uid")] public int? Uid { get; set; }
    [JsonPropertyName("s")] public int Start { get; set; }
    [JsonPropertyName("l")] public int? Length { get; set; }
    [JsonPropertyName("pa")] public string? PathAudio { get; set; }
    [JsonPropertyName("pw")] public string? PathWave { get; set; }
    [JsonPropertyName("n_channel")] public int? NChannel { get; set; } = 2;
    [JsonPropertyName("len_sec")] public int? LenSec { get; set; } = 0;
}

public abstract class AISTrack
{
    [JsonPropertyName("i")] public int? Idx { get; set; }
    [JsonPropertyName("s")] public bool? Solo { get; set; } = false;
    [JsonPropertyName("m")] public bool? Mute { get; set; } = false;
    [JsonPropertyName("v")] public double? Volume { get; set; } = 0;
    [JsonPropertyName("n")] public string? Name { get; set; }
}

public sealed class AISSingVoiceTrack : AISTrack
{
    [JsonPropertyName("t")] public int TrackType => 0;
    [JsonPropertyName("sn")] public string? SingerNameCn { get; set; }
    [JsonPropertyName("se")] public string? SingerNameEn { get; set; } = "";
    [JsonPropertyName("sh")] public string? SingerHeadPath { get; set; } = "";
    [JsonPropertyName("im")] public List<AISSingVoicePattern> Items { get; set; } = new();
}

public sealed class AISAudioTrack : AISTrack
{
    [JsonPropertyName("t")] public int TrackType => 1;
    [JsonPropertyName("im")] public List<AISAudioPattern> Items { get; set; } = new();
}

public sealed class AISMidiTrack : AISTrack
{
    [JsonPropertyName("t")] public int TrackType => 2;
}

public sealed class AISTimeSignature
{
    [JsonPropertyName("beat_zi")] public int BeatZi { get; set; } = 4;
    [JsonPropertyName("beat_mu")] public int BeatMu { get; set; } = 4;
    [JsonPropertyName("start_bar")] public int StartBar { get; set; }
    [JsonPropertyName("str")] public string StrValue => $"{BeatZi}/{BeatMu}";
}

public sealed class AISTempo
{
    [JsonPropertyName("tempo_float")] public double? TempoFloat { get; set; }
    [JsonPropertyName("start_128")] public int Start128 { get; set; }
    [JsonPropertyName("start_bar")] public int StartBar { get; set; }
    [JsonPropertyName("start_beat_in_bar")] public int? StartBeatInBar { get; set; }
}

public sealed class AISProjectBody
{
    [JsonPropertyName("tracks")] public List<AISTrack> Tracks { get; set; } = new();
    [JsonPropertyName("num_track")] public int NumTrack => Tracks.Count;
}

public sealed class AISProjectHead
{
    [JsonPropertyName("tempo")] public List<AISTempo> Tempo { get; set; } = new();
    [JsonPropertyName("signature")] public List<AISTimeSignature> Signature { get; set; } = new();
    [JsonPropertyName("flags")] public int? Flags { get; set; } = -256;
    [JsonPropertyName("flage")] public int? Flage { get; set; } = -128;
    [JsonPropertyName("time")] public int? Time { get; set; }
    [JsonPropertyName("bar")] public int? Bar { get; set; }
}
