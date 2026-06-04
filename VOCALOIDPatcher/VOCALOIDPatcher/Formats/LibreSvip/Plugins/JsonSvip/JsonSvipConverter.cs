using System.Text.Json;
using System.Text.Json.Serialization;
using VOCALOIDPatcher.Formats.LibreSvip.Framework;
using VOCALOIDPatcher.Formats.LibreSvip.Model;
using VOCALOIDPatcher.Formats.LibreSvip.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.JsonSvip;

public sealed class JsonSvipConverter : FormatConverter
{
    public override bool CanLoad => true;
    public override bool CanDump => true;

    private static readonly JsonSerializerOptions Inner = BuildInner();
    private static readonly JsonSerializerOptions Outer = BuildOuter();

    private static JsonSerializerOptions BuildInner()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        options.Converters.Add(new PointArrayConverter());
        return options;
    }

    private static JsonSerializerOptions BuildOuter()
    {
        var options = new JsonSerializerOptions(Inner);
        options.Converters.Add(new TrackJsonConverter(Inner));
        return options;
    }

    public override Project Load(byte[] content) =>
        JsonSerializer.Deserialize<Project>(TextHelper.DetectAndDecode(content), Outer)!;

    public override byte[] Dump(Project project) =>
        TextHelper.EncodeUtf8(JsonSerializer.Serialize(project, Outer));
}
