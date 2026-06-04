using VOCALOIDPatcher.Formats.LibreSvip.Framework;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ufdata;

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
    }
}
