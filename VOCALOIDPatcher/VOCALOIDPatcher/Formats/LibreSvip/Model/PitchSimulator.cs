using System;
using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.LibreSvip.Core;

namespace VOCALOIDPatcher.Formats.LibreSvip.Model;

public sealed class PitchSimulator
{
    private readonly struct PitchIntervalSegment
    {
        public readonly double Start;
        public readonly double End;
        public readonly int StartValue;
        public readonly int EndValue;

        public PitchIntervalSegment(double start, double end, int startValue, int endValue)
        {
            Start = start;
            End = end;
            StartValue = startValue;
            EndValue = endValue;
        }
    }

    private readonly TimeSynchronizer _synchronizer;
    private readonly PortamentoPitch _portamento;
    private readonly PiecewiseIntervalDict _intervalDict = new();
    private PiecewiseIntervalDict? _pitchIntervalDict;
    private readonly List<PitchIntervalSegment> _pitchIntervalSegments = new();
    private List<double> _pitchIntervalStarts = new();
    private List<double> _pitchIntervalEnds = new();

    public PitchSimulator(
        TimeSynchronizer synchronizer,
        PortamentoPitch portamento,
        List<Note> noteList,
        List<TimeSignature> timeSignatureList)
    {
        _synchronizer = synchronizer;
        _portamento = portamento;
        Build(noteList, timeSignatureList);
    }

    private (double Ticks, double Time) VocaloidMaxPortamento(Note note, double maxPortamentoPercent)
    {
        double maxPortamentoTicks = maxPortamentoPercent * note.Length;
        if (maxPortamentoTicks >= 60)
            maxPortamentoTicks = 60;
        else if (note.Length <= 120)
            maxPortamentoTicks = note.Length / 2.0;
        double maxPortamentoTime = _synchronizer.GetDurationSecsFromTicks(
            (int)(note.EndPos - maxPortamentoTicks * 1.4),
            (int)(note.EndPos - maxPortamentoTicks * 0.4));
        return (maxPortamentoTicks, maxPortamentoTime);
    }

    private void Build(List<Note> noteList, List<TimeSignature> timeSignatureList)
    {
        if (noteList.Count == 0)
            return;
        var currentNote = noteList[0];
        double maxPortamentoPercent = _portamento.MaxInterTimePercent;

        double maxPortamentoTicks;
        double maxPortamentoTime;
        if (_portamento.VocaloidMode)
            (maxPortamentoTicks, maxPortamentoTime) = VocaloidMaxPortamento(currentNote, maxPortamentoPercent);
        else
        {
            maxPortamentoTicks = 0;
            maxPortamentoTime = _portamento.MaxInterTimeInSecs;
        }

        double currentHead = _synchronizer.GetActualSecsFromTicks(currentNote.StartPos);
        double currentDur = _synchronizer.GetDurationSecsFromTicks(currentNote.StartPos, currentNote.EndPos);
        double currentPortamento = Math.Min(currentDur * maxPortamentoPercent, maxPortamentoTime);

        _intervalDict.SetConstant(0.0, currentHead, currentNote.KeyNumber);
        double prevPortamentoEnd = currentHead;
        foreach (var nextNote in noteList.Skip(1))
        {
            if (currentNote.EndPos > nextNote.StartPos)
                throw new NotesOverlappedException(
                    $"Notes overlapped near bar {TickCounter.FindBarIndex(timeSignatureList, nextNote.StartPos)}");
            if (_portamento.VocaloidMode
                && nextNote.StartPos - currentNote.EndPos >= Constants.MinBreakLengthBetweenPitchSections)
                maxPortamentoTime = 0;
            double nextHead = _synchronizer.GetActualSecsFromTicks(nextNote.StartPos);
            double nextDur = _synchronizer.GetDurationSecsFromTicks(nextNote.StartPos, nextNote.EndPos);
            if (_portamento.VocaloidMode && nextNote.StartPos > currentNote.EndPos)
            {
                double currentTail = _synchronizer.GetActualSecsFromTicks(currentNote.EndPos);
                if (prevPortamentoEnd < currentTail)
                    _intervalDict.SetConstant(prevPortamentoEnd, currentTail, currentNote.KeyNumber);
                currentNote = nextNote;
                currentHead = nextHead;
                currentDur = nextDur;
                (maxPortamentoTicks, maxPortamentoTime) = VocaloidMaxPortamento(currentNote, maxPortamentoPercent);
                currentPortamento = Math.Min(currentDur * maxPortamentoPercent, maxPortamentoTime);
                prevPortamentoEnd = currentHead;
                continue;
            }
            double nextPortamento = Math.Min(nextDur * maxPortamentoPercent, maxPortamentoTime);
            double interval;
            double middleTime;
            if (_portamento.VocaloidMode)
            {
                double middlePos = (nextNote.Lyric == "-"
                    ? currentNote.EndPos
                    : (nextNote.StartPos + currentNote.EndPos) / 2.0) - maxPortamentoTicks * 0.4;
                interval = _synchronizer.GetDurationSecsFromTicks(
                    (int)(middlePos - maxPortamentoTicks), (int)middlePos);
                middleTime = _synchronizer.GetActualSecsFromTicks((int)middlePos);
            }
            else
            {
                interval = (nextHead - currentHead - currentDur) / 2;
                middleTime = (nextHead + currentHead + currentDur) / 2;
            }
            double currentPortamentoStart;
            double currentPortamentoEnd;
            if (interval <= maxPortamentoTime)
            {
                currentPortamentoStart = middleTime - currentPortamento;
                currentPortamentoEnd = middleTime + nextPortamento;
            }
            else
            {
                currentPortamentoStart = middleTime - maxPortamentoTime;
                currentPortamentoEnd = middleTime + maxPortamentoTime;
            }
            _intervalDict.SetConstant(prevPortamentoEnd, currentPortamentoStart, currentNote.KeyNumber);
            if (currentNote.KeyNumber == nextNote.KeyNumber)
            {
                _intervalDict.SetConstant(currentPortamentoStart, currentPortamentoEnd, currentNote.KeyNumber);
            }
            else if (currentPortamentoStart < currentPortamentoEnd)
            {
                var start = (currentPortamentoStart, (double)currentNote.KeyNumber);
                var end = (currentPortamentoEnd, (double)nextNote.KeyNumber);
                var interFunc = _portamento.InterFunc;
                _intervalDict.Set(currentPortamentoStart, currentPortamentoEnd, x => interFunc(x, start, end));
            }
            currentNote = nextNote;
            currentHead = nextHead;
            currentDur = nextDur;
            currentPortamento = nextPortamento;
            prevPortamentoEnd = currentPortamentoEnd;
            if (_portamento.VocaloidMode)
                (maxPortamentoTicks, maxPortamentoTime) = VocaloidMaxPortamento(currentNote, maxPortamentoPercent);
        }
        _intervalDict.SetConstant(prevPortamentoEnd, double.PositiveInfinity, currentNote.KeyNumber);
    }

    public void MergePitchCurve(ParamCurve pitchCurve, int firstBarLength)
    {
        _pitchIntervalDict = new PiecewiseIntervalDict();
        _pitchIntervalSegments.Clear();
        foreach (var pointPart in SplitAt(pitchCurve.Points, p => p.Y == -100))
        {
            if (pointPart.Count == 0)
                continue;
            for (int i = 0; i < pointPart.Count - 1; i++)
            {
                var prevPoint = pointPart[i];
                var point = pointPart[i + 1];
                double startTime = _synchronizer.GetActualSecsFromTicks(prevPoint.X - firstBarLength);
                double endTime = _synchronizer.GetActualSecsFromTicks(point.X - firstBarLength);
                if (startTime >= endTime)
                    continue;
                var start = (startTime, (double)prevPoint.Y);
                var end = (endTime, (double)point.Y);
                _pitchIntervalDict.Set(startTime, endTime, x => MusicMath.LinearInterpolation(x, start, end));
                _pitchIntervalSegments.Add(new PitchIntervalSegment(startTime, endTime, prevPoint.Y, point.Y));
            }
        }
        _pitchIntervalStarts = _pitchIntervalSegments.Select(s => s.Start).ToList();
        _pitchIntervalEnds = _pitchIntervalSegments.Select(s => s.End).ToList();
    }

    public double? PitchAtTicks(int ticks) =>
        PitchAtSecs(_synchronizer.GetActualSecsFromTicks(ticks));

    public List<double?> PitchAtTicksBatch(List<int> ticksList)
    {
        if (ticksList.Count == 0)
            return new List<double?>();
        var secsList = _synchronizer.GetActualSecsFromTicksBatch(ticksList);
        return secsList.Select(PitchAtSecs).ToList();
    }

    public double? PitchAtSecs(double secs)
    {
        double? overrideValue = PitchOverrideAtSecs(secs);
        if (overrideValue != null)
            return overrideValue;
        double? value = _intervalDict.Get(secs);
        if (value != null && value.Value != 0)
            return value.Value * 100;
        return null;
    }

    private double? PitchOverrideAtSecs(double secs)
    {
        if (_pitchIntervalSegments.Count > 0)
        {
            int? index = FindPitchIntervalIndex(secs);
            if (index != null)
            {
                var segment = _pitchIntervalSegments[index.Value];
                return MusicMath.LinearInterpolation(secs,
                    (segment.Start, segment.StartValue), (segment.End, segment.EndValue));
            }
        }
        if (_pitchIntervalDict != null)
        {
            double? value = _pitchIntervalDict.Get(secs);
            if (value != null && value.Value != 0)
                return value;
        }
        return null;
    }

    private int? FindPitchIntervalIndex(double secs)
    {
        if (_pitchIntervalSegments.Count == 0)
            return null;
        int left = 0;
        int right = _pitchIntervalSegments.Count - 1;
        while (left <= right)
        {
            int middle = (left + right) / 2;
            if (secs < _pitchIntervalStarts[middle])
                right = middle - 1;
            else if (secs >= _pitchIntervalEnds[middle])
                left = middle + 1;
            else
                return middle;
        }
        return null;
    }

    private static List<List<Point>> SplitAt(List<Point> points, Func<Point, bool> pred)
    {
        var result = new List<List<Point>>();
        var current = new List<Point>();
        foreach (var p in points)
        {
            if (pred(p))
            {
                result.Add(current);
                current = new List<Point>();
            }
            else
            {
                current.Add(p);
            }
        }
        result.Add(current);
        return result;
    }
}
