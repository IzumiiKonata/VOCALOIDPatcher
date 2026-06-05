using System.Globalization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Acep;

public sealed class AcepNormalizationArgument
{
    public AcepNormalizationMethod NormalizeMethod { get; set; } = AcepNormalizationMethod.None;
    public double LowerThreshold { get; set; } = 0;
    public double UpperThreshold { get; set; } = 10;
    public double Scale { get; set; } = 0;
    public double Bias { get; set; } = 0;

    public bool Enabled => NormalizeMethod != AcepNormalizationMethod.None;

    public static AcepNormalizationArgument FromStr(string value)
    {
        var result = new AcepNormalizationArgument();
        if (string.IsNullOrWhiteSpace(value))
            return result;
        var parts = value.Split(',');
        if (parts.Length > 0)
            result.NormalizeMethod = ParseMethod(parts[0]);
        if (parts.Length > 1 && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lower))
            result.LowerThreshold = lower;
        if (parts.Length > 2 && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var upper))
            result.UpperThreshold = upper;
        if (parts.Length > 3 && double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var scale))
            result.Scale = scale;
        if (parts.Length > 4 && double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var bias))
            result.Bias = bias;
        return result;
    }

    private static AcepNormalizationMethod ParseMethod(string value) => value.Trim().ToLowerInvariant() switch
    {
        "zscore" => AcepNormalizationMethod.ZScore,
        "minmax" => AcepNormalizationMethod.MinMax,
        _ => AcepNormalizationMethod.None,
    };
}
