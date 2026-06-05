using VOCALOIDPatcher.Formats.LibreSvip.Framework;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Aisp;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.JsonSvip;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ufdata;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ds;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Lrc;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.MusicXml;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.S5p;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Subtitle;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Svp;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Tlp;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ust;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ustx;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Vog;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Vsqx;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.VvProj;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Y77;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Vpr;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Vsq;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ccs;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Tlpx;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ppsf;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Acep;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Dv;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Tssln;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Svip;

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
            NameKey = "VOCALOIDPatcher_Format_Name_ufdata",
            Extension = "ufdata",
            Converter = new UfdataConverter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "json",
            DisplayName = "OpenSVIP",
            NameKey = "VOCALOIDPatcher_Format_Name_json",
            Extension = "json",
            Converter = new JsonSvipConverter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "ust",
            DisplayName = "UTAU Sequence Text",
            NameKey = "VOCALOIDPatcher_Format_Name_ust",
            Extension = "ust",
            MultipleFile = true,
            Converter = new UstConverter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "vog",
            DisplayName = "Vogen",
            NameKey = "VOCALOIDPatcher_Format_Name_vog",
            Extension = "vog",
            Converter = new VogConverter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "lrc",
            DisplayName = "LRC Lyrics",
            NameKey = "VOCALOIDPatcher_Format_Name_lrc",
            Extension = "lrc",
            Converter = new LrcConverter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "srt",
            DisplayName = "SRT Subtitle",
            NameKey = "VOCALOIDPatcher_Format_Name_srt",
            Extension = "srt",
            Converter = new SrtConverter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "ass",
            DisplayName = "ASS Subtitle",
            NameKey = "VOCALOIDPatcher_Format_Name_ass",
            Extension = "ass",
            Converter = new AssConverter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "s5p",
            DisplayName = "Synthesizer V Editor",
            NameKey = "VOCALOIDPatcher_Format_Name_s5p",
            Extension = "s5p",
            Converter = new S5pConverter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "ustx",
            DisplayName = "OpenUTAU",
            NameKey = "VOCALOIDPatcher_Format_Name_ustx",
            Extension = "ustx",
            Converter = new UstxConverter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "musicxml",
            DisplayName = "MusicXML",
            NameKey = "VOCALOIDPatcher_Format_Name_musicxml",
            Extension = "musicxml",
            OtherExtensions = new[] { "xml", "mxl" },
            Converter = new MusicXmlConverter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "vvproj",
            DisplayName = "VOICEVOX",
            NameKey = "VOCALOIDPatcher_Format_Name_vvproj",
            Extension = "vvproj",
            Converter = new VvProjConverter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "y77",
            DisplayName = "Yuan77",
            NameKey = "VOCALOIDPatcher_Format_Name_y77",
            Extension = "y77",
            Converter = new Y77Converter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "ds",
            DisplayName = "DiffSinger",
            NameKey = "VOCALOIDPatcher_Format_Name_ds",
            Extension = "ds",
            Converter = new DsConverter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "aisp",
            DisplayName = "AISingers",
            NameKey = "VOCALOIDPatcher_Format_Name_aisp",
            Extension = "aisp",
            Converter = new AispConverter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "tlp",
            DisplayName = "TuneLab (Legacy)",
            NameKey = "VOCALOIDPatcher_Format_Name_tlp",
            Extension = "tlp",
            Converter = new TlpConverter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "svp",
            DisplayName = "Synthesizer V Studio",
            NameKey = "VOCALOIDPatcher_Format_Name_svp",
            Extension = "svp",
            Converter = new SvpConverter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "vsqx",
            DisplayName = "VOCALOID4 (vsqx)",
            NameKey = "VOCALOIDPatcher_Format_Name_vsqx",
            Extension = "vsqx",
            Converter = new VsqxConverter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "vpr",
            DisplayName = "VOCALOID5 (vpr)",
            NameKey = "VOCALOIDPatcher_Format_Name_vpr",
            Extension = "vpr",
            Converter = new VprConverter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "vsq",
            DisplayName = "VOCALOID2 (vsq)",
            NameKey = "VOCALOIDPatcher_Format_Name_vsq",
            Extension = "vsq",
            Converter = new VsqConverter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "ccs",
            DisplayName = "CeVIO Creative Studio",
            NameKey = "VOCALOIDPatcher_Format_Name_ccs",
            Extension = "ccs",
            Converter = new CcsConverter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "tlpx",
            DisplayName = "TuneLab",
            NameKey = "VOCALOIDPatcher_Format_Name_tlpx",
            Extension = "tlpx",
            Converter = new TlpxConverter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "ppsf",
            DisplayName = "Piapro Studio NT",
            NameKey = "VOCALOIDPatcher_Format_Name_ppsf",
            Extension = "ppsf",
            Converter = new PpsfConverter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "acep",
            DisplayName = "ACE Studio",
            NameKey = "VOCALOIDPatcher_Format_Name_acep",
            Extension = "acep",
            OtherExtensions = new[] { "acet" },
            Converter = new AcepConverter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "dv",
            DisplayName = "DeepVocal",
            NameKey = "VOCALOIDPatcher_Format_Name_dv",
            Extension = "dv",
            OtherExtensions = new[] { "sk" },
            Converter = new DvConverter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "tssln",
            DisplayName = "VoiSona",
            NameKey = "VOCALOIDPatcher_Format_Name_tssln",
            Extension = "tssln",
            Converter = new TsslnConverter(),
        });

        SvipFormatRegistry.Register(new SvipFormatInfo
        {
            Id = "svip",
            DisplayName = "X Studio (svip)",
            NameKey = "VOCALOIDPatcher_Format_Name_svip",
            Extension = "svip",
            Converter = new SvipConverter(),
        });
    }
}
