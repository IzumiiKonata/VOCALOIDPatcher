using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Svp;

public sealed class SvpPointsConverter : JsonConverter<List<SvParamPoint>>
{
    public override List<SvParamPoint> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var flat = new List<double>();
        if (reader.TokenType == JsonTokenType.StartArray)
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                flat.Add(reader.GetDouble());
        var result = new List<SvParamPoint>();
        for (int i = 0; i + 1 < flat.Count; i += 2)
            result.Add(new SvParamPoint((long)flat[i], flat[i + 1]));
        return result;
    }

    public override void Write(Utf8JsonWriter writer, List<SvParamPoint> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var p in value)
        {
            writer.WriteNumberValue(p.Offset);
            writer.WriteNumberValue(p.Value);
        }
        writer.WriteEndArray();
    }
}
