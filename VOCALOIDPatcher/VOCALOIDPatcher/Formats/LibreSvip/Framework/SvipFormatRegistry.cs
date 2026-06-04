using System;
using System.Collections.Generic;
using System.Linq;

namespace VOCALOIDPatcher.Formats.LibreSvip.Framework;

public sealed class SvipFormatInfo
{
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";
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

    public static IEnumerable<SvipFormatInfo> Importable => Order.Where(i => i.CanImport);

    public static IEnumerable<SvipFormatInfo> Exportable => Order.Where(i => i.CanExport);

    public static SvipFormatInfo? FindImportableByExtension(string ext) =>
        Order.FirstOrDefault(i => i.CanImport && i.MatchesExtension(ext));
}
