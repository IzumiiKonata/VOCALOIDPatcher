using VOCALOIDPatcher.Formats.LibreSvip.Core;

namespace VOCALOIDPatcher.Formats.LibreSvip.Model;

public sealed class PortamentoPitch
{
    public double MaxInterTimeInSecs { get; }
    public double MaxInterTimePercent { get; }
    public InterpolationFunc InterFunc { get; }
    public bool VocaloidMode { get; }

    public PortamentoPitch(double maxInterTimeInSecs, double maxInterTimePercent, InterpolationFunc interFunc, bool vocaloidMode = false)
    {
        MaxInterTimeInSecs = maxInterTimeInSecs;
        MaxInterTimePercent = maxInterTimePercent;
        InterFunc = interFunc;
        VocaloidMode = vocaloidMode;
    }

    public static PortamentoPitch NoPortamento() =>
        new(0.0, 0.0, MusicMath.LinearInterpolation);

    public static PortamentoPitch VocaloidPortamento() =>
        new(0.05, 0.15, MusicMath.VocaloidInterpolation, true);
}
