using System.IO;
using System.Xml.Linq;
using VOCALOIDPatcher.Patch.Patches;
using VOCALOIDPatcher.Utils;
using Yamaha.VOCALOID.Properties;

namespace VOCALOIDPatcher.Translation;

public static class TranslationManager
{
    private static readonly Dictionary<string, string> Dict = new();

    public static List<string> AvailableLanguages { get; } = new();

    public static string? CurrentLanguage { get; private set; }

    private static readonly string TranslationsDir =
        Path.Combine(Patcher.DataDir, "translations");
    
    public static readonly Dictionary<string, string> HardcodedPropertyMapping = new(), HardcodedPropertyMappingReversed = new();

    public static event EventHandler<string> LanguageChanged; 

    public static void Initialize()
    {
        if (!Directory.Exists(TranslationsDir))
        {
            MessageUtils.ShowErrorMessage("未找到翻译文件夹! 请确保您将 \"VOCALOIDPatcher\" 文件夹也复制到了编辑器目录中");
            return;
        }
        
        LoadHardcodedMappings();

        AvailableLanguages.Clear();

        foreach (var file in Directory.GetFiles(TranslationsDir, "*.xml"))
        {
            var lang = Path.GetFileNameWithoutExtension(file);
            AvailableLanguages.Add(lang);
        }

        if (AvailableLanguages.Count == 0)
        {
            MessageUtils.ShowErrorMessage("未找到任何翻译! 请确保您将 \"VOCALOIDPatcher\" 文件夹也复制到了编辑器目录中");
            return;
        }

        if (!LoadLanguage(Patcher.ConfigManager.Get("Language", AvailableLanguages[0])))
        {
            LoadLanguage(AvailableLanguages[0]);
            Patcher.ConfigManager.Set("Language", AvailableLanguages[0]);
        }
    }

    private static void LoadHardcodedMappings()
    {
        var path = Path.Combine(Patcher.DataDir, "HardcodedPropertyMap.xml");
        
        if (!File.Exists(path))
        {
            MessageUtils.ShowErrorMessage($"硬编码映射不存在: HardcodedPropertyMap.xml");
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
        catch (Exception _)
        {
        }
    }

    public static bool LoadLanguage(string language)
    {
        var path = Path.Combine(TranslationsDir, language + ".xml");

        if (!File.Exists(path))
        {
            MessageUtils.ShowErrorMessage($"试图加载不存在的翻译: {language}.xml");
            return false;
        }

        Dict.Clear();
        // ResourceManagerPatch.ReversedMap.Clear();

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
                
                if (Dict.TryAdd(key, value))
                {
                    var reversed = ResourceManagerPatch.GetString(Resources.ResourceManager, key, null);
                    if (reversed != null)
                    {
                        TranslatedToOriginalMap[value] = reversed;
                        TranslatedToTranslationKeyMap[value] = key;
                        // MessageUtils.Dbg($"ReversedMap[{value}] = {reversed}");
                        // MessageUtils.Dbg($"TranslatedToTranslationKeyMap[{value}] = {key}");
                    }
                    else
                    {
                        if (HardcodedPropertyMappingReversed.TryGetValue(key, out var reversedValue))
                        {
                            TranslatedToOriginalMap[value] = reversedValue;
                            TranslatedToTranslationKeyMap[value] = key;
                            // MessageUtils.Dbg($"TranslatedToOriginalMap[{value}] = {reversedValue}");
                            // MessageUtils.Dbg($"TranslatedToTranslationKeyMap[{value}] = {key}");
                        }
                    }
                }
            }

            CurrentLanguage = language;
            LanguageChanged.Invoke(null, CurrentLanguage);
            return true;
        }
        catch (Exception _)
        {
            return false;
        }
    }

    private static readonly List<string> MissingKeyList = [];

    public static string? Get(string key)
    {
        var value = Dict.GetValueOrDefault(key);

        if (value == null && !MissingKeyList.Contains(key))
        {
            MessageUtils.Dbg($"Missing key: {key}");
            MissingKeyList.Add(key);
        }
        
        return value;
    }

    public static readonly Dictionary<string, string> TranslatedToOriginalMap = new();
    public static readonly Dictionary<string, string> TranslatedToTranslationKeyMap = new();
}