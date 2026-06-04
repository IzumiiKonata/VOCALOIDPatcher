using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Vog;

public sealed class VogenNote
{
    [JsonPropertyName("pitch")] public int? Pitch { get; set; }
    [JsonPropertyName("lyric")] public string? Lyric { get; set; }
    [JsonPropertyName("rom")] public string? Rom { get; set; }
    [JsonPropertyName("on")] public int? On { get; set; }
    [JsonPropertyName("dur")] public int? Dur { get; set; }
}

public sealed class VogenTrack
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("singerId")] public string? SingerId { get; set; }
    [JsonPropertyName("romScheme")] public string? RomScheme { get; set; } = "";
    [JsonPropertyName("notes")] public List<VogenNote> Notes { get; set; } = new();
}

public sealed class VogenProject
{
    [JsonPropertyName("timeSig0")] public string TimeSig0 { get; set; } = "4/4";
    [JsonPropertyName("bpm0")] public double Bpm0 { get; set; }
    [JsonPropertyName("accomOffset")] public int? AccomOffset { get; set; } = 0;
    [JsonPropertyName("utts")] public List<VogenTrack> Utts { get; set; } = new();
}
