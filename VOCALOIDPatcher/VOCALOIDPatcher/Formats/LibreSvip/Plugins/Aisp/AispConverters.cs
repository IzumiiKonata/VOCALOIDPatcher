using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Aisp;

public sealed class AisPitConverter : JsonConverter<List<double>>
{
    public override List<double> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var result = new List<double>();
        if (reader.TokenType == JsonTokenType.String)
        {
            foreach (var token in (reader.GetString() ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                int x = token.IndexOf('x');
                if (x >= 0)
                {
                    double value = double.Parse(token[..x], CultureInfo.InvariantCulture);
                    int repeat = int.Parse(token[(x + 1)..], CultureInfo.InvariantCulture);
                    for (int i = 0; i < repeat; i++)
                        result.Add(value);
                }
                else
                {
                    result.Add(double.Parse(token, CultureInfo.InvariantCulture));
                }
            }
        }
        else if (reader.TokenType == JsonTokenType.StartArray)
        {
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                result.Add(reader.GetDouble());
        }
        return result;
    }

    public override void Write(Utf8JsonWriter writer, List<double> value, JsonSerializerOptions options)
    {
        if (value.Count == 0)
        {
            writer.WriteStringValue("0x500");
            return;
        }
        var builder = new StringBuilder();
        int i = 0;
        while (i < value.Count)
        {
            double key = value[i];
            int count = 1;
            while (i + count < value.Count && value[i + count] == key)
                count++;
            string keyStr = Math.Round(key, 2).ToString(CultureInfo.InvariantCulture);
            builder.Append(count > 1 ? $"{keyStr}x{count} " : $"{keyStr} ");
            i += count;
        }
        writer.WriteStringValue(builder.ToString().Trim());
    }
}

public sealed class AisTrackConverter : JsonConverter<AISTrack>
{
    private readonly JsonSerializerOptions _inner;

    public AisTrackConverter(JsonSerializerOptions inner) => _inner = inner;

    public override AISTrack Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        int type = root.TryGetProperty("t", out var t) ? t.GetInt32() : 0;
        string raw = root.GetRawText();
        return type switch
        {
            1 => JsonSerializer.Deserialize<AISAudioTrack>(raw, _inner)!,
            2 => JsonSerializer.Deserialize<AISMidiTrack>(raw, _inner)!,
            _ => JsonSerializer.Deserialize<AISSingVoiceTrack>(raw, _inner)!,
        };
    }

    public override void Write(Utf8JsonWriter writer, AISTrack value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case AISSingVoiceTrack s:
                JsonSerializer.Serialize(writer, s, _inner);
                break;
            case AISAudioTrack a:
                JsonSerializer.Serialize(writer, a, _inner);
                break;
            default:
                JsonSerializer.Serialize(writer, (AISMidiTrack)value, _inner);
                break;
        }
    }
}
