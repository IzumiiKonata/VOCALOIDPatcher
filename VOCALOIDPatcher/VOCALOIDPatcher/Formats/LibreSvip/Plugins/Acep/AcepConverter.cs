using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using VOCALOIDPatcher.Formats.LibreSvip.Framework;
using VOCALOIDPatcher.Formats.LibreSvip.Model;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Acep;

public sealed class AcepConverter : FormatConverter
{
    public bool ImportPitch { get; set; } = true;
    public bool ImportBreath { get; set; } = true;
    public bool ImportGender { get; set; } = true;
    public bool ImportInstrumentalTrack { get; set; } = true;
    public bool KeepAllPronunciation { get; set; } = false;
    public bool ImportTension { get; set; } = true;
    public bool ImportEnergy { get; set; } = true;
    public double EnergyCoefficient { get; set; } = 0.5;
    public int CurveSampleInterval { get; set; } = 5;
    public string BreathNormalization { get; set; } = "none,0,10,0,0";
    public string TensionNormalization { get; set; } = "none,0,10,0,0";
    public string EnergyNormalization { get; set; } = "none,0,10,0,0";

    public string Singer { get; set; } = AcepSingers.DefaultSinger;
    public int Breath { get; set; } = 600;
    public StrengthMappingOption MapStrengthInfo { get; set; } = StrengthMappingOption.Both;
    public int SplitThreshold { get; set; } = 1;
    public AcepLyricsLanguage LyricLanguage { get; set; } = AcepLyricsLanguage.Chinese;
    public double DefaultConsonantLength { get; set; } = 0.0;
    public AcepSerialization Serialization { get; set; } = AcepSerialization.Json;

    public override bool CanLoad => true;
    public override bool CanDump => true;

    private static JsonSerializerOptions BuildJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        options.Converters.Add(new AcepParamCurveListConverter());
        options.Converters.Add(new AcepTrackConverter());
        return options;
    }

    public override Project Load(byte[] content)
    {
        byte[] acepBytes = content;
        if (content.Length >= 2 && content[0] == 0x50 && content[1] == 0x4B)
        {
            using var archive = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read);
            var entry = archive.Entries.FirstOrDefault(e => e.Name.EndsWith(".acep", StringComparison.OrdinalIgnoreCase))
                        ?? throw new InvalidDataException("acet 压缩包内未找到 .acep 文件");
            using var entryStream = entry.Open();
            using var ms = new MemoryStream();
            entryStream.CopyTo(ms);
            acepBytes = ms.ToArray();
        }

        JsonObject obj = AcepIo.Decompress(acepBytes);
        var jsonOptions = BuildJsonOptions();
        var aceProject = obj.Deserialize<AcepProject>(jsonOptions)
                         ?? throw new InvalidDataException("acep 项目反序列化失败");
        if (aceProject.TimeSignatures.Count == 0)
            aceProject.TimeSignatures.Add(new AcepTimeSignature
            {
                BarPos = 0,
                Numerator = aceProject.BeatsPerBar,
                Denominator = 4,
            });

        var parser = new AcepParser(BuildInputOptions());
        return parser.ParseProject(aceProject);
    }

    public override byte[] Dump(Project project)
    {
        var generator = new AcepGenerator(BuildOutputOptions());
        var aceProject = generator.GenerateProject(project);
        var jsonOptions = BuildJsonOptions();
        var node = JsonSerializer.SerializeToNode(aceProject, jsonOptions) as JsonObject
                   ?? throw new InvalidOperationException("acep 项目序列化失败");
        using var ms = new MemoryStream();
        AcepIo.Compress(node, ms, Serialization);
        return ms.ToArray();
    }

    private AcepInputOptions BuildInputOptions() => new()
    {
        ImportPitch = ImportPitch,
        ImportBreath = ImportBreath,
        ImportGender = ImportGender,
        ImportInstrumentalTrack = ImportInstrumentalTrack,
        KeepAllPronunciation = KeepAllPronunciation,
        ImportTension = ImportTension,
        ImportEnergy = ImportEnergy,
        EnergyCoefficient = EnergyCoefficient,
        CurveSampleInterval = CurveSampleInterval,
        BreathNormalization = AcepNormalizationArgument.FromStr(BreathNormalization),
        TensionNormalization = AcepNormalizationArgument.FromStr(TensionNormalization),
        EnergyNormalization = AcepNormalizationArgument.FromStr(EnergyNormalization),
    };

    private AcepOutputOptions BuildOutputOptions() => new()
    {
        Singer = Singer,
        Breath = Breath,
        MapStrengthInfo = MapStrengthInfo,
        SplitThreshold = SplitThreshold,
        LyricLanguage = LyricLanguage,
        DefaultConsonantLength = DefaultConsonantLength,
        Serialization = Serialization,
    };
}
