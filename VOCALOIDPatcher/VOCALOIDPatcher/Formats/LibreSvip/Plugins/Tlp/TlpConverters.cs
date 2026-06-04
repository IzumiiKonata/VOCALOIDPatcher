using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Tlp;

internal static class TlpFlat
{
    public static List<TlpPoint> ReadCurrentArray(ref Utf8JsonReader reader)
    {
        var flat = new List<double>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            flat.Add(reader.TokenType == JsonTokenType.Null ? double.NaN : reader.GetDouble());
        var result = new List<TlpPoint>();
        for (int i = 0; i + 1 < flat.Count; i += 2)
            result.Add(new TlpPoint(flat[i], flat[i + 1]));
        return result;
    }

    public static void WriteArray(Utf8JsonWriter writer, List<TlpPoint> points)
    {
        writer.WriteStartArray();
        foreach (var p in points)
        {
            WriteNumber(writer, p.Pos);
            WriteNumber(writer, p.Value);
        }
        writer.WriteEndArray();
    }

    private static void WriteNumber(Utf8JsonWriter writer, double value)
    {
        if (double.IsNaN(value))
            writer.WriteRawValue("NaN");
        else
            writer.WriteNumberValue(value);
    }
}

public sealed class TlpFlatPointsConverter : JsonConverter<List<TlpPoint>>
{
    public override List<TlpPoint> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        TlpFlat.ReadCurrentArray(ref reader);

    public override void Write(Utf8JsonWriter writer, List<TlpPoint> value, JsonSerializerOptions options) =>
        TlpFlat.WriteArray(writer, value);
}

public sealed class TlpPointGroupsConverter : JsonConverter<List<List<TlpPoint>>>
{
    public override List<List<TlpPoint>> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var groups = new List<List<TlpPoint>>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            groups.Add(TlpFlat.ReadCurrentArray(ref reader));
        return groups;
    }

    public override void Write(Utf8JsonWriter writer, List<List<TlpPoint>> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var group in value)
            TlpFlat.WriteArray(writer, group);
        writer.WriteEndArray();
    }
}

public sealed class TlpPartListConverter : JsonConverter<List<TuneLabPart>>
{
    public override List<TuneLabPart> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var result = new List<TuneLabPart>();
        using var doc = JsonDocument.ParseValue(ref reader);
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            string type = element.TryGetProperty("type", out var t) ? t.GetString() ?? "midi" : "midi";
            string raw = element.GetRawText();
            TuneLabPart? part = type == "audio"
                ? JsonSerializer.Deserialize<TuneLabAudioPart>(raw, options)
                : JsonSerializer.Deserialize<TuneLabMidiPart>(raw, options);
            if (part != null)
                result.Add(part);
        }
        return result;
    }

    public override void Write(Utf8JsonWriter writer, List<TuneLabPart> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var part in value)
        {
            switch (part)
            {
                case TuneLabMidiPart midi:
                    JsonSerializer.Serialize(writer, midi, options);
                    break;
                case TuneLabAudioPart audio:
                    JsonSerializer.Serialize(writer, audio, options);
                    break;
            }
        }
        writer.WriteEndArray();
    }
}
