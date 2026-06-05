using System;
using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Model;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Dv;

internal sealed class DvSegmentPitchRawData
{
    public int TickOffset { get; }
    public List<DvPoint> Data { get; }

    public DvSegmentPitchRawData(int tickOffset, List<DvPoint> data)
    {
        TickOffset = tickOffset;
        Data = data;
    }
}

internal sealed class DvNoteWithPitch
{
    public Note Note { get; set; } = new();
    public int PorHead { get; set; }
    public int PorTail { get; set; }
    public int BenLen { get; set; }
    public int BenDep { get; set; }
    public List<DvPoint> Vibrato { get; set; } = new();
}

internal static class DvPitch
{
    public static int ConvertNoteKeyInt(int key) => (int)DvConstants.NoteKeySum - key;

    public static double ConvertNoteKeyFloat(double key) => DvConstants.NoteKeySum - key;

    private static List<Point>? MergePointsFromSegments(List<DvSegmentPitchRawData> segments)
    {
        var points = new List<Point>();
        foreach (var segment in segments)
        {
            foreach (var dvPoint in segment.Data)
            {
                int rawTick = dvPoint.X;
                if (rawTick >= 0)
                {
                    int tick = rawTick + segment.TickOffset;
                    if (dvPoint.Y >= 0)
                    {
                        int value = (int)Math.Round(ConvertNoteKeyFloat(dvPoint.Y / 100.0) * 100);
                        points.Add(new Point(tick, value));
                    }
                    else
                    {
                        points.Add(new Point(tick, -100));
                    }
                }
            }
        }
        return points.Count > 0 ? points : null;
    }

    private static List<Point>? MergeSameTickPoints(List<Point> points)
    {
        var merged = new List<Point>();
        int i = 0;
        while (i < points.Count)
        {
            int tick = points[i].X;
            int start = i;
            while (i < points.Count && points[i].X == tick)
                i++;
            var group = points.GetRange(start, i - start);
            if (group.Count > 1)
            {
                if (group.Any(p => p.Y == -100))
                    merged.Add(new Point(tick, -100));
                else
                    merged.Add(new Point(tick, (int)Math.Round(group.Average(p => (double)p.Y))));
            }
            else
            {
                merged.Add(group[0]);
            }
        }
        return merged.Count > 0 ? merged : null;
    }

    private static List<Point>? MergeSameValuePoints(List<Point> points)
    {
        var merged = new List<Point>();
        int i = 0;
        while (i < points.Count)
        {
            int value = points[i].Y;
            merged.Add(points[i]);
            i++;
            while (i < points.Count && points[i].Y == value)
                i++;
        }
        return merged.Count > 0 ? merged : null;
    }

    private static List<Point> ApplyDefaultPitch(
        int firstBarLength,
        List<Point> points,
        List<DvNoteWithPitch> notes,
        List<SongTempo> tempos)
    {
        if (points.Count == 0 || notes.Count == 0)
            return points;

        var synchronizer = new TimeSynchronizer(tempos);
        var basePitch = GetBasePitch(notes, synchronizer);
        var bendDiff = GetBendPitch(notes, synchronizer);
        var vibratoDiff = GetVibratoPitch(notes, synchronizer);

        var result = new List<Point>();
        Point? lastPoint = null;
        foreach (var point in points)
        {
            if (lastPoint != null && lastPoint.Value.Y == -100)
            {
                for (int tick = lastPoint.Value.X + 1; tick < point.X; tick += DvConstants.SamplingIntervalTick)
                {
                    int value = Get(basePitch, tick) + Get(bendDiff, tick) + Get(vibratoDiff, tick);
                    result.Add(new Point(tick + firstBarLength, value));
                }
                result.Add(new Point(lastPoint.Value.X + firstBarLength, -100));
            }
            if (point.Y != -100)
                result.Add(point.WithX(point.X + firstBarLength));
            else
                result.Add(new Point(point.X + firstBarLength, -100));
            lastPoint = point;
        }
        if (result.Count == 0 || result[0].X > notes[^1].Note.EndPos)
        {
            var prefix = new List<Point>();
            for (int tick = 0; tick < notes[^1].Note.EndPos; tick += DvConstants.SamplingIntervalTick)
            {
                int value = Get(basePitch, tick) + Get(bendDiff, tick) + Get(vibratoDiff, tick);
                prefix.Add(new Point(tick + firstBarLength, value));
            }
            prefix.AddRange(result);
            result = prefix;
        }
        if (result.Count > 0)
        {
            result.Insert(0, Point.StartPoint());
            result.Add(Point.EndPoint());
        }
        return result;
    }

    private static int Get(Dictionary<int, int> dict, int key) => dict.TryGetValue(key, out int v) ? v : 0;

    private static Dictionary<int, int> GetBasePitch(List<DvNoteWithPitch> notes, TimeSynchronizer transformer)
    {
        var result = new Dictionary<int, int>();
        var lastNotes = new List<DvNoteWithPitch?> { null };
        lastNotes.AddRange(notes);
        var thisNotes = new List<DvNoteWithPitch?>(notes) { null };
        for (int idx = 0; idx < lastNotes.Count; idx++)
        {
            var lastNote = lastNotes[idx];
            var thisNote = thisNotes[idx];
            var portamento = new List<Point>();
            if (lastNote != null && thisNote != null)
            {
                portamento = GetPortamento(lastNote, transformer, thisNote);
                foreach (var point in portamento)
                    result[point.X] = point.Y;
            }
            if (lastNote != null)
            {
                int end = portamento.Count > 0 ? portamento[0].X : lastNote.Note.EndPos;
                for (int tick = TickHalfStart(lastNote.Note); tick < end; tick++)
                    result[tick] = lastNote.Note.KeyNumber * 100;
            }
            if (thisNote != null)
            {
                int start;
                if (lastNote == null)
                    start = 0;
                else
                    start = portamento.Count > 0 ? portamento[^1].X : thisNote.Note.StartPos;
                for (int tick = start; tick < TickHalfStart(thisNote.Note); tick++)
                    result[tick] = thisNote.Note.KeyNumber * 100;
            }
        }
        return result;
    }

    private static Dictionary<int, int> GetBendPitch(List<DvNoteWithPitch> notes, TimeSynchronizer transformer)
    {
        var result = new Dictionary<int, int>();
        foreach (var note in notes)
        {
            int startTick = note.Note.StartPos;
            double startSec = transformer.GetActualSecsFromTicks(startTick);
            double valleySec = startSec + DvConstants.BendDownLengthFixedSec;
            double valleyTick = Math.Min(
                transformer.GetActualTicksFromSecs(valleySec),
                note.Note.StartPos + note.Note.Length / 2 - 1);

            double lengthSec;
            if (note.BenLen <= 50)
                lengthSec = DvConstants.BendLengthMinSec;
            else
                lengthSec = (DvConstants.BendLengthMaxSec - DvConstants.BendLengthMinSec) * ((note.BenLen - 50) / 50)
                    + DvConstants.BendLengthMinSec;
            double endSec = startSec + lengthSec;
            double endTick = Math.Min(
                transformer.GetActualTicksFromSecs(endSec),
                note.Note.StartPos - 1);

            double valleyValue = -DvConstants.BendValueMax * note.BenDep;
            var valleyPoint = new Point((int)Math.Round(valleyTick), (int)Math.Round(valleyValue));

            var bendDown = MusicMath.InterpolateLinear(
                new List<Point> { new Point(startTick, 0), valleyPoint }, 1);

            var bendUpFull = MusicMath.InterpolateCosineEaseInOut(
                new List<Point> { valleyPoint, new Point((int)Math.Round(endTick), 0) }, 1);
            var bendUp = bendUpFull.Count > 1 ? bendUpFull.GetRange(1, bendUpFull.Count - 1) : new List<Point>();

            foreach (var point in bendDown)
                result[point.X] = point.Y;
            foreach (var point in bendUp)
                result[point.X] = point.Y;
        }
        return result;
    }

    private static List<Point> GetPortamento(
        DvNoteWithPitch lastNote,
        TimeSynchronizer transformer,
        DvNoteWithPitch thisNote)
    {
        double tailLengthSec = DvConstants.PortamentoLengthMaxSec * lastNote.PorTail / 100;
        double startSec = transformer.GetActualSecsFromTicks(lastNote.Note.EndPos) - tailLengthSec;
        double startTick = Math.Max(
            transformer.GetActualTicksFromSecs(startSec),
            TickHalfStart(lastNote.Note));

        double headLengthSec = DvConstants.PortamentoLengthMaxSec * thisNote.PorHead / 100;
        double endSec = transformer.GetActualSecsFromTicks(thisNote.Note.StartPos) + headLengthSec;
        double endTick = Math.Min(
            transformer.GetActualTicksFromSecs(endSec),
            TickHalfStart(thisNote.Note) - 1);

        return MusicMath.InterpolateCosineEaseInOut(new List<Point>
        {
            new Point((int)Math.Round(startTick), lastNote.Note.KeyNumber * 100),
            new Point((int)Math.Round(endTick), thisNote.Note.KeyNumber * 100),
        }, 1);
    }

    private static Dictionary<int, int> GetVibratoPitch(List<DvNoteWithPitch> notes, TimeSynchronizer transformer)
    {
        var result = new Dictionary<int, int>();
        foreach (var note in notes)
        {
            int startTick = note.Note.StartPos;
            double startSec = transformer.GetActualSecsFromTicks(startTick);
            var vibratoPoints = new List<Point>();
            foreach (var vibPoint in note.Vibrato)
            {
                double tick = transformer.GetActualTicksFromSecs(startSec + vibPoint.X / 1000.0);
                if (startTick <= tick && tick < note.Note.EndPos)
                    vibratoPoints.Add(new Point((int)Math.Round(tick), -vibPoint.Y));
            }
            foreach (var point in MusicMath.InterpolateLinear(vibratoPoints, 1))
                result[point.X] = point.Y;
        }
        return result;
    }

    private static int TickHalfStart(Note note) => (note.StartPos + note.EndPos) / 2;

    public static ParamCurve? PitchFromDvTrack(
        int firstBarLength,
        List<DvSegmentPitchRawData> segments,
        List<DvNoteWithPitch> notes,
        List<SongTempo> tempos)
    {
        var mergedPoints = MergePointsFromSegments(segments);
        if (mergedPoints == null)
            return null;
        mergedPoints = MergeSameTickPoints(mergedPoints);
        if (mergedPoints == null)
            return null;
        mergedPoints = MergeSameValuePoints(mergedPoints);
        if (mergedPoints == null)
            return null;
        return new ParamCurve { Points = ApplyDefaultPitch(firstBarLength, mergedPoints, notes, tempos) };
    }

    public static DvSegmentPitchRawData? GenerateForDv(int firstBarLength, ParamCurve pitch, List<Note> notes)
    {
        if (pitch.Points.Count == 0)
            return null;
        var data = new List<DvPoint> { new DvPoint(-1, -1) };
        int? lastValue = null;
        foreach (var point in pitch.Points)
        {
            if ((lastValue == null && point.Y != -100) || point.Y == -100)
                data.Add(new DvPoint(point.X - firstBarLength, -1));
            if (point.Y != -100)
                data.Add(new DvPoint(
                    point.X - firstBarLength,
                    (int)Math.Round(ConvertNoteKeyFloat(point.Y / 100.0) * 100)));
            lastValue = point.Y;
        }
        data.Add(new DvPoint(data.Count > 1 ? data[^1].X + 1 : 307201, -1));
        return new DvSegmentPitchRawData(0, data);
    }
}
