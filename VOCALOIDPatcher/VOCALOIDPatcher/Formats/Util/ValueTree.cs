using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VOCALOIDPatcher.Formats.Util;

public sealed class Variant
{
    public Variant(byte[] payload) => Payload = payload;

    public byte[] Payload { get; }

    public byte Marker => Payload[0];

    public int AsInt() => Payload[1] | (Payload[2] << 8) | (Payload[3] << 16) | (Payload[4] << 24);

    public long AsInt64()
    {
        long value = 0;
        for (int i = 0; i < 8; i++)
            value |= (long)Payload[1 + i] << (8 * i);
        return value;
    }

    public double AsDouble() => BitConverter.ToDouble(EnsureLittleEndian(Payload, 1, 8), 0);

    public bool AsBool() => Marker == 2;

    public string AsString()
    {
        int length = Payload.Length - 1;
        if (length > 0 && Payload[^1] == 0)
            length--;
        return Encoding.UTF8.GetString(Payload, 1, length);
    }

    public byte[] AsBytes() => Payload.Skip(1).ToArray();

    public static Variant FromInt(int value) =>
        new(new byte[] { 1, (byte)(value & 0xff), (byte)((value >> 8) & 0xff), (byte)((value >> 16) & 0xff), (byte)((value >> 24) & 0xff) });

    public static Variant FromDouble(double value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (!BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        var payload = new byte[9];
        payload[0] = 4;
        Array.Copy(bytes, 0, payload, 1, 8);
        return new Variant(payload);
    }

    public static Variant FromString(string value)
    {
        var utf8 = Encoding.UTF8.GetBytes(value);
        var payload = new byte[utf8.Length + 2];
        payload[0] = 5;
        Array.Copy(utf8, 0, payload, 1, utf8.Length);
        payload[^1] = 0;
        return new Variant(payload);
    }

    public static Variant FromBool(bool value) => new(new byte[] { (byte)(value ? 2 : 3) });

    public static Variant FromBytes(byte[] value)
    {
        var payload = new byte[value.Length + 1];
        payload[0] = 8;
        Array.Copy(value, 0, payload, 1, value.Length);
        return new Variant(payload);
    }

    public Variant Clone() => new((byte[])Payload.Clone());

    private static byte[] EnsureLittleEndian(byte[] source, int offset, int count)
    {
        var result = new byte[count];
        Array.Copy(source, offset, result, 0, count);
        if (!BitConverter.IsLittleEndian)
            Array.Reverse(result);
        return result;
    }
}

public sealed class ValueTree
{
    public string Type { get; set; } = "";
    public List<(string Name, Variant Value)> Attributes { get; set; } = new();
    public List<ValueTree> Children { get; set; } = new();

    public Variant? Get(string name)
    {
        foreach (var (attrName, value) in Attributes)
            if (attrName == name)
                return value;
        return null;
    }

    public void Set(string name, Variant value)
    {
        for (int i = 0; i < Attributes.Count; i++)
            if (Attributes[i].Name == name)
            {
                Attributes[i] = (name, value);
                return;
            }

        Attributes.Add((name, value));
    }

    public ValueTree Clone() => new()
    {
        Type = Type,
        Attributes = Attributes.Select(a => (a.Name, a.Value.Clone())).ToList(),
        Children = Children.Select(c => c.Clone()).ToList(),
    };

    public static ValueTree Parse(byte[] bytes)
    {
        var reader = new Reader(bytes);
        return reader.ReadTree();
    }

    public byte[] Dump()
    {
        var output = new List<byte>();
        WriteTree(output, this);
        return output.ToArray();
    }

    private static void WriteTree(List<byte> output, ValueTree tree)
    {
        WriteString(output, tree.Type);
        WriteCompressedInt(output, tree.Attributes.Count);
        foreach (var (name, value) in tree.Attributes)
        {
            WriteString(output, name);
            WriteCompressedInt(output, value.Payload.Length);
            output.AddRange(value.Payload);
        }

        WriteCompressedInt(output, tree.Children.Count);
        foreach (var child in tree.Children)
            WriteTree(output, child);
    }

    private static void WriteString(List<byte> output, string value)
    {
        output.AddRange(Encoding.UTF8.GetBytes(value));
        output.Add(0);
    }

    private static void WriteCompressedInt(List<byte> output, int value)
    {
        uint un = (uint)(value < 0 ? -value : value);
        var bytes = new List<byte>();
        while (un > 0)
        {
            bytes.Add((byte)(un & 0xff));
            un >>= 8;
        }

        byte head = (byte)bytes.Count;
        if (value < 0)
            head |= 0x80;
        output.Add(head);
        output.AddRange(bytes);
    }

    private sealed class Reader
    {
        private readonly byte[] _bytes;
        private int _position;

        public Reader(byte[] bytes) => _bytes = bytes;

        public ValueTree ReadTree()
        {
            var tree = new ValueTree { Type = ReadString() };
            int numProps = ReadCompressedInt();
            for (int i = 0; i < numProps; i++)
            {
                string name = ReadString();
                var value = ReadVariant();
                tree.Attributes.Add((name, value));
            }

            int numChildren = ReadCompressedInt();
            for (int i = 0; i < numChildren; i++)
                tree.Children.Add(ReadTree());
            return tree;
        }

        private string ReadString()
        {
            int start = _position;
            while (_bytes[_position] != 0)
                _position++;
            string result = Encoding.UTF8.GetString(_bytes, start, _position - start);
            _position++;
            return result;
        }

        private Variant ReadVariant()
        {
            int length = ReadCompressedInt();
            var payload = new byte[length];
            Array.Copy(_bytes, _position, payload, 0, length);
            _position += length;
            return new Variant(payload);
        }

        private int ReadCompressedInt()
        {
            byte head = _bytes[_position++];
            int numBytes = head & 0x7f;
            int value = 0;
            for (int i = 0; i < numBytes; i++)
                value |= _bytes[_position++] << (8 * i);
            if ((head & 0x80) != 0)
                value = -value;
            return value;
        }
    }
}
