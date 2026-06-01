using System;
using System.Text;

namespace VOCALOIDPatcher.Formats.Util;

public sealed class ArrayBufferReader
{
    private readonly byte[] _buffer;

    public ArrayBufferReader(byte[] buffer) => _buffer = buffer;

    public int Index { get; private set; }

    public void Skip(int length) => Index += length;

    public int ReadInt()
    {
        int value = _buffer[Index] | (_buffer[Index + 1] << 8) | (_buffer[Index + 2] << 16) | (_buffer[Index + 3] << 24);
        Index += 4;
        return value;
    }

    public byte[] ReadBytes()
    {
        int length = ReadInt();
        var result = new byte[length];
        Array.Copy(_buffer, Index, result, 0, length);
        Index += length;
        return result;
    }

    public string ReadString() => Encoding.UTF8.GetString(ReadBytes());
}
