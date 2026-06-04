using VOCALOIDPatcher.Formats.LibreSvip.Framework;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Aisp;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.JsonSvip;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ufdata;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ds;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Lrc;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.MusicXml;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.S5p;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Subtitle;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Tlp;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ust;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ustx;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Vog;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.VvProj;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Y77;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins;

public static class PluginRegistration
{
    private static bool _registered;

    public static void RegisterAll()
    {
        if (_registered)
            return;
        _registered = true;

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "ufdata",
            DisplayName = "UtaFormatix Data",
            Extension = "ufdata",
            Converter = new UfdataConverter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "json",
            DisplayName = "OpenSVIP",
            Extension = "json",
            Converter = new JsonSvipConverter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "ust",
            DisplayName = "UTAU Sequence Text",
            Extension = "ust",
            MultipleFile = true,
            Converter = new UstConverter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "vog",
            DisplayName = "Vogen",
            Extension = "vog",
            Converter = new VogConverter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "lrc",
            DisplayName = "LRC 歌词",
            Extension = "lrc",
            Converter = new LrcConverter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "srt",
            DisplayName = "SRT 字幕",
            Extension = "srt",
            Converter = new SrtConverter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "ass",
            DisplayName = "ASS 字幕",
            Extension = "ass",
            Converter = new AssConverter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "s5p",
            DisplayName = "Synthesizer V Editor",
            Extension = "s5p",
            Converter = new S5pConverter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "ustx",
            DisplayName = "OpenUTAU",
            Extension = "ustx",
            Converter = new UstxConverter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "musicxml",
            DisplayName = "MusicXML",
            Extension = "musicxml",
            OtherExtensions = new[] { "xml", "mxl" },
            Converter = new MusicXmlConverter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "vvproj",
            DisplayName = "VOICEVOX",
            Extension = "vvproj",
            Converter = new VvProjConverter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "y77",
            DisplayName = "元七七",
            Extension = "y77",
            Converter = new Y77Converter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "ds",
            DisplayName = "DiffSinger",
            Extension = "ds",
            Converter = new DsConverter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "aisp",
            DisplayName = "AISingers",
            Extension = "aisp",
            Converter = new AispConverter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "tlp",
            DisplayName = "TuneLab (Legacy)",
            Extension = "tlp",
            Converter = new TlpConverter(),
        });
    }
}
