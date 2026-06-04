using VOCALOIDPatcher.Formats.LibreSvip.Framework;
using VOCALOIDPatcher.Formats.LibreSvip.Model;
using VOCALOIDPatcher.Formats.LibreSvip.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ufdata;

public sealed class UfdataConverter : FormatConverter
{
    public override bool CanLoad => true;
    public override bool CanDump => true;

    public override Project Load(byte[] content)
    {
        var ufdata = JsonHelper.Deserialize<UFData>(TextHelper.DetectAndDecode(content));
        return new UfdataParser(new UfdataInputOptions()).ParseProject(ufdata);
    }

    public override byte[] Dump(Project project)
    {
        var ufdata = new UfdataGenerator(new UfdataOutputOptions()).GenerateProject(project);
        return TextHelper.EncodeUtf8(JsonHelper.Serialize(ufdata));
    }
}
