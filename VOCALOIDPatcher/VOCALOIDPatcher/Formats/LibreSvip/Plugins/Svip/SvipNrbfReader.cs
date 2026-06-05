using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Svip;

internal sealed class SvipNrbfReader
{
    private readonly BinaryReader _br;
    private readonly Dictionary<int, NrbfRecord> _classes = new();
    private readonly Dictionary<int, NrbfRecord> _objects = new();
    private readonly Dictionary<int, string> _libraries = new();
    private readonly Dictionary<int, NrbfReference> _references = new();

    public string Magic = "";
    public string Version = "";
    public int RootId;
    public NrbfRecord? RootRecord;

    public SvipNrbfReader(byte[] content)
    {
        _br = new BinaryReader(new MemoryStream(content), Encoding.UTF8);
    }

    public static string ReadPascalString(BinaryReader br)
    {
        int length = br.ReadByte();
        byte[] bytes = br.ReadBytes(length);
        return Encoding.UTF8.GetString(bytes);
    }

    private static string ReadLengthPrefixedString(BinaryReader br)
    {
        int length = 0;
        int shift = 0;
        for (int i = 0; i < 5; i++)
        {
            byte b = br.ReadByte();
            length += (b & 0x7F) << shift;
            shift += 7;
            if ((b & 0x80) == 0)
                break;
        }
        byte[] bytes = br.ReadBytes(length);
        return Encoding.UTF8.GetString(bytes);
    }

    public void Read()
    {
        Magic = ReadPascalString(_br);
        Version = ReadPascalString(_br);

        var records = new List<NrbfRecord>();
        while (true)
        {
            NrbfRecord record = ReadRecord();
            records.Add(record);
            if (record.RecordType == NrbfRecordType.MessageEnd)
                break;
            if (_br.BaseStream.Position >= _br.BaseStream.Length)
                break;
        }

        ResolveReferences();

        foreach (var record in records)
        {
            if (record.RecordType == NrbfRecordType.SerializedStreamHeader)
            {
                RootId = record.RootId;
            }
            else if (IsClassRecord(record.RecordType) && record.ClassInfo != null && record.ClassInfo.ObjectId == RootId)
            {
                RootRecord = record;
                break;
            }
        }
    }

    private static bool IsClassRecord(NrbfRecordType type) =>
        type == NrbfRecordType.ClassWithId
        || type == NrbfRecordType.SystemClassWithMembers
        || type == NrbfRecordType.ClassWithMembers
        || type == NrbfRecordType.SystemClassWithMembersAndTypes
        || type == NrbfRecordType.ClassWithMembersAndTypes;

    private void ResolveReferences()
    {
        foreach (var pair in _references)
        {
            if (pair.Value.RealObj == null)
            {
                NrbfRecord? target = null;
                if (_classes.TryGetValue(pair.Value.IdRef, out var cls))
                    target = cls;
                else if (_objects.TryGetValue(pair.Value.IdRef, out var obj))
                    target = obj;
                pair.Value.RealObj = target;
            }
        }
    }

    private NrbfRecord ReadRecord()
    {
        var type = (NrbfRecordType)_br.ReadByte();
        var record = new NrbfRecord { RecordType = type };
        switch (type)
        {
            case NrbfRecordType.SerializedStreamHeader:
                record.RootId = _br.ReadInt32();
                _br.ReadInt32();
                _br.ReadInt32();
                _br.ReadInt32();
                break;
            case NrbfRecordType.BinaryLibrary:
                int libraryId = _br.ReadInt32();
                record.ObjectId = libraryId;
                record.LibraryName = ReadLengthPrefixedString(_br);
                _libraries[libraryId] = record.LibraryName;
                break;
            case NrbfRecordType.ClassWithMembers:
                record.ClassInfo = ReadClassInfo();
                record.LibraryId = _br.ReadInt32();
                ReadMemberValuesUntyped(record);
                _classes[record.ClassInfo.ObjectId] = record;
                break;
            case NrbfRecordType.SystemClassWithMembers:
                record.ClassInfo = ReadClassInfo();
                ReadMemberValuesUntyped(record);
                _classes[record.ClassInfo.ObjectId] = record;
                break;
            case NrbfRecordType.SystemClassWithMembersAndTypes:
                record.ClassInfo = ReadClassInfo();
                record.MemberTypeInfo = ReadMemberTypeInfo(record.ClassInfo.MemberNames.Count);
                ReadMemberValuesTyped(record);
                _classes[record.ClassInfo.ObjectId] = record;
                break;
            case NrbfRecordType.ClassWithMembersAndTypes:
                record.ClassInfo = ReadClassInfo();
                record.MemberTypeInfo = ReadMemberTypeInfo(record.ClassInfo.MemberNames.Count);
                record.LibraryId = _br.ReadInt32();
                ReadMemberValuesTyped(record);
                _classes[record.ClassInfo.ObjectId] = record;
                break;
            case NrbfRecordType.ClassWithId:
                record.ObjectId = _br.ReadInt32();
                int metadataId = _br.ReadInt32();
                NrbfRecord meta = _classes[metadataId];
                record.ClassInfo = meta.ClassInfo;
                record.MemberTypeInfo = meta.MemberTypeInfo;
                if (record.MemberTypeInfo != null)
                    ReadMemberValuesTyped(record);
                else
                    ReadMemberValuesUntyped(record);
                _objects[record.ObjectId] = record;
                break;
            case NrbfRecordType.BinaryObjectString:
                record.ObjectId = _br.ReadInt32();
                record.StringValue = ReadLengthPrefixedString(_br);
                _objects[record.ObjectId] = record;
                break;
            case NrbfRecordType.BinaryArray:
                ReadBinaryArray(record);
                _objects[record.ObjectId] = record;
                break;
            case NrbfRecordType.ArraySinglePrimitive:
                record.ObjectId = _br.ReadInt32();
                record.ArrayLength = _br.ReadInt32();
                record.ArrayPrimitiveType = (NrbfPrimitiveType)_br.ReadByte();
                for (int i = 0; i < record.ArrayLength; i++)
                    record.MemberValues.Add(ReadPrimitive(record.ArrayPrimitiveType));
                _objects[record.ObjectId] = record;
                break;
            case NrbfRecordType.ArraySingleObject:
            case NrbfRecordType.ArraySingleString:
                record.ObjectId = _br.ReadInt32();
                record.ArrayLength = _br.ReadInt32();
                for (int i = 0; i < record.ArrayLength; i++)
                    record.MemberValues.Add(ReadRecord());
                _objects[record.ObjectId] = record;
                break;
            case NrbfRecordType.MemberPrimitiveTyped:
                var primType = (NrbfPrimitiveType)_br.ReadByte();
                record.MemberValues.Add(ReadPrimitive(primType));
                break;
            case NrbfRecordType.MemberReference:
                record.IdRef = _br.ReadInt32();
                if (!_references.TryGetValue(record.IdRef, out var refObj))
                {
                    refObj = new NrbfReference { IdRef = record.IdRef };
                    _references[record.IdRef] = refObj;
                }
                break;
            case NrbfRecordType.ObjectNullMultiple256:
                record.NullCount = _br.ReadByte();
                break;
            case NrbfRecordType.ObjectNullMultiple:
                record.NullCount = _br.ReadInt32();
                break;
            case NrbfRecordType.ObjectNull:
            case NrbfRecordType.MessageEnd:
                break;
            default:
                throw new InvalidDataException($"Unsupported NRBF record type {type}");
        }
        return record;
    }

    private NrbfClassInfo ReadClassInfo()
    {
        var info = new NrbfClassInfo
        {
            ObjectId = _br.ReadInt32(),
            Name = ReadLengthPrefixedString(_br),
        };
        int memberCount = _br.ReadInt32();
        for (int i = 0; i < memberCount; i++)
            info.MemberNames.Add(ReadLengthPrefixedString(_br));
        return info;
    }

    private NrbfMemberTypeInfo ReadMemberTypeInfo(int count)
    {
        var info = new NrbfMemberTypeInfo();
        for (int i = 0; i < count; i++)
            info.BinaryTypes.Add((NrbfBinaryType)_br.ReadByte());
        for (int i = 0; i < count; i++)
            info.AdditionalInfos.Add(ReadBinaryTypeAdditional(info.BinaryTypes[i]));
        return info;
    }

    private object? ReadBinaryTypeAdditional(NrbfBinaryType binaryType)
    {
        switch (binaryType)
        {
            case NrbfBinaryType.Primitive:
            case NrbfBinaryType.PrimitiveArray:
                return (NrbfPrimitiveType)_br.ReadByte();
            case NrbfBinaryType.SystemClass:
                return ReadLengthPrefixedString(_br);
            case NrbfBinaryType.Class:
                return new NrbfClassTypeInfo
                {
                    TypeName = ReadLengthPrefixedString(_br),
                    LibraryId = _br.ReadInt32(),
                };
            default:
                return null;
        }
    }

    private void ReadMemberValuesUntyped(NrbfRecord record)
    {
        int count = record.ClassInfo!.MemberNames.Count;
        for (int i = 0; i < count; i++)
            record.MemberValues.Add(ReadRecord());
    }

    private void ReadMemberValuesTyped(NrbfRecord record)
    {
        var typeInfo = record.MemberTypeInfo!;
        int count = record.ClassInfo!.MemberNames.Count;
        for (int i = 0; i < count; i++)
        {
            if (typeInfo.BinaryTypes[i] == NrbfBinaryType.Primitive)
            {
                var primType = (NrbfPrimitiveType)typeInfo.AdditionalInfos[i]!;
                record.MemberValues.Add(ReadPrimitive(primType));
            }
            else
            {
                record.MemberValues.Add(ReadRecord());
            }
        }
    }

    private void ReadBinaryArray(NrbfRecord record)
    {
        record.ObjectId = _br.ReadInt32();
        record.ArrayType = (NrbfBinaryArrayType)_br.ReadByte();
        record.Rank = _br.ReadInt32();
        for (int i = 0; i < record.Rank; i++)
            record.Lengths.Add(_br.ReadInt32());
        if (record.ArrayType == NrbfBinaryArrayType.SingleOffset
            || record.ArrayType == NrbfBinaryArrayType.JaggedOffset
            || record.ArrayType == NrbfBinaryArrayType.RectangularOffset)
        {
            for (int i = 0; i < record.Rank; i++)
                _br.ReadInt32();
        }
        record.ArrayBinaryType = (NrbfBinaryType)_br.ReadByte();
        record.ArrayInfo = record.ArrayBinaryType == NrbfBinaryType.Class
            ? new NrbfClassTypeInfo
            {
                TypeName = ReadLengthPrefixedString(_br),
                LibraryId = _br.ReadInt32(),
            }
            : null;
        if (record.ArrayBinaryType == NrbfBinaryType.PrimitiveArray
            || record.ArrayBinaryType == NrbfBinaryType.Primitive)
            record.ArrayPrimitiveType = (NrbfPrimitiveType)_br.ReadByte();
        else if (record.ArrayBinaryType == NrbfBinaryType.SystemClass)
            ReadLengthPrefixedString(_br);

        if (record.ArrayType != NrbfBinaryArrayType.Rectangular
            && record.ArrayType != NrbfBinaryArrayType.RectangularOffset
            && record.Rank == 1
            && record.Lengths[0] > 0)
        {
            int total = record.Lengths[0];
            int consumed = 0;
            while (consumed < total)
            {
                NrbfRecord element;
                if (record.ArrayBinaryType == NrbfBinaryType.Primitive)
                {
                    element = new NrbfRecord { RecordType = NrbfRecordType.MemberPrimitiveTyped };
                    element.MemberValues.Add(ReadPrimitive(record.ArrayPrimitiveType));
                    consumed += 1;
                }
                else
                {
                    element = ReadRecord();
                    if (element.RecordType == NrbfRecordType.ObjectNullMultiple
                        || element.RecordType == NrbfRecordType.ObjectNullMultiple256)
                        consumed += element.NullCount;
                    else
                        consumed += 1;
                }
                record.MemberValues.Add(element);
            }
        }
    }

    private object? ReadPrimitive(NrbfPrimitiveType type)
    {
        switch (type)
        {
            case NrbfPrimitiveType.Boolean:
                return _br.ReadByte() != 0;
            case NrbfPrimitiveType.Byte:
                return _br.ReadByte();
            case NrbfPrimitiveType.SByte:
                return _br.ReadSByte();
            case NrbfPrimitiveType.Int16:
                return _br.ReadInt16();
            case NrbfPrimitiveType.UInt16:
                return _br.ReadUInt16();
            case NrbfPrimitiveType.Int32:
                return _br.ReadInt32();
            case NrbfPrimitiveType.UInt32:
                return _br.ReadUInt32();
            case NrbfPrimitiveType.Int64:
                return _br.ReadInt64();
            case NrbfPrimitiveType.UInt64:
                return _br.ReadUInt64();
            case NrbfPrimitiveType.Single:
                return _br.ReadSingle();
            case NrbfPrimitiveType.Double:
                return _br.ReadDouble();
            case NrbfPrimitiveType.TimeSpan:
                return _br.ReadInt64();
            case NrbfPrimitiveType.DateTime:
                return _br.ReadInt64();
            case NrbfPrimitiveType.Char:
                return ReadUtf8CodePoint();
            case NrbfPrimitiveType.Decimal:
                return ReadLengthPrefixedString(_br);
            case NrbfPrimitiveType.String:
                return ReadLengthPrefixedString(_br);
            case NrbfPrimitiveType.Null:
                return null;
            default:
                throw new InvalidDataException($"Unsupported primitive type {type}");
        }
    }

    private string ReadUtf8CodePoint()
    {
        byte first = _br.ReadByte();
        int length;
        if ((first & 0x80) == 0)
            length = 1;
        else if ((first & 0xE0) == 0xC0)
            length = 2;
        else if ((first & 0xF0) == 0xE0)
            length = 3;
        else if ((first & 0xF8) == 0xF0)
            length = 4;
        else
            throw new InvalidDataException("Invalid UTF-8 code point");
        var bytes = new byte[length];
        bytes[0] = first;
        for (int i = 1; i < length; i++)
            bytes[i] = _br.ReadByte();
        return Encoding.UTF8.GetString(bytes);
    }

    public NrbfRecord? ResolveRecord(object? value)
    {
        switch (value)
        {
            case NrbfRecord rec:
                if (rec.RecordType == NrbfRecordType.MemberReference)
                {
                    if (_references.TryGetValue(rec.IdRef, out var refObj))
                        return refObj.RealObj;
                    return null;
                }
                return rec;
            default:
                return null;
        }
    }
}
