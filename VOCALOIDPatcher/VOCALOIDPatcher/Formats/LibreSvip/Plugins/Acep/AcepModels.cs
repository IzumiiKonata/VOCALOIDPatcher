using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Model;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Acep;

public sealed class AcepParamCurve
{
    [JsonPropertyName("type")] public string CurveType { get; set; } = "data";
    [JsonPropertyName("offset")] public int Offset { get; set; } = 0;
    [JsonPropertyName("values")] public List<double?> Values { get; set; } = new();
    [JsonPropertyName("points")] public List<double>? Points { get; set; }
    [JsonPropertyName("pointsVUV")] public List<double>? PointsVuv { get; set; }

    public void NormalizeAnchorPoints()
    {
        if (CurveType == "anchor" && Points != null && Points.Count >= 2)
        {
            var anchors = new List<(double X, double Y)>();
            for (int i = 0; i + 1 < Points.Count; i += 2)
                anchors.Add((Points[i], Points[i + 1]));
            if (anchors.Count == 0)
                return;
            var interpolator = new HermiteInterpolator(anchors);
            Offset = (int)Math.Floor(anchors[0].X);
            int end = (int)Math.Ceiling(anchors[^1].X);
            var xs = new List<double>();
            for (int x = Offset; x <= end; x++)
                xs.Add(x);
            Values = interpolator.Interpolate(xs).Select(v => (double?)v).ToList();
        }
    }

    public AcepParamCurve Transform(Func<double, double> valueTransform)
    {
        return new AcepParamCurve
        {
            CurveType = CurveType,
            Offset = Offset,
            Values = Values.Select(v => v.HasValue ? (double?)valueTransform(v.Value) : null).ToList(),
            Points = Points?.ToList(),
            PointsVuv = PointsVuv?.ToList(),
        };
    }
}

public sealed class AcepParamCurveList
{
    public List<AcepParamCurve> Root { get; set; } = new();

    public AcepParamCurveList Plus(AcepParamCurveList? others, double defaultValue, Func<double, double> transform)
    {
        if (others == null || others.Root.Count == 0)
            return this;
        var ranges = new RangeInterval(
            Root.Concat(others.Root).Select(c => (c.Offset, c.Offset + c.Values.Count))).SubRanges();
        var result = new AcepParamCurveList();
        foreach (var (start, end) in ranges)
        {
            var resultCurve = new AcepParamCurve { Offset = start };
            for (int i = 0; i < end - start; i++)
                resultCurve.Values.Add(0.0);
            foreach (var selfCurve in Root.Where(c => start <= c.Offset && c.Offset < end))
            {
                int index = selfCurve.Offset - start;
                foreach (var value in selfCurve.Values)
                {
                    if (index >= 0 && index < resultCurve.Values.Count)
                        resultCurve.Values[index] = value;
                    index++;
                }
            }
            foreach (var otherCurve in others.Root.Where(c => start <= c.Offset && c.Offset < end))
            {
                int index = otherCurve.Offset - start;
                foreach (var value in otherCurve.Values)
                {
                    if (index >= 0 && index < resultCurve.Values.Count)
                    {
                        if (resultCurve.Values[index] == 0.0)
                            resultCurve.Values[index] = defaultValue;
                        resultCurve.Values[index] += transform(value ?? 0.0);
                    }
                    index++;
                }
            }
            result.Root.Add(resultCurve);
        }
        return result;
    }

    public AcepParamCurveList Exclude(Func<double, bool> predicate)
    {
        var result = new AcepParamCurveList();
        foreach (var curve in Root)
        {
            var buffer = new List<double?>();
            int pos = curve.Offset;
            foreach (var value in curve.Values)
            {
                pos++;
                if (predicate(value ?? 0.0))
                {
                    if (buffer.Count > 0)
                    {
                        result.Root.Add(new AcepParamCurve { Offset = pos - buffer.Count, Values = new List<double?>(buffer) });
                        buffer.Clear();
                    }
                }
                else
                {
                    buffer.Add(value);
                }
            }
            if (buffer.Count > 0)
                result.Root.Add(new AcepParamCurve { Offset = pos - buffer.Count, Values = new List<double?>(buffer) });
        }
        return result;
    }

    public AcepParamCurveList ZScoreNormalize(double d, double b)
    {
        if (Root.Count == 0)
            return this;
        var points = Root.SelectMany(c => c.Values).Select(v => v ?? 0.0).ToList();
        double mean = points.Average();
        double sigma = Stdev(points, mean);
        return new AcepParamCurveList
        {
            Root = Root.Select(c => c.Transform(x => (x - mean) / sigma * d + b)).ToList(),
        };
    }

    public AcepParamCurveList MinMaxNormalize(double r, double b)
    {
        if (Root.Count == 0)
            return this;
        var points = Root.SelectMany(c => c.Values).Select(v => v ?? 0.0).ToList();
        double min = points.Count > 0 ? points.Min() : 0.0;
        double max = points.Count > 0 ? points.Max() : 0.0;
        var result = new AcepParamCurveList();
        if (Math.Abs(max - min) > 1e-3)
            result.Root = Root.Select(c => c.Transform(x => r * (2 * (x - min) / (max - min) - 1) + b)).ToList();
        else
            result.Root = Root.Select(c => c.Transform(_ => 0.0)).ToList();
        return result;
    }

    private static double Stdev(List<double> values, double mean)
    {
        if (values.Count < 2)
            return 1.0;
        double sum = values.Sum(v => (v - mean) * (v - mean));
        return Math.Sqrt(sum / (values.Count - 1));
    }
}

public sealed class AcepMaster
{
    [JsonPropertyName("gain")] public double Gain { get; set; } = 0.0;
}

public sealed class AcepTempo
{
    [JsonPropertyName("bpm")] public double Bpm { get; set; } = 0.0;
    [JsonPropertyName("position")] public int Position { get; set; } = 0;
    [JsonPropertyName("isLerp")] public bool? IsLerp { get; set; } = false;
    [JsonPropertyName("bend")] public double? Bend { get; set; }
}

public sealed class AcepParams
{
    [JsonPropertyName("pitchDelta")] public AcepParamCurveList PitchDelta { get; set; } = new();
    [JsonPropertyName("energy")] public AcepParamCurveList Energy { get; set; } = new();
    [JsonPropertyName("breathiness")] public AcepParamCurveList Breathiness { get; set; } = new();
    [JsonPropertyName("tension")] public AcepParamCurveList Tension { get; set; } = new();
    [JsonPropertyName("falsetto")] public AcepParamCurveList Falsetto { get; set; } = new();
    [JsonPropertyName("gender")] public AcepParamCurveList Gender { get; set; } = new();
    [JsonPropertyName("realEnergy")] public AcepParamCurveList RealEnergy { get; set; } = new();
    [JsonPropertyName("realBreathiness")] public AcepParamCurveList RealBreathiness { get; set; } = new();
    [JsonPropertyName("realTension")] public AcepParamCurveList RealTension { get; set; } = new();
    [JsonPropertyName("realFalsetto")] public AcepParamCurveList RealFalsetto { get; set; } = new();
    [JsonPropertyName("vuv")] public AcepParamCurveList? Vuv { get; set; } = new();
}

public sealed class AcepVibrato
{
    [JsonPropertyName("start")] public double Start { get; set; } = 0.0;
    [JsonPropertyName("amplitude")] public double Amplitude { get; set; } = 0.0;
    [JsonPropertyName("frequency")] public double Frequency { get; set; } = 0.0;
    [JsonPropertyName("attackLen")] public double AttackLen { get; set; } = 0.0;
    [JsonPropertyName("releaseLen")] public double ReleaseLen { get; set; } = 0.0;
    [JsonPropertyName("releaseVol")] public double ReleaseVol { get; set; } = 0.0;
    [JsonPropertyName("phase")] public double Phase { get; set; } = 0.0;
    [JsonPropertyName("startPos")] public double StartPos { get; set; } = 0.0;
    [JsonPropertyName("releaseLevel")] public double ReleaseLevel { get; set; } = 0.0;
    [JsonPropertyName("releaseRatio")] public double ReleaseRatio { get; set; } = 0.0;
    [JsonPropertyName("attackLevel")] public double AttackLevel { get; set; } = 0.0;
    [JsonPropertyName("attackRatio")] public double AttackRatio { get; set; } = 0.0;
}

public sealed class AcepNote
{
    [JsonPropertyName("pos")] public int Pos { get; set; } = 0;
    [JsonPropertyName("dur")] public int Dur { get; set; } = 0;
    [JsonPropertyName("pitch")] public int Pitch { get; set; } = 0;
    [JsonPropertyName("uuid")] public string? Uuid { get; set; }
    [JsonPropertyName("language")] public string Language { get; set; } = "CHN";
    [JsonPropertyName("lyric")] public string Lyric { get; set; } = "";
    [JsonPropertyName("pronunciation")] public string? Pronunciation { get; set; }
    [JsonPropertyName("freezedDefaultSyllable")] public string? FreezedDefaultSyllable { get; set; }
    [JsonPropertyName("newLine")] public bool NewLine { get; set; } = false;
    [JsonPropertyName("consonantLen")] public int? ConsonantLen { get; set; }
    [JsonPropertyName("headConsonants")] public List<double>? HeadConsonants { get; set; } = new();
    [JsonPropertyName("tailConsonants")] public List<double>? TailConsonants { get; set; } = new();
    [JsonPropertyName("syllable")] public string Syllable { get; set; } = "";
    [JsonPropertyName("brLen")] public double BrLen { get; set; } = 0.0;
    [JsonPropertyName("vibrato")] public AcepVibrato? Vibrato { get; set; }

    [JsonIgnore] public AcepLyricsLanguage LanguageEnum => AcepLyricsLanguageHelper.FromCode(Language);
}

public abstract class AcepPattern
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("pos")] public double Pos { get; set; } = 0.0;
    [JsonPropertyName("dur")] public double Dur { get; set; } = 0.0;
    [JsonPropertyName("uuid")] public string? Uuid { get; set; }
    [JsonPropertyName("clipPos")] public double ClipPos { get; set; } = 0.0;
    [JsonPropertyName("clipDur")] public double ClipDur { get; set; } = 0.0;
    [JsonPropertyName("enabled")] public bool? Enabled { get; set; } = true;
    [JsonPropertyName("color")] public string? Color { get; set; }
}

public sealed class AcepAudioPattern : AcepPattern
{
    [JsonPropertyName("path")] public string Path { get; set; } = "";
    [JsonPropertyName("gain")] public double? Gain { get; set; }
    [JsonPropertyName("timeUnit")] public string? TimeUnit { get; set; } = "sec";
}

public sealed class AcepVocalPattern : AcepPattern
{
    [JsonPropertyName("language")] public string Language { get; set; } = "CHN";
    [JsonPropertyName("extendLyrics")] public string ExtendLyrics { get; set; } = "";
    [JsonPropertyName("notes")] public List<AcepNote> Notes { get; set; } = new();
    [JsonPropertyName("timeUnit")] public string? TimeUnit { get; set; } = "tick";
    [JsonPropertyName("parameters")] public AcepParams Parameters { get; set; } = new();
}

public sealed class AcepSeedComposition
{
    [JsonPropertyName("code")] public int Code { get; set; } = AcepSingers.DefaultSeed;
    [JsonPropertyName("lock")] public bool Lock { get; set; } = true;
    [JsonPropertyName("style")] public double Style { get; set; } = 1.0;
    [JsonPropertyName("timbre")] public double Timbre { get; set; } = 1.0;
}

public sealed class AcepCustomSinger
{
    [JsonPropertyName("composition")] public List<AcepSeedComposition> Composition { get; set; } = new();
    [JsonPropertyName("state")] public string State { get; set; } = "Unmixed";
    [JsonPropertyName("name")] public string Name { get; set; } = AcepSingers.DefaultSinger;
    [JsonPropertyName("id")] public int? SingerId { get; set; } = AcepSingers.DefaultSingerId;
    [JsonPropertyName("head")] public int? Head { get; set; } = -1;
    [JsonPropertyName("router")] public int? Router { get; set; } = 1;
    [JsonPropertyName("group")] public string? Group { get; set; } = "";
}

public sealed class AcepSingerConfig
{
    [JsonPropertyName("singer")] public AcepCustomSinger Singer { get; set; } = new();
    [JsonPropertyName("gain")] public double Gain { get; set; } = 0.0;
    [JsonPropertyName("mute")] public bool Mute { get; set; } = false;
    [JsonPropertyName("randomSeed")] public int RandomSeed { get; set; } = 0;
}

public abstract class AcepTrack
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("color")] public string Color { get; set; } = "#91bcdc";
    [JsonPropertyName("gain")] public double Gain { get; set; } = 0.0;
    [JsonPropertyName("pan")] public double Pan { get; set; } = 0.0;
    [JsonPropertyName("mute")] public bool Mute { get; set; } = false;
    [JsonPropertyName("solo")] public bool Solo { get; set; } = false;
    [JsonPropertyName("record")] public bool Record { get; set; } = false;
    [JsonPropertyName("channel")] public int? Channel { get; set; } = 0;
    [JsonPropertyName("listen")] public bool? Listen { get; set; } = false;

    [JsonIgnore] public abstract string TrackType { get; }
}

public sealed class AcepEmptyTrack : AcepTrack
{
    public override string TrackType => "empty";
}

public sealed class AcepAudioTrack : AcepTrack
{
    public override string TrackType => "audio";
    [JsonPropertyName("patterns")] public List<AcepAudioPattern> Patterns { get; set; } = new();
}

public sealed class AcepVocalTrack : AcepTrack
{
    public override string TrackType => "sing";
    [JsonPropertyName("language")] public string Language { get; set; } = "CHN";
    [JsonPropertyName("patterns")] public List<AcepVocalPattern> Patterns { get; set; } = new();
    [JsonPropertyName("singers")] public List<AcepSingerConfig> Singers { get; set; } = new();

    public int Length()
    {
        if (Patterns.Count == 0)
            return 0;
        var last = Patterns[^1];
        return (int)(last.Pos + last.ClipDur - last.ClipPos);
    }
}

public sealed class AcepTimeSignature
{
    [JsonPropertyName("barPos")] public int BarPos { get; set; } = 0;
    [JsonPropertyName("numerator")] public int Numerator { get; set; } = 4;
    [JsonPropertyName("denominator")] public int Denominator { get; set; } = 4;
}

public sealed class AcepProject
{
    [JsonPropertyName("beatsPerBar")] public int BeatsPerBar { get; set; } = 4;
    [JsonPropertyName("colorIndex")] public int ColorIndex { get; set; } = 0;
    [JsonPropertyName("duration")] public int Duration { get; set; } = 0;
    [JsonPropertyName("master")] public AcepMaster Master { get; set; } = new();
    [JsonPropertyName("pianoCells")] public int PianoCells { get; set; } = 2147483646;
    [JsonPropertyName("tempos")] public List<AcepTempo> Tempos { get; set; } = new();
    [JsonPropertyName("trackCells")] public int TrackCells { get; set; } = 2147483646;
    [JsonPropertyName("tracks")] public List<AcepTrack> Tracks { get; set; } = new();
    [JsonPropertyName("version")] public int Version { get; set; } = 9;
    [JsonPropertyName("versionRevision")] public int? VersionRevision { get; set; } = 0;
    [JsonPropertyName("mergedPatternIndex")] public int MergedPatternIndex { get; set; } = 0;
    [JsonPropertyName("recordPatternIndex")] public int RecordPatternIndex { get; set; } = 0;
    [JsonPropertyName("singerLibraryId")] public string? SingerLibraryId { get; set; } = "1200593006";
    [JsonPropertyName("timeSignatures")] public List<AcepTimeSignature> TimeSignatures { get; set; } = new();
}
