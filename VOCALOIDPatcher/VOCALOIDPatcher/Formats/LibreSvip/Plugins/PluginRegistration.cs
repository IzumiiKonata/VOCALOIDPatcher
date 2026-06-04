using VOCALOIDPatcher.Formats.LibreSvip.Framework;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.JsonSvip;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ufdata;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ust;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Vog;

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
    }
}
