namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Acep;

public enum AcepLyricsLanguage
{
    Chinese,
    Japanese,
    English,
    Spanish,
    Korean,
    Portuguese,
    French,
    Italian,
}

public enum AcepSerialization
{
    Json,
    Cbor,
}

public enum StrengthMappingOption
{
    Both,
    Energy,
    Tension,
}

public enum AcepNormalizationMethod
{
    None,
    ZScore,
    MinMax,
}

public static class AcepLyricsLanguageHelper
{
    public static string ToCode(AcepLyricsLanguage language) => language switch
    {
        AcepLyricsLanguage.Chinese => "CHN",
        AcepLyricsLanguage.Japanese => "JPN",
        AcepLyricsLanguage.English => "ENG",
        AcepLyricsLanguage.Spanish => "SPA",
        AcepLyricsLanguage.Korean => "KOR",
        AcepLyricsLanguage.Portuguese => "POR",
        AcepLyricsLanguage.French => "FRA",
        AcepLyricsLanguage.Italian => "ITA",
        _ => "CHN",
    };

    public static AcepLyricsLanguage FromCode(string? code) => code switch
    {
        "CHN" => AcepLyricsLanguage.Chinese,
        "JPN" => AcepLyricsLanguage.Japanese,
        "ENG" => AcepLyricsLanguage.English,
        "SPA" => AcepLyricsLanguage.Spanish,
        "KOR" => AcepLyricsLanguage.Korean,
        "POR" => AcepLyricsLanguage.Portuguese,
        "FRA" => AcepLyricsLanguage.French,
        "ITA" => AcepLyricsLanguage.Italian,
        _ => AcepLyricsLanguage.Chinese,
    };
}
