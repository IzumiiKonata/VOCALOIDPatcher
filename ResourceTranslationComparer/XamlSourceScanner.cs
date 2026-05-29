using System.Text.RegularExpressions;

namespace ResourceTranslationComparer;

public static class XamlSourceScanner
{
    public sealed class ScanResult
    {
        public HashSet<string> ResourceKeysUsed { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, SortedSet<string>> Literals { get; } = new(StringComparer.Ordinal); // 字面量 -> 来源文件
        public int XamlFileCount { get; set; }
    }

    // {x:Static [ns:]Resources.KEY}
    private static readonly Regex ResourceKeyRegex =
        new(@"Resources\.([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);

    // 标准文本属性：Header="..." / Content="..." / Text="..." / ToolTip="..."
    // (token 必须紧贴 = 号，避免误匹配 TextAlignment/TextWrapping 之类)
    private static readonly Regex AttrLiteralRegex =
        new(@"(?:Header|Content|Text|ToolTip)\s*=\s*""([^""{}<>]+)""", RegexOptions.Compiled);

    // 自定义"标签型"属性：名字里含 Label/Title/Caption 的依赖属性，例如
    //   ExpressionSliderTitleLabel="EXHALATION"、VoiceColorLabelT="HARD"、VoiceColorLabelB="SOFT"
    private static readonly Regex CustomLabelAttrRegex =
        new(@"\b[A-Za-z_][\w.:]*(?:Label|Title|Caption)[A-Za-z]*\s*=\s*""([^""{}<>]+)""", RegexOptions.Compiled);

    // <TextBlock ...>inner</TextBlock> 等元素内嵌文本
    private static readonly Regex InnerTextRegex =
        new(@"<(?:TextBlock|Run|Label|AccessText)\b[^>]*>([^<>{}]+)</(?:TextBlock|Run|Label|AccessText)>",
            RegexOptions.Compiled);

    // 形如 "M 0 0 L 4 4 L 0 8" 的 Path 几何数据
    private static readonly Regex PathGeometryRegex =
        new(@"^[MmLlHhVvCcSsQqTtAaZz][\sMmLlHhVvCcSsQqTtAaZz0-9.,+-]*$", RegexOptions.Compiled);

    public static ScanResult Scan(string v6srcDir)
    {
        var result = new ScanResult();

        if (!Directory.Exists(v6srcDir))
            throw new DirectoryNotFoundException($"V6 源码目录不存在: {v6srcDir}");

        foreach (var file in Directory.EnumerateFiles(v6srcDir, "*.xaml", SearchOption.AllDirectories))
        {
            result.XamlFileCount++;
            var text = File.ReadAllText(file);
            var relative = Path.GetFileName(file);

            foreach (Match m in ResourceKeyRegex.Matches(text))
                result.ResourceKeysUsed.Add(m.Groups[1].Value);

            foreach (Match m in AttrLiteralRegex.Matches(text))
                AddLiteral(result, m.Groups[1].Value, relative);

            foreach (Match m in CustomLabelAttrRegex.Matches(text))
                AddLiteral(result, m.Groups[1].Value, relative);

            foreach (Match m in InnerTextRegex.Matches(text))
                AddLiteral(result, m.Groups[1].Value, relative);
        }

        return result;
    }

    private static void AddLiteral(ScanResult result, string raw, string sourceFile)
    {
        var literal = DecodeXml(raw).Trim();

        if (!IsHumanText(literal))
            return;

        if (!result.Literals.TryGetValue(literal, out var files))
            result.Literals[literal] = files = new SortedSet<string>(StringComparer.Ordinal);

        files.Add(sourceFile);
    }

    // 滤掉明显不是 UI 文本的字面量
    private static bool IsHumanText(string s)
    {
        if (s.Length < 2)
            return false;

        if (!s.Any(char.IsLetter))
            return false;

        if (PathGeometryRegex.IsMatch(s)) // Path 几何数据 "M 0 0 L 4 4 ..."
            return false;

        if (s is "XXX" or "eng" || Regex.IsMatch(s, @"^Ver\s+X", RegexOptions.IgnoreCase)) // 占位符
            return false;

        return true;
    }

    private static string DecodeXml(string s) =>
        s.Replace("&amp;", "&")
         .Replace("&lt;", "<")
         .Replace("&gt;", ">")
         .Replace("&quot;", "\"")
         .Replace("&apos;", "'")
         .Replace("&#10;", "\n")
         .Replace("&#13;", "\r")
         .Replace("&#x0a;", "\n")
         .Replace("&#x0d;", "\r");
}
