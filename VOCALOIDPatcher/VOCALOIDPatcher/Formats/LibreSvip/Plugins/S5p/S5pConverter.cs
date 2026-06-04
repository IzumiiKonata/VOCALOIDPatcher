using VOCALOIDPatcher.Formats.LibreSvip.Framework;
using VOCALOIDPatcher.Formats.LibreSvip.Model;
using VOCALOIDPatcher.Formats.LibreSvip.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.S5p;

public sealed class S5pConverter : FormatConverter
{
    public bool ImportPitch { get; set; } = true;
    public bool ImportInstrumental { get; set; } = true;

    public override bool CanLoad => true;
    public override bool CanDump => true;

    public override Project Load(byte[] content)
    {
        var project = JsonHelper.Deserialize<S5pProject>(TextHelper.DetectAndDecode(content));
        return new S5pParser(ImportPitch, ImportInstrumental).ParseProject(project);
    }

    public override byte[] Dump(Project project)
    {
        var s5pProject = new S5pGenerator().GenerateProject(project);
        return TextHelper.EncodeUtf8(JsonHelper.Serialize(s5pProject));
    }
}
