using VOCALOIDPatcher.Formats.LibreSvip.Framework;
using VOCALOIDPatcher.Formats.LibreSvip.Model;
using VOCALOIDPatcher.Formats.LibreSvip.Serialization;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ustx;

public sealed class UstxConverter : FormatConverter
{
    public bool ImportInstrumental { get; set; } = true;

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    public override bool CanLoad => true;
    public override bool CanDump => true;

    public override Project Load(byte[] content)
    {
        var ustx = Deserializer.Deserialize<USTXProject>(TextHelper.DetectAndDecode(content));
        return new UstxParser(ImportInstrumental).ParseProject(ustx);
    }

    public override byte[] Dump(Project project)
    {
        var ustx = new UstxGenerator().GenerateProject(project);
        return TextHelper.EncodeUtf8(Serializer.Serialize(ustx));
    }
}
