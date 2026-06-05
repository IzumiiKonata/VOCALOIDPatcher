using System.IO;
using VOCALOIDPatcher.Formats.LibreSvip.Framework;
using VOCALOIDPatcher.Formats.LibreSvip.Model;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Svip;

public enum SvipVersion
{
    Auto,
    Svip700,
    Svip600,
    Compat,
}

public sealed class SvipConverter : FormatConverter
{
    public bool ImportPitch { get; set; } = true;
    public bool ImportVolume { get; set; } = true;
    public bool ImportBreath { get; set; } = true;
    public bool ImportGender { get; set; } = true;
    public bool ImportStrength { get; set; } = true;
    public bool ImportInstrumentalTrack { get; set; } = true;

    public string Singer { get; set; } = "陈水若";
    public int Tempo { get; set; } = 60;
    public SvipVersion Version { get; set; } = SvipVersion.Auto;

    public override bool CanLoad => true;
    public override bool CanDump => true;

    public override Project Load(byte[] content)
    {
        var reader = new SvipNrbfReader(content);
        reader.Read();
        var model = new SvipModelBuilder(reader).Build();
        if (model == null)
            throw new InvalidDataException("svip: 未找到根对象");
        string version = $"{reader.Magic}{reader.Version}";
        var parser = new SvipParser(
            ImportPitch,
            ImportVolume,
            ImportBreath,
            ImportGender,
            ImportStrength,
            ImportInstrumentalTrack);
        return parser.ParseProject(version, model);
    }

    public override byte[] Dump(Project project)
    {
        switch (Version)
        {
            case SvipVersion.Svip700:
                project.Version = "SVIP7.0.0";
                break;
            case SvipVersion.Auto:
                if (project.Version == "SVIP0.0.0")
                    project.Version = "SVIP6.0.0";
                break;
            case SvipVersion.Svip600:
                project.Version = "SVIP6.0.0";
                break;
            case SvipVersion.Compat:
                project.Version = "SVIP0.0.0";
                break;
        }

        bool isPower = Version == SvipVersion.Svip700;
        var generator = new SvipGenerator(Singer, Tempo, isPower);
        var (version, model) = generator.GenerateProject(project);
        var writer = new SvipNrbfWriter();
        return writer.Write(version, model);
    }
}
