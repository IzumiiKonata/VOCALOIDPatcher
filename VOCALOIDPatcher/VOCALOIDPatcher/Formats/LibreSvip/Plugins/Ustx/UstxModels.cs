using System;
using System.Collections.Generic;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using YamlDotNet.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ustx;

public sealed class UCurve
{
    public List<int> Xs { get; set; } = new();
    public List<int> Ys { get; set; } = new();
    public string? Abbr { get; set; }

    [YamlIgnore]
    public bool IsEmpty
    {
        get
        {
            if (Xs.Count == 0)
                return true;
            foreach (int y in Ys)
                if (y != 0)
                    return false;
            return true;
        }
    }

    public int Sample(int x)
    {
        int idx = BisectLeft(Xs, x);
        if (idx < Xs.Count && Xs[idx] == x)
            return Ys[idx];
        if (idx > 0 && idx < Xs.Count)
        {
            double value = MusicMath.LinearInterpolation(
                x, (Xs[idx - 1], Ys[idx - 1]), (Xs[idx], Ys[idx]));
            return (int)Math.Round(value, MidpointRounding.ToEven);
        }
        return 0;
    }

    private static int BisectLeft(List<int> values, int x)
    {
        int lo = 0;
        int hi = values.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (values[mid] < x)
                lo = mid + 1;
            else
                hi = mid;
        }
        return lo;
    }
}

public sealed class UTempo
{
    public int Position { get; set; }
    public double Bpm { get; set; } = Constants.DefaultBpm;
}

public sealed class UTimeSignature
{
    public int BarPosition { get; set; }
    public int BeatPerBar { get; set; } = 4;
    public int BeatUnit { get; set; } = 4;
}

public sealed class URendererSettings
{
    public string? Renderer { get; set; }
    public string? Resampler { get; set; }
    public string? Wavtool { get; set; }
}

public sealed class UTrack
{
    public string? TrackName { get; set; }
    public string? TrackColor { get; set; }
    public string? Singer { get; set; }
    public string? Phonemizer { get; set; }
    public URendererSettings? RendererSettings { get; set; }
    public bool Mute { get; set; }
    public bool Solo { get; set; }
    public double Volume { get; set; }
    public double? Pan { get; set; }
}

public sealed class PitchPoint
{
    public double X { get; set; }
    public double Y { get; set; }
    public string? Shape { get; set; } = "io";
}

public sealed class UPitch
{
    public List<PitchPoint> Data { get; set; } = new();
    public bool? SnapFirst { get; set; }
}

public sealed class UVibrato
{
    public double Length { get; set; } = 75.0;
    public double Period { get; set; } = 175.0;
    public double Depth { get; set; } = 25.0;

    [YamlMember(Alias = "in", ApplyNamingConventions = false)]
    public double InValue { get; set; } = 10.0;

    public double Out { get; set; } = 10.0;
    public double Shift { get; set; }
    public double Drift { get; set; }
    public double VolLink { get; set; }

    [YamlIgnore]
    public double NormalizedStart => 1.0 - Length / 100.0;

    public (double Time, double Tone) Evaluate(double nPos, double nPeriod, UNote note)
    {
        double nStart = NormalizedStart;
        double nIn = Length / 100.0 * InValue / 100.0;
        double nInPos = nStart + nIn;
        double nOut = Length / 100.0 * Out / 100.0;
        double nOutPos = 1.0 - nOut;
        double t = (nPos - nStart) / nPeriod + Shift / 100.0;
        double y = Math.Sin(2.0 * Math.PI * t) * Depth;
        if (nPos < nStart)
            y = 0.0;
        else if (nPos < nInPos)
            y *= (nPos - nStart) / nIn;
        else if (nPos > nOutPos)
            y *= (1.0 - nPos) / nOut;
        return (note.Position + note.Duration * nPos, note.Tone + y / 100.0);
    }
}

public sealed class UNote
{
    public int Position { get; set; }
    public int Duration { get; set; }
    public int Tone { get; set; }
    public string Lyric { get; set; } = "";
    public UPitch? Pitch { get; set; }
    public UVibrato? Vibrato { get; set; }

    [YamlIgnore]
    public int End => Position + Duration;
}

public sealed class UVoicePart
{
    public string Name { get; set; } = "";
    public int Position { get; set; }
    public int TrackNo { get; set; }
    public string? Comment { get; set; } = "";
    public int Duration { get; set; }
    public List<UNote> Notes { get; set; } = new();
    public List<UCurve> Curves { get; set; } = new();
}

public sealed class UWavePart
{
    public string Name { get; set; } = "";
    public int Position { get; set; }
    public int TrackNo { get; set; }
    public string? Comment { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public double FileDurationMs { get; set; }
    public double SkipMs { get; set; }
    public double TrimMs { get; set; }
}

public sealed class USTXProject
{
    public string Name { get; set; } = "New Project";
    public string Comment { get; set; } = "";
    public string OutputDir { get; set; } = "Vocal";
    public string CacheDir { get; set; } = "UCache";
    public string UstxVersion { get; set; } = "0.6";
    public double Bpm { get; set; } = Constants.DefaultBpm;
    public int BeatPerBar { get; set; } = 4;
    public int BeatUnit { get; set; } = 4;
    public int Resolution { get; set; } = Constants.TicksInBeat;
    public List<UTimeSignature> TimeSignatures { get; set; } = new();
    public List<UTempo> Tempos { get; set; } = new();
    public List<UTrack> Tracks { get; set; } = new();
    public List<UVoicePart> VoiceParts { get; set; } = new();
    public List<UWavePart> WaveParts { get; set; } = new();
}
