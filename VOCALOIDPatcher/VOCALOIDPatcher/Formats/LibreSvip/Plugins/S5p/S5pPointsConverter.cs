using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.S5p;

public sealed class S5pPointsConverter : JsonConverter<List<S5pPoint>>
{
    public override List<S5pPoint> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var flat = new List<double>();
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("S5pPoints 应为数组");
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            flat.Add(reader.GetDouble());
        var result = new List<S5pPoint>();
        for (int i = 0; i + 1 < flat.Count; i += 2)
            result.Add(new S5pPoint(flat[i], flat[i + 1]));
        return result;
    }

    public override void Write(Utf8JsonWriter writer, List<S5pPoint> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var point in value)
        {
            writer.WriteNumberValue(point.Offset);
            writer.WriteNumberValue(point.Value);
        }
        writer.WriteEndArray();
    }
}
