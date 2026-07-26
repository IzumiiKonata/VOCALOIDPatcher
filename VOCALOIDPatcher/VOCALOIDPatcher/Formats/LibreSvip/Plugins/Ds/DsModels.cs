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

    [JsonPropertyName("ph_seq")]
    [JsonConverter(typeof(SpaceSeparatedStringListConverter))]
    public List<string> PhSeq { get; set; } = new();

    [JsonPropertyName("note_dur")]
    [JsonConverter(typeof(SpaceSeparatedDoubleListConverter))]
    public List<double>? NoteDur { get; set; }

    [JsonPropertyName("note_slur")]
    [JsonConverter(typeof(SpaceSeparatedIntListConverter))]
    public List<int>? NoteSlur { get; set; }

    [JsonPropertyName("note_dur_seq")]
    [JsonConverter(typeof(SpaceSeparatedDoubleListConverter))]
    public List<double>? NoteDurSeq { get; set; }

    [JsonPropertyName("is_slur_seq")]
    [JsonConverter(typeof(SpaceSeparatedIntListConverter))]
    public List<int>? IsSlurSeq { get; set; }

    [JsonPropertyName("ph_dur")]
    [JsonConverter(typeof(SpaceSeparatedDoubleListConverter))]
    public List<double>? PhDur { get; set; }

    [JsonPropertyName("ph_num")]
    [JsonConverter(typeof(SpaceSeparatedIntListConverter))]
    public List<int>? PhNum { get; set; }

    [JsonPropertyName("f0_timestep")]
    public double? F0Timestep { get; set; }

    [JsonPropertyName("f0_seq")]
    [JsonConverter(typeof(SpaceSeparatedDoubleListConverter))]
    public List<double>? F0Seq { get; set; }

    [JsonPropertyName("gender_timestep")]
    public double? GenderTimestep { get; set; }

    [JsonPropertyName("gender")]
    [JsonConverter(typeof(SpaceSeparatedDoubleListConverter))]
    public List<double>? Gender { get; set; }

    [JsonPropertyName("seed")]
    public int? Seed { get; set; }

    [JsonPropertyName("input_type")]
    public string? InputType { get; set; }

    [JsonPropertyName("offset")]
    [JsonConverter(typeof(StringOrDoubleConverter))]
    public double Offset { get; set; }
}
