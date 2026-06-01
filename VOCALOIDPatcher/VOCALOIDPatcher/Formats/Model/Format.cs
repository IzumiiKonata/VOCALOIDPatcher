using System;
using System.Collections.Generic;
using System.Linq;

namespace VOCALOIDPatcher.Formats.Model;

public enum Format
{
    VSQX,
    VPR,
    UST,
    USTX,
    CCS,
    SVP,
    S5P,
    MusicXml,
    DV,
    VSQ,
    VocaloidMid,
    StandardMid,
    PPSF,
    Tssln,
    UfData,
}

public sealed class FormatInfo
{
    public Format Format { get; init; }
    public string Extension { get; init; } = "";
    public IReadOnlyList<string> OtherExtensions { get; init; } = new List<string>();
    public bool MultipleFile { get; init; }
    public IReadOnlyList<JapaneseLyricsType> PossibleLyricsTypes { get; init; } = new List<JapaneseLyricsType>();
    public JapaneseLyricsType? SuggestedLyricType { get; init; }
    public IReadOnlyList<Feature> AvailableFeaturesForGeneration { get; init; } = new List<Feature>();
    public string? Alias { get; init; }
    public Func<ImportFile, bool>? CustomMatcher { get; set; }

    public Func<IReadOnlyList<ImportFile>, ImportParams, Project>? Parser { get; set; }
    public Func<Project, IReadOnlyList<FeatureConfig>, ExportResult>? Generator { get; set; }

    public IEnumerable<string> AllExtensions => new[] { Extension }.Concat(OtherExtensions);

    public string DisplayName => Alias ?? Format.ToString();

    public string GetFileName(string name) => $"{name}.{Extension}";

    public bool Match(IReadOnlyList<ImportFile> files)
    {
        if (CustomMatcher != null)
            return files.All(CustomMatcher);

        var extensions = files.Select(f => f.ExtensionName).Distinct().ToList();
        return extensions.Count == 1 && AllExtensions.Contains(extensions[0]);
    }
}

public static class FormatRegistry
{
    private static readonly Dictionary<Format, FormatInfo> Map = Build();

    public static FormatInfo Get(Format format) => Map[format];

    public static IEnumerable<FormatInfo> All => Map.Values;

    public static readonly IReadOnlyList<Format> Importable = new[]
    {
        Format.VSQX, Format.VPR, Format.VSQ, Format.VocaloidMid, Format.UST, Format.USTX,
        Format.CCS, Format.MusicXml, Format.SVP, Format.S5P, Format.DV, Format.PPSF,
        Format.StandardMid, Format.Tssln, Format.UfData,
    };

    public static readonly IReadOnlyList<Format> Exportable = new[]
    {
        Format.VSQX, Format.VPR, Format.VSQ, Format.VocaloidMid, Format.UST, Format.USTX,
        Format.CCS, Format.MusicXml, Format.SVP, Format.S5P, Format.DV, Format.StandardMid,
        Format.Tssln, Format.UfData,
    };

    public static readonly IReadOnlyList<Format> VocaloidFormats = new[]
    {
        Format.VSQ, Format.VSQX, Format.VocaloidMid, Format.VPR,
    };

    private static Dictionary<Format, FormatInfo> Build()
    {
        var cv = JapaneseLyricsType.RomajiCv;
        var kanaCv = JapaneseLyricsType.KanaCv;
        var romajiVcv = JapaneseLyricsType.RomajiVcv;
        var kanaVcv = JapaneseLyricsType.KanaVcv;

        var list = new List<FormatInfo>
        {
            new() { Format = Format.VSQX, Extension = "vsqx", PossibleLyricsTypes = new[] { cv, kanaCv }, AvailableFeaturesForGeneration = new[] { Feature.ConvertPitch, Feature.ConvertPhonemes } },
            new() { Format = Format.VPR, Extension = "vpr", PossibleLyricsTypes = new[] { cv, kanaCv }, AvailableFeaturesForGeneration = new[] { Feature.ConvertPitch, Feature.ConvertPhonemes } },
            new() { Format = Format.UST, Extension = "ust", MultipleFile = true, PossibleLyricsTypes = new[] { cv, romajiVcv, kanaCv, kanaVcv }, AvailableFeaturesForGeneration = new[] { Feature.ConvertPitch } },
            new() { Format = Format.USTX, Extension = "ustx", PossibleLyricsTypes = new[] { cv, romajiVcv, kanaCv, kanaVcv }, AvailableFeaturesForGeneration = new[] { Feature.ConvertPitch, Feature.ConvertPhonemes } },
            new() { Format = Format.CCS, Extension = "ccs", PossibleLyricsTypes = new[] { kanaCv }, AvailableFeaturesForGeneration = new[] { Feature.ConvertPitch, Feature.ConvertPhonemes } },
            new() { Format = Format.SVP, Extension = "svp", PossibleLyricsTypes = new[] { cv, kanaCv }, AvailableFeaturesForGeneration = new[] { Feature.ConvertPitch, Feature.SplitProject, Feature.ConvertPhonemes } },
            new() { Format = Format.S5P, Extension = "s5p", PossibleLyricsTypes = new[] { cv, kanaCv }, AvailableFeaturesForGeneration = new[] { Feature.ConvertPitch } },
            new() { Format = Format.MusicXml, Extension = "musicxml", OtherExtensions = new[] { "xml" }, PossibleLyricsTypes = new[] { cv, kanaCv }, SuggestedLyricType = kanaCv },
            new() { Format = Format.DV, Extension = "dv", PossibleLyricsTypes = new[] { cv, kanaCv }, SuggestedLyricType = cv, AvailableFeaturesForGeneration = new[] { Feature.ConvertPitch } },
            new() { Format = Format.VSQ, Extension = "vsq", PossibleLyricsTypes = new[] { cv, kanaCv }, AvailableFeaturesForGeneration = new[] { Feature.ConvertPitch, Feature.ConvertPhonemes } },
            new() { Format = Format.VocaloidMid, Extension = "mid", PossibleLyricsTypes = new[] { cv, kanaCv }, AvailableFeaturesForGeneration = new[] { Feature.ConvertPitch }, Alias = "Mid (VOCALOID)" },
            new() { Format = Format.StandardMid, Extension = "mid", PossibleLyricsTypes = new[] { cv, kanaCv }, Alias = "Mid (Standard)" },
            new() { Format = Format.PPSF, Extension = "ppsf", PossibleLyricsTypes = new[] { cv, kanaCv } },
            new() { Format = Format.Tssln, Extension = "tssln", PossibleLyricsTypes = new[] { kanaCv, cv }, AvailableFeaturesForGeneration = new[] { Feature.ConvertPhonemes } },
            new() { Format = Format.UfData, Extension = "ufdata", PossibleLyricsTypes = new[] { cv, romajiVcv, kanaCv, kanaVcv }, AvailableFeaturesForGeneration = new[] { Feature.ConvertPitch, Feature.ConvertPhonemes } },
        };

        return list.ToDictionary(info => info.Format);
    }
}
