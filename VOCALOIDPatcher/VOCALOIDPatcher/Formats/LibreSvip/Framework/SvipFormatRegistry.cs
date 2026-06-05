using System;
using System.Collections.Generic;
using System.Linq;

namespace VOCALOIDPatcher.Formats.LibreSvip.Framework;

public sealed class SvipFormatInfo
{
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string? NameKey { get; init; }
    public string Extension { get; init; } = "";
    public IReadOnlyList<string> OtherExtensions { get; init; } = Array.Empty<string>();
    public bool MultipleFile { get; init; }
    public FormatConverter Converter { get; init; } = null!;

    public IEnumerable<string> AllExtensions => new[] { Extension }.Concat(OtherExtensions);

    public bool CanImport => Converter.CanLoad;
    public bool CanExport => Converter.CanDump;

    public string GetFileName(string name) => $"{name}.{Extension}";

    public bool MatchesExtension(string ext) =>
        AllExtensions.Any(e => string.Equals(e, ext, StringComparison.OrdinalIgnoreCase));
}

public static class SvipFormatRegistry
{
    private static readonly Dictionary<string, SvipFormatInfo> Map = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<SvipFormatInfo> Order = new();

    private static readonly string[] DisplayPriority =
    {
        "svp", "ustx", "ust", "vpr", "vsqx", "s5p", "vsq",
        "ccs", "tssln", "acep", "vvproj", "ppsf", "dv", "tlpx", "tlp",
        "svip", "ds", "aisp", "y77", "vog",
        "musicxml", "json", "ufdata",
        "lrc", "srt", "ass",
    };

    private static int Rank(SvipFormatInfo info)
    {
        int index = Array.IndexOf(DisplayPriority, info.Id);
        return index < 0 ? int.MaxValue : index;
    }

    public static void Register(SvipFormatInfo info)
    {
        if (Map.ContainsKey(info.Id))
            return;
        Map[info.Id] = info;
        Order.Add(info);
    }

    public static SvipFormatInfo Get(string id) => Map[id];

    public static bool TryGet(string id, out SvipFormatInfo info) => Map.TryGetValue(id, out info!);

    public static IReadOnlyList<SvipFormatInfo> All => Order;

    public static IEnumerable<SvipFormatInfo> Importable => Order.Where(i => i.CanImport).OrderBy(Rank);

    public static IEnumerable<SvipFormatInfo> Exportable => Order.Where(i => i.CanExport).OrderBy(Rank);

    public static SvipFormatInfo? FindImportableByExtension(string ext) =>
        Order.FirstOrDefault(i => i.CanImport && i.MatchesExtension(ext));
}
