using System.Collections.Generic;
using System.Text.Json.Serialization;
using VOCALOIDPatcher.Formats.LibreSvip.Core;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Y77;

public sealed class Y77Note
{
    [JsonPropertyName("py")] public string Py { get; set; } = "";
    [JsonPropertyName("len")] public int Length { get; set; }
    [JsonPropertyName("start")] public int Start { get; set; }
    [JsonPropertyName("lyric")] public string Lyric { get; set; } = "";
    [JsonPropertyName("pitch")] public int Pitch { get; set; }
    [JsonPropertyName("pbs")] public int Pbs { get; set; }
    [JsonPropertyName("pit")] public List<double> Pit { get; set; } = new();
}

public sealed class Y77Project
{
    [JsonPropertyName("bpm")] public double Bpm { get; set; } = Constants.DefaultBpm;
    [JsonPropertyName("bars")] public int Bars { get; set; }
    [JsonPropertyName("notes")] public List<Y77Note> Notes { get; set; } = new();
    [JsonPropertyName("nnote")] public int NNote { get; set; }
    [JsonPropertyName("bbar")] public int BBar { get; set; } = 4;
    [JsonPropertyName("v")] public int V { get; set; } = 10001;
    [JsonPropertyName("bbeat")] public int BBeat { get; set; } = 4;
}
