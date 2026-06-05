using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Tssln;

public enum JuceVarType : byte
{
    Int = 1,
    BoolTrue = 2,
    BoolFalse = 3,
    Double = 4,
    String = 5,
    Int64 = 6,
    Array = 7,
    Binary = 8,
    Undefined = 9,
}

public sealed class JuceVariant
{
    public JuceVarType? Type { get; set; }
    public object? Value { get; set; }

    public static JuceVariant OfInt(int value) => new() { Type = JuceVarType.Int, Value = value };
    public static JuceVariant OfBool(bool value) => new() { Type = value ? JuceVarType.BoolTrue : JuceVarType.BoolFalse, Value = value };
    public static JuceVariant OfDouble(double value) => new() { Type = JuceVarType.Double, Value = value };
    public static JuceVariant OfString(string value) => new() { Type = JuceVarType.String, Value = value };
    public static JuceVariant OfInt64(long value) => new() { Type = JuceVarType.Int64, Value = value };
    public static JuceVariant OfArray(List<JuceVariant> value) => new() { Type = JuceVarType.Array, Value = value };
    public static JuceVariant OfBinary(byte[] value) => new() { Type = JuceVarType.Binary, Value = value };
}

public sealed class JuceNamedVariant
{
    public string Name { get; set; } = "";
    public JuceVariant Data { get; set; } = new();
}

public sealed class JuceNode
{
    public string Name { get; set; } = "";
    public List<JuceNamedVariant> Attrs { get; set; } = new();
    public List<JuceNode> Children { get; set; } = new();
}

public static class JuceBinary
{
    private static readonly Encoding Utf8 = new UTF8Encoding(false);

    public static int ReadCompressedInt(BinaryReader reader)
    {
        int width = reader.ReadByte();
        long result = 0;
        for (int i = 0; i < width; i++)
            result |= (long)reader.ReadByte() << (8 * i);
        return (int)result;
    }

    public static void WriteCompressedInt(BinaryWriter writer, int value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value));
        int width = 1;
        long remaining = (uint)value;
        long probe = remaining >> 8;
        while (probe != 0)
        {
            width++;
            probe >>= 8;
        }
        writer.Write((byte)width);
        for (int i = 0; i < width; i++)
            writer.Write((byte)((value >> (8 * i)) & 0xFF));
    }

    public static string ReadCString(BinaryReader reader)
    {
        var bytes = new List<byte>();
        while (true)
        {
            byte b = reader.ReadByte();
            if (b == 0)
                break;
            bytes.Add(b);
        }
        return Utf8.GetString(bytes.ToArray());
    }

    public static void WriteCString(BinaryWriter writer, string value)
    {
        writer.Write(Utf8.GetBytes(value));
        writer.Write((byte)0);
    }

    public static JuceVariant ReadVariant(BinaryReader reader)
    {
        int length = ReadCompressedInt(reader);
        byte[] payload = reader.ReadBytes(length);
        if (payload.Length == 0)
            return new JuceVariant { Type = null, Value = null };
        using var ms = new MemoryStream(payload);
        using var inner = new BinaryReader(ms);
        var type = (JuceVarType)inner.ReadByte();
        var variant = new JuceVariant { Type = type };
        switch (type)
        {
            case JuceVarType.Int:
                variant.Value = inner.ReadInt32();
                break;
            case JuceVarType.BoolTrue:
                variant.Value = true;
                break;
            case JuceVarType.BoolFalse:
                variant.Value = false;
                break;
            case JuceVarType.Double:
                variant.Value = inner.ReadDouble();
                break;
            case JuceVarType.String:
                variant.Value = ReadCString(inner);
                break;
            case JuceVarType.Int64:
                variant.Value = inner.ReadInt64();
                break;
            case JuceVarType.Array:
                int count = ReadCompressedInt(inner);
                var items = new List<JuceVariant>(count);
                for (int i = 0; i < count; i++)
                    items.Add(ReadVariant(inner));
                variant.Value = items;
                break;
            case JuceVarType.Binary:
                variant.Value = inner.ReadBytes((int)(ms.Length - ms.Position));
                break;
            default:
                variant.Value = inner.ReadBytes((int)(ms.Length - ms.Position));
                break;
        }
        return variant;
    }

    public static void WriteVariant(BinaryWriter writer, JuceVariant variant)
    {
        byte[] payload;
        if (variant.Type == null)
        {
            payload = Array.Empty<byte>();
        }
        else
        {
            using var ms = new MemoryStream();
            using (var inner = new BinaryWriter(ms))
            {
                inner.Write((byte)variant.Type.Value);
                switch (variant.Type.Value)
                {
                    case JuceVarType.Int:
                        inner.Write(Convert.ToInt32(variant.Value));
                        break;
                    case JuceVarType.BoolTrue:
                    case JuceVarType.BoolFalse:
                        break;
                    case JuceVarType.Double:
                        inner.Write(Convert.ToDouble(variant.Value));
                        break;
                    case JuceVarType.String:
                        WriteCString(inner, (string)variant.Value!);
                        break;
                    case JuceVarType.Int64:
                        inner.Write(Convert.ToInt64(variant.Value));
                        break;
                    case JuceVarType.Array:
                        var items = (List<JuceVariant>)variant.Value!;
                        WriteCompressedInt(inner, items.Count);
                        foreach (var item in items)
                            WriteVariant(inner, item);
                        break;
                    case JuceVarType.Binary:
                        inner.Write((byte[])variant.Value!);
                        break;
                    default:
                        inner.Write((byte[])variant.Value!);
                        break;
                }
            }
            payload = ms.ToArray();
        }
        WriteCompressedInt(writer, payload.Length);
        writer.Write(payload);
    }

    public static JuceNode ReadNode(BinaryReader reader)
    {
        var node = new JuceNode { Name = ReadCString(reader) };
        int attrCount = ReadCompressedInt(reader);
        for (int i = 0; i < attrCount; i++)
        {
            var named = new JuceNamedVariant { Name = ReadCString(reader) };
            named.Data = ReadVariant(reader);
            node.Attrs.Add(named);
        }
        int childCount = ReadCompressedInt(reader);
        for (int i = 0; i < childCount; i++)
            node.Children.Add(ReadNode(reader));
        return node;
    }

    public static void WriteNode(BinaryWriter writer, JuceNode node)
    {
        WriteCString(writer, node.Name);
        WriteCompressedInt(writer, node.Attrs.Count);
        foreach (var attr in node.Attrs)
        {
            WriteCString(writer, attr.Name);
            WriteVariant(writer, attr.Data);
        }
        WriteCompressedInt(writer, node.Children.Count);
        foreach (var child in node.Children)
            WriteNode(writer, child);
    }

    public static JuceNode Parse(byte[] content)
    {
        using var ms = new MemoryStream(content);
        using var reader = new BinaryReader(ms);
        return ReadNode(reader);
    }

    public static byte[] Build(JuceNode node)
    {
        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms))
            WriteNode(writer, node);
        return ms.ToArray();
    }
}
