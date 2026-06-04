using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ds;

public sealed class SpaceSeparatedDoubleListConverter : JsonConverter<List<double>>
{
    public override List<double> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return (reader.GetString() ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => double.Parse(s, CultureInfo.InvariantCulture)).ToList();
        var list = new List<double>();
        if (reader.TokenType == JsonTokenType.StartArray)
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                list.Add(reader.GetDouble());
        return list;
    }

    public override void Write(Utf8JsonWriter writer, List<double> value, JsonSerializerOptions options) =>
        writer.WriteStringValue(string.Join(" ", value.Select(v => v.ToString(CultureInfo.InvariantCulture))));
}

public sealed class SpaceSeparatedIntListConverter : JsonConverter<List<int>>
{
    public override List<int> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return (reader.GetString() ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse).ToList();
        var list = new List<int>();
        if (reader.TokenType == JsonTokenType.StartArray)
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                list.Add(reader.GetInt32());
        return list;
    }

    public override void Write(Utf8JsonWriter writer, List<int> value, JsonSerializerOptions options) =>
        writer.WriteStringValue(string.Join(" ", value));
}

public sealed class SpaceSeparatedStringListConverter : JsonConverter<List<string>>
{
    public override List<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return (reader.GetString() ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var list = new List<string>();
        if (reader.TokenType == JsonTokenType.StartArray)
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                list.Add(reader.GetString() ?? "");
        return list;
    }

    public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options) =>
        writer.WriteStringValue(string.Join(" ", value));
}

public sealed class StringOrDoubleConverter : JsonConverter<double>
{
    public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return double.TryParse(reader.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : 0;
        return reader.GetDouble();
    }

    public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
}
