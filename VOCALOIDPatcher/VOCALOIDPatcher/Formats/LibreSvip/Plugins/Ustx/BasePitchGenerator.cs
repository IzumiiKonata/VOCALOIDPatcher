using System;
using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.LibreSvip.Core;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ustx;

public sealed class BasePitchGenerator
{
    public const int PitchStart = 0;
    public const int PitchInterval = 5;

    private readonly TimeAxis _timeAxis;

    public BasePitchGenerator(USTXProject project)
    {
        _timeAxis = new TimeAxis();
        _timeAxis.BuildSegments(project);
    }

    private sealed class PPoint
    {
        public double X;
        public double Y;
        public string? Shape;
    }

    public List<double> BasePitch(UVoicePart part)
    {
        var pitches = new List<double>();
        if (part.Notes.Count > 0)
        {
            int count = FloorDiv(part.Notes[^1].End, PitchInterval);
            for (int i = 0; i < count; i++)
                pitches.Add(0.0);
        }

        int index = 0;
        foreach (var note in part.Notes)
        {
            if (index > 0 && index < pitches.Count
                && PitchStart + index * PitchInterval < note.Position)
            {
                pitches[index] = pitches[index - 1];
                index++;
            }
            if (index < pitches.Count)
            {
                int pad = FloorDiv(note.End - PitchStart, PitchInterval) - index;
                if (pad >= 0)
                {
                    int sliceEnd = Math.Min(index + pad + 1, pitches.Count);
                    int removeCount = sliceEnd - index;
                    pitches.RemoveRange(index, removeCount);
                    pitches.InsertRange(index, Enumerable.Repeat((double)(note.Tone * 100), pad + 1));
                    index += pad + 1;
                }
            }
        }
        index = Math.Max(1, index);
        if (index < pitches.Count)
        {
            double fill = pitches[index - 1];
            for (int k = index; k < pitches.Count; k++)
                pitches[k] = fill;
        }

        foreach (var note in part.Notes)
        {
            var vibrato = note.Vibrato ?? new UVibrato();
            if (vibrato.Length <= 0)
                continue;
            int startIndex = Math.Max(0, (int)((note.Position - PitchStart) / (double)PitchInterval));
            int endIndex = Math.Min(pitches.Count, FloorDiv(note.End - PitchStart, PitchInterval));
            double nPeriod = vibrato.Period / _timeAxis.MsBetweenTickPos(note.Position, note.End);
            for (int i = startIndex; i < endIndex; i++)
            {
                double nPos = (PitchStart + i * PitchInterval - note.Position) / (double)note.Duration;
                var point = vibrato.Evaluate(nPos, nPeriod, note);
                pitches[i] = point.Tone * 100;
            }
        }

        UNote? prevNote = null;
        foreach (var note in part.Notes)
        {
            var rawPoints = new List<PPoint>();
            var data = note.Pitch?.Data ?? new List<PitchPoint>();
            foreach (var point in data)
            {
                double remappedX = _timeAxis.MsPosToTickPos(
                    _timeAxis.TickPosToMsPos(part.Position + note.Position) + point.X) - part.Position;
                rawPoints.Add(new PPoint
                {
                    X = remappedX,
                    Y = point.Y * 10 + note.Tone * 100,
                    Shape = point.Shape,
                });
            }
            var pitchPoints = new List<PPoint>();
            foreach (var p in IterTools.UniqueJustSeen(rawPoints, q => q.X))
                pitchPoints.Add(p);

            if (pitchPoints.Count == 0)
            {
                pitchPoints.Add(new PPoint { X = note.Position, Y = note.Tone * 100, Shape = "io" });
                pitchPoints.Add(new PPoint { X = note.End, Y = note.Tone * 100, Shape = "io" });
            }
            if (ReferenceEquals(note, part.Notes[0]) && pitchPoints[0].X > PitchStart)
            {
                pitchPoints.Insert(0, new PPoint { X = PitchStart, Y = pitchPoints[0].Y, Shape = "io" });
            }
            else if (pitchPoints[0].X > note.Position)
            {
                pitchPoints.Insert(0, new PPoint { X = note.Position, Y = pitchPoints[0].Y, Shape = "io" });
            }
            if (pitchPoints[^1].X < note.End)
            {
                pitchPoints.Add(new PPoint { X = note.End, Y = pitchPoints[^1].Y, Shape = "io" });
            }

            var prevPoint = pitchPoints[0];
            index = Math.Max(0, (int)((prevPoint.X - PitchStart) / (double)PitchInterval));
            for (int pi = 1; pi < pitchPoints.Count; pi++)
            {
                var point = pitchPoints[pi];
                double x = PitchStart + index * PitchInterval;
                while (x <= point.X && index < pitches.Count)
                {
                    double pitch = UstxShapeInterpolation.InterpolateShape(
                        (prevPoint.X, prevPoint.Y),
                        (point.X, point.Y),
                        x,
                        string.IsNullOrEmpty(prevPoint.Shape) ? "lo" : prevPoint.Shape);
                    double basePitch = prevNote != null && x <= prevNote.End
                        ? prevNote.Tone * 100
                        : note.Tone * 100;
                    pitches[index] += pitch - basePitch;
                    index++;
                    x += PitchInterval;
                }
                prevPoint = point;
            }
            prevNote = note;
        }

        return pitches;
    }

    private static int FloorDiv(int a, int b)
    {
        int q = a / b;
        if ((a % b != 0) && ((a < 0) != (b < 0)))
            q--;
        return q;
    }
}
