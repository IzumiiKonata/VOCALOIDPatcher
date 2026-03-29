namespace VOCALOIDPatcher.Config;

public static class Settings
{
    public static string TranslateHardcodedStringsKey => "TranslateHardcodedStrings";
    public static bool TranslateHardcodedStrings
    {
        get => Patcher.ConfigManager.Get(TranslateHardcodedStringsKey, true);
        set => Patcher.ConfigManager.Set(TranslateHardcodedStringsKey, value);
    }
}