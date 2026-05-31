using System;
using System.Collections.Generic;
using System.Linq;

namespace VOCALOIDPatcher.Formats.Model;

public enum Format
{
    Vsqx,
    Vpr,
    Ust,
    Ustx,
    Ccs,
    Svp,
    S5p,
    MusicXml,
    Dv,
    Vsq,
    VocaloidMid,
    StandardMid,
    Ppsf,
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
    public Func<ImportFile, bool>? CustomMatcher { get; init; }

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
        Format.Vsqx, Format.Vpr, Format.Vsq, Format.VocaloidMid, Format.Ust, Format.Ustx,
        Format.Ccs, Format.MusicXml, Format.Svp, Format.S5p, Format.Dv, Format.Ppsf,
        Format.StandardMid, Format.Tssln, Format.UfData,
    };

    public static readonly IReadOnlyList<Format> Exportable = new[]
    {
        Format.Vsqx, Format.Vpr, Format.Vsq, Format.VocaloidMid, Format.Ust, Format.Ustx,
        Format.Ccs, Format.MusicXml, Format.Svp, Format.S5p, Format.Dv, Format.StandardMid,
        Format.Tssln, Format.UfData,
    };

    public static readonly IReadOnlyList<Format> VocaloidFormats = new[]
    {
        Format.Vsq, Format.Vsqx, Format.VocaloidMid, Format.Vpr,
    };

    private static Dictionary<Format, FormatInfo> Build()
    {
        var cv = JapaneseLyricsType.RomajiCv;
        var kanaCv = JapaneseLyricsType.KanaCv;
        var romajiVcv = JapaneseLyricsType.RomajiVcv;
        var kanaVcv = JapaneseLyricsType.KanaVcv;

        var list = new List<FormatInfo>
        {
            new() { Format = Format.Vsqx, Extension = "vsqx", PossibleLyricsTypes = new[] { cv, kanaCv }, AvailableFeaturesForGeneration = new[] { Feature.ConvertPitch, Feature.ConvertPhonemes } },
            new() { Format = Format.Vpr, Extension = "vpr", PossibleLyricsTypes = new[] { cv, kanaCv }, AvailableFeaturesForGeneration = new[] { Feature.ConvertPitch, Feature.ConvertPhonemes } },
            new() { Format = Format.Ust, Extension = "ust", MultipleFile = true, PossibleLyricsTypes = new[] { cv, romajiVcv, kanaCv, kanaVcv }, AvailableFeaturesForGeneration = new[] { Feature.ConvertPitch } },
            new() { Format = Format.Ustx, Extension = "ustx", PossibleLyricsTypes = new[] { cv, romajiVcv, kanaCv, kanaVcv }, AvailableFeaturesForGeneration = new[] { Feature.ConvertPitch, Feature.ConvertPhonemes } },
            new() { Format = Format.Ccs, Extension = "ccs", PossibleLyricsTypes = new[] { kanaCv }, AvailableFeaturesForGeneration = new[] { Feature.ConvertPitch, Feature.ConvertPhonemes } },
            new() { Format = Format.Svp, Extension = "svp", PossibleLyricsTypes = new[] { cv, kanaCv }, AvailableFeaturesForGeneration = new[] { Feature.ConvertPitch, Feature.SplitProject, Feature.ConvertPhonemes } },
            new() { Format = Format.S5p, Extension = "s5p", PossibleLyricsTypes = new[] { cv, kanaCv }, AvailableFeaturesForGeneration = new[] { Feature.ConvertPitch } },
            new() { Format = Format.MusicXml, Extension = "musicxml", OtherExtensions = new[] { "xml" }, PossibleLyricsTypes = new[] { cv, kanaCv }, SuggestedLyricType = kanaCv },
            new() { Format = Format.Dv, Extension = "dv", PossibleLyricsTypes = new[] { cv, kanaCv }, SuggestedLyricType = cv, AvailableFeaturesForGeneration = new[] { Feature.ConvertPitch } },
            new() { Format = Format.Vsq, Extension = "vsq", PossibleLyricsTypes = new[] { cv, kanaCv }, AvailableFeaturesForGeneration = new[] { Feature.ConvertPitch, Feature.ConvertPhonemes } },
            new() { Format = Format.VocaloidMid, Extension = "mid", PossibleLyricsTypes = new[] { cv, kanaCv }, AvailableFeaturesForGeneration = new[] { Feature.ConvertPitch }, Alias = "Mid (VOCALOID)" },
            new() { Format = Format.StandardMid, Extension = "mid", PossibleLyricsTypes = new[] { cv, kanaCv }, Alias = "Mid (Standard)" },
            new() { Format = Format.Ppsf, Extension = "ppsf", PossibleLyricsTypes = new[] { cv, kanaCv } },
            new() { Format = Format.Tssln, Extension = "tssln", PossibleLyricsTypes = new[] { kanaCv, cv }, AvailableFeaturesForGeneration = new[] { Feature.ConvertPhonemes } },
            new() { Format = Format.UfData, Extension = "ufdata", PossibleLyricsTypes = new[] { cv, romajiVcv, kanaCv, kanaVcv }, AvailableFeaturesForGeneration = new[] { Feature.ConvertPitch, Feature.ConvertPhonemes } },
        };

        return list.ToDictionary(info => info.Format);
    }
}
