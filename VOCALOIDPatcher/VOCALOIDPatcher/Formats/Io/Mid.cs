using System;
using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.Model;
using VOCALOIDPatcher.Formats.Util;

namespace VOCALOIDPatcher.Formats.Io;

public static class Mid
{
    public const bool IsLittleEndian = false;

    private static readonly byte[] HeaderLabel = { 0x4d, 0x54, 0x68, 0x64 };
    private static readonly byte[] TimeDivisions = { 0x01, 0xe0 };
    private static readonly byte[] TrackLabel = { 0x4d, 0x54, 0x72, 0x6b };

    public static (List<Tempo> Tempos, List<TimeSignature> TimeSignatures, long TickPrefix) ParseMasterTrack(
        int timeDivision,
        IReadOnlyList<MidiEvent> events,
        int measurePrefix,
        List<ImportWarning> warnings)
    {
        long tickPosition = 0;
        var tickCounter = new TickCounter();
        var rawTempos = new List<Tempo>();
        var rawTimeSignatures = new List<TimeSignature>();

        foreach (var ev in events)
        {
            tickPosition += MidiUtil.ConvertInputTimeToStandardTime(ev.DeltaTime, timeDivision);
            switch (ev.Type)
            {
                case "setTempo":
                    rawTempos.Add(new Tempo(tickPosition, MidiUtil.ConvertMidiTempoToBpm(ev.MicrosecondsPerBeat)));
                    break;
                case "timeSignature":
                    tickCounter.GoToTick(tickPosition, ev.Numerator, ev.Denominator);
                    rawTimeSignatures.Add(new TimeSignature(tickCounter.Measure, ev.Numerator, ev.Denominator));
                    break;
            }
        }

        if (rawTimeSignatures.Count == 0)
        {
            rawTimeSignatures.Add(TimeSignature.Default);
            warnings.Add(new ImportWarning.TimeSignatureNotFound());
        }

        if (rawTempos.Count == 0)
        {
            rawTempos.Add(Tempo.Default);
            warnings.Add(new ImportWarning.TempoNotFound());
        }

        long tickPrefix = GetTickPrefix(rawTimeSignatures, measurePrefix);

        var timeSignatures = rawTimeSignatures.Select(t => t with { MeasurePosition = t.MeasurePosition - measurePrefix }).ToList();
        int firstTimeSignatureIndex = timeSignatures.FindLastIndex(t => t.MeasurePosition <= 0);
        for (int i = 0; i < firstTimeSignatureIndex; i++)
        {
            var removed = timeSignatures[0];
            timeSignatures.RemoveAt(0);
            warnings.Add(new ImportWarning.TimeSignatureIgnoredInPreMeasure(removed));
        }

        timeSignatures[0] = timeSignatures[0] with { MeasurePosition = 0 };

        var tempos = rawTempos.Select(t => t with { TickPosition = t.TickPosition - tickPrefix }).ToList();
        int firstTempoIndex = tempos.FindLastIndex(t => t.TickPosition <= 0);
        for (int i = 0; i < firstTempoIndex; i++)
        {
            var removed = tempos[0];
            tempos.RemoveAt(0);
            warnings.Add(new ImportWarning.TempoIgnoredInPreMeasure(removed));
        }

        tempos[0] = tempos[0] with { TickPosition = 0 };

        return (tempos, timeSignatures, tickPrefix);
    }

    private static long GetTickPrefix(IReadOnlyList<TimeSignature> timeSignatures, int measurePrefix)
    {
        var counter = new TickCounter();
        foreach (var ts in timeSignatures.Where(t => t.MeasurePosition < measurePrefix))
            counter.GoToMeasure(ts);
        counter.GoToMeasure(measurePrefix);
        return counter.Tick;
    }

    public static List<string> ExtractVsqTextsFromMetaEvents(IReadOnlyList<List<MidiEvent>> midiTracks)
    {
        var sjis = Texts.ShiftJis();
        return midiTracks.Skip(1).Select(track =>
        {
            string accumulator = "";
            foreach (var element in track)
            {
                if (element.Type != "text" || element.TextBytes == null)
                    continue;
                string text = sjis.GetString(element.TextBytes);
                text = text.Length >= 3 ? text[3..] : "";
                int colon = text.IndexOf(':');
                text = colon >= 0 ? text[(colon + 1)..] : text;
                accumulator += text;
            }

            return accumulator;
        }).ToList();
    }

    public static byte[] GenerateContent(Project project, Func<Track, int, int, List<byte>> generateTrackBytes)
    {
        var bytes = new List<byte>();
        bytes.AddRange(HeaderLabel);
        bytes.AddInt(6, IsLittleEndian);
        bytes.AddShort(1, IsLittleEndian);
        bytes.AddShort((short)(project.Tracks.Count + 1), IsLittleEndian);
        bytes.AddRange(TimeDivisions);

        int tickPrefix = project.TimeSignatures[0].TicksInMeasure * project.MeasurePrefix;

        bytes.AddRange(TrackLabel);
        bytes.AddBlock(GenerateMasterTrack(project, tickPrefix), IsLittleEndian);

        foreach (var track in project.Tracks)
        {
            bytes.AddRange(TrackLabel);
            bytes.AddBlock(generateTrackBytes(track, tickPrefix, project.MeasurePrefix), IsLittleEndian);
        }

        return bytes.ToArray();
    }

    private static List<byte> GenerateMasterTrack(Project project, int tickPrefix)
    {
        var bytes = new List<byte>();
        bytes.Add(0x00);
        bytes.AddRange(MidiUtil.MetaType.TrackName.EventHeaderBytes());
        bytes.AddString("Master Track", IsLittleEndian, lengthInVariableLength: true);

        var tickEventPairs = new List<(long Tick, object Event)>();
        foreach (var tempo in project.Tempos)
        {
            long tick = tempo.TickPosition == 0L ? 0L : tempo.TickPosition + tickPrefix;
            tickEventPairs.Add((tick, tempo));
        }

        var counter = new TickCounter();
        counter.GoToMeasure(project.TimeSignatures[0]);
        tickEventPairs.Add((0L, project.TimeSignatures[0]));
        foreach (var ts in project.TimeSignatures.Skip(1))
        {
            counter.GoToMeasure(ts);
            tickEventPairs.Add((counter.OutputTick + tickPrefix, ts));
        }

        var sorted = tickEventPairs.OrderBy(p => p.Tick).ToList();
        var deltaEventPairs = new List<(long Delta, object Event)> { (0L, sorted[0].Event) };
        for (int i = 0; i < sorted.Count - 1; i++)
            deltaEventPairs.Add((sorted[i + 1].Tick - sorted[i].Tick, sorted[i + 1].Event));

        foreach (var (delta, ev) in deltaEventPairs)
        {
            bytes.AddIntVariableLengthBigEndian((int)delta);
            if (ev is TimeSignature ts)
            {
                bytes.AddRange(MidiUtil.MetaType.TimeSignature.EventHeaderBytes());
                bytes.AddBlock(MidiUtil.GenerateMidiTimeSignatureBytes(ts.Numerator, ts.Denominator), IsLittleEndian, lengthInVariableLength: true);
            }
            else if (ev is Tempo tempo)
            {
                bytes.AddRange(MidiUtil.MetaType.Tempo.EventHeaderBytes());
                int midiTempo = MidiUtil.ConvertBpmToMidiTempo(tempo.Bpm);
                var tempoBytes = new List<byte> { (byte)((midiTempo >> 16) & 0xff), (byte)((midiTempo >> 8) & 0xff), (byte)(midiTempo & 0xff) };
                bytes.AddBlock(tempoBytes, IsLittleEndian, lengthInVariableLength: true);
            }
        }

        bytes.Add(0x00);
        bytes.AddRange(MidiUtil.MetaType.EndOfTrack.EventHeaderBytes());
        bytes.Add(0x00);
        return bytes;
    }
}
