using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ds;

public sealed class DsItem
{
    [JsonPropertyName("text")]
    [JsonConverter(typeof(SpaceSeparatedStringListConverter))]
    public List<string> Text { get; set; } = new();

    [JsonPropertyName("note_seq")]
    [JsonConverter(typeof(SpaceSeparatedStringListConverter))]
    public List<string> NoteSeq { get; set; } = new();

    [JsonPropertyName("note_dur")]
    [JsonConverter(typeof(SpaceSeparatedDoubleListConverter))]
    public List<double>? NoteDur { get; set; }

    [JsonPropertyName("note_slur")]
    [JsonConverter(typeof(SpaceSeparatedIntListConverter))]
    public List<int>? NoteSlur { get; set; }

    [JsonPropertyName("f0_timestep")]
    public double? F0Timestep { get; set; }

    [JsonPropertyName("f0_seq")]
    [JsonConverter(typeof(SpaceSeparatedDoubleListConverter))]
    public List<double>? F0Seq { get; set; }

    [JsonPropertyName("offset")]
    [JsonConverter(typeof(StringOrDoubleConverter))]
    public double Offset { get; set; }
}
