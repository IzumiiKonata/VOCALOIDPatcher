using System.Collections;
using System.Reflection;
using System.Resources;
using System.Text.Json.Nodes;
using System.Xml.Linq;

public static class ResourceTranslationComparer
{
    public static void Main()
    {
        string dllPath = "C:\\Program Files\\VOCALOID6\\Editor\\VOCALOID6.dll";
        string xmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\..\\..\\..\\translations\\中文 (简体).xml");

        var result = Compare(dllPath, xmlPath);

        Console.WriteLine();
        Console.WriteLine("========== 比较摘要 ==========");
        Console.WriteLine($"DLL 资源总数 : {result.TotalInDll}");
        Console.WriteLine($"XML 翻译总数 : {result.TotalInXml}");
        Console.WriteLine($"已翻译       : {result.TranslatedEntries.Count}");
        Console.WriteLine($"缺失 (待翻译): {result.MissingInXml.Count}");
        Console.WriteLine($"过时 (可清理): {result.ObsoleteInXml.Count}");
        if (result.TotalInDll > 0)
        {
            double coverage = result.TranslatedEntries.Count * 100.0 / result.TotalInDll;
            Console.WriteLine($"翻译覆盖率   : {coverage:F1}%");
        }
    }
    
    private const string ResourceBaseName = "Yamaha.VOCALOID.Properties.Resources";

    public static CompareResult Compare(string dllPath, string xmlTranslationPath)
    {
        if (!File.Exists(dllPath))
            throw new FileNotFoundException($"DLL 文件不存在: {dllPath}");
        if (!File.Exists(xmlTranslationPath))
            throw new FileNotFoundException($"翻译 XML 不存在: {xmlTranslationPath}");

        var dllEntries = ExtractFromDll(dllPath);
        var xmlEntries = ReadTranslationXml(xmlTranslationPath);
        var result = BuildResult(dllEntries, xmlEntries);

        PrintMissingToConsole(result);

        return result;
    }

    private static Dictionary<string, string> ExtractFromDll(string dllPath)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        Assembly asm;
        try
        {
            asm = Assembly.LoadFile(Path.GetFullPath(dllPath));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"无法加载 DLL: {ex.Message}", ex);
        }

        var resourceNames = asm.GetManifestResourceNames();
        var targetStream = resourceNames.FirstOrDefault(n =>
            n.Equals(ResourceBaseName + ".resources", StringComparison.OrdinalIgnoreCase));

        if (targetStream != null)
        {
            using var stream = asm.GetManifestResourceStream(targetStream)!;
            using var reader = new ResourceReader(stream);
            foreach (DictionaryEntry entry in reader)
                if (entry.Value is string str)
                    result[entry.Key.ToString()!] = str;
            return result;
        }

        try
        {
            var rm = new ResourceManager(ResourceBaseName, asm);
            var set = rm.GetResourceSet(
                System.Globalization.CultureInfo.InvariantCulture,
                createIfNotExists: true,
                tryParents: true);

            if (set != null)
                foreach (DictionaryEntry entry in set)
                    if (entry.Value is string str)
                        result[entry.Key.ToString()!] = str;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"ResourceManager 加载失败，请确认资源基名 '{ResourceBaseName}' 正确。\n详情: {ex.Message}", ex);
        }

        if (result.Count == 0)
        {
            Console.Error.WriteLine("[警告] 未提取到任何字符串资源，DLL 内嵌资源列表：");
            foreach (var n in resourceNames)
                Console.Error.WriteLine($"  {n}");
        }

        return result;
    }

    private static Dictionary<string, string> ReadTranslationXml(string path)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var doc = XDocument.Load(path);
        foreach (var dataEl in doc.Descendants("data"))
        {
            var name = dataEl.Attribute("name")?.Value;
            if (!string.IsNullOrEmpty(name))
                result[name!] = dataEl.Element("value")?.Value ?? string.Empty;
        }
        return result;
    }

    private static CompareResult BuildResult(
        Dictionary<string, string> dllEntries,
        Dictionary<string, string> xmlEntries)
    {
        var missing = dllEntries
            .Where(kv => !xmlEntries.ContainsKey(kv.Key))
            .OrderBy(kv => kv.Key)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        var obsolete = xmlEntries
            .Where(kv => !dllEntries.ContainsKey(kv.Key))
            .OrderBy(kv => kv.Key)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        var translated = dllEntries
            .Where(kv => xmlEntries.ContainsKey(kv.Key))
            .OrderBy(kv => kv.Key)
            .ToDictionary(kv => kv.Key, kv => xmlEntries[kv.Key]);

        return new CompareResult
        {
            TotalInDll        = dllEntries.Count,
            TotalInXml        = xmlEntries.Count,
            MissingInXml      = missing,
            ObsoleteInXml     = obsolete,
            TranslatedEntries = translated
        };
    }

    private static void PrintMissingToConsole(CompareResult result)
    {
        foreach (var kv in result.MissingInXml)
            Console.WriteLine($"  <data name=\"{kv.Key}\" xml:space=\"preserve\"><value>{kv.Value}</value></data>");
    }

    public static void ListDllResourceNames(string dllPath)
    {
        var asm = Assembly.LoadFile(Path.GetFullPath(dllPath));
        foreach (var name in asm.GetManifestResourceNames())
            Console.WriteLine(name);
    }
    
    public class CompareResult
    {
        public int TotalInDll { get; set; }
        public int TotalInXml { get; set; }
        public Dictionary<string, string> MissingInXml { get; set; } = new();
        public Dictionary<string, string> ObsoleteInXml { get; set; } = new();
        public Dictionary<string, string> TranslatedEntries { get; set; } = new();
    }
}

