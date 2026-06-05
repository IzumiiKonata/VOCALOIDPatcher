using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using VOCALOIDPatcher.Formats.LibreSvip.Framework;
using VOCALOIDPatcher.Formats.LibreSvip.Model;
using VOCALOIDPatcher.Formats.LibreSvip.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ppsf;

public sealed class PpsfConverter : FormatConverter
{
    public bool ImportPitch { get; set; } = true;
    public bool ImportInstrumentalTrack { get; set; } = true;

    public override bool CanLoad => true;
    public override bool CanDump => true;

    public override Project Load(byte[] content)
    {
        if (content.Length >= 2 && content[0] == 0x50 && content[1] == 0x4B)
        {
            using var archive = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read);
            var entry = archive.GetEntry("ppsf.json")
                ?? throw new InvalidDataException("ppsf ZIP missing ppsf.json");
            using var entryStream = entry.Open();
            using var ms = new MemoryStream();
            entryStream.CopyTo(ms);
            var proj = JsonHelper.Deserialize<PpsfProject>(ms.ToArray());
            return new PpsfParser(ImportPitch, ImportInstrumentalTrack).ParseProject(proj);
        }
        if (content.Length >= 4
            && content[0] == 0x50 && content[1] == 0x50
            && content[2] == 0x53 && content[3] == 0x46)
        {
            throw new NotSupportedException(
                "Legacy binary PPSF format is not supported for import.");
        }
        var text = TextHelper.DetectAndDecode(content);
        var jsonProj = JsonHelper.Deserialize<PpsfProject>(text);
        return new PpsfParser(ImportPitch, ImportInstrumentalTrack).ParseProject(jsonProj);
    }

    public override byte[] Dump(Project project)
    {
        var ppsfProject = new PpsfGenerator().GenerateProject(project);
        string json = JsonHelper.Serialize(ppsfProject);
        byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("ppsf.json");
            using var entryStream = entry.Open();
            entryStream.Write(jsonBytes, 0, jsonBytes.Length);
        }
        return ms.ToArray();
    }
}
