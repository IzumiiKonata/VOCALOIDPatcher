using System.IO;
using System.Xml.Linq;

namespace VOCALOIDPatcher.Translation;

public static class TranslationManager
{
    private static readonly Dictionary<string, string> _dict = new();

    public static List<string> AvailableLanguages { get; } = new();

    public static string CurrentLanguage { get; private set; }

    private static readonly string _translationsDir =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VOCALOIDPatcher", "translations");

    public static void Initialize(string defaultLanguage = null)
    {
        if (!Directory.Exists(_translationsDir))
            return;

        AvailableLanguages.Clear();

        foreach (var file in Directory.GetFiles(_translationsDir, "*.xml"))
        {
            var lang = Path.GetFileNameWithoutExtension(file);
            AvailableLanguages.Add(lang);
        }

        if (AvailableLanguages.Count == 0)
            return;

        if (!string.IsNullOrEmpty(defaultLanguage) && AvailableLanguages.Contains(defaultLanguage))
        {
            LoadLanguage(defaultLanguage);
        }
        else
        {
            LoadLanguage(AvailableLanguages[0]);
        }
    }

    public static bool LoadLanguage(string language)
    {
        var path = Path.Combine(_translationsDir, language + ".xml");

        if (!File.Exists(path))
            return false;

        _dict.Clear();

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

                if (!_dict.ContainsKey(key))
                {
                    _dict[key] = value;
                }
            }

            CurrentLanguage = language;
            return true;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    public static string? Get(string key)
    {
        if (_dict.TryGetValue(key, out var value))
            return value;

        return null;
    }
}