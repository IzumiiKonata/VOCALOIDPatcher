using VOCALOIDPatcher.Formats.LibreSvip.Framework;
using VOCALOIDPatcher.Formats.LibreSvip.Model;
using VOCALOIDPatcher.Formats.LibreSvip.Serialization;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ustx;

public enum UstxPlusHandlingMode
{
    Auto,
    Monosyllabic,
    Polysyllabic,
}

public enum UstxEnglishCompatibility
{
    NonArpa,
    Arpa,
}

public sealed class UstxConverter : FormatConverter
{
    public bool ImportInstrumental { get; set; } = true;
    public bool ImportPitch { get; set; } = true;
    public UstxPlusHandlingMode PlusHandlingMode { get; set; } = UstxPlusHandlingMode.Auto;
    public string BreathLyrics { get; set; } = "Asp AP";
    public string SilenceLyrics { get; set; } = "R SP";
    public UstxEnglishCompatibility EnglishPhonemizerCompatibility { get; set; } = UstxEnglishCompatibility.NonArpa;

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
        return new UstxParser(
            ImportInstrumental, ImportPitch, PlusHandlingMode, BreathLyrics, SilenceLyrics).ParseProject(ustx);
    }

    public override byte[] Dump(Project project)
    {
        var ustx = new UstxGenerator(EnglishPhonemizerCompatibility).GenerateProject(project);
        return TextHelper.EncodeUtf8(Serializer.Serialize(ustx));
    }
}
