using VOCALOIDPatcher.Formats.Model;

namespace VOCALOIDPatcher.Formats.Io;

public static class FormatHandlers
{
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized)
            return;
        _initialized = true;

        var ufData = FormatRegistry.Get(Format.UfData);
        ufData.Parser = UfData.Parse;
        ufData.Generator = UfData.Generate;
    }
}
