using System.Text.Json;
using System.Text.Json.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Serialization;

public static class JsonHelper
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static readonly JsonSerializerOptions Indented = new(Default)
    {
        WriteIndented = true,
    };

    public static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Default)
        ?? throw new JsonException($"反序列化为 {typeof(T).Name} 得到 null");

    public static T Deserialize<T>(byte[] utf8Json) =>
        JsonSerializer.Deserialize<T>(utf8Json, Default)
        ?? throw new JsonException($"反序列化为 {typeof(T).Name} 得到 null");

    public static string Serialize<T>(T value, bool indented = false) =>
        JsonSerializer.Serialize(value, indented ? Indented : Default);
}
