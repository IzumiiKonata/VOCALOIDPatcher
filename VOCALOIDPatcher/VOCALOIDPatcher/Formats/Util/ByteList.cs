using System.Collections.Generic;
using System.Text;
using VOCALOIDPatcher.Formats.Exceptions;

namespace VOCALOIDPatcher.Formats.Util;

public static class ByteList
{
    public static void AddInt(this List<byte> list, int value, bool littleEndian = true)
    {
        byte b0 = (byte)(value & 0xff);
        byte b1 = (byte)((value >> 8) & 0xff);
        byte b2 = (byte)((value >> 16) & 0xff);
        byte b3 = (byte)((value >> 24) & 0xff);
        if (littleEndian)
        {
            list.Add(b0);
            list.Add(b1);
            list.Add(b2);
            list.Add(b3);
        }
        else
        {
            list.Add(b3);
            list.Add(b2);
            list.Add(b1);
            list.Add(b0);
        }
    }

    public static void AddShort(this List<byte> list, short value, bool littleEndian = true)
    {
        byte b0 = (byte)(value & 0xff);
        byte b1 = (byte)((value >> 8) & 0xff);
        if (littleEndian)
        {
            list.Add(b0);
            list.Add(b1);
        }
        else
        {
            list.Add(b1);
            list.Add(b0);
        }
    }

    public static void AddIntVariableLengthBigEndian(this List<byte> list, int value)
    {
        const int maximum = 268435455;
        if (value >= maximum)
            throw new ValueTooLargeException(value.ToString(), maximum.ToString());
        if (value == 0)
        {
            list.Add(0x00);
            return;
        }

        var bytes = new List<byte>();
        int rest = value;
        while (rest > 0)
        {
            bytes.Insert(0, (byte)(rest % 128));
            rest /= 128;
        }

        for (int i = 0; i < bytes.Count - 1; i++)
            bytes[i] = (byte)(bytes[i] + 128);
        list.AddRange(bytes);
    }

    public static void AddBlock(this List<byte> list, IReadOnlyList<byte> block, bool littleEndian = true, bool lengthInVariableLength = false)
    {
        if (lengthInVariableLength)
            list.AddIntVariableLengthBigEndian(block.Count);
        else
            list.AddInt(block.Count, littleEndian);
        list.AddRange(block);
    }

    public static void AddString(this List<byte> list, string value, bool littleEndian = true, bool lengthInVariableLength = false, Encoding? encoding = null)
    {
        var bytes = (encoding ?? Encoding.UTF8).GetBytes(value);
        if (lengthInVariableLength)
            list.AddIntVariableLengthBigEndian(bytes.Length);
        else
            list.AddInt(bytes.Length, littleEndian);
        list.AddRange(bytes);
    }
}
