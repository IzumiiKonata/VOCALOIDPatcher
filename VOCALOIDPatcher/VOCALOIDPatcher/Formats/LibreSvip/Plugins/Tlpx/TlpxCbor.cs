using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Tlpx;

internal static class TlpxCbor
{
    public static object? Decode(byte[] data)
    {
        int pos = 0;
        return ReadValue(data, ref pos);
    }

    public static byte[] Encode(object? value)
    {
        using var ms = new MemoryStream();
        WriteValue(ms, value);
        return ms.ToArray();
    }

    private static object? ReadValue(byte[] data, ref int pos)
    {
        byte initial = data[pos++];
        int major = initial >> 5;
        int info = initial & 0x1F;

        if (major == 7)
            return ReadSimpleOrFloat(data, ref pos, info);

        ulong additional = ReadAdditionalUInt(data, ref pos, info);

        switch (major)
        {
            case 0:
                return (long)additional;
            case 1:
                return (long)(~additional);
            case 2:
            {
                var bytes = new byte[(int)additional];
                Array.Copy(data, pos, bytes, 0, (int)additional);
                pos += (int)additional;
                return bytes;
            }
            case 3:
            {
                var s = Encoding.UTF8.GetString(data, pos, (int)additional);
                pos += (int)additional;
                return s;
            }
            case 4:
            {
                var list = new List<object?>();
                for (ulong i = 0; i < additional; i++)
                    list.Add(ReadValue(data, ref pos));
                return list;
            }
            case 5:
            {
                var dict = new Dictionary<string, object?>();
                for (ulong i = 0; i < additional; i++)
                {
                    var key = ReadValue(data, ref pos);
                    var val = ReadValue(data, ref pos);
                    dict[key?.ToString() ?? ""] = val;
                }
                return dict;
            }
            case 6:
                return ReadValue(data, ref pos);
            default:
                return null;
        }
    }

    private static object? ReadSimpleOrFloat(byte[] data, ref int pos, int info)
    {
        switch (info)
        {
            case 20:
                return false;
            case 21:
                return true;
            case 22:
                return null;
            case 23:
                return null;
            case 24:
                pos++;
                return null;
            case 25:
            {
                ushort raw = (ushort)((data[pos] << 8) | data[pos + 1]);
                pos += 2;
                return DecodeHalfFloat(raw);
            }
            case 26:
            {
                uint raw = ((uint)data[pos] << 24) | ((uint)data[pos + 1] << 16) |
                           ((uint)data[pos + 2] << 8) | data[pos + 3];
                pos += 4;
                return (double)BitConverter.Int32BitsToSingle((int)raw);
            }
            case 27:
            {
                ulong raw = 0;
                for (int i = 0; i < 8; i++)
                    raw = (raw << 8) | data[pos + i];
                pos += 8;
                return BitConverter.Int64BitsToDouble((long)raw);
            }
            default:
                return null;
        }
    }

    private static ulong ReadAdditionalUInt(byte[] data, ref int pos, int info)
    {
        if (info < 24)
            return (ulong)info;
        if (info == 24)
            return data[pos++];
        if (info == 25)
        {
            ulong v = ((ulong)data[pos] << 8) | data[pos + 1];
            pos += 2;
            return v;
        }
        if (info == 26)
        {
            ulong v = ((ulong)data[pos] << 24) | ((ulong)data[pos + 1] << 16) |
                      ((ulong)data[pos + 2] << 8) | data[pos + 3];
            pos += 4;
            return v;
        }
        if (info == 27)
        {
            ulong v = 0;
            for (int i = 0; i < 8; i++)
                v = (v << 8) | data[pos + i];
            pos += 8;
            return v;
        }
        return 0;
    }

    private static double DecodeHalfFloat(ushort raw)
    {
        int exp = (raw >> 10) & 0x1F;
        int mant = raw & 0x3FF;
        double val;
        if (exp == 0)
            val = mant * Math.Pow(2, -24);
        else if (exp == 31)
            val = mant == 0 ? double.PositiveInfinity : double.NaN;
        else
            val = (mant + 1024) / 1024.0 * Math.Pow(2, exp - 15);
        return (raw & 0x8000) != 0 ? -val : val;
    }

    private static void WriteValue(Stream s, object? value)
    {
        switch (value)
        {
            case null:
                s.WriteByte(0xF6);
                break;
            case bool b:
                s.WriteByte(b ? (byte)0xF5 : (byte)0xF4);
                break;
            case long l:
                WriteInt(s, l);
                break;
            case int i:
                WriteInt(s, i);
                break;
            case double d:
                WriteDouble(s, d);
                break;
            case float f:
                WriteDouble(s, f);
                break;
            case string str:
                WriteString(s, str);
                break;
            case byte[] bytes:
                WriteHead(s, 2, (ulong)bytes.Length);
                s.Write(bytes, 0, bytes.Length);
                break;
            case List<object?> list:
                WriteHead(s, 4, (ulong)list.Count);
                foreach (var item in list)
                    WriteValue(s, item);
                break;
            case Dictionary<string, object?> dict:
                WriteHead(s, 5, (ulong)dict.Count);
                foreach (var kv in dict)
                {
                    WriteString(s, kv.Key);
                    WriteValue(s, kv.Value);
                }
                break;
            default:
                s.WriteByte(0xF6);
                break;
        }
    }

    private static void WriteInt(Stream s, long v)
    {
        if (v >= 0)
            WriteHead(s, 0, (ulong)v);
        else
            WriteHead(s, 1, (ulong)(~v));
    }

    private static void WriteDouble(Stream s, double d)
    {
        s.WriteByte(0xFB);
        long bits = BitConverter.DoubleToInt64Bits(d);
        for (int i = 7; i >= 0; i--)
            s.WriteByte((byte)((bits >> (i * 8)) & 0xFF));
    }

    private static void WriteString(Stream s, string str)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(str);
        WriteHead(s, 3, (ulong)bytes.Length);
        s.Write(bytes, 0, bytes.Length);
    }

    private static void WriteHead(Stream s, int major, ulong value)
    {
        int prefix = major << 5;
        if (value < 24)
            s.WriteByte((byte)(prefix | (int)value));
        else if (value <= 0xFF)
        {
            s.WriteByte((byte)(prefix | 24));
            s.WriteByte((byte)value);
        }
        else if (value <= 0xFFFF)
        {
            s.WriteByte((byte)(prefix | 25));
            s.WriteByte((byte)(value >> 8));
            s.WriteByte((byte)(value & 0xFF));
        }
        else if (value <= 0xFFFFFFFF)
        {
            s.WriteByte((byte)(prefix | 26));
            s.WriteByte((byte)(value >> 24));
            s.WriteByte((byte)(value >> 16 & 0xFF));
            s.WriteByte((byte)(value >> 8 & 0xFF));
            s.WriteByte((byte)(value & 0xFF));
        }
        else
        {
            s.WriteByte((byte)(prefix | 27));
            for (int i = 7; i >= 0; i--)
                s.WriteByte((byte)((value >> (i * 8)) & 0xFF));
        }
    }
}
