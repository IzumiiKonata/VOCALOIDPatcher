using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Svip;

internal static class SvipSingers
{
    private static readonly Dictionary<string, string> IdToName = new()
    {
        ["XiaoIce"] = "小冰", ["M3"] = "杨以凡", ["M13"] = "陈子渝", ["M803"] = "挚彬同学",
        ["M806"] = "雲宇光", ["M810"] = "辉宇·星", ["M812"] = "陆思川", ["M820"] = "严清语",
        ["M822"] = "泰伦", ["M825"] = "雨宫晓", ["M832"] = "埃洛", ["M835"] = "小格",
        ["M841"] = "五世百米", ["M844"] = "翱天", ["M848"] = "洛希", ["M850"] = "玖辰",
        ["M853"] = "九棠Twi", ["M905"] = "孙枫", ["M906"] = "袁率", ["M907"] = "稻光",
        ["M909"] = "豪叶", ["M910"] = "冰冰火", ["M911"] = "云灏", ["M916"] = "沐凌弦",
        ["M917"] = "白鬼月", ["M918"] = "枫月", ["M932"] = "遂安", ["M935"] = "十瑚",
        ["M937"] = "墨讱_DΞfαuΙΓ", ["M938"] = "柯泰璃", ["M946"] = "殷子之瀛", ["M958"] = "霖漓",
        ["M959"] = "雾怜之", ["M966"] = "墨皑", ["M972"] = "云野",
        ["F10"] = "陈水若", ["F11"] = "何畅", ["F801"] = "徐爱颜", ["F802"] = "荼鸢",
        ["F809"] = "海莉", ["F813"] = "蔷芜", ["F814"] = "幻神·米丝", ["F819"] = "方念",
        ["F827"] = "大宝剑", ["F828"] = "舞语", ["F830"] = "绮萱", ["F836"] = "果妹",
        ["F838"] = "小夜", ["F840"] = "Arya", ["F842"] = "曦和 律", ["F843"] = "小傻",
        ["F845"] = "瑶(YAO)", ["F846"] = "月柬依", ["F849"] = "娜芙(Nerv)", ["F851"] = "楚瓷",
        ["F852"] = "时久琉", ["F854"] = "叶妮娅(Еня)", ["F901"] = "麦笛奈", ["F903"] = "尘霜",
        ["F908"] = "虞宁xs", ["F912"] = "寺倾花", ["F914"] = "时枝", ["F922"] = "艾尔法",
        ["F923"] = "狐狸座", ["F924"] = "华智冰", ["F931"] = "诗芸", ["F933"] = "小珞",
        ["F934"] = "潘", ["F939"] = "月月", ["F940"] = "溯羽", ["F941"] = "君凝华",
        ["F942"] = "枫聆月Lyria", ["F944"] = "破晓 AkiRA", ["F949"] = "雨令", ["F961"] = "炉柚RoYo",
        ["F964"] = "桃灼", ["F965"] = "奈月", ["F968"] = "冰雪唐", ["F971"] = "安可",
        ["F974"] = "十叶剡", ["0"] = "观雨",
    };

    private static readonly Dictionary<string, string> NameToId = BuildInverse();

    private static readonly Regex IdPattern = new(@"^[FM]\d+", RegexOptions.Compiled);
    private static readonly Regex WrappedIdPattern = new(@"^\$\([FM]\d+\)", RegexOptions.Compiled);

    private static Dictionary<string, string> BuildInverse()
    {
        var dict = new Dictionary<string, string>();
        foreach (var pair in IdToName)
            dict[pair.Value] = pair.Key;
        return dict;
    }

    public static string GetName(string id)
    {
        if (IdToName.TryGetValue(id, out var name))
            return name;
        return IdPattern.IsMatch(id) ? $"$({id})" : "";
    }

    public static string GetId(string name)
    {
        if (NameToId.TryGetValue(name, out var id))
            return id;
        return WrappedIdPattern.IsMatch(name) ? name.Substring(2, name.Length - 3) : "";
    }
}

internal static class SvipReverbPresets
{
    private static readonly Dictionary<string, XSReverbPresetEnum> NameToEnum = new()
    {
        ["干声"] = XSReverbPresetEnum.None,
        ["浮光"] = XSReverbPresetEnum.Default,
        ["午后"] = XSReverbPresetEnum.SmallHall1,
        ["月光"] = XSReverbPresetEnum.MediumHall1,
        ["水晶"] = XSReverbPresetEnum.LargeHall1,
        ["汽水"] = XSReverbPresetEnum.SmallRoom1,
        ["夜莺"] = XSReverbPresetEnum.MediumRoom1,
        ["大梦"] = XSReverbPresetEnum.LongReverb2,
    };

    private static readonly Dictionary<XSReverbPresetEnum, string> EnumToName = BuildInverse();

    private static Dictionary<XSReverbPresetEnum, string> BuildInverse()
    {
        var dict = new Dictionary<XSReverbPresetEnum, string>();
        foreach (var pair in NameToEnum)
            dict[pair.Value] = pair.Key;
        return dict;
    }

    public static string? GetName(XSReverbPresetEnum value) =>
        EnumToName.TryGetValue(value, out var name) ? name : null;

    public static XSReverbPresetEnum GetEnum(string name, XSReverbPresetEnum fallback) =>
        NameToEnum.TryGetValue(name, out var value) ? value : fallback;
}

internal static class SvipNoteHeadTags
{
    public static string? GetTag(XSNoteHeadTagEnum value) => value switch
    {
        XSNoteHeadTagEnum.SilTag => "0",
        XSNoteHeadTagEnum.SpTag => "V",
        _ => null,
    };

    public static XSNoteHeadTagEnum GetEnum(string? tag) => tag switch
    {
        "0" => XSNoteHeadTagEnum.SilTag,
        "V" => XSNoteHeadTagEnum.SpTag,
        _ => XSNoteHeadTagEnum.NoTag,
    };
}

internal static class SvipText
{
    private static readonly Regex ChineseStart = new(
        @"^[〇一-鿿㐀-䶿豈-﫿⺀-⻿㇀-㇯]",
        RegexOptions.Compiled);

    private static readonly Regex TrailingPunctuation = new(
        @"[,，.。?？!！]+$", RegexOptions.Compiled);

    public static bool StartsWithChinese(string text) =>
        !string.IsNullOrEmpty(text) && ChineseStart.IsMatch(text);

    public static string CleanseText(string text) =>
        string.IsNullOrEmpty(text) ? text : TrailingPunctuation.Replace(text, "");
}
