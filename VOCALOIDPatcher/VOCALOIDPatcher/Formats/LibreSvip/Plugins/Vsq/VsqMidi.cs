using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Vsq;

internal sealed class MidiTempoEvent
{
    public int Tick { get; set; }
    public int Tempo { get; set; }
}

internal sealed class MidiTimeSigEvent
{
    public int Tick { get; set; }
    public int Numerator { get; set; }
    public int Denominator { get; set; }
}

internal sealed class MidiTextEvent
{
    public int Tick { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
}

internal sealed class MidiTrackData
{
    public List<MidiTempoEvent> Tempos { get; } = new();
    public List<MidiTimeSigEvent> TimeSigs { get; } = new();
    public List<MidiTextEvent> TextEvents { get; } = new();
    public bool IsMaster { get; set; }
}

internal sealed class MidiFile
{
    public int TicksPerBeat { get; set; }
    public List<MidiTrackData> Tracks { get; } = new();

    public static MidiFile Parse(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var reader = new BinaryReader(ms);
        return Parse(reader);
    }

    private static MidiFile Parse(BinaryReader reader)
    {
        var header = reader.ReadBytes(4);
        if (header[0] != 0x4D || header[1] != 0x54 || header[2] != 0x68 || header[3] != 0x64)
            throw new InvalidDataException("Not a MIDI file");
        int headerLen = ReadInt32BE(reader);
        if (headerLen < 6)
            throw new InvalidDataException("Invalid MIDI header length");
        int format = ReadInt16BE(reader);
        int trackCount = ReadInt16BE(reader);
        int tpb = ReadInt16BE(reader);
        if (headerLen > 6)
            reader.ReadBytes(headerLen - 6);
        var mf = new MidiFile { TicksPerBeat = tpb };
        for (int i = 0; i < trackCount; i++)
        {
            var trackMarker = reader.ReadBytes(4);
            if (trackMarker[0] != 0x4D || trackMarker[1] != 0x54 || trackMarker[2] != 0x72 || trackMarker[3] != 0x6B)
                throw new InvalidDataException("Expected MTrk");
            int trackLen = ReadInt32BE(reader);
            var trackBytes = reader.ReadBytes(trackLen);
            mf.Tracks.Add(ParseTrack(trackBytes, i == 0));
        }
        return mf;
    }

    private static MidiTrackData ParseTrack(byte[] data, bool isMaster)
    {
        var track = new MidiTrackData { IsMaster = isMaster };
        int pos = 0;
        int tick = 0;
        byte runningStatus = 0;
        while (pos < data.Length)
        {
            int delta = ReadVarInt(data, ref pos);
            tick += delta;
            if (pos >= data.Length)
                break;
            byte b = data[pos];
            if ((b & 0x80) != 0)
            {
                runningStatus = b;
                pos++;
            }
            byte status = runningStatus;
            if (status == 0xFF)
            {
                if (pos >= data.Length) break;
                byte metaType = data[pos++];
                int metaLen = ReadVarInt(data, ref pos);
                if (pos + metaLen > data.Length) break;
                var metaData = new byte[metaLen];
                Array.Copy(data, pos, metaData, 0, metaLen);
                pos += metaLen;
                if (metaType == 0x51 && metaLen == 3)
                {
                    int tempo = (metaData[0] << 16) | (metaData[1] << 8) | metaData[2];
                    track.Tempos.Add(new MidiTempoEvent { Tick = tick, Tempo = tempo });
                }
                else if (metaType == 0x58 && metaLen == 4)
                {
                    int num = metaData[0];
                    int den = 1 << metaData[1];
                    track.TimeSigs.Add(new MidiTimeSigEvent { Tick = tick, Numerator = num, Denominator = den });
                }
                else if (metaType == 0x01 || metaType == 0x03)
                {
                    track.TextEvents.Add(new MidiTextEvent { Tick = tick, Data = metaData });
                }
                else if (metaType == 0x2F)
                {
                    break;
                }
            }
            else if (status == 0xF0 || status == 0xF7)
            {
                int sysexLen = ReadVarInt(data, ref pos);
                pos += sysexLen;
            }
            else
            {
                int channel = status & 0x0F;
                int msgType = status & 0xF0;
                switch (msgType)
                {
                    case 0x80:
                    case 0x90:
                    case 0xA0:
                    case 0xB0:
                    case 0xE0:
                        if (pos + 1 < data.Length) pos += 2;
                        break;
                    case 0xC0:
                    case 0xD0:
                        if (pos < data.Length) pos++;
                        break;
                    default:
                        pos++;
                        break;
                }
            }
        }
        return track;
    }

    private static int ReadVarInt(byte[] data, ref int pos)
    {
        int result = 0;
        while (pos < data.Length)
        {
            byte b = data[pos++];
            result = (result << 7) | (b & 0x7F);
            if ((b & 0x80) == 0)
                break;
        }
        return result;
    }

    private static int ReadInt32BE(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(4);
        return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
    }

    private static int ReadInt16BE(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(2);
        return (bytes[0] << 8) | bytes[1];
    }

    public static double Tempo2Bpm(int tempo) => (60.0 * 1_000_000) / tempo;
    public static int Bpm2Tempo(double bpm) => (int)Math.Round(60.0 * 1_000_000 / bpm);
}

internal sealed class MidiWriter
{
    public static byte[] Build(int ticksPerBeat, IReadOnlyList<MidiTrackBytes> tracks)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        WriteInt32BE(writer, 0x4D546864);
        WriteInt32BE(writer, 6);
        WriteInt16BE(writer, 1);
        WriteInt16BE(writer, tracks.Count);
        WriteInt16BE(writer, ticksPerBeat);
        foreach (var track in tracks)
        {
            writer.Write(new byte[] { 0x4D, 0x54, 0x72, 0x6B });
            WriteInt32BE(writer, track.Data.Length);
            writer.Write(track.Data);
        }
        return ms.ToArray();
    }

    private static void WriteInt32BE(BinaryWriter writer, int value)
    {
        writer.Write((byte)(value >> 24));
        writer.Write((byte)(value >> 16));
        writer.Write((byte)(value >> 8));
        writer.Write((byte)value);
    }

    private static void WriteInt16BE(BinaryWriter writer, int value)
    {
        writer.Write((byte)(value >> 8));
        writer.Write((byte)value);
    }
}

internal sealed class MidiTrackBytes
{
    public byte[] Data { get; }

    public MidiTrackBytes(IReadOnlyList<MidiRawEvent> events)
    {
        using var ms = new MemoryStream();
        int prevTick = 0;
        foreach (var ev in events)
        {
            int delta = ev.Tick - prevTick;
            prevTick = ev.Tick;
            WriteVarInt(ms, delta);
            ms.Write(ev.Data, 0, ev.Data.Length);
        }
        WriteEndOfTrack(ms);
        Data = ms.ToArray();
    }

    private static void WriteVarInt(Stream s, int value)
    {
        if (value < 0) value = 0;
        var bytes = new List<byte>();
        bytes.Add((byte)(value & 0x7F));
        value >>= 7;
        while (value > 0)
        {
            bytes.Add((byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }
        bytes.Reverse();
        foreach (var b in bytes)
            s.WriteByte(b);
    }

    private static void WriteEndOfTrack(Stream s)
    {
        WriteVarInt(s, 0);
        s.WriteByte(0xFF);
        s.WriteByte(0x2F);
        s.WriteByte(0x00);
    }

    public static MidiRawEvent MakeTextEvent(int tick, byte[] textData)
    {
        using var ms = new MemoryStream();
        ms.WriteByte(0xFF);
        ms.WriteByte(0x01);
        WriteVarInt(ms, textData.Length);
        ms.Write(textData, 0, textData.Length);
        return new MidiRawEvent(tick, ms.ToArray());
    }

    public static MidiRawEvent MakeTempoEvent(int tick, int tempo)
    {
        return new MidiRawEvent(tick, new byte[]
        {
            0xFF, 0x51, 0x03,
            (byte)(tempo >> 16),
            (byte)(tempo >> 8),
            (byte)(tempo)
        });
    }

    public static MidiRawEvent MakeTimeSigEvent(int tick, int numerator, int denominator)
    {
        int denExp = 0;
        int d = denominator;
        while (d > 1) { d >>= 1; denExp++; }
        return new MidiRawEvent(tick, new byte[]
        {
            0xFF, 0x58, 0x04,
            (byte)numerator,
            (byte)denExp,
            24,
            8
        });
    }

    private static void WriteVarInt(MemoryStream ms, int value)
    {
        if (value < 0) value = 0;
        var bytes = new List<byte>();
        bytes.Add((byte)(value & 0x7F));
        value >>= 7;
        while (value > 0)
        {
            bytes.Add((byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }
        bytes.Reverse();
        foreach (var b in bytes)
            ms.WriteByte(b);
    }
}

internal sealed class MidiRawEvent
{
    public int Tick { get; }
    public byte[] Data { get; }

    public MidiRawEvent(int tick, byte[] data)
    {
        Tick = tick;
        Data = data;
    }
}
