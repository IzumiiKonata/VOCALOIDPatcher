using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace VOCALOIDPatcher.Formats.LibreSvip.Framework;

public enum FormatOptionDirection
{
    Import,
    Export,
}

public sealed class FormatOption
{
    public PropertyInfo Property { get; init; } = null!;
    public string LabelKey { get; init; } = "";
}

public static class FormatOptionCatalog
{
    private static readonly Dictionary<string, string> LabelOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ds:SplitThreshold"] = "VOCALOIDPatcher_FormatOption_DsSplitThreshold",
    };

    private static readonly Dictionary<string, string[]> ImportProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ufdata"] = new[] { "ImportPitch" },
        ["ust"] = new[] { "ImportPitch", "Encoding" },
        ["s5p"] = new[] { "ImportPitch", "ImportInstrumental" },
        ["ustx"] = new[]
        {
            "ImportPitch", "ImportInstrumental", "PlusHandlingMode", "BreathLyrics", "SilenceLyrics",
        },
        ["musicxml"] = new[] { "ImportTempo", "ImportDynamics", "ApplyFermataStretch" },
        ["vvproj"] = new[] { "ImportPitch" },
        ["y77"] = new[] { "ImportPitch" },
        ["ds"] = new[] { "ImportPitch", "Tempo" },
        ["aisp"] = new[] { "ImportPitch", "ImportInstrumental" },
        ["tlp"] = new[] { "ImportPitch", "ImportInstrumental" },
        ["svp"] = new[]
        {
            "ImportPitch", "ImportVolume", "ImportBreath", "ImportGender", "ImportStrength",
            "ImportInstrumental", "Instant", "PitchMode", "Breath", "Group",
        },
        ["vsqx"] = new[]
        {
            "ImportPitch", "ImportVolume", "ImportBreath", "ImportGender", "ImportStrength",
            "ImportInstrumental", "CombineSyllables",
        },
        ["vpr"] = new[]
        {
            "ImportPitch", "ImportInstrumental", "ImportVolume", "ImportBreath",
            "ImportGender", "ImportStrength", "ExtractAudio",
        },
        ["vsq"] = new[]
        {
            "ImportPitch", "ImportVolume", "ImportBreath", "ImportGender", "ImportStrength",
            "LyricEncoding", "Breath",
        },
        ["ccs"] = new[] { "ImportPitch", "ImportInstrumentalTrack" },
        ["tlpx"] = new[] { "ImportPitch", "ImportInstrumental" },
        ["ppsf"] = new[] { "ImportPitch", "ImportInstrumentalTrack" },
        ["acep"] = new[]
        {
            "ImportPitch", "ImportBreath", "ImportGender", "ImportInstrumentalTrack",
            "KeepAllPronunciation", "ImportTension", "ImportEnergy", "EnergyCoefficient",
            "CurveSampleInterval",
            "BreathNormalizationMethod", "BreathLowerThreshold", "BreathUpperThreshold",
            "BreathNormalizationScale", "BreathNormalizationBias",
            "TensionNormalizationMethod", "TensionLowerThreshold", "TensionUpperThreshold",
            "TensionNormalizationScale", "TensionNormalizationBias",
            "EnergyNormalizationMethod", "EnergyLowerThreshold", "EnergyUpperThreshold",
            "EnergyNormalizationScale", "EnergyNormalizationBias",
        },
        ["dv"] = new[] { "ImportPitch", "ImportInstrumentalTrack" },
        ["tssln"] = new[] { "ImportPitch", "ImportInstrumentalTrack" },
        ["svip"] = new[]
        {
            "ImportPitch", "ImportVolume", "ImportBreath", "ImportGender",
            "ImportStrength", "ImportInstrumentalTrack",
        },
    };

    private static readonly Dictionary<string, string[]> ExportProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        ["json"] = new[] { "Indented" },
        ["vog"] = new[] { "Tempo", "SingerName" },
        ["ust"] = new[] { "Version", "Encoding", "TrackIndex" },
        ["ustx"] = new[] { "EnglishPhonemizerCompatibility" },
        ["lrc"] = new[]
        {
            "Artist", "Title", "Album", "By", "Offset", "OffsetPolicy",
            "SplitBy", "IgnoreSlurNotes", "Timeline", "Encoding",
        },
        ["srt"] = new[] { "Offset", "TrackIndex", "SplitBy", "IgnoreSlurNotes", "Encoding" },
        ["ass"] = new[] { "Offset", "TrackIndex", "SplitBy", "IgnoreSlurNotes", "Encoding" },
        ["ds"] = new[]
        {
            "TrackIndex", "DictName", "SplitThreshold", "MinInterval",
            "Seed", "ExportGender", "Indent",
        },
        ["svp"] = new[] { "VersionCompatibility", "Vibrato", "DownSample", "LanguageOverride" },
        ["y77"] = new[] { "Tempo", "TrackIndex" },
        ["vsqx"] = new[] { "Version", "PrettyXml", "DefaultLanguage", "DefaultCompId", "DefaultSingerName" },
        ["vpr"] = new[] { "IsAiSinger", "DefaultLangId", "DefaultCompId", "DefaultSingerName" },
        ["vsq"] = new[] { "TicksPerBeat", "LyricEncoding" },
        ["ccs"] = new[] { "DefaultSingerName", "DefaultSingerId", "DefaultSingerVersion" },
        ["acep"] = new[]
        {
            "Singer", "Breath", "MapStrengthInfo", "SplitThreshold",
            "LyricLanguage", "DefaultConsonantLength", "Serialization",
        },
        ["svip"] = new[] { "Singer", "Tempo", "Version" },
    };

    public static IReadOnlyList<FormatOption> Get(SvipFormatInfo info, FormatOptionDirection direction)
    {
        var map = direction == FormatOptionDirection.Import ? ImportProperties : ExportProperties;
        if (!map.TryGetValue(info.Id, out var names))
            return Array.Empty<FormatOption>();
        var type = info.Converter.GetType();
        return names
            .Select(name => type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public))
            .Where(property => property is { CanRead: true, CanWrite: true })
            .Select(property => new FormatOption
            {
                Property = property!,
                LabelKey = LabelOverrides.TryGetValue($"{info.Id}:{property!.Name}", out string? label)
                    ? label
                    : $"VOCALOIDPatcher_FormatOption_{property.Name}",
            })
            .ToList();
    }
}
