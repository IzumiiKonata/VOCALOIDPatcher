using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Acep;

public sealed class AcepParamCurveListConverter : JsonConverter<AcepParamCurveList>
{
    public override AcepParamCurveList Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var list = new AcepParamCurveList();
        if (reader.TokenType == JsonTokenType.Null)
            return list;
        var curves = JsonSerializer.Deserialize<List<AcepParamCurve>>(ref reader, options);
        if (curves != null)
        {
            foreach (var curve in curves)
                curve.NormalizeAnchorPoints();
            list.Root = curves;
        }
        return list;
    }

    public override void Write(Utf8JsonWriter writer, AcepParamCurveList value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value.Root, options);
    }
}

public sealed class AcepTrackConverter : JsonConverter<AcepTrack>
{
    public override AcepTrack Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var node = JsonNode.Parse(ref reader);
        if (node is not JsonObject obj)
            return new AcepEmptyTrack();
        string type = obj["type"]?.GetValue<string>() ?? "empty";
        AcepTrack track = type switch
        {
            "audio" => obj.Deserialize<AcepAudioTrack>(WithoutSelf(options))!,
            "sing" => DeserializeVocal(obj, options),
            _ => obj.Deserialize<AcepEmptyTrack>(WithoutSelf(options))!,
        };
        return track;
    }

    private static AcepVocalTrack DeserializeVocal(JsonObject obj, JsonSerializerOptions options)
    {
        var track = obj.Deserialize<AcepVocalTrack>(WithoutSelf(options))!;
        var singerNode = obj["singer"];
        if (singerNode != null)
        {
            if (singerNode is JsonValue singerValue && singerValue.TryGetValue<int>(out int singerId))
            {
                track.Singers.Add(new AcepSingerConfig
                {
                    Singer = new AcepCustomSinger { SingerId = singerId },
                });
            }
            else if (singerNode is JsonObject singerObj)
            {
                var custom = singerObj.Deserialize<AcepCustomSinger>(WithoutSelf(options));
                if (custom != null)
                    track.Singers.Add(new AcepSingerConfig { Singer = custom });
            }
        }
        return track;
    }

    public override void Write(Utf8JsonWriter writer, AcepTrack value, JsonSerializerOptions options)
    {
        JsonObject obj = value switch
        {
            AcepAudioTrack a => JsonSerializer.SerializeToNode(a, WithoutSelf(options))!.AsObject(),
            AcepVocalTrack v => JsonSerializer.SerializeToNode(v, WithoutSelf(options))!.AsObject(),
            _ => JsonSerializer.SerializeToNode((AcepEmptyTrack)value, WithoutSelf(options))!.AsObject(),
        };
        obj["type"] = value.TrackType;
        obj.WriteTo(writer, options);
    }

    private static JsonSerializerOptions WithoutSelf(JsonSerializerOptions options)
    {
        var clone = new JsonSerializerOptions(options);
        for (int i = clone.Converters.Count - 1; i >= 0; i--)
            if (clone.Converters[i] is AcepTrackConverter)
                clone.Converters.RemoveAt(i);
        return clone;
    }
}
