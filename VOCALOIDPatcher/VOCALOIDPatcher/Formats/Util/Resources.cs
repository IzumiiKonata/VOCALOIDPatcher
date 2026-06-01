using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace VOCALOIDPatcher.Formats.Util;

public static class Resources
{
    private static string Load(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var name = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase)
                                 || n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        if (name == null)
            throw new InvalidOperationException($"Embedded resource not found: {fileName}");

        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static string CcsTemplate => Load("template.ccs");
    public static string MusicXmlTemplate => Load("template.musicxml");
    public static string S5pTemplate => Load("template.s5p");
    public static string SvpTemplate => Load("template.svp");
    public static string TsslnTemplate => Load("template.tssln.json");
    public static string UstxTemplate => Load("template.ustx");
    public static string VprTemplate => Load("template.vprjson");
    public static string VsqxTemplate => Load("template.vsqx");
}
