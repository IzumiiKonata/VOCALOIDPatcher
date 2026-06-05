namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Acep;

public sealed class AcepInputOptions
{
    public bool ImportPitch { get; set; } = true;
    public bool ImportBreath { get; set; } = true;
    public bool ImportGender { get; set; } = true;
    public bool ImportInstrumentalTrack { get; set; } = true;
    public bool KeepAllPronunciation { get; set; } = false;
    public bool ImportTension { get; set; } = true;
    public bool ImportEnergy { get; set; } = true;
    public double EnergyCoefficient { get; set; } = 0.5;
    public int CurveSampleInterval { get; set; } = 5;
    public AcepNormalizationArgument BreathNormalization { get; set; } = new();
    public AcepNormalizationArgument TensionNormalization { get; set; } = new();
    public AcepNormalizationArgument EnergyNormalization { get; set; } = new();
}

public sealed class AcepOutputOptions
{
    public string Singer { get; set; } = AcepSingers.DefaultSinger;
    public int Breath { get; set; } = 600;
    public StrengthMappingOption MapStrengthInfo { get; set; } = StrengthMappingOption.Both;
    public int SplitThreshold { get; set; } = 1;
    public AcepLyricsLanguage LyricLanguage { get; set; } = AcepLyricsLanguage.Chinese;
    public double DefaultConsonantLength { get; set; } = 0.0;
    public AcepSerialization Serialization { get; set; } = AcepSerialization.Json;
}
