using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Xml.Linq;

namespace ResourceTranslationComparer;

public static class Program
{
    private const string ResourceBaseName = "Yamaha.VOCALOID.Properties.Resources";

    // Patcher 添加的翻译键
    private static readonly string[] PatcherOwnedKeyPrefixes = { "VOCALOIDPatcher" };

    public static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var repoRoot = FindRepoRoot();

        var dllPath = args.FirstOrDefault(a => a.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                      ?? @"C:\Program Files\VOCALOID6\Editor\VOCALOID6.dll";
        var translationPath = Path.Combine(repoRoot, "translations", "中文 (简体).xml");
        var hardcodedMapPath = Path.Combine(repoRoot, "HardcodedPropertyMap.xml");
        var v6srcDir = Path.Combine(repoRoot, "V6src");

        if (!File.Exists(dllPath))
        {
            Console.Error.WriteLine($"[错误] DLL 不存在: {dllPath}");
            return;
        }

        var dll = ExtractFromDll(dllPath);                       // 资源键 -> 英文原文
        var translation = ReadTranslationXml(translationPath);   // 资源键/硬编码键 -> 译文
        var hardcoded = ReadHardcodedMap(hardcodedMapPath);      // 字面量 -> 硬编码键
        var scan = XamlSourceScanner.Scan(v6srcDir);             // XAML 引用的键 + 字面量

        var report = Analyze(dll, translation, hardcoded, scan);
        PrintReport(report, dll, translation, hardcoded, scan);

        if (args.Contains("--write-map"))
        {
            WriteCleanedHardcodedMap(hardcodedMapPath, hardcoded, report, scan);
        }
    }

    private sealed class Report
    {
        public SortedDictionary<string, string> MissingUsed = new(StringComparer.Ordinal);          // XAML 用到、resx 有、没翻译
        public SortedDictionary<string, string> MissingUnused = new(StringComparer.Ordinal);        // resx 有、没翻译、XAML 没直接引用
        public SortedDictionary<string, SortedSet<string>> UnregisteredLiterals = new(StringComparer.Ordinal); // XAML 写死、未收录的字面量
        public SortedDictionary<string, string> ObsoleteTranslationKeys = new(StringComparer.Ordinal); // 翻译里有、resx 和硬编码都不认识
        public SortedDictionary<string, string> ObsoleteHardcoded = new(StringComparer.Ordinal);    // 收录了、但源码里找不到的字面量
    }

    private static Report Analyze(
        Dictionary<string, string> dll,
        Dictionary<string, string> translation,
        Dictionary<string, string> hardcoded,
        XamlSourceScanner.ScanResult scan)
    {
        var report = new Report();
        var dllEnglishValues = new HashSet<string>(dll.Values, StringComparer.Ordinal);
        var hardcodedKeys = new HashSet<string>(hardcoded.Values, StringComparer.Ordinal);

        foreach (var (key, english) in dll)
        {
            if (translation.ContainsKey(key))
                continue;

            if (scan.ResourceKeysUsed.Contains(key))
                report.MissingUsed[key] = english;
            else
                report.MissingUnused[key] = english;
        }

        foreach (var (literal, files) in scan.Literals)
        {
            if (dllEnglishValues.Contains(literal))
                continue;
            if (hardcoded.ContainsKey(literal))
                continue;

            report.UnregisteredLiterals[literal] = files;
        }

        foreach (var (key, translated) in translation)
        {
            if (dll.ContainsKey(key) || hardcodedKeys.Contains(key))
                continue;
            if (PatcherOwnedKeyPrefixes.Any(p => key.StartsWith(p, StringComparison.Ordinal)))
                continue;

            report.ObsoleteTranslationKeys[key] = translated;
        }

        foreach (var (literal, key) in hardcoded)
        {
            if (!scan.Literals.ContainsKey(literal))
                report.ObsoleteHardcoded[literal] = key;
        }

        return report;
    }

    private static void PrintReport(
        Report report,
        Dictionary<string, string> dll,
        Dictionary<string, string> translation,
        Dictionary<string, string> hardcoded,
        XamlSourceScanner.ScanResult scan)
    {
        Console.WriteLine($"编辑器中的翻译键数量    : {dll.Count}");
        Console.WriteLine($"翻译条目(简体): {translation.Count}");
        Console.WriteLine($"硬编码映射    : {hardcoded.Count}");
        Console.WriteLine($"XAML 文件     : {scan.XamlFileCount}");
        Console.WriteLine($"XAML 引用的键 : {scan.ResourceKeysUsed.Count}");
        Console.WriteLine($"XAML 字面量   : {scan.Literals.Count}");
        Console.WriteLine();

        PrintSection(
            $"[1] 未翻译条目 ({report.MissingUsed.Count})",
            report.MissingUsed.Select(kv => $"  <data name=\"{kv.Key}\" xml:space=\"preserve\"><value>{kv.Value}</value></data>"));

        PrintSection(
            $"[2] 未翻译且编辑器未使用条目 ({report.MissingUnused.Count})",
            report.MissingUnused.Select(kv => $"  <data name=\"{kv.Key}\" xml:space=\"preserve\"><value>{kv.Value}</value></data>"));

        PrintSection(
            $"[3] 硬编码字符串, 添加到 HardcodedPropertyMap ({report.UnregisteredLiterals.Count})",
            report.UnregisteredLiterals.Select(kv => $"  \"{kv.Key}\"  (来源: {string.Join(", ", kv.Value)})"));

        PrintSection(
            $"[4] 多余键 ({report.ObsoleteTranslationKeys.Count})",
            report.ObsoleteTranslationKeys.Select(kv => $"  {kv.Key} = {kv.Value}"));

        PrintSection(
            $"[5] 未使用的硬编码字符串 ({report.ObsoleteHardcoded.Count})",
            report.ObsoleteHardcoded.Select(kv => $"  \"{kv.Key}\" -> {kv.Value}"));

        var translatableTotal = dll.Count;
        var translated = dll.Keys.Count(translation.ContainsKey);
        if (translatableTotal > 0)
            Console.WriteLine($"翻译覆盖率  : {translated * 100.0 / translatableTotal:F1}%  ({translated}/{translatableTotal})");
    }

    private static void PrintSection(string title, IEnumerable<string> lines)
    {
        Console.WriteLine(title);
        var any = false;
        foreach (var line in lines)
        {
            Console.WriteLine(line);
            any = true;
        }
        if (!any)
            Console.WriteLine("  (无)");
        Console.WriteLine();
    }

    private static void WriteCleanedHardcodedMap(
        string hardcodedMapPath,
        Dictionary<string, string> hardcoded,
        Report report,
        XamlSourceScanner.ScanResult scan)
    {
        var outPath = Path.ChangeExtension(hardcodedMapPath, ".generated.xml");
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<root>");

        foreach (var (literal, key) in hardcoded.OrderBy(kv => kv.Value, StringComparer.Ordinal))
        {
            sb.AppendLine($"  <data name=\"{Escape(literal)}\" xml:space=\"preserve\"><value>{Escape(key)}</value></data>");
        }

        if (report.UnregisteredLiterals.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("  <!-- 新的硬编码字符串 -->");
            foreach (var (literal, files) in report.UnregisteredLiterals)
            {
                var suggestedKey = SuggestKey(literal, files);
                sb.AppendLine($"  <data name=\"{Escape(literal)}\" xml:space=\"preserve\"><value>{Escape(suggestedKey)}</value></data>");
            }
        }

        sb.AppendLine("</root>");
        File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(true));
        Console.WriteLine($"已导出整理后的硬编码映射: {outPath}");
        Console.WriteLine();
    }

    private static string SuggestKey(string literal, IEnumerable<string> files)
    {
        var fileBase = Path.GetFileNameWithoutExtension(files.First())
            .Replace("Yamaha.VOCALOID.", "")
            .Replace(".", "_");
        var slug = new string(literal.ToUpperInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray())
            .Trim('_');
        while (slug.Contains("__"))
            slug = slug.Replace("__", "_");
        return $"{fileBase}_{slug}_Header";
    }

    private static string Escape(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "VOCALOIDPatcher.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        // 找不到就从 bin/Debug/netX.0 往上回退 4 级
        return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
    }

    private static Dictionary<string, string> ExtractFromDll(string dllPath)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var asm = Assembly.LoadFile(Path.GetFullPath(dllPath));

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

        var rm = new ResourceManager(ResourceBaseName, asm);
        var set = rm.GetResourceSet(CultureInfo.InvariantCulture, true, true);
        if (set != null)
            foreach (DictionaryEntry entry in set)
                if (entry.Value is string str)
                    result[entry.Key.ToString()!] = str;

        return result;
    }

    private static Dictionary<string, string> ReadTranslationXml(string path)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(path))
            return result;
        foreach (var dataEl in XDocument.Load(path).Descendants("data"))
        {
            var name = dataEl.Attribute("name")?.Value;
            if (!string.IsNullOrEmpty(name))
                result[name!] = dataEl.Element("value")?.Value ?? string.Empty;
        }
        return result;
    }

    private static Dictionary<string, string> ReadHardcodedMap(string path)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(path))
            return result;
        foreach (var dataEl in XDocument.Load(path).Descendants("data"))
        {
            var name = dataEl.Attribute("name")?.Value;
            var value = dataEl.Element("value")?.Value;
            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(value))
                result[name!] = value!;
        }
        return result;
    }
}
