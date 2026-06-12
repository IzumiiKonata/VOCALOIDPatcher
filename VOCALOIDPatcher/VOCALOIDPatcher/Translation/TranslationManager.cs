using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using VOCALOIDPatcher.Utils;
using Yamaha.VOCALOID.Properties;

namespace VOCALOIDPatcher.Translation;

public static class TranslationManager
{
    private static readonly Dictionary<string, string> Dict = new();

    private static readonly Dictionary<string, string> KeyByOriginal = new();
    private static readonly Dictionary<string, string> OriginalByKey = new();

    private static readonly string TranslationsDir =
        Path.Combine(Patcher.DataDir, "translations");

    public static readonly Dictionary<string, string> HardcodedPropertyMapping = new(),
        HardcodedPropertyMappingReversed = new();

    public static readonly Dictionary<string, string> TranslatedToOriginalMap = new();
    public static readonly Dictionary<string, string> TranslatedToTranslationKeyMap = new();

    private static readonly HashSet<string> MissingKeyList = new();

    public static List<string> AvailableLanguages { get; } = new();

    public static string? CurrentLanguage { get; private set; }

    public static event EventHandler<string>? LanguageChanged;

    public static void Initialize()
    {
        if (!Directory.Exists(TranslationsDir))
        {
            Debug.ShowErrorMessage(Tr("VOCALOIDPatcher_Debug_Translation_FolderNotFound"));
            return;
        }

        AvailableLanguages.Clear();

        foreach (var file in Directory.GetFiles(TranslationsDir, "*.xml"))
        {
            var lang = Path.GetFileNameWithoutExtension(file);
            AvailableLanguages.Add(lang);
        }

        if (AvailableLanguages.Count == 0)
        {
            Debug.ShowErrorMessage(Tr("VOCALOIDPatcher_Debug_Translation_NoTranslations"));
            return;
        }

        var configured = Patcher.ConfigManager.Contains("Language")
            ? Patcher.ConfigManager.Get("Language", AvailableLanguages[0])
            : ResolveSystemLanguage();

        if (!LoadLanguage(configured))
        {
            configured = AvailableLanguages[0];
            LoadLanguage(configured);
        }

        BuildResourceIndex();
        LoadHardcodedMappings();
        RebuildReverseMaps();

        Patcher.ConfigManager.Set("Language", configured);
    }

    [DllImport("kernel32.dll")]
    private static extern ushort GetUserDefaultUILanguage();

    private static CultureInfo GetUserUiCulture()
    {
        try
        {
            return CultureInfo.GetCultureInfo(GetUserDefaultUILanguage());
        }
        catch
        {
            return CultureInfo.InstalledUICulture;
        }
    }

    private static string ResolveSystemLanguage()
    {
        try
        {
            var culture = GetUserUiCulture();
            if (culture.TwoLetterISOLanguageName == "zh")
            {
                var name = culture.Name;
                var traditional = name.Contains("Hant", StringComparison.OrdinalIgnoreCase)
                                  || name.EndsWith("-TW", StringComparison.OrdinalIgnoreCase)
                                  || name.EndsWith("-HK", StringComparison.OrdinalIgnoreCase)
                                  || name.EndsWith("-MO", StringComparison.OrdinalIgnoreCase);

                var preferred = traditional
                    ? new[] { "中文 (繁體)", "中文 (简体)" }
                    : new[] { "中文 (简体)", "中文 (繁體)" };

                foreach (var lang in preferred)
                    if (AvailableLanguages.Contains(lang))
                    {
                        Debug.Print(Tr("VOCALOIDPatcher_Debug_Translation_FirstRunSystemLang", name, lang));
                        return lang;
                    }
            }
        }
        catch (Exception e)
        {
            Debug.Print(Tr("VOCALOIDPatcher_Debug_Translation_MatchSystemLangFailed", e.Message));
        }

        return AvailableLanguages.Contains("English") ? "English" : AvailableLanguages[0];
    }

    private static void BuildResourceIndex()
    {
        KeyByOriginal.Clear();
        OriginalByKey.Clear();

        try
        {
            var set = Resources.ResourceManager.GetResourceSet(CultureInfo.InvariantCulture, true, true);

            if (set == null)
            {
                Debug.ShowErrorMessage(Tr("VOCALOIDPatcher_Debug_Translation_ResourceSetUnavailable"));
                return;
            }

            foreach (DictionaryEntry entry in set)
            {
                if (entry.Key is not string key || entry.Value is not string original)
                    continue;

                OriginalByKey[key] = original;
                KeyByOriginal.TryAdd(original, key);
            }

            Debug.Print(Tr("VOCALOIDPatcher_Debug_Translation_ResourceIndexLoaded", OriginalByKey.Count));
        }
        catch (Exception e)
        {
            Debug.ShowErrorMessage(Tr("VOCALOIDPatcher_Debug_Translation_BuildResourceIndexFailed"), e);
        }
    }

    private static void LoadHardcodedMappings()
    {
        var path = Path.Combine(Patcher.DataDir, "HardcodedPropertyMap.xml");

        if (!File.Exists(path))
        {
            Debug.ShowErrorMessage(Tr("VOCALOIDPatcher_Debug_Translation_HardcodedMapMissing"));
            return;
        }

        try
        {
            var doc = XDocument.Load(path);

            foreach (var data in doc.Descendants("data"))
            {
                var keyAttr = data.Attribute("name");
                var valueElement = data.Element("value");

                if (keyAttr == null || valueElement == null)
                    continue;

                var key = keyAttr.Value;
                var value = valueElement.Value;

                HardcodedPropertyMapping.TryAdd(key, value);
                HardcodedPropertyMappingReversed.TryAdd(value, key);
            }
        }
        catch (Exception)
        {
        }
    }

    public static bool LoadLanguage(string language)
    {
        var path = Path.Combine(TranslationsDir, language + ".xml");

        if (!File.Exists(path))
        {
            Debug.ShowErrorMessage(Tr("VOCALOIDPatcher_Debug_Translation_LoadMissingLanguage", language));
            return false;
        }

        Dict.Clear();

        try
        {
            var doc = XDocument.Load(path);

            foreach (var data in doc.Descendants("data"))
            {
                var key = data.Attribute("name")?.Value;
                var value = data.Element("value")?.Value;

                if (key == null || value == null)
                    continue;

                Dict.TryAdd(key, value);
            }

            CurrentLanguage = language;
            RebuildReverseMaps();
            LanguageChanged?.Invoke(null, CurrentLanguage);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void RebuildReverseMaps()
    {
        TranslatedToOriginalMap.Clear();
        TranslatedToTranslationKeyMap.Clear();

        foreach (var pair in Dict)
        {
            if (OriginalByKey.TryGetValue(pair.Key, out var original)
                || HardcodedPropertyMappingReversed.TryGetValue(pair.Key, out original))
            {
                TranslatedToOriginalMap[pair.Value] = original;
                TranslatedToTranslationKeyMap[pair.Value] = pair.Key;
            }
        }
    }

    public static string? Get(string key)
    {
        var value = Dict.GetValueOrDefault(key);

        if (value == null && MissingKeyList.Add(key)) Debug.Print($"Missing key: {key}");

        return value;
    }

    public static string Tr(string key) => Get(key) ?? key;

    public static string Tr(string key, params object?[] args)
    {
        var format = Get(key) ?? key;

        if (args.Length == 0)
            return format;

        try
        {
            return string.Format(format, args);
        }
        catch (FormatException)
        {
            return format;
        }
    }

    public static string? GetKeyByOriginal(string original)
    {
        return KeyByOriginal.GetValueOrDefault(original);
    }
}
