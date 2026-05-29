namespace VOCALOIDPatcher.Config;

public static class Settings
{
    public static string TranslateHardcodedStringsKey => "TranslateHardcodedStrings";

    public static bool TranslateHardcodedStrings
    {
        get => Patcher.ConfigManager.Get(TranslateHardcodedStringsKey, true);
        set => Patcher.ConfigManager.Set(TranslateHardcodedStringsKey, value);
    }

    public static string ShowOtherTracksNotesKey => "ShowOtherTracksNotes";

    public static bool ShowOtherTracksNotes
    {
        get => Patcher.ConfigManager.Get(ShowOtherTracksNotesKey, false);
        set => Patcher.ConfigManager.Set(ShowOtherTracksNotesKey, value);
    }

    public static string ShowOtherTracksSkipMutedKey => "ShowOtherTracksSkipMuted";

    public static bool ShowOtherTracksSkipMuted
    {
        get => Patcher.ConfigManager.Get(ShowOtherTracksSkipMutedKey, false);
        set => Patcher.ConfigManager.Set(ShowOtherTracksSkipMutedKey, value);
    }

    public static string ShowCharacterArtKey => "ShowCharacterArt";

    public static bool ShowCharacterArt
    {
        get => Patcher.ConfigManager.Get(ShowCharacterArtKey, false);
        set => Patcher.ConfigManager.Set(ShowCharacterArtKey, value);
    }

    public static string ShowNotePitchKey => "ShowNotePitch";

    public static bool ShowNotePitch
    {
        get => Patcher.ConfigManager.Get(ShowNotePitchKey, false);
        set => Patcher.ConfigManager.Set(ShowNotePitchKey, value);
    }

    public static string AutoSaveEnabledKey => "AutoSaveEnabled";

    public static bool AutoSaveEnabled
    {
        get => Patcher.ConfigManager.Get(AutoSaveEnabledKey, false);
        set => Patcher.ConfigManager.Set(AutoSaveEnabledKey, value);
    }

    public static string AutoSaveIntervalMinutesKey => "AutoSaveIntervalMinutes";

    public static int AutoSaveIntervalMinutes
    {
        get => Patcher.ConfigManager.Get(AutoSaveIntervalMinutesKey, 5);
        set => Patcher.ConfigManager.Set(AutoSaveIntervalMinutesKey, value);
    }
}
