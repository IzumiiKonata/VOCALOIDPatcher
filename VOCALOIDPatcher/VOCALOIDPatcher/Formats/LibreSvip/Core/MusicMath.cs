using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using VOCALOIDPatcher.Formats.LibreSvip.Model;

namespace VOCALOIDPatcher.Formats.LibreSvip.Core;

public delegate double InterpolationFunc(double x, (double X, double Y) start, (double X, double Y) end);

public static class MusicMath
{
    private static readonly string[] PitchMap =
        { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    private static readonly Dictionary<char, int> Note2MidiPitch = new()
    {
        ['C'] = 0, ['D'] = 2, ['E'] = 4, ['F'] = 5, ['G'] = 7, ['A'] = 9, ['B'] = 11,
    };

    private static readonly Dictionary<char, int> Note2MidiAcc = new()
    {
        ['#'] = 1, ['b'] = -1, ['!'] = -1, ['♯'] = 1, ['♭'] = -1, ['♮'] = 0,
    };

    private static readonly Regex NoteRe = new(
        @"^(?<note>[A-Ga-g])(?<accidental>[#♯b!♭♮]*)(?<octave>[+-]?\d+)?",
        RegexOptions.Compiled);

    public static string Midi2Note(double midi)
    {
        int m = (int)Math.Round(midi);
        int octave = m / Constants.KeyInOctave - 1;
        string pitch = PitchMap[((m % Constants.KeyInOctave) + Constants.KeyInOctave) % Constants.KeyInOctave];
        return $"{pitch}{octave}";
    }

    public static int Note2Midi(string note)
    {
        var match = NoteRe.Match(note);
        if (!match.Success)
            throw new ArgumentException($"Invalid note format: {note}");
        char pitch = char.ToUpperInvariant(match.Groups["note"].Value[0]);
        int offset = 0;
        foreach (char c in match.Groups["accidental"].Value)
            if (Note2MidiAcc.TryGetValue(c, out int v))
                offset += v;
        string octaveStr = match.Groups["octave"].Value;
        int octave = string.IsNullOrEmpty(octaveStr) ? 0 : int.Parse(octaveStr, CultureInfo.InvariantCulture);
        return Constants.KeyInOctave * (octave + 1) + Note2MidiPitch[pitch] + offset;
    }

    public static double Hz2Midi(double hz, int a4Midi = 69, double baseFreq = 440.0) =>
        a4Midi + Constants.KeyInOctave * Math.Log2(hz / baseFreq);

    public static double Midi2Hz(double midi, int a4Midi = 69, double baseFreq = 440.0) =>
        baseFreq * Math.Pow(2, (midi - a4Midi) / Constants.KeyInOctave);

    public static double Clamp(double x, double? lower = null, double? upper = null)
    {
        double lo = lower ?? double.NegativeInfinity;
        double hi = upper ?? double.PositiveInfinity;
        if (hi < lo)
            throw new ArgumentException($"expected upper bound ({hi}) >= lower bound ({lo})");
        return Math.Min(Math.Max(x, lo), hi);
    }

    private static double Transform(double x, (double X, double Y) start, (double X, double Y) end, Func<double, double> g)
    {
        double r = (x - start.X) / (end.X - start.X);
        return start.Y + (end.Y - start.Y) * g(r);
    }

    public static double LinearInterpolation(double x, (double X, double Y) start, (double X, double Y) end) =>
        Transform(x, start, end, r => r);

    public static double CosineEasingInInterpolation(double x, (double X, double Y) start, (double X, double Y) end) =>
        Transform(x, start, end, r => 1 - Math.Cos(r * Math.PI / 2));

    public static double CosineEasingOutInterpolation(double x, (double X, double Y) start, (double X, double Y) end) =>
        Transform(x, start, end, r => Math.Sin(r * Math.PI / 2));

    public static double CosineEasingInOutInterpolation(double x, (double X, double Y) start, (double X, double Y) end) =>
        Transform(x, start, end, r => (1 - Math.Cos(r * Math.PI)) / 2);

    public static double CubicInterpolation(double x, (double X, double Y) start, (double X, double Y) end) =>
        Transform(x, start, end, r => (3 - 2 * r) * r * r);

    public static double VocaloidInterpolation(double x, (double X, double Y) start, (double X, double Y) end) =>
        Transform(x, start, end, r => r <= 0.5 ? Math.Sin(r * Math.PI) / 2 : r);

    public static double SigmoidInterpolation(double x, (double X, double Y) start, (double X, double Y) end, double k) =>
        Transform(x, start, end, r => 1 / (1 + Math.Exp(k * (-2 * r + 1))));

    public static double DbToFloat(double db, bool usingAmplitude = true) =>
        usingAmplitude ? Math.Pow(10, db / 20) : Math.Pow(10, db / 10);

    public static double RatioToDb(double ratio, double? val2 = null, bool usingAmplitude = true)
    {
        if (val2 != null)
            ratio /= val2.Value;
        if (ratio == 0)
            return double.NegativeInfinity;
        return usingAmplitude ? 20 * Math.Log10(ratio) : 10 * Math.Log10(ratio);
    }

    public static List<Point> InnerInterpolate(
        List<Point> data, int samplingIntervalTick,
        InterpolationFunc mapping)
    {
        if (data.Count == 0)
            return data;
        var result = new List<Point> { data[0] };
        for (int i = 0; i < data.Count - 1; i++)
        {
            var start = data[i];
            var end = data[i + 1];
            for (int x = start.X + 1; x < end.X; x += samplingIntervalTick)
                result.Add(new Point(x, (int)Math.Round(mapping(x, (start.X, start.Y), (end.X, end.Y)))));
        }
        result.Add(data[^1]);
        return result;
    }

    public static List<Point> InterpolateLinear(List<Point> data, int samplingIntervalTick) =>
        InnerInterpolate(data, samplingIntervalTick, LinearInterpolation);

    public static List<Point> InterpolateCosineEaseInOut(List<Point> data, int samplingIntervalTick) =>
        InnerInterpolate(data, samplingIntervalTick, CosineEasingInOutInterpolation);

    public static List<Point> InterpolateCosineEaseIn(List<Point> data, int samplingIntervalTick) =>
        InnerInterpolate(data, samplingIntervalTick, CosineEasingInInterpolation);

    public static List<Point> InterpolateCosineEaseOut(List<Point> data, int samplingIntervalTick) =>
        InnerInterpolate(data, samplingIntervalTick, CosineEasingOutInterpolation);
}
