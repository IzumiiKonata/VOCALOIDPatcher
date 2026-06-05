using VOCALOIDPatcher.Formats.LibreSvip.Core;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ustx;

public static class UstxShapeInterpolation
{
    public static double InterpolateShape(
        (double X, double Y) start, (double X, double Y) end, double x, string? shape)
    {
        return shape switch
        {
            "io" => MusicMath.CosineEasingInOutInterpolation(x, start, end),
            "i" => MusicMath.CosineEasingInInterpolation(x, start, end),
            "o" => MusicMath.CosineEasingOutInterpolation(x, start, end),
            "sp" => MusicMath.CubicInterpolation(x, start, end),
            _ => MusicMath.LinearInterpolation(x, start, end),
        };
    }
}
