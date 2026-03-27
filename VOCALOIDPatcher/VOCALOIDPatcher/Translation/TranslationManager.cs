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
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VOCALOIDPatcher", "translations");

    public static void Initialize()
    {
        if (!Directory.Exists(TranslationsDir))
        {
            MessageUtils.ShowErrorMessage("未找到翻译文件夹! 请确保您将 \"VOCALOIDPatcher\" 文件夹也复制到了编辑器目录中");
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
            MessageUtils.ShowErrorMessage("未找到任何翻译! 请确保您将 \"VOCALOIDPatcher\" 文件夹也复制到了编辑器目录中");
            return;
        }

        if (!LoadLanguage(Patcher.ConfigManager.Get("Language", AvailableLanguages[0])))
        {
            LoadLanguage(AvailableLanguages[0]);
            Patcher.ConfigManager.Set("Language", AvailableLanguages[0]);
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
                        ResourceManagerPatch.ReversedMap[value] = reversed;
                }
            }

            CurrentLanguage = language;
            return true;
        }
        catch (Exception _)
        {
            return false;
        }
    }

    public static string? Get(string key)
    {
        return Dict.GetValueOrDefault(key);
    }
}