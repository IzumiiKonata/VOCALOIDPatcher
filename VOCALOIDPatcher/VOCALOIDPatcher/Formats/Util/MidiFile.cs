using System.Collections.Generic;
using VOCALOIDPatcher.Formats.Exceptions;

namespace VOCALOIDPatcher.Formats.Util;

public sealed class MidiEvent
{
    public int DeltaTime { get; set; }
    public string Type { get; set; } = "unknown";
    public byte[]? TextBytes { get; set; }
    public int Channel { get; set; }
    public int NoteNumber { get; set; }
    public int Velocity { get; set; }
    public int MicrosecondsPerBeat { get; set; }
    public int Numerator { get; set; }
    public int Denominator { get; set; }
}

public sealed class MidiData
{
    public int TicksPerBeat { get; set; }
    public List<List<MidiEvent>> Tracks { get; set; } = new();
}

public static class MidiFile
{
    public static MidiData Parse(byte[] bytes)
    {
        var reader = new Reader(bytes);
        if (reader.ReadString(4) != "MThd")
            throw new IllegalFileException.IllegalMidiFile();
        int headerLength = reader.ReadInt32();
        reader.ReadInt16();
        int trackCount = reader.ReadInt16();
        int division = reader.ReadInt16();
        for (int i = 6; i < headerLength; i++)
            reader.ReadByte();

        var data = new MidiData { TicksPerBeat = division & 0x7fff };
        for (int t = 0; t < trackCount && reader.HasMore; t++)
        {
            if (reader.ReadString(4) != "MTrk")
                break;
            int length = reader.ReadInt32();
            int end = reader.Position + length;
            data.Tracks.Add(ParseTrack(reader, end));
        }

        return data;
    }

    private static List<MidiEvent> ParseTrack(Reader reader, int end)
    {
        var events = new List<MidiEvent>();
        byte runningStatus = 0;
        while (reader.Position < end)
        {
            int delta = reader.ReadVariableLength();
            byte status = reader.PeekByte();
            if (status < 0x80)
                status = runningStatus;
            else
                reader.ReadByte();

            var ev = new MidiEvent { DeltaTime = delta };
            if (status == 0xff)
            {
                byte metaType = reader.ReadByte();
                int len = reader.ReadVariableLength();
                var payload = reader.ReadBytes(len);
                switch (metaType)
                {
                    case 0x01:
                        ev.Type = "text";
                        ev.TextBytes = payload;
                        break;
                    case 0x03:
                        ev.Type = "trackName";
                        ev.TextBytes = payload;
                        break;
                    case 0x05:
                        ev.Type = "lyrics";
                        ev.TextBytes = payload;
                        break;
                    case 0x51:
                        ev.Type = "setTempo";
                        ev.MicrosecondsPerBeat = (payload.Length >= 3) ? (payload[0] << 16) | (payload[1] << 8) | payload[2] : 0;
                        break;
                    case 0x58:
                        ev.Type = "timeSignature";
                        ev.Numerator = payload.Length >= 1 ? payload[0] : 4;
                        ev.Denominator = payload.Length >= 2 ? 1 << payload[1] : 4;
                        break;
                    case 0x2f:
                        ev.Type = "endOfTrack";
                        break;
                    default:
                        ev.Type = "unknown";
                        break;
                }
            }
            else if (status == 0xf0 || status == 0xf7)
            {
                int len = reader.ReadVariableLength();
                reader.ReadBytes(len);
                ev.Type = "unknown";
            }
            else
            {
                runningStatus = status;
                int high = status >> 4;
                int channel = status & 0x0f;
                ev.Channel = channel;
                switch (high)
                {
                    case 0x8:
                        ev.Type = "noteOff";
                        ev.NoteNumber = reader.ReadByte();
                        ev.Velocity = reader.ReadByte();
                        break;
                    case 0x9:
                        ev.Type = "noteOn";
                        ev.NoteNumber = reader.ReadByte();
                        ev.Velocity = reader.ReadByte();
                        break;
                    case 0xa:
                    case 0xb:
                    case 0xe:
                        ev.Type = "unknown";
                        reader.ReadByte();
                        reader.ReadByte();
                        break;
                    case 0xc:
                    case 0xd:
                        ev.Type = "unknown";
                        reader.ReadByte();
                        break;
                    default:
                        ev.Type = "unknown";
                        break;
                }
            }

            events.Add(ev);
        }

        return events;
    }

    private sealed class Reader
    {
        private readonly byte[] _bytes;

        public Reader(byte[] bytes) => _bytes = bytes;

        public int Position { get; private set; }

        public bool HasMore => Position < _bytes.Length;

        public byte ReadByte() => _bytes[Position++];

        public byte PeekByte() => _bytes[Position];

        public byte[] ReadBytes(int count)
        {
            var result = new byte[count];
            for (int i = 0; i < count; i++)
                result[i] = _bytes[Position++];
            return result;
        }

        public string ReadString(int count)
        {
            var chars = new char[count];
            for (int i = 0; i < count; i++)
                chars[i] = (char)_bytes[Position++];
            return new string(chars);
        }

        public int ReadInt32() => (_bytes[Position++] << 24) | (_bytes[Position++] << 16) | (_bytes[Position++] << 8) | _bytes[Position++];

        public int ReadInt16() => (_bytes[Position++] << 8) | _bytes[Position++];

        public int ReadVariableLength()
        {
            int result = 0;
            while (true)
            {
                byte b = _bytes[Position++];
                result = (result << 7) | (b & 0x7f);
                if ((b & 0x80) == 0)
                    break;
            }

            return result;
        }
    }
}
