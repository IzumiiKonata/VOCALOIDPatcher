using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using VOCALOIDPatcher.Formats.LibreSvip.Model;

namespace VOCALOIDPatcher.Formats.LibreSvip.Serialization;

public sealed class PointArrayConverter : JsonConverter<Point>
{
    public override Point Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Point 应为 [x, y] 数组");
        reader.Read();
        int x = reader.GetInt32();
        reader.Read();
        int y = reader.GetInt32();
        reader.Read();
        if (reader.TokenType != JsonTokenType.EndArray)
            throw new JsonException("Point 数组应仅含两个元素");
        return new Point(x, y);
    }

    public override void Write(Utf8JsonWriter writer, Point value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Y);
        writer.WriteEndArray();
    }
}

public sealed class TrackJsonConverter : JsonConverter<Track>
{
    private readonly JsonSerializerOptions _inner;

    public TrackJsonConverter(JsonSerializerOptions inner) => _inner = inner;

    public override Track Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        string type = root.TryGetProperty("Type", out var typeProp) ? typeProp.GetString() ?? "Singing" : "Singing";
        string raw = root.GetRawText();
        return type == "Instrumental"
            ? JsonSerializer.Deserialize<InstrumentalTrack>(raw, _inner)!
            : JsonSerializer.Deserialize<SingingTrack>(raw, _inner)!;
    }

    public override void Write(Utf8JsonWriter writer, Track value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case SingingTrack singing:
                JsonSerializer.Serialize(writer, singing, _inner);
                break;
            case InstrumentalTrack instrumental:
                JsonSerializer.Serialize(writer, instrumental, _inner);
                break;
            default:
                throw new JsonException($"未知轨道类型 {value.GetType().Name}");
        }
    }
}
