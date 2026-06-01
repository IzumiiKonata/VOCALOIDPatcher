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

        var ust = FormatRegistry.Get(Format.Ust);
        ust.Parser = Ust.Parse;
        ust.Generator = Ust.Generate;

        var s5p = FormatRegistry.Get(Format.S5p);
        s5p.Parser = S5p.Parse;
        s5p.Generator = S5p.Generate;

        var svp = FormatRegistry.Get(Format.Svp);
        svp.Parser = Svp.Parse;
        svp.Generator = Svp.Generate;

        var ccs = FormatRegistry.Get(Format.Ccs);
        ccs.Parser = Ccs.Parse;
        ccs.Generator = Ccs.Generate;

        var ustx = FormatRegistry.Get(Format.Ustx);
        ustx.Parser = Ustx.Parse;
        ustx.Generator = Ustx.Generate;

        var standardMid = FormatRegistry.Get(Format.StandardMid);
        standardMid.Parser = StandardMid.Parse;
        standardMid.Generator = StandardMid.Generate;

        var vsq = FormatRegistry.Get(Format.Vsq);
        vsq.Parser = Vsq.Parse;
        vsq.Generator = Vsq.Generate;

        var vocaloidMid = FormatRegistry.Get(Format.VocaloidMid);
        vocaloidMid.Parser = VocaloidMid.Parse;
        vocaloidMid.Generator = VocaloidMid.Generate;
        vocaloidMid.CustomMatcher = VsqLike.MatchFile;
    }
}
