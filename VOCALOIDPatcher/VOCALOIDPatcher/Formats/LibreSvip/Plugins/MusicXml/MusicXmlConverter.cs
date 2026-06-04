using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using VOCALOIDPatcher.Formats.LibreSvip.Framework;
using VOCALOIDPatcher.Formats.LibreSvip.Model;
using VOCALOIDPatcher.Formats.LibreSvip.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.MusicXml;

public sealed class MusicXmlConverter : FormatConverter
{
    public bool ImportTempo { get; set; } = true;

    public override bool CanLoad => true;
    public override bool CanDump => true;

    public override Project Load(byte[] content)
    {
        byte[] xmlBytes = content.Length >= 2 && content[0] == 'P' && content[1] == 'K'
            ? ExtractFromZip(content)
            : content;
        var root = LoadXml(TextHelper.DetectAndDecode(xmlBytes));
        if (root.Name.LocalName != "score-partwise")
            throw new InvalidDataException("仅支持 score-partwise 的 MusicXML");
        return new MusicXmlParser(ImportTempo).ParseProject(root);
    }

    public override byte[] Dump(Project project)
    {
        var root = new MusicXmlGenerator().GenerateProject(project);
        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", "no"),
            new XDocumentType("score-partwise", "-//Recordare//DTD MusicXML 4.0 Partwise//EN",
                "http://www.musicxml.org/dtds/partwise.dtd", null),
            root);
        using var ms = new MemoryStream();
        var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };
        using (var writer = XmlWriter.Create(ms, settings))
            doc.Save(writer);
        return ms.ToArray();
    }

    private static byte[] ExtractFromZip(byte[] content)
    {
        using var archive = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read);
        var containerEntry = archive.GetEntry("META-INF/container.xml")
            ?? throw new InvalidDataException("mxl 缺少 META-INF/container.xml");
        string fullPath;
        using (var stream = containerEntry.Open())
        {
            var container = XDocument.Load(stream).Root!;
            fullPath = container.Descendant("rootfile")?.Attribute("full-path")?.Value
                ?? throw new InvalidDataException("mxl container 缺少 rootfile");
        }
        var rootEntry = archive.GetEntry(fullPath)
            ?? throw new InvalidDataException($"mxl 缺少 {fullPath}");
        using var rootStream = rootEntry.Open();
        using var rootMs = new MemoryStream();
        rootStream.CopyTo(rootMs);
        return rootMs.ToArray();
    }

    private static XElement LoadXml(string text)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
        using var reader = XmlReader.Create(new StringReader(text), settings);
        return XDocument.Load(reader).Root!;
    }
}
