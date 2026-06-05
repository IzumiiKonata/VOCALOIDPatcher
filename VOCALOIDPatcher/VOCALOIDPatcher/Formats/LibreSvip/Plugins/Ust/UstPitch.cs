using System;
using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Model;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ust;

public sealed class UtauNoteVibrato
{
    public double Length { get; set; }
    public double Period { get; set; }
    public double Depth { get; set; }
    public double FadeIn { get; set; }
    public double FadeOut { get; set; }
    public double PhaseShift { get; set; }
    public double Shift { get; set; }
}

public sealed class UtauMode2NotePitchData
{
    public double Bpm { get; set; }
    public double? Start { get; set; }
    public double? StartShift { get; set; }
    public List<double> Widths { get; set; } = new();
    public List<double> Shifts { get; set; } = new();
    public List<string> CurveTypes { get; set; } = new();
    public UtauNoteVibrato? VibratoParams { get; set; }
}

public static class UstPitch
{
    private const int Mode1Interval = 5;
    private const int Mode2Interval = 4;
    private const int Mode2MaxPointCount = 50;

    private static double MilliSecFromTick(double tick, double bpm) =>
        tick / Constants.TicksInBeat * 60 / bpm * 1000;

    public static double BpmForNote(List<SongTempo> tempos, Note note)
    {
        int i = Search.FindLastIndex(tempos, t => t.Position <= note.StartPos);
        return tempos[i < 0 ? 0 : i].Bpm;
    }

    public static ParamCurve PitchFromMode1(
        List<List<int>?> notePitches, TimeSynchronizer synchronizer,
        List<Note> notes, List<TimeSignature> timeSignatures)
    {
        var pitchPoints = new List<Point>();
        for (int i = 0; i < notes.Count; i++)
        {
            var pts = i < notePitches.Count ? notePitches[i] : null;
            if (pts == null || pts.Count == 0)
                continue;
            for (int index = 0; index < pts.Count; index++)
                pitchPoints.Add(new Point(notes[i].StartPos + index * Mode1Interval, pts[index]));
        }
        if (pitchPoints.Count == 0)
            return new ParamCurve();
        var simulator = new PitchSimulator(synchronizer, PortamentoPitch.NoPortamento(), notes, timeSignatures);
        return new RelativePitchCurve(1920, notes[0].StartPos, notes[^1].EndPos).ToAbsolute(pitchPoints, simulator);
    }

    public static ParamCurve PitchFromMode2(
        List<UtauMode2NotePitchData?> pitchData, TimeSynchronizer synchronizer,
        List<Note> notes, List<TimeSignature> timeSignatures)
    {
        var pitchPoints = new List<Point>();
        Note? lastNote = null;
        var pending = new List<Point>();
        for (int i = 0; i < notes.Count; i++)
        {
            var note = notes[i];
            var notePitch = i < pitchData.Count ? pitchData[i] : null;
            if (notePitch != null)
            {
                var points = new List<Point>();
                double noteStartMs = synchronizer.GetActualSecsFromTicks(note.StartPos) * 1000;
                if (notePitch.Start != null)
                {
                    double posMs = noteStartMs + notePitch.Start.Value;
                    double tickPos = synchronizer.GetActualTicksFromSecs(posMs / 1000);
                    int startShift = (int)Math.Round(
                        lastNote != null && note.StartPos == lastNote.EndPos
                            ? (lastNote.KeyNumber - note.KeyNumber) * 100
                            : notePitch.StartShift != null ? notePitch.StartShift.Value * 10 : 0);
                    points.Add(new Point((int)Math.Round(tickPos), startShift));
                    int n = Math.Max(notePitch.Widths.Count, Math.Max(notePitch.Shifts.Count, notePitch.CurveTypes.Count));
                    for (int k = 0; k < notePitch.Widths.Count; k++)
                    {
                        double width = notePitch.Widths[k];
                        double shift = k < notePitch.Shifts.Count ? notePitch.Shifts[k] : 0;
                        string curveType = k < notePitch.CurveTypes.Count ? notePitch.CurveTypes[k] : "";
                        posMs += width;
                        tickPos = synchronizer.GetActualTicksFromSecs(posMs / 1000);
                        var thisPoint = new Point((int)Math.Round(tickPos), (int)Math.Round(shift * 10));
                        var lastPoint = points[^1];
                        if (thisPoint.X != lastPoint.X && thisPoint.Y != lastPoint.Y)
                        {
                            var interpolated = Interpolate(lastPoint, thisPoint, curveType, Mode2Interval);
                            points.AddRange(interpolated.Skip(1));
                        }
                        else
                        {
                            points.Add(thisPoint);
                        }
                    }
                }
                int firstX = points.Count > 0 ? points[0].X : int.MaxValue;
                pitchPoints.AddRange(pending.Where(p => p.X < firstX));
                pending = Shape(AppendVibrato(
                    AppendEndPoint(AppendStartPoint(FixPointsAtLastNote(points, note, lastNote), note), note),
                    notePitch.VibratoParams, note, synchronizer, Mode2Interval));
            }
            lastNote = note;
        }
        pitchPoints.AddRange(pending);
        if (pitchPoints.Count == 0)
            return new ParamCurve();
        var simulator = new PitchSimulator(synchronizer, PortamentoPitch.NoPortamento(), notes, timeSignatures);
        return new RelativePitchCurve(1920, notes[0].StartPos, notes[^1].EndPos).ToAbsolute(pitchPoints, simulator);
    }

    private static List<Point> Interpolate(Point last, Point cur, string curveType, int interval)
    {
        var input = new List<Point> { last, cur };
        return curveType switch
        {
            "s" => MusicMath.InterpolateLinear(input, interval),
            "j" => MusicMath.InterpolateCosineEaseIn(input, interval),
            "r" => MusicMath.InterpolateCosineEaseOut(input, interval),
            _ => MusicMath.InterpolateCosineEaseInOut(input, interval),
        };
    }

    private static List<Point> FixPointsAtLastNote(List<Point> data, Note thisNote, Note? lastNote)
    {
        if (lastNote == null || lastNote.EndPos != thisNote.StartPos)
            return data;
        var fixedPts = data.Select(p => p.X < thisNote.StartPos
            ? new Point(p.X, p.Y + (thisNote.KeyNumber - lastNote.KeyNumber) * 100)
            : p).ToList();
        var lastPoint = fixedPts.Count > 0 ? fixedPts[^1] : (Point?)null;
        if (lastPoint != null && lastPoint.Value.X < thisNote.StartPos)
            fixedPts.Add(new Point(thisNote.StartPos, 0));
        return fixedPts;
    }

    private static List<Point> AppendStartPoint(List<Point> data, Note thisNote)
    {
        if (data.Count == 0)
            return new List<Point> { new(thisNote.StartPos, 0) };
        if (data[0].X > thisNote.StartPos)
        {
            var result = new List<Point> { new(thisNote.StartPos, data[0].Y) };
            result.AddRange(data);
            return result;
        }
        return data;
    }

    private static List<Point> AppendEndPoint(List<Point> data, Note thisNote)
    {
        if (data.Count == 0)
            return new List<Point> { new(thisNote.EndPos, 0) };
        if (data[^1].X < thisNote.EndPos)
            return new List<Point>(data) { new(thisNote.EndPos, data[^1].Y) };
        return data;
    }

    private static List<Point> Shape(List<Point> data)
    {
        var sorted = data.OrderBy(p => p.X).ToList();
        var result = new List<Point>();
        foreach (var point in sorted)
        {
            if (result.Count > 0 && result[^1].X == point.X)
                result[^1] = new Point(result[^1].X, (int)Math.Floor((result[^1].Y + point.Y) / 2.0));
            else
                result.Add(point);
        }
        return result;
    }

    private static List<Point> AppendVibrato(
        List<Point> noteValues, UtauNoteVibrato? vibrato, Note note,
        TimeSynchronizer sync, int sampleIntervalTick)
    {
        if (vibrato == null)
            return noteValues;
        double noteLength = sync.GetDurationSecsFromTicks(note.StartPos, note.EndPos) * 1000;
        double vibratoLength = noteLength * vibrato.Length / 100;
        double depth = vibrato.Depth / 100;
        if (vibratoLength <= 0 || vibrato.Period == 0 || depth <= 0)
            return noteValues;
        double frequency = 1.0 / vibrato.Period;
        double easeInLength = noteLength * vibrato.FadeIn / 100;
        double easeOutLength = noteLength * vibrato.FadeOut / 100;
        double phase = vibrato.PhaseShift / 100;
        double shift = vibrato.Shift / 100;
        double start = noteLength - vibratoLength;

        double Vibrato(double t)
        {
            if (t < start)
                return 0.0;
            double easeIn = easeInLength != 0 ? MusicMath.Clamp((t - start) / easeInLength, 0.0, 1.0) : 1.0;
            double easeOut = easeOutLength != 0 ? MusicMath.Clamp((noteLength - t) / easeOutLength, 0.0, 1.0) : 1.0;
            double x = 2 * Math.PI * (frequency * (t - start) - phase);
            return depth * easeIn * easeOut * (Math.Sin(x) + shift) * 100;
        }

        double noteStartMs = sync.GetActualSecsFromTicks(note.StartPos) * 1000;
        double sampleMs = sync.GetDurationSecsFromTicks(note.StartPos, note.StartPos + sampleIntervalTick) * 1000;

        var interpolated = new List<(double X, double Y)>();
        for (int i = 0; i < noteValues.Count; i++)
        {
            double xMs = sync.GetActualSecsFromTicks(noteValues[i].X) * 1000 - noteStartMs;
            interpolated.Add((xMs, noteValues[i].Y + Vibrato(xMs)));
            if (i < noteValues.Count - 1)
            {
                double nextEndMs = sync.GetActualSecsFromTicks(noteValues[i + 1].X) * 1000 - noteStartMs;
                double pos = xMs + sampleMs;
                while (pos < nextEndMs)
                {
                    interpolated.Add((pos, noteValues[i].Y + Vibrato(pos)));
                    pos += sampleMs;
                }
            }
        }
        return interpolated
            .Select(p => new Point(
                (int)Math.Round(sync.GetActualTicksFromSecs((p.X + noteStartMs) / 1000)),
                (int)Math.Round(p.Y)))
            .ToList();
    }

    public static List<List<int>?> PitchToMode1(ParamCurve pitch, List<Note> notes)
    {
        var result = new List<List<int>?>();
        foreach (var note in notes)
        {
            var data = pitch.Points
                .Where(p => note.StartPos <= p.X - 1920 && p.X - 1920 < note.EndPos && p.Y != -100)
                .Select(p => new Point(p.X, p.Y))
                .ToList();
            if (data.Count == 0)
            {
                result.Add(new List<int>());
                continue;
            }
            var resampled = UstResampling.DotResampled(data, Mode1Interval);
            result.Add(resampled.Select(p => p.Y - note.KeyNumber * 100).ToList());
        }
        return result;
    }

    public static UtauNoteVibrato? VibratoToUtau(
        VibratoParam? vibrato, Note note, TimeSynchronizer sync)
    {
        if (vibrato == null)
            return null;
        double startPercent = vibrato.StartPercent;
        double endPercent = vibrato.EndPercent;
        if (endPercent <= startPercent)
            return null;
        double lengthPercent = (endPercent - startPercent) * 100;
        if (lengthPercent <= 0)
            return null;
        double noteLengthMs = sync.GetDurationSecsFromTicks(note.StartPos, note.EndPos) * 1000;
        if (noteLengthMs <= 0)
            return null;
        int midTick = note.StartPos + (int)Math.Round((startPercent + endPercent) / 2 * note.Length);
        double frequencyHz = SampleCurve(vibrato.Frequency, midTick, 5.5);
        double amplitudeSemitone = SampleCurve(vibrato.Amplitude, midTick, 1.0);
        if (frequencyHz <= 0 || amplitudeSemitone <= 0)
            return null;
        double periodMs = 1000.0 / frequencyHz;
        double depthCent = amplitudeSemitone * 100;
        return new UtauNoteVibrato
        {
            Length = lengthPercent,
            Period = periodMs,
            Depth = depthCent,
            FadeIn = 0,
            FadeOut = 0,
            PhaseShift = vibrato.IsAntiPhase ? 50 : 0,
            Shift = 0,
        };
    }

    private static double SampleCurve(ParamCurve curve, int tick, double fallback)
    {
        if (curve.Points.Count == 0)
            return fallback;
        Point? best = null;
        foreach (var p in curve.Points)
        {
            if (p.X <= tick && (best == null || p.X >= best.Value.X))
                best = p;
        }
        return (best ?? curve.Points[0]).Y;
    }

    public static List<UtauMode2NotePitchData?> PitchToMode2(ParamCurve pitch, List<Note> notes, List<SongTempo> tempos)
    {
        var absolutePitch = pitch.Points.Where(p => p.Y != -100).Select(p => new Point(p.X - 1920, p.Y)).ToList();

        List<Point> ToRelative(IEnumerable<Point> from, int key) =>
            from.Select(p => new Point(p.X, (p.Y == 0 ? key * 100 : p.Y) - key * 100)).ToList();

        var dotPitData = new List<(List<Point> Points, int Offset, double Bpm)>();
        if (notes.Count > 0)
        {
            dotPitData.Add((
                ToRelative(absolutePitch.Where(p => p.X < notes[0].EndPos), notes[0].KeyNumber),
                -absolutePitch.Where(p => p.X < 0).Select(p => p.X).DefaultIfEmpty(0).Min(),
                BpmForNote(tempos, notes[0])));
            for (int i = 1; i < notes.Count; i++)
            {
                var note = notes[i];
                var inNote = absolutePitch.Where(p => note.StartPos <= p.X && p.X < note.EndPos).ToList();
                int offset = (absolutePitch.Where(p => p.X >= note.StartPos).Select(p => (int?)p.X).FirstOrDefault() ?? note.StartPos) - note.StartPos;
                dotPitData.Add((ToRelative(inNote, note.KeyNumber), offset, BpmForNote(tempos, note)));
            }
        }

        var result = new List<UtauMode2NotePitchData?>();
        foreach (var (points, offset, bpm) in dotPitData)
        {
            var simplified = SimplifyShapeTo(points, Mode2MaxPointCount);
            if (simplified.Count == 0)
            {
                result.Add(null);
                continue;
            }
            var widths = new List<double>();
            for (int k = 0; k < simplified.Count - 1; k++)
                widths.Add(MilliSecFromTick(simplified[k + 1].X - simplified[k].X, bpm));
            result.Add(new UtauMode2NotePitchData
            {
                Bpm = bpm,
                Start = MilliSecFromTick(offset, bpm),
                StartShift = simplified[0].Y / 10.0,
                Widths = widths,
                Shifts = simplified.Skip(1).Select(p => p.Y / 10.0).ToList(),
                CurveTypes = Enumerable.Repeat("", simplified.Count - 1).ToList(),
            });
        }
        return result;
    }

    private static List<Point> SimplifyShapeTo(List<Point> points, int maxPointCount)
    {
        double step = 0.05;
        double epsilon = step;
        while (true)
        {
            var result = SimplifyShape(points, epsilon);
            if (result.Count < maxPointCount)
                return result;
            epsilon += step;
        }
    }

    private static List<Point> SimplifyShape(List<Point> points, double epsilon)
    {
        if (points.Count < 2)
            return points;
        double dmax = 0;
        int index = 0;
        int end = points.Count - 1;
        for (int i = 1; i < end; i++)
        {
            double d = PerpendicularDistance(points[i], points[0], points[end]);
            if (d > dmax)
            {
                index = i;
                dmax = d;
            }
        }
        if (dmax <= epsilon)
            return new List<Point> { points[0], points[^1] };
        var first = SimplifyShape(points.GetRange(0, index + 1), epsilon);
        var last = SimplifyShape(points.GetRange(index, points.Count - index), epsilon);
        var result = first.GetRange(0, first.Count - 1);
        result.AddRange(last);
        return result;
    }

    private static double PerpendicularDistance(Point pt, Point lineStart, Point lineEnd)
    {
        double dx = lineEnd.X - lineStart.X;
        double dy = lineEnd.Y - lineStart.Y;
        double mag = Math.Sqrt(dx * dx + dy * dy);
        if (mag > 0)
        {
            dx /= mag;
            dy /= mag;
        }
        double pvx = pt.X - lineStart.X;
        double pvy = pt.Y - lineStart.Y;
        double pvdot = dx * pvx + dy * pvy;
        double ax = pvx - pvdot * dx;
        double ay = pvy - pvdot * dy;
        return Math.Sqrt(ax * ax + ay * ay);
    }
}
