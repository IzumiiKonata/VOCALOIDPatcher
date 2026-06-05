using System;
using System.Text.Json.Nodes;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Acep;

public static class AcepCbor
{
    public static JsonNode Decode(byte[] data) =>
        throw new NotSupportedException(
            "ACE Studio 2.0.7+ (.acep CBOR) 项目使用 CBOR 容器, 当前未引用 CBOR 实现 (例如 System.Formats.Cbor). " +
            "请添加 System.Formats.Cbor 依赖后在此处接入 CBOR 解码 (JSON 序列化方式可正常工作).");

    public static byte[] Encode(JsonNode node) =>
        throw new NotSupportedException(
            "ACE Studio 2.0.7+ (.acep CBOR) 项目使用 CBOR 容器, 当前未引用 CBOR 实现 (例如 System.Formats.Cbor). " +
            "请添加 System.Formats.Cbor 依赖后在此处接入 CBOR 编码 (JSON 序列化方式可正常工作).");
}
