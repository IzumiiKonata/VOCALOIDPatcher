using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VOCALOIDPatcher.Formats.Model;
using VOCALOIDPatcher.Formats.Process;
using VOCALOIDPatcher.Formats.Process.Pitch;
using VOCALOIDPatcher.Formats.Util;

namespace VOCALOIDPatcher.Formats.Io;

public static class Dv
{
    private const double NoteKeySum = 115.5;
    private const int StartingMeasurePosition = -3;
    private const int FixedMeasurePrefix = 4;
    private const int DefaultVolume = 30;
    private const int MinSegmentLength = 480 * 4;

    public static int ConvertNoteKey(int key) => (int)NoteKeySum - key;

    public static double ConvertNoteKey(double key) => NoteKeySum - key;

    public static Model.Project Parse(IReadOnlyList<ImportFile> files, ImportParams parms)
    {
        var file = files[0];
        var reader = new ArrayBufferReader(file.Content);
        var warnings = new List<ImportWarning>();

        reader.Skip(48);

        int tempoCount = reader.ReadInt();
        var tempos = new List<Tempo>();
        for (int i = 0; i < tempoCount; i++)
        {
            long tickPosition = reader.ReadInt();
            double bpm = reader.ReadInt() / 100.0;
            tempos.Add(new Tempo(tickPosition, bpm));
        }

        reader.Skip(4);
        int timeSignatureCount = reader.ReadInt();
        var timeSignatures = new List<TimeSignature>();
        for (int i = 0; i < timeSignatureCount; i++)
        {
            int measurePosition = reader.ReadInt();
            int numerator = reader.ReadInt();
            int denominator = reader.ReadInt();
            timeSignatures.Add(new TimeSignature(measurePosition, numerator, denominator));
        }

        long tickPrefix = GetTickPrefix(timeSignatures);
        var cleanedTempos = CleanupTempos(tempos, tickPrefix, warnings);
        var cleanedTimeSignatures = CleanupTimeSignatures(timeSignatures, warnings);

        int trackCount = reader.ReadInt();
        var tracks = new List<Track>();
        for (int i = 0; i < trackCount; i++)
        {
            var track = ParseTrack(tickPrefix, cleanedTempos, parms, reader);
            if (track != null)
                tracks.Add(track.ValidateNotes());
        }

        tracks = tracks.Select((track, index) => track with { Id = index }).ToList();

        return new Model.Project(Format.Dv, files, file.NameWithoutExtension, tracks, cleanedTimeSignatures, cleanedTempos, FixedMeasurePrefix, warnings);
    }

    private static Track? ParseTrack(long tickPrefix, IReadOnlyList<Tempo> tempos, ImportParams parms, ArrayBufferReader reader)
    {
        int trackType = reader.ReadInt();
        if (trackType != 0)
        {
            SkipRestOfInstTrack(reader);
            return null;
        }

        string trackName = reader.ReadString();
        reader.Skip(14);

        var notesWithPitch = new List<DvNoteWithPitch>();
        int segmentCount = reader.ReadInt();
        var segmentPitchDataList = new List<DvSegmentPitchRawData>();
        for (int s = 0; s < segmentCount; s++)
        {
            int segmentStart = reader.ReadInt();
            reader.ReadInt();
            reader.ReadString();
            reader.ReadString();
            reader.Skip(4);
            int noteCount = reader.ReadInt();
            for (int n = 0; n < noteCount; n++)
            {
                int noteStart = reader.ReadInt();
                int noteLength = reader.ReadInt();
                int noteKey = ConvertNoteKey(reader.ReadInt());
                reader.Skip(4);
                string lyric = reader.ReadString();
                reader.ReadString();
                reader.Skip(1);
                var vibratoData = ParseNoteVibratoData(reader);
                reader.ReadBytes();
                reader.Skip(18);
                int benDep = reader.ReadInt();
                int benLen = reader.ReadInt();
                int porTail = reader.ReadInt();
                int porHead = reader.ReadInt();
                reader.ReadInt();
                reader.ReadBytes();
                reader.ReadInt();
                var note = new Note(0, noteKey, lyric, segmentStart + noteStart - tickPrefix, segmentStart + noteStart - tickPrefix + noteLength);
                notesWithPitch.Add(new DvNoteWithPitch(note, porHead, porTail, benLen, benDep, vibratoData));
            }

            reader.ReadBytes();
            if (parms.SimpleImport)
                SkipPitchData(reader);
            else
                segmentPitchDataList.Add(ParsePitchData(segmentStart - tickPrefix, reader));
            SkipRestOfSegment(reader);
        }

        var notesValidated = notesWithPitch.ValidateNotes();
        Model.Pitch? pitch = parms.SimpleImport ? null : DeepVocalPitchConversion.PitchFromDvTrack(segmentPitchDataList, notesValidated, tempos);
        return new Track(0, trackName, notesValidated.Select(n => n.Note).ToList(), pitch);
    }

    private static List<(int, int)> ParseNoteVibratoData(ArrayBufferReader reader)
    {
        reader.ReadInt();
        reader.ReadBytes();
        reader.ReadBytes();
        reader.ReadInt();
        int pointLength = reader.ReadInt();
        var data = new List<(int, int)>();
        for (int i = 0; i < pointLength; i++)
            data.Add((reader.ReadInt(), reader.ReadInt()));
        return data;
    }

    private static DvSegmentPitchRawData ParsePitchData(long tickOffset, ArrayBufferReader reader)
    {
        reader.ReadInt();
        int pointLength = reader.ReadInt();
        var data = new List<(int, int)>();
        for (int i = 0; i < pointLength; i++)
            data.Add((reader.ReadInt(), reader.ReadInt()));
        return new DvSegmentPitchRawData(tickOffset, data);
    }

    private static void SkipPitchData(ArrayBufferReader reader) => reader.ReadBytes();

    private static void SkipRestOfInstTrack(ArrayBufferReader reader)
    {
        reader.ReadBytes();
        reader.Skip(14);
        if (reader.ReadInt() > 0)
        {
            reader.Skip(8);
            reader.ReadBytes();
            reader.ReadBytes();
        }
    }

    private static void SkipRestOfSegment(ArrayBufferReader reader)
    {
        for (int i = 0; i < 5; i++)
            reader.ReadBytes();
    }

    private static long GetTickPrefix(IReadOnlyList<TimeSignature> timeSignatures)
    {
        var counter = new TickCounter();
        foreach (var ts in timeSignatures
                     .Select(t => t with { MeasurePosition = t.MeasurePosition - StartingMeasurePosition })
                     .Where(t => t.MeasurePosition < FixedMeasurePrefix))
            counter.GoToMeasure(ts);
        counter.GoToMeasure(FixedMeasurePrefix);
        return counter.Tick;
    }

    private static List<TimeSignature> CleanupTimeSignatures(IReadOnlyList<TimeSignature> timeSignatures, List<ImportWarning> warnings)
    {
        var results = timeSignatures
            .Select(t => t with { MeasurePosition = t.MeasurePosition - StartingMeasurePosition - FixedMeasurePrefix })
            .ToList();
        int firstIndex = results.FindLastIndex(t => t.MeasurePosition <= 0);
        for (int i = 0; i < firstIndex; i++)
        {
            warnings.Add(new ImportWarning.TimeSignatureIgnoredInPreMeasure(results[0]));
            results.RemoveAt(0);
        }

        results[0] = results[0] with { MeasurePosition = 0 };
        return results;
    }

    private static List<Tempo> CleanupTempos(IReadOnlyList<Tempo> tempos, long tickPrefix, List<ImportWarning> warnings)
    {
        var results = tempos.Select(t => t with { TickPosition = t.TickPosition - tickPrefix }).ToList();
        int firstIndex = results.FindLastIndex(t => t.TickPosition <= 0);
        for (int i = 0; i < firstIndex; i++)
        {
            warnings.Add(new ImportWarning.TempoIgnoredInPreMeasure(results[0]));
            results.RemoveAt(0);
        }

        results[0] = results[0] with { TickPosition = 0 };
        return results;
    }

    public static ExportResult Generate(Model.Project project, IReadOnlyList<FeatureConfig> features)
    {
        var content = GenerateContent(project, features);
        return new ExportResult(content, FormatRegistry.Get(Format.Dv).GetFileName(project.Name), new List<ExportNotification>());
    }

    private static byte[] GenerateContent(Model.Project project, IReadOnlyList<FeatureConfig> features)
    {
        var bytes = new List<byte>();
        bytes.AddRange(DvData.Header);
        long tickPrefix = (long)project.TimeSignatures[0].TicksInMeasure * FixedMeasurePrefix;

        var mainBlock = new List<byte>();
        mainBlock.AddRange(Encoding.UTF8.GetBytes("ext1ext2ext3ext4ext5ext6ext7"));
        mainBlock.AddListBlock(GenerateTempos(project.Tempos, tickPrefix));
        mainBlock.AddListBlock(GenerateTimeSignatures(project.TimeSignatures));
        mainBlock.AddList(project.Tracks.Select(t => (IReadOnlyList<byte>)GenerateTrack(t, tickPrefix, features)).ToList());

        bytes.AddBlock(mainBlock);
        return bytes.ToArray();
    }

    private static List<IReadOnlyList<byte>> GenerateTempos(IReadOnlyList<Tempo> tempos, long tickPrefix)
    {
        var list = new List<Tempo> { tempos[0] with { TickPosition = 0 } };
        list.AddRange(tempos.Skip(1).Select(t => t with { TickPosition = t.TickPosition + tickPrefix }));
        return list.Select(t =>
        {
            var b = new List<byte>();
            b.AddInt((int)t.TickPosition);
            b.AddInt((int)(t.Bpm * 100));
            return (IReadOnlyList<byte>)b;
        }).ToList();
    }

    private static List<IReadOnlyList<byte>> GenerateTimeSignatures(IReadOnlyList<TimeSignature> timeSignatures)
    {
        var list = new List<TimeSignature> { timeSignatures[0] with { MeasurePosition = StartingMeasurePosition } };
        list.AddRange(timeSignatures.Skip(1).Select(t => t with { MeasurePosition = t.MeasurePosition + FixedMeasurePrefix + StartingMeasurePosition }));
        return list.Select(t =>
        {
            var b = new List<byte>();
            b.AddInt(t.MeasurePosition);
            b.AddInt(t.Numerator);
            b.AddInt(t.Denominator);
            return (IReadOnlyList<byte>)b;
        }).ToList();
    }

    private static List<byte> GenerateTrack(Track track, long tickPrefix, IReadOnlyList<FeatureConfig> features)
    {
        var segmentBytes = new List<byte>();
        segmentBytes.AddInt((int)tickPrefix);
        int lastNoteTickOff = track.Notes.Count > 0 ? (int)track.Notes[^1].TickOff : 0;
        segmentBytes.AddInt(Math.Max(lastNoteTickOff, MinSegmentLength));
        segmentBytes.AddString(track.Name);
        segmentBytes.AddString("");
        segmentBytes.AddListBlock(track.Notes.Select(n => (IReadOnlyList<byte>)GenerateNote(n, features)).ToList());
        segmentBytes.AddRange(DvData.SegmentDefaultParameterData1);
        if (features.Contains(Feature.ConvertPitch))
        {
            var pitch = track.Pitch?.GenerateForDv(track.Notes);
            if (pitch == null)
                segmentBytes.AddRange(DvData.SegmentDefaultParameterDataPitch);
            else
                segmentBytes.AddListBlock(GeneratePitchPoints(pitch.Data));
        }
        else
        {
            segmentBytes.AddRange(DvData.SegmentDefaultParameterDataPitch);
        }

        segmentBytes.AddRange(DvData.SegmentDefaultParameterData2);

        var result = new List<byte>();
        result.AddInt(0);
        result.AddString(track.Name);
        result.Add(0x00);
        result.Add(0x00);
        result.AddInt(DefaultVolume);
        result.AddInt(0);
        result.AddListBlock(new List<IReadOnlyList<byte>> { segmentBytes });
        return result;
    }

    private static List<byte> GenerateNote(Note note, IReadOnlyList<FeatureConfig> features)
    {
        var bytes = new List<byte>();
        bytes.AddInt((int)note.TickOn);
        bytes.AddInt((int)note.Length);
        bytes.AddInt(ConvertNoteKey(note.Key));
        bytes.AddInt(0);
        bytes.AddString(note.Lyric);
        bytes.AddString(note.Lyric);
        bytes.Add(0x00);

        var inner = new List<byte>();
        inner.AddListBlock(new List<IReadOnlyList<byte>> { Pair(-1, 0), Pair(100001, 0) });
        inner.AddListBlock(new List<IReadOnlyList<byte>> { Pair(-1, 0), Pair(100001, 0) });
        inner.AddListBlock(new List<IReadOnlyList<byte>> { Pair(0, 0), Pair(1124, 0) });
        bytes.AddBlock(inner);

        bytes.AddBlock(DvData.NoteUnknownDataBlock);
        bytes.AddRange(DvData.NoteUnknownPhonemes);
        if (features.Contains(Feature.ConvertPitch))
        {
            for (int i = 0; i < 4; i++)
                bytes.AddInt(0);
        }
        else
        {
            bytes.AddInt(8);
            bytes.AddInt(5);
            bytes.AddInt(16);
            bytes.AddInt(16);
        }

        bytes.AddInt(-1);
        bytes.AddString("");
        bytes.AddInt(-1);
        return bytes;
    }

    private static List<byte> Pair(int a, int b)
    {
        var b2 = new List<byte>();
        b2.AddInt(a);
        b2.AddInt(b);
        return b2;
    }

    private static List<IReadOnlyList<byte>> GeneratePitchPoints(IReadOnlyList<(int Tick, int Value)> data) =>
        data.Select(point =>
        {
            var b = new List<byte>();
            b.AddInt(point.Tick);
            b.AddInt(point.Value);
            return (IReadOnlyList<byte>)b;
        }).ToList();
}
