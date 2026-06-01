using System;
using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.Model;

namespace VOCALOIDPatcher.Formats.Process.Pitch;

public sealed record VocaloidPartPitchData(
    long StartPos,
    IReadOnlyList<VocaloidPartPitchData.Event> Pit,
    IReadOnlyList<VocaloidPartPitchData.Event> Pbs)
{
    public sealed record Event(long Pos, int Value);
}

public static class VocaloidPitchConversion
{
    private const int PitchMaxValue = 8191;
    private const int DefaultPitchBendSensitivity = 2;
    private const long MinBreakLengthBetweenPitchSections = 480L;
    private const long BorderAppendRadius = 5L;

    public static Model.Pitch? PitchFromVocaloidParts(IReadOnlyList<VocaloidPartPitchData> dataByParts)
    {
        var pitchRawDataByPart = dataByParts.Select(part =>
        {
            var pit = part.Pit;
            var pbs = part.Pbs;
            var pitMultipliedByPbs = new List<(long Pos, int Value)>();
            int pitIndex = 0;
            int pbsCurrentValue = DefaultPitchBendSensitivity;
            foreach (var pbsEvent in pbs)
            {
                for (int i = pitIndex; i < pit.Count; i++)
                {
                    var pitEvent = pit[i];
                    if (pitEvent.Pos < pbsEvent.Pos)
                    {
                        pitMultipliedByPbs.Add((pitEvent.Pos, pitEvent.Value * pbsCurrentValue));
                        if (i == pit.Count - 1)
                            pitIndex = i;
                    }
                    else
                    {
                        pitIndex = i;
                        break;
                    }
                }

                pbsCurrentValue = pbsEvent.Value;
            }

            if (pitIndex < pit.Count - 1)
                for (int i = pitIndex; i < pit.Count; i++)
                    pitMultipliedByPbs.Add((pit[i].Pos, pit[i].Value * pbsCurrentValue));

            return pitMultipliedByPbs.Select(p => (p.Pos + part.StartPos, p.Value)).ToList();
        }).ToList();

        var pitchRawData = new List<(long Pos, int Value)>();
        foreach (var element in pitchRawDataByPart)
        {
            if (element.Count == 0)
                continue;
            long firstPos = element[0].Item1;
            int firstInvalid = pitchRawData.FindIndex(p => p.Pos >= firstPos);
            if (firstInvalid < 0)
                pitchRawData.AddRange(element.Select(p => (p.Item1, p.Item2)));
            else
            {
                pitchRawData = pitchRawData.Take(firstInvalid).ToList();
                pitchRawData.AddRange(element.Select(p => (p.Item1, p.Item2)));
            }
        }

        var data = pitchRawData.Select(p => ((long, double?))(p.Pos, p.Value / (double)PitchMaxValue)).ToList();
        return data.Count > 0 ? new Model.Pitch(data, false) : null;
    }

    public static VocaloidPartPitchData? GenerateForVocaloid(this Model.Pitch pitch, IReadOnlyList<Note> notes)
    {
        var data = pitch.GetRelativeData(notes, BorderAppendRadius);
        if (data == null)
            return null;

        var pitchSectioned = new List<List<(long Pos, double Value)>>();
        long currentPos = 0L;
        foreach (var pitchEvent in data)
        {
            if (pitchSectioned.Count == 0)
                pitchSectioned.Add(new List<(long, double)> { pitchEvent });
            else if (pitchEvent.Tick - currentPos >= MinBreakLengthBetweenPitchSections)
                pitchSectioned.Add(new List<(long, double)> { pitchEvent });
            else
                pitchSectioned[^1].Add(pitchEvent);
            currentPos = pitchEvent.Tick;
        }

        var pit = new List<VocaloidPartPitchData.Event>();
        var pbs = new List<VocaloidPartPitchData.Event>();
        foreach (var section in pitchSectioned)
        {
            double maxAbsValue = section.Count > 0 ? section.Max(p => Math.Abs(p.Value)) : 0.0;
            int pbsForThisSection = (int)Math.Ceiling(Math.Abs(maxAbsValue));
            if (pbsForThisSection > DefaultPitchBendSensitivity)
            {
                pbs.Add(new VocaloidPartPitchData.Event(section[0].Pos, pbsForThisSection));
                pbs.Add(new VocaloidPartPitchData.Event(section[^1].Pos + MinBreakLengthBetweenPitchSections / 2, DefaultPitchBendSensitivity));
            }
            else
            {
                pbsForThisSection = DefaultPitchBendSensitivity;
            }

            foreach (var (pitchPos, pitchValue) in section)
            {
                int value = Math.Clamp((int)Math.Round(pitchValue * PitchMaxValue / pbsForThisSection), -PitchMaxValue, PitchMaxValue);
                pit.Add(new VocaloidPartPitchData.Event(pitchPos, value));
            }
        }

        return new VocaloidPartPitchData(0, pit, pbs);
    }
}
