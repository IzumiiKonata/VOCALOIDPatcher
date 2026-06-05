using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Acep;

public static class AcepZstd
{
    public static byte[] Decompress(byte[] compressed)
    {
        using var decompressor = new ZstdSharp.Decompressor();
        return decompressor.Unwrap(compressed).ToArray();
    }

    public static byte[] Compress(byte[] raw)
    {
        using var compressor = new ZstdSharp.Compressor();
        return compressor.Wrap(raw).ToArray();
    }
}

public static class AcepIo
{
    private static readonly byte[] Acep2Magic = { 0x41, 0x43, 0x45, 0x50, 0x32 };
    private const byte Acep2Flag = 0x01;

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static JsonObject Decompress(byte[] content)
    {
        if (content.Length >= 5 && content.Take(5).SequenceEqual(Acep2Magic))
        {
            byte[] compressed = ReadAcep2Container(content);
            byte[] decompressed = AcepZstd.Decompress(compressed);
            return AcepCbor.Decode(decompressed) as JsonObject
                   ?? throw new InvalidDataException("acep CBOR 根节点不是对象");
        }
        else
        {
            var outer = JsonNode.Parse(Encoding.UTF8.GetString(content)) as JsonObject
                        ?? throw new InvalidDataException("acep 外层 JSON 解析失败");
            string base64 = outer["content"]?.GetValue<string>() ?? "";
            int version = outer["version"]?.GetValue<int>() ?? 1000;
            byte[] inner = Convert.FromBase64String(base64);
            if (version == 1 || version == 2)
                throw new NotSupportedException("acep 旧版本 (加密) 项目暂不支持");
            byte[] decompressed = AcepZstd.Decompress(inner);
            return JsonNode.Parse(Encoding.UTF8.GetString(decompressed)) as JsonObject
                   ?? throw new InvalidDataException("acep 内容 JSON 解析失败");
        }
    }

    private static byte[] ReadAcep2Container(byte[] content)
    {
        var span = new ReadOnlySpan<byte>(content);
        ulong contentOffset = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(8, 8));
        ulong compressedSize = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(16, 8));
        var result = new byte[compressedSize];
        Array.Copy(content, (int)contentOffset, result, 0, (int)compressedSize);
        return result;
    }

    public static void Compress(JsonObject project, Stream target, AcepSerialization serialization)
    {
        if (serialization == AcepSerialization.Json)
        {
            byte[] raw = Encoding.UTF8.GetBytes(project.ToJsonString(WriteOptions));
            byte[] compressed = AcepZstd.Compress(raw);
            string base64 = Convert.ToBase64String(compressed);
            var acepFile = new JsonObject
            {
                ["compressMethod"] = "zstd",
                ["debugInfo"] = new JsonObject
                {
                    ["os"] = "windows",
                    ["platform"] = "pc",
                    ["version"] = "10",
                },
                ["salt"] = "",
                ["version"] = 1000,
                ["content"] = base64,
            };
            byte[] outer = Encoding.UTF8.GetBytes(acepFile.ToJsonString(WriteOptions));
            target.Write(outer, 0, outer.Length);
        }
        else
        {
            byte[] raw = AcepCbor.Encode(project);
            int contentSize = raw.Length;
            byte[] compressed = AcepZstd.Compress(raw);
            WriteAcep2Container(target, compressed, contentSize);
        }
    }

    private static void WriteAcep2Container(Stream target, byte[] compressed, int contentSize)
    {
        const int contentOffset = 192;
        var header = new byte[contentOffset];
        Array.Copy(Acep2Magic, 0, header, 0, 5);
        header[5] = Acep2Flag;
        BinaryPrimitives.WriteUInt16LittleEndian(new Span<byte>(header, 6, 2), (ushort)0);
        BinaryPrimitives.WriteUInt64LittleEndian(new Span<byte>(header, 8, 8), (ulong)contentOffset);
        BinaryPrimitives.WriteUInt64LittleEndian(new Span<byte>(header, 16, 8), (ulong)compressed.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(new Span<byte>(header, 24, 8), (ulong)contentSize);
        BinaryPrimitives.WriteUInt16LittleEndian(new Span<byte>(header, 32, 2), (ushort)138);
        header[34 + 138] = Acep2Flag;
        target.Write(header, 0, header.Length);
        target.Write(compressed, 0, compressed.Length);
    }
}
