using System;
using System.Collections.Generic;
using VOCALOIDPatcher.Formats.LibreSvip.Core;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Acep;

public sealed class AcepBasePitchCurve
{
    private struct NoteInSeconds
    {
        public int Semitone;
        public double Start;
        public double End;
    }

    private readonly PiecewiseIntervalDict _vibratoValue = new();
    private readonly PiecewiseIntervalDict _vibratoCoef = new();
    private readonly List<double> _valuesInSemitone;

    public AcepBasePitchCurve(IEnumerable<AcepNote> notes, TimeSynchronizer synchronizer, int tickOffset = 0)
    {
        var noteList = new List<NoteInSeconds>();
        foreach (var note in notes)
        {
            double noteEnd = synchronizer.GetActualSecsFromTicks(note.Pos + note.Dur + tickOffset);
            noteList.Add(new NoteInSeconds
            {
                Start = synchronizer.GetActualSecsFromTicks(note.Pos + tickOffset),
                End = noteEnd,
                Semitone = note.Pitch,
            });
            if (note.Vibrato != null)
            {
                var vibrato = note.Vibrato;
                double vibratoStart = synchronizer.GetActualSecsFromTicks(
                    (int)(note.Pos + note.Vibrato.StartPos + tickOffset));
                double vibratoDuration = noteEnd - vibratoStart;
                _vibratoValue.Set(vibratoStart, noteEnd,
                    secs => Math.Sin(Math.PI * (2 * (secs - vibratoStart) * vibrato.Frequency - vibrato.Phase))
                            * vibrato.Amplitude * 0.5);
                double attackTime = vibratoStart + vibrato.AttackRatio * vibratoDuration;
                double releaseTime = noteEnd - vibrato.ReleaseRatio * vibratoDuration;
                if (vibrato.ReleaseRatio != 0)
                {
                    double rStart = releaseTime;
                    double rLevel = vibrato.ReleaseLevel;
                    _vibratoCoef.Set(releaseTime, noteEnd,
                        x => MusicMath.LinearInterpolation(x, (rStart, rLevel), (noteEnd, 0)));
                }
                double aTime = attackTime;
                double aLevel = vibrato.AttackLevel;
                double rLevel2 = vibrato.ReleaseLevel;
                double rTime = releaseTime;
                _vibratoCoef.Set(attackTime, releaseTime,
                    x => MusicMath.LinearInterpolation(x, (aTime, aLevel), (rTime, rLevel2)));
                if (vibrato.AttackRatio != 0)
                {
                    double vStart = vibratoStart;
                    double aLevel2 = vibrato.AttackLevel;
                    _vibratoCoef.Set(vibratoStart, attackTime,
                        x => MusicMath.LinearInterpolation(x, (vStart, 0), (aTime, aLevel2)));
                }
            }
        }
        _valuesInSemitone = Convolve(noteList);
    }

    private static List<double> Convolve(List<NoteInSeconds> noteList)
    {
        if (noteList.Count == 0)
            return new List<double>();
        int totalPoints = (int)Math.Round(1000 * (noteList[^1].End + 0.12)) + 1;
        if (totalPoints < 1)
            totalPoints = 1;
        var initValues = new double[totalPoints];
        int noteIndex = 0;
        for (int i = 0; i < totalPoints; i++)
        {
            initValues[i] = noteList[noteIndex].Semitone;
            if (noteIndex < noteList.Count - 1)
            {
                double ts = 0.001 * i;
                if (ts >= 0.5 * (noteList[noteIndex].End + noteList[noteIndex + 1].Start))
                    noteIndex++;
            }
        }
        var kernel = new double[119];
        for (int i = 0; i < 119; i++)
        {
            double ts = 0.001 * (i - 59);
            kernel[i] = Math.Cos(Math.PI * ts / 0.12);
        }
        double kernelSum = 0;
        for (int i = 0; i < 119; i++)
            kernelSum += kernel[i];
        for (int i = 0; i < 119; i++)
            kernel[i] /= kernelSum;

        int n = initValues.Length;
        int m = kernel.Length;
        var full = new double[n + m - 1];
        for (int i = 0; i < n; i++)
        {
            double v = initValues[i];
            if (v == 0)
                continue;
            for (int j = 0; j < m; j++)
                full[i + j] += v * kernel[j];
        }
        var result = new List<double>();
        for (int i = 59; i < full.Length - 59; i++)
            result.Add(full[i]);
        return result;
    }

    public double SemitoneValueAt(double seconds)
    {
        double position = 1000 * Math.Max(0.0, seconds);
        int leftIndex = (int)Math.Floor(position);
        double lambda = position - leftIndex;
        int count = _valuesInSemitone.Count;
        if (count == 0)
            return 0.0;
        int clippedLeft = Math.Min(leftIndex, count - 1);
        int clippedRight = Math.Min(clippedLeft + 1, count - 1);
        double pitchValue = (1 - lambda) * _valuesInSemitone[clippedLeft] + lambda * _valuesInSemitone[clippedRight];
        double? vibratoValue = _vibratoValue.Get(seconds);
        if (vibratoValue != null)
            pitchValue += vibratoValue.Value * _vibratoCoef.Get(seconds, 0);
        return pitchValue;
    }
}
