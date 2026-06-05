using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Dv;

internal static class DvBinary
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("SHARPKEY");
    private static readonly string[] FeatureNames = { "ext1", "ext2", "ext3", "ext4", "ext5", "ext6", "ext7" };
    private static readonly Encoding Utf8 = new UTF8Encoding(false);

    public static DvProject Parse(byte[] content)
    {
        using var stream = new MemoryStream(content, false);
        using var reader = new BinaryReader(stream);
        var magic = reader.ReadBytes(Magic.Length);
        for (int i = 0; i < Magic.Length; i++)
        {
            if (i >= magic.Length || magic[i] != Magic[i])
                throw new InvalidDataException("Not a DeepVocal project file");
        }
        var project = new DvProject
        {
            Version = ReadInt32(reader),
        };
        int innerLength = ReadInt32(reader);
        byte[] innerBytes = reader.ReadBytes(innerLength);
        using var innerStream = new MemoryStream(innerBytes, false);
        using var innerReader = new BinaryReader(innerStream);
        project.InnerProject = ReadInnerProject(innerReader);
        return project;
    }

    public static byte[] Build(DvProject project)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(Magic);
        WriteInt32(writer, project.Version);
        byte[] inner = BuildInnerProject(project.InnerProject);
        WriteInt32(writer, inner.Length);
        writer.Write(inner);
        writer.Flush();
        return stream.ToArray();
    }

    private static DvInnerProject ReadInnerProject(BinaryReader reader)
    {
        var inner = new DvInnerProject();
        foreach (string feature in FeatureNames)
        {
            byte[] expected = Encoding.ASCII.GetBytes(feature);
            long pos = reader.BaseStream.Position;
            if (reader.BaseStream.Length - pos >= expected.Length)
            {
                byte[] peek = reader.ReadBytes(expected.Length);
                bool match = peek.Length == expected.Length;
                for (int i = 0; match && i < expected.Length; i++)
                {
                    if (peek[i] != expected[i])
                        match = false;
                }
                if (match)
                    inner.Features.Add(feature);
                else
                    reader.BaseStream.Position = pos;
            }
        }
        SkipInt32(reader);
        int tempoCount = ReadInt32(reader);
        for (int i = 0; i < tempoCount; i++)
        {
            inner.Tempos.Add(new DvTempo
            {
                Position = ReadInt32(reader),
                Bpm = ReadInt32(reader),
            });
        }
        SkipInt32(reader);
        int tsCount = ReadInt32(reader);
        for (int i = 0; i < tsCount; i++)
        {
            inner.TimeSignatures.Add(new DvTimeSignature
            {
                MeasurePosition = ReadInt32(reader),
                Numerator = ReadInt32(reader),
                Denominator = ReadInt32(reader),
            });
        }
        int trackCount = ReadInt32(reader);
        for (int i = 0; i < trackCount; i++)
            inner.Tracks.Add(ReadTrack(reader, inner.Features));
        return inner;
    }

    private static byte[] BuildInnerProject(DvInnerProject inner)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        foreach (string feature in FeatureNames)
            writer.Write(Encoding.ASCII.GetBytes(feature));
        byte[] tempoBlock = BuildArray(inner.Tempos, (w, t) =>
        {
            WriteInt32(w, t.Position);
            WriteInt32(w, t.Bpm);
        });
        WritePrefixed(writer, tempoBlock);
        byte[] tsBlock = BuildArray(inner.TimeSignatures, (w, t) =>
        {
            WriteInt32(w, t.MeasurePosition);
            WriteInt32(w, t.Numerator);
            WriteInt32(w, t.Denominator);
        });
        WritePrefixed(writer, tsBlock);
        WriteInt32(writer, inner.Tracks.Count);
        foreach (var track in inner.Tracks)
            WriteTrack(writer, track);
        writer.Flush();
        return stream.ToArray();
    }

    private static DvTrack ReadTrack(BinaryReader reader, HashSet<string> features)
    {
        var track = new DvTrack
        {
            TrackType = (DvTrackType)ReadInt32(reader),
        };
        if (track.TrackType == DvTrackType.Singing)
            track.SingingTrack = ReadSingingTrack(reader, features);
        else
            track.AudioTrack = ReadAudioTrack(reader);
        return track;
    }

    private static void WriteTrack(BinaryWriter writer, DvTrack track)
    {
        WriteInt32(writer, (int)track.TrackType);
        if (track.TrackType == DvTrackType.Singing && track.SingingTrack != null)
            WriteSingingTrack(writer, track.SingingTrack);
        else if (track.AudioTrack != null)
            WriteAudioTrack(writer, track.AudioTrack);
    }

    private static DvSingingTrack ReadSingingTrack(BinaryReader reader, HashSet<string> features)
    {
        var track = new DvSingingTrack
        {
            Name = ReadStr(reader),
            Mute = reader.ReadByte(),
            Solo = reader.ReadByte(),
            Volume = ReadInt32(reader),
            Balance = ReadInt32(reader),
        };
        SkipInt32(reader);
        int segCount = ReadInt32(reader);
        for (int i = 0; i < segCount; i++)
            track.Segments.Add(ReadSegment(reader, features));
        return track;
    }

    private static void WriteSingingTrack(BinaryWriter writer, DvSingingTrack track)
    {
        WriteStr(writer, track.Name);
        writer.Write(track.Mute);
        writer.Write(track.Solo);
        WriteInt32(writer, track.Volume);
        WriteInt32(writer, track.Balance);
        byte[] segmentsBlock = BuildArray(track.Segments, WriteSegment);
        WritePrefixed(writer, segmentsBlock);
    }

    private static DvAudioTrack ReadAudioTrack(BinaryReader reader)
    {
        var track = new DvAudioTrack
        {
            Name = ReadStr(reader),
            Mute = reader.ReadByte(),
            Solo = reader.ReadByte(),
            Volume = ReadInt32(reader),
            Balance = ReadInt32(reader),
        };
        SkipInt32(reader);
        int count = ReadInt32(reader);
        for (int i = 0; i < count; i++)
        {
            track.Infos.Add(new DvAudioInfo
            {
                Start = ReadInt32(reader),
                Length = ReadInt32(reader),
                Name = ReadStr(reader),
                Path = ReadStr(reader),
            });
        }
        return track;
    }

    private static void WriteAudioTrack(BinaryWriter writer, DvAudioTrack track)
    {
        WriteStr(writer, track.Name);
        writer.Write(track.Mute);
        writer.Write(track.Solo);
        WriteInt32(writer, track.Volume);
        WriteInt32(writer, track.Balance);
        byte[] infosBlock = BuildArray(track.Infos, (w, info) =>
        {
            WriteInt32(w, info.Start);
            WriteInt32(w, info.Length);
            WriteStr(w, info.Name);
            WriteStr(w, info.Path);
        });
        WritePrefixed(writer, infosBlock);
    }

    private static DvSegment ReadSegment(BinaryReader reader, HashSet<string> features)
    {
        var segment = new DvSegment
        {
            Start = ReadInt32(reader),
            Length = ReadInt32(reader),
            Name = ReadStr(reader),
            SingerName = ReadStr(reader),
        };
        SkipInt32(reader);
        int noteCount = ReadInt32(reader);
        for (int i = 0; i < noteCount; i++)
            segment.Notes.Add(ReadNote(reader, features));
        segment.VolumeData = ReadParam(reader);
        segment.PitchData = ReadParam(reader);
        segment.BreathData = ReadParam(reader);
        if (features.Contains("ext3"))
            segment.Ext3Data = ReadParam(reader);
        if (features.Contains("ext5"))
            segment.Ext5Data = ReadParam(reader);
        if (features.Contains("ext6"))
            segment.Ext6Data = ReadParam(reader);
        if (features.Contains("ext7"))
            segment.Ext7Data = ReadParam(reader);
        return segment;
    }

    private static void WriteSegment(BinaryWriter writer, DvSegment segment)
    {
        WriteInt32(writer, segment.Start);
        WriteInt32(writer, segment.Length);
        WriteStr(writer, segment.Name);
        WriteStr(writer, segment.SingerName);
        byte[] notesBlock = BuildArray(segment.Notes, WriteNote);
        WritePrefixed(writer, notesBlock);
        WriteParam(writer, segment.VolumeData);
        WriteParam(writer, segment.PitchData);
        WriteParam(writer, segment.BreathData);
        if (segment.Ext3Data != null)
            WriteParam(writer, segment.Ext3Data);
        if (segment.Ext5Data != null)
            WriteParam(writer, segment.Ext5Data);
        if (segment.Ext6Data != null)
            WriteParam(writer, segment.Ext6Data);
        if (segment.Ext7Data != null)
            WriteParam(writer, segment.Ext7Data);
    }

    private static DvNote ReadNote(BinaryReader reader, HashSet<string> features)
    {
        var note = new DvNote
        {
            Start = ReadInt32(reader),
            Length = ReadInt32(reader),
            Key = ReadInt32(reader),
            Vibrato = ReadInt32(reader),
            Phoneme = ReadStr(reader),
            Word = ReadStr(reader),
            Padding1 = reader.ReadByte(),
        };
        SkipInt32(reader);
        note.NoteVibratoData = ReadNoteParameter(reader);
        int unknownByteLen = ReadInt32(reader);
        int unknownCount = ReadInt32(reader);
        for (int i = 0; i < unknownCount; i++)
            note.Unknown.Add(reader.ReadSingle());
        if (features.Contains("ext1"))
        {
            note.Phonemes = new DvPhoneme
            {
                Unknown1 = reader.ReadSByte(),
                ConsonantRate = reader.ReadSingle(),
                VowelModified = reader.ReadSByte(),
                Medial = reader.ReadSingle(),
                Rime = reader.ReadSingle(),
                Ending = reader.ReadSingle(),
            };
        }
        if (features.Contains("ext2"))
        {
            note.BenDepth = ReadInt32(reader);
            note.BenLength = ReadInt32(reader);
            note.PorTail = ReadInt32(reader);
            note.PorHead = ReadInt32(reader);
        }
        if (features.Contains("ext4"))
            note.Timbre = ReadInt32(reader);
        if (features.Contains("ext7"))
        {
            note.CrossLyric = ReadStr(reader);
            note.CrossTimbre = ReadInt32(reader);
        }
        return note;
    }

    private static void WriteNote(BinaryWriter writer, DvNote note)
    {
        WriteInt32(writer, note.Start);
        WriteInt32(writer, note.Length);
        WriteInt32(writer, note.Key);
        WriteInt32(writer, note.Vibrato);
        WriteStr(writer, note.Phoneme);
        WriteStr(writer, note.Word);
        writer.Write(note.Padding1);
        byte[] vibratoBlock = BuildNoteParameter(note.NoteVibratoData);
        WritePrefixed(writer, vibratoBlock);
        byte[] unknownBlock = BuildArray(note.Unknown, (w, value) => w.Write(value));
        WritePrefixed(writer, unknownBlock);
        if (note.Phonemes != null)
        {
            writer.Write(note.Phonemes.Unknown1);
            writer.Write(note.Phonemes.ConsonantRate);
            writer.Write(note.Phonemes.VowelModified);
            writer.Write(note.Phonemes.Medial);
            writer.Write(note.Phonemes.Rime);
            writer.Write(note.Phonemes.Ending);
        }
        if (note.BenDepth != null)
        {
            WriteInt32(writer, note.BenDepth.Value);
            WriteInt32(writer, note.BenLength ?? 0);
            WriteInt32(writer, note.PorTail ?? 0);
            WriteInt32(writer, note.PorHead ?? 0);
        }
        if (note.Timbre != null)
            WriteInt32(writer, note.Timbre.Value);
        if (note.CrossLyric != null)
        {
            WriteStr(writer, note.CrossLyric);
            WriteInt32(writer, note.CrossTimbre ?? -1);
        }
    }

    private static DvNoteParameter ReadNoteParameter(BinaryReader reader)
    {
        return new DvNoteParameter
        {
            AmplitudePoints = ReadParam(reader),
            FrequencyPoints = ReadParam(reader),
            VibratoPoints = ReadParam(reader),
        };
    }

    private static byte[] BuildNoteParameter(DvNoteParameter param)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        WriteParam(writer, param.AmplitudePoints);
        WriteParam(writer, param.FrequencyPoints);
        WriteParam(writer, param.VibratoPoints);
        writer.Flush();
        return stream.ToArray();
    }

    private static List<DvPoint> ReadParam(BinaryReader reader)
    {
        SkipInt32(reader);
        int count = ReadInt32(reader);
        var points = new List<DvPoint>(count);
        for (int i = 0; i < count; i++)
        {
            points.Add(new DvPoint
            {
                X = ReadInt32(reader),
                Y = ReadInt32(reader),
            });
        }
        return points;
    }

    private static void WriteParam(BinaryWriter writer, List<DvPoint> points)
    {
        byte[] block = BuildArray(points, (w, p) =>
        {
            WriteInt32(w, p.X);
            WriteInt32(w, p.Y);
        });
        WritePrefixed(writer, block);
    }

    private static byte[] BuildArray<T>(List<T> items, Action<BinaryWriter, T> writeItem)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        WriteInt32(writer, items.Count);
        foreach (var item in items)
            writeItem(writer, item);
        writer.Flush();
        return stream.ToArray();
    }

    private static void WritePrefixed(BinaryWriter writer, byte[] block)
    {
        WriteInt32(writer, block.Length);
        writer.Write(block);
    }

    private static int ReadInt32(BinaryReader reader) => reader.ReadInt32();

    private static void WriteInt32(BinaryWriter writer, int value) => writer.Write(value);

    private static void SkipInt32(BinaryReader reader) => reader.ReadInt32();

    private static string ReadStr(BinaryReader reader)
    {
        int length = ReadInt32(reader);
        if (length <= 0)
            return "";
        byte[] bytes = reader.ReadBytes(length);
        return Utf8.GetString(bytes);
    }

    private static void WriteStr(BinaryWriter writer, string value)
    {
        byte[] bytes = Utf8.GetBytes(value ?? "");
        WriteInt32(writer, bytes.Length);
        writer.Write(bytes);
    }
}
