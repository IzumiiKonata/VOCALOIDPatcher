using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
            Debug.ShowErrorMessage("未找到翻译文件夹! 请确保您将 \"VOCALOIDPatcher\" 文件夹也复制到了编辑器目录中");
            return;
        }

        BuildResourceIndex();
        LoadHardcodedMappings();

        AvailableLanguages.Clear();

        foreach (var file in Directory.GetFiles(TranslationsDir, "*.xml"))
        {
            var lang = Path.GetFileNameWithoutExtension(file);
            AvailableLanguages.Add(lang);
        }

        if (AvailableLanguages.Count == 0)
        {
            Debug.ShowErrorMessage("未找到任何翻译! 请确保您将 \"VOCALOIDPatcher\" 文件夹也复制到了编辑器目录中");
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

        Patcher.ConfigManager.Set("Language", configured);
    }

    private static string ResolveSystemLanguage()
    {
        try
        {
            var culture = CultureInfo.CurrentUICulture;
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
                        Debug.Print($"首次运行, 使用系统语言 {name} -> {lang}");
                        return lang;
                    }
            }
        }
        catch (Exception e)
        {
            Debug.Print($"匹配系统语言失败: {e.Message}");
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
                Debug.ShowErrorMessage("无法读取编辑器资源集");
                return;
            }

            foreach (DictionaryEntry entry in set)
            {
                if (entry.Key is not string key || entry.Value is not string original)
                    continue;

                OriginalByKey[key] = original;
                KeyByOriginal.TryAdd(original, key);
            }

            Debug.Print($"已加载 {OriginalByKey.Count} 条资源索引");
        }
        catch (Exception e)
        {
            Debug.ShowErrorMessage("构建资源索引失败", e);
        }
    }

    private static void LoadHardcodedMappings()
    {
        var path = Path.Combine(Patcher.DataDir, "HardcodedPropertyMap.xml");

        if (!File.Exists(path))
        {
            Debug.ShowErrorMessage("硬编码映射不存在: HardcodedPropertyMap.xml");
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
            Debug.ShowErrorMessage($"试图加载不存在的翻译: {language}.xml");
            return false;
        }

        Dict.Clear();
        TranslatedToOriginalMap.Clear();
        TranslatedToTranslationKeyMap.Clear();

        try
        {
            var doc = XDocument.Load(path);

            foreach (var data in doc.Descendants("data"))
            {
                var key = data.Attribute("name")?.Value;
                var value = data.Element("value")?.Value;

                if (key == null || value == null)
                    continue;

                if (!Dict.TryAdd(key, value))
                    continue;

                if (OriginalByKey.TryGetValue(key, out var original)
                    || HardcodedPropertyMappingReversed.TryGetValue(key, out original))
                {
                    TranslatedToOriginalMap[value] = original;
                    TranslatedToTranslationKeyMap[value] = key;
                }
            }

            CurrentLanguage = language;
            LanguageChanged?.Invoke(null, CurrentLanguage);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static string? Get(string key)
    {
        var value = Dict.GetValueOrDefault(key);

        if (value == null && MissingKeyList.Add(key)) Debug.Print($"Missing key: {key}");

        return value;
    }

    public static string? GetKeyByOriginal(string original)
    {
        return KeyByOriginal.GetValueOrDefault(original);
    }
}
