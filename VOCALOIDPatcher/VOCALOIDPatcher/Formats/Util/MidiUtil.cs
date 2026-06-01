using System;
using System.Collections.Generic;

namespace VOCALOIDPatcher.Formats.Util;

public static class MidiUtil
{
    private const int StandardTimeDivision = 480;

    public static int ConvertInputTimeToStandardTime(int inputTime, int timeDivision) =>
        (int)((long)inputTime * StandardTimeDivision / timeDivision);

    public enum EventType : byte
    {
        NoteOff = 0x08,
        NoteOn = 0x09,
    }

    public static byte GetStatusByte(this EventType type, int channel) =>
        (byte)(((byte)type << 4) | channel);

    public enum MetaType : byte
    {
        Text = 0x01,
        TrackName = 0x03,
        Lyric = 0x05,
        Tempo = 0x51,
        TimeSignature = 0x58,
        EndOfTrack = 0x2f,
    }

    public static List<byte> EventHeaderBytes(this MetaType type) => new() { 0xff, (byte)type };

    public static double ConvertMidiTempoToBpm(int midiTempo) =>
        (int)(1000.0 * 1000 * 60 / midiTempo * 100) / 100.0;

    public static int ConvertBpmToMidiTempo(double bpm) => (int)(1000.0 * 1000 * 60 / bpm);

    public static List<byte> GenerateMidiTimeSignatureBytes(int numerator, int denominator) => new()
    {
        (byte)numerator,
        (byte)(int)Math.Log2(denominator),
        0x18,
        0x08,
    };
}
