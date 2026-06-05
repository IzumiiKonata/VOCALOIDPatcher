using System.Text;
using System.Xml.Linq;
using VOCALOIDPatcher.Formats.LibreSvip.Framework;
using VOCALOIDPatcher.Formats.LibreSvip.Model;
using VOCALOIDPatcher.Formats.LibreSvip.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ccs;

public sealed class CcsConverter : FormatConverter
{
    public bool ImportPitch { get; set; } = true;
    public bool ImportInstrumentalTrack { get; set; } = true;
    public string DefaultSingerName { get; set; } = "さとうささら";
    public string DefaultSingerId { get; set; } = "A";
    public string DefaultSingerVersion { get; set; } = "1.0.0";

    public override bool CanLoad => true;
    public override bool CanDump => true;

    public override Project Load(byte[] content)
    {
        string xml = TextHelper.DetectAndDecode(content);
        var doc = XDocument.Parse(xml);
        var root = doc.Root!;
        var project = CcsProject.FromXml(root);
        return new CcsParser(ImportPitch, ImportInstrumentalTrack).ParseProject(project);
    }

    public override byte[] Dump(Project project)
    {
        var ccsProject = new CcsGenerator(DefaultSingerName, DefaultSingerId, DefaultSingerVersion)
            .GenerateProject(project);
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            ccsProject.ToXml());
        var utf8NoBom = new UTF8Encoding(false);
        var sb = new StringBuilder();
        using var writer = System.Xml.XmlWriter.Create(sb, new System.Xml.XmlWriterSettings
        {
            Indent = true,
            Encoding = utf8NoBom,
        });
        doc.WriteTo(writer);
        writer.Flush();
        return utf8NoBom.GetBytes(sb.ToString());
    }
}
