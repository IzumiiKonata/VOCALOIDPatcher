using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Svip;

internal sealed class SvipNrbfWriter
{
    private const string LibraryModel = "SingingTool.Model, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
    private const string LibraryLib = "SingingTool.Library, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";

    private sealed class WriteRef
    {
        public int IdRef;
        public string? SubconClassName;
        public XSObject? RealDataclass;
        public NrbfRecord? RealRecord;
    }

    private readonly Queue<int> _ids = new();
    private int _idMax;
    private int _modelLibraryId;
    private int _libLibraryId;
    private readonly Dictionary<string, NrbfRecord> _classDefs = new();
    private readonly Dictionary<int, WriteRef> _references = new();
    private readonly List<NrbfRecord> _records = new();
    private string _version = "";

    private int Enq()
    {
        _idMax += 1;
        _ids.Enqueue(_idMax);
        return _idMax;
    }

    public byte[] Write(string version, XSAppModel model)
    {
        string magic = version.Length >= 4 ? version.Substring(0, 4) : version;
        _version = version.Length > 4 ? version.Substring(4) : "";

        _idMax = 0;
        _idMax += 1;
        int appModelId = _idMax;

        _records.Add(new NrbfRecord
        {
            RecordType = NrbfRecordType.SerializedStreamHeader,
            RootId = appModelId,
        });

        _modelLibraryId = WriteLibrary(LibraryModel);
        _libLibraryId = WriteLibrary(LibraryLib);

        _records.Add(WriteDataclass(model, appModelId, null));

        while (_ids.Count > 0)
        {
            int notWritten = _ids.Dequeue();
            WriteRef refObj = _references[notWritten];
            if (refObj.RealDataclass != null)
                _records.Add(WriteDataclass(refObj.RealDataclass, notWritten, refObj.SubconClassName));
            else if (refObj.RealRecord != null)
                _records.Add(refObj.RealRecord);
        }

        _records.Add(new NrbfRecord { RecordType = NrbfRecordType.MessageEnd });

        return Serialize(magic, _version);
    }

    private int WriteLibrary(string libraryName)
    {
        _idMax += 1;
        int result = _idMax;
        _records.Add(new NrbfRecord
        {
            RecordType = NrbfRecordType.BinaryLibrary,
            ObjectId = result,
            LibraryName = libraryName,
        });
        return result;
    }

    private NrbfRecord CreateString(string value)
    {
        _idMax += 1;
        return new NrbfRecord
        {
            RecordType = NrbfRecordType.BinaryObjectString,
            ObjectId = _idMax,
            StringValue = value,
        };
    }

    private NrbfRecord CreateReferenceRecord(int objectId)
    {
        return new NrbfRecord { RecordType = NrbfRecordType.MemberReference, IdRef = objectId };
    }

    private NrbfRecord CreateReferenceDataclass(XSObject value, int objectId, string? subconClassName)
    {
        if (subconClassName != null && subconClassName.Contains("`1"))
        {
            int idx = subconClassName.IndexOf("[[", StringComparison.Ordinal);
            if (idx >= 0)
            {
                string tail = subconClassName.Substring(idx + 2);
                int comma = tail.IndexOf(", ", StringComparison.Ordinal);
                subconClassName = comma >= 0 ? tail.Substring(0, comma) : tail;
            }
        }
        if (!_references.ContainsKey(objectId))
        {
            _references[objectId] = new WriteRef
            {
                IdRef = objectId,
                SubconClassName = subconClassName,
                RealDataclass = value,
            };
        }
        return CreateReferenceRecord(objectId);
    }

    private NrbfRecord WriteNullArray(int count)
    {
        if (count == 1)
            return new NrbfRecord { RecordType = NrbfRecordType.ObjectNull };
        if (count <= 256)
            return new NrbfRecord { RecordType = NrbfRecordType.ObjectNullMultiple256, NullCount = count };
        return new NrbfRecord { RecordType = NrbfRecordType.ObjectNullMultiple, NullCount = count };
    }

    private NrbfRecord WriteBinaryArray(List<XSObject?> values, string typeName, int libraryId)
    {
        int objectId = Enq();
        int paddedLength = values.Count == 0
            ? 4
            : Math.Max(4, 1 << (int)Math.Ceiling(Math.Log2(values.Count)));

        var arrayRecord = new NrbfRecord
        {
            RecordType = NrbfRecordType.BinaryArray,
            ObjectId = objectId,
            ArrayType = NrbfBinaryArrayType.Single,
            Rank = 1,
            ArrayBinaryType = NrbfBinaryType.Class,
            ArrayInfo = new NrbfClassTypeInfo
            {
                TypeName = typeName
                    .Replace("InstrumentTrack", "ITrack")
                    .Replace("SingingTrack", "ITrack"),
                LibraryId = libraryId,
            },
        };
        arrayRecord.Lengths.Add(paddedLength);

        foreach (var value in values)
            arrayRecord.MemberValues.Add(CreateReferenceDataclass(value!, Enq(), typeName));

        if (values.Count < paddedLength)
            arrayRecord.MemberValues.Add(WriteNullArray(paddedLength - values.Count));

        _references[objectId] = new WriteRef { IdRef = objectId, RealRecord = arrayRecord };
        return CreateReferenceDataclassFromRecord(arrayRecord, objectId);
    }

    private NrbfRecord CreateReferenceDataclassFromRecord(NrbfRecord record, int objectId)
    {
        if (!_references.ContainsKey(objectId))
            _references[objectId] = new WriteRef { IdRef = objectId, RealRecord = record };
        return CreateReferenceRecord(objectId);
    }

    private NrbfRecord CreatePrimitiveArray(byte[] values)
    {
        int objectId = Enq();
        var record = new NrbfRecord
        {
            RecordType = NrbfRecordType.ArraySinglePrimitive,
            ObjectId = objectId,
            ArrayLength = values.Length,
            ArrayPrimitiveType = NrbfPrimitiveType.Byte,
        };
        foreach (byte b in values)
            record.MemberValues.Add(b);
        _references[objectId] = new WriteRef { IdRef = objectId, RealRecord = record };
        return CreateReferenceDataclassFromRecord(record, objectId);
    }

    private NrbfRecord WriteDataclass(XSObject obj, int objectId, string? subconClassName)
    {
        var fields = obj.Fields.OrderBy(f => f.Order).ToList();
        string className = obj.ClassName;

        if (subconClassName != null && className.EndsWith("List", StringComparison.Ordinal)
            && subconClassName != className)
        {
            className = $"{className}`1[[{subconClassName}, {LibraryModel}]]";
        }

        if (!_classDefs.ContainsKey(className))
        {
            var record = new NrbfRecord
            {
                ClassInfo = new NrbfClassInfo { ObjectId = objectId, Name = className },
                MemberTypeInfo = new NrbfMemberTypeInfo(),
            };
            if (className.StartsWith("System.", StringComparison.Ordinal))
            {
                record.RecordType = NrbfRecordType.SystemClassWithMembersAndTypes;
            }
            else
            {
                if (className.StartsWith("SingingTool.Model.", StringComparison.Ordinal))
                    record.LibraryId = _modelLibraryId;
                else if (className.StartsWith("SingingTool.Library.", StringComparison.Ordinal))
                    record.LibraryId = _libLibraryId;
                record.RecordType = NrbfRecordType.ClassWithMembersAndTypes;
            }

            foreach (var field in fields)
            {
                if (string.IsNullOrEmpty(field.Alias))
                    continue;
                if (field.UpdatesSubcon && field.ElementClassName != null)
                    subconClassName = field.ElementClassName;
                if (field.Name == "edited_power_line" && _version != "7.0.0")
                    continue;

                AddMemberType(record, obj, field, ref subconClassName);
                AddMemberValue(record, obj, field, ref subconClassName);
                record.ClassInfo.MemberNames.Add(field.Alias);
            }

            _classDefs[className] = record;
            return record;
        }
        else
        {
            var record = new NrbfRecord
            {
                RecordType = NrbfRecordType.ClassWithId,
                ObjectId = objectId,
                ClassInfo = new NrbfClassInfo
                {
                    ObjectId = _classDefs[className].ClassInfo!.ObjectId,
                    Name = className,
                },
            };
            record.MemberTypeInfo = _classDefs[className].MemberTypeInfo;
            record.ClassInfo.MemberNames.AddRange(_classDefs[className].ClassInfo!.MemberNames);

            foreach (var field in fields)
            {
                if (string.IsNullOrEmpty(field.Alias))
                    continue;
                if (field.UpdatesSubcon && field.ElementClassName != null)
                    subconClassName = field.ElementClassName;
                if (field.Name == "edited_power_line" && _version != "7.0.0")
                    continue;
                AddMemberValue(record, obj, field, ref subconClassName);
            }
            return record;
        }
    }

    private void AddMemberType(NrbfRecord record, XSObject obj, XSField field, ref string? subconClassName)
    {
        var typeInfo = record.MemberTypeInfo!;
        switch (field.Kind)
        {
            case XSFieldKind.String:
                typeInfo.BinaryTypes.Add(NrbfBinaryType.String);
                typeInfo.AdditionalInfos.Add(null);
                break;
            case XSFieldKind.Bool:
                typeInfo.BinaryTypes.Add(NrbfBinaryType.Primitive);
                typeInfo.AdditionalInfos.Add(NrbfPrimitiveType.Boolean);
                break;
            case XSFieldKind.Single:
                typeInfo.BinaryTypes.Add(NrbfBinaryType.Primitive);
                typeInfo.AdditionalInfos.Add(NrbfPrimitiveType.Single);
                break;
            case XSFieldKind.Double:
                typeInfo.BinaryTypes.Add(NrbfBinaryType.Primitive);
                typeInfo.AdditionalInfos.Add(NrbfPrimitiveType.Double);
                break;
            case XSFieldKind.Int32:
                typeInfo.BinaryTypes.Add(NrbfBinaryType.Primitive);
                typeInfo.AdditionalInfos.Add(NrbfPrimitiveType.Int32);
                break;
            case XSFieldKind.List:
                typeInfo.BinaryTypes.Add(NrbfBinaryType.Class);
                typeInfo.AdditionalInfos.Add(new NrbfClassTypeInfo
                {
                    TypeName = $"{subconClassName}[]",
                    LibraryId = _modelLibraryId,
                });
                break;
            case XSFieldKind.Bytes:
                typeInfo.BinaryTypes.Add(NrbfBinaryType.PrimitiveArray);
                typeInfo.AdditionalInfos.Add(NrbfPrimitiveType.Byte);
                break;
            case XSFieldKind.InlineWrapper:
            case XSFieldKind.Dataclass:
            {
                string subClassName = GetFieldClassName(obj, field, subconClassName);
                if (subClassName.StartsWith("System.", StringComparison.Ordinal))
                {
                    typeInfo.BinaryTypes.Add(NrbfBinaryType.SystemClass);
                    typeInfo.AdditionalInfos.Add(subClassName);
                }
                else
                {
                    typeInfo.BinaryTypes.Add(NrbfBinaryType.Class);
                    typeInfo.AdditionalInfos.Add(new NrbfClassTypeInfo
                    {
                        TypeName = subClassName,
                        LibraryId = subClassName.StartsWith("SingingTool.Model.", StringComparison.Ordinal)
                            ? _modelLibraryId
                            : _libLibraryId,
                    });
                }
                break;
            }
        }
    }

    private string GetFieldClassName(XSObject obj, XSField field, string? subconClassName)
    {
        object? value = field.Get(obj);
        string subClassName;
        if (value is XSObject xs)
            subClassName = xs.ClassName;
        else
            subClassName = field.ElementClassName ?? "";
        if (subClassName.EndsWith("List", StringComparison.Ordinal))
            subClassName = $"{subClassName}`1[[{subconClassName}, {LibraryModel}]]";
        return subClassName;
    }

    private void AddMemberValue(NrbfRecord record, XSObject obj, XSField field, ref string? subconClassName)
    {
        object? value = field.Get(obj);

        if (value == null)
        {
            record.MemberValues.Add(new NrbfRecord { RecordType = NrbfRecordType.ObjectNull });
            return;
        }

        switch (field.Kind)
        {
            case XSFieldKind.String:
                record.MemberValues.Add(CreateString(value as string ?? ""));
                break;
            case XSFieldKind.Bool:
            case XSFieldKind.Int32:
            case XSFieldKind.Single:
            case XSFieldKind.Double:
                record.MemberValues.Add(value);
                break;
            case XSFieldKind.List:
            {
                var items = (List<XSObject?>)value;
                if (items.Count > 0 && items[0] != null)
                    subconClassName = items[0]!.ClassName;
                record.MemberValues.Add(WriteBinaryArray(items, subconClassName ?? "", _modelLibraryId));
                break;
            }
            case XSFieldKind.Bytes:
                record.MemberValues.Add(CreatePrimitiveArray((byte[])value));
                break;
            case XSFieldKind.InlineWrapper:
            {
                var xs = (XSObject)value;
                _idMax += 1;
                int objId = _idMax;
                record.MemberValues.Add(WriteDataclass(xs, -objId, xs.ClassName));
                break;
            }
            case XSFieldKind.Dataclass:
            {
                var xs = (XSObject)value;
                string subClassName = xs.ClassName;
                if (subClassName.EndsWith("List", StringComparison.Ordinal))
                    subClassName = $"{subClassName}`1[[{subconClassName}, {LibraryModel}]]";

                int objId;
                if (field.Name == "buf_1")
                    objId = _idMax;
                else
                    objId = Enq();

                if (field.Name.EndsWith("_line", StringComparison.Ordinal)
                    && value is XSLineParam lp && lp.LineParam.Length == 0)
                {
                    record.MemberValues.Add(new NrbfRecord { RecordType = NrbfRecordType.ObjectNull });
                }
                else
                {
                    record.MemberValues.Add(CreateReferenceDataclass(xs, objId, subClassName));
                }
                break;
            }
        }
    }

    private byte[] Serialize(string magic, string version)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms, Encoding.UTF8);
        WritePascalString(bw, magic);
        WritePascalString(bw, version);
        foreach (var record in _records)
            WriteRecord(bw, record);
        bw.Flush();
        return ms.ToArray();
    }

    private static void WritePascalString(BinaryWriter bw, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        bw.Write((byte)bytes.Length);
        bw.Write(bytes);
    }

    private static void WriteLengthPrefixedString(BinaryWriter bw, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        int length = bytes.Length;
        while (length > 0x7F)
        {
            bw.Write((byte)((length & 0x7F) | 0x80));
            length >>= 7;
        }
        bw.Write((byte)length);
        bw.Write(bytes);
    }

    private void WriteRecord(BinaryWriter bw, NrbfRecord record)
    {
        bw.Write((byte)record.RecordType);
        switch (record.RecordType)
        {
            case NrbfRecordType.SerializedStreamHeader:
                bw.Write(record.RootId);
                bw.Write(-1);
                bw.Write(1);
                bw.Write(0);
                break;
            case NrbfRecordType.BinaryLibrary:
                bw.Write(record.ObjectId);
                WriteLengthPrefixedString(bw, record.LibraryName);
                break;
            case NrbfRecordType.ClassWithMembersAndTypes:
                WriteClassInfo(bw, record.ClassInfo!);
                WriteMemberTypeInfo(bw, record.MemberTypeInfo!);
                bw.Write(record.LibraryId);
                WriteMemberValues(bw, record);
                break;
            case NrbfRecordType.SystemClassWithMembersAndTypes:
                WriteClassInfo(bw, record.ClassInfo!);
                WriteMemberTypeInfo(bw, record.MemberTypeInfo!);
                WriteMemberValues(bw, record);
                break;
            case NrbfRecordType.ClassWithId:
                bw.Write(record.ObjectId);
                bw.Write(record.ClassInfo!.ObjectId);
                WriteMemberValues(bw, record);
                break;
            case NrbfRecordType.BinaryObjectString:
                bw.Write(record.ObjectId);
                WriteLengthPrefixedString(bw, record.StringValue);
                break;
            case NrbfRecordType.BinaryArray:
                WriteBinaryArrayRecord(bw, record);
                break;
            case NrbfRecordType.ArraySinglePrimitive:
                bw.Write(record.ObjectId);
                bw.Write(record.ArrayLength);
                bw.Write((byte)record.ArrayPrimitiveType);
                foreach (var v in record.MemberValues)
                    bw.Write(Convert.ToByte(v));
                break;
            case NrbfRecordType.MemberReference:
                bw.Write(record.IdRef);
                break;
            case NrbfRecordType.ObjectNullMultiple256:
                bw.Write((byte)record.NullCount);
                break;
            case NrbfRecordType.ObjectNullMultiple:
                bw.Write(record.NullCount);
                break;
            case NrbfRecordType.ObjectNull:
            case NrbfRecordType.MessageEnd:
                break;
            default:
                throw new InvalidDataException($"Cannot serialize record {record.RecordType}");
        }
    }

    private static void WriteClassInfo(BinaryWriter bw, NrbfClassInfo info)
    {
        bw.Write(info.ObjectId);
        WriteLengthPrefixedString(bw, info.Name);
        bw.Write(info.MemberNames.Count);
        foreach (var name in info.MemberNames)
            WriteLengthPrefixedString(bw, name);
    }

    private static void WriteMemberTypeInfo(BinaryWriter bw, NrbfMemberTypeInfo info)
    {
        foreach (var bt in info.BinaryTypes)
            bw.Write((byte)bt);
        for (int i = 0; i < info.BinaryTypes.Count; i++)
        {
            object? add = info.AdditionalInfos[i];
            switch (info.BinaryTypes[i])
            {
                case NrbfBinaryType.Primitive:
                case NrbfBinaryType.PrimitiveArray:
                    bw.Write((byte)(NrbfPrimitiveType)add!);
                    break;
                case NrbfBinaryType.SystemClass:
                    WriteLengthPrefixedString(bw, (string)add!);
                    break;
                case NrbfBinaryType.Class:
                    var cti = (NrbfClassTypeInfo)add!;
                    WriteLengthPrefixedString(bw, cti.TypeName);
                    bw.Write(cti.LibraryId);
                    break;
            }
        }
    }

    private void WriteMemberValues(BinaryWriter bw, NrbfRecord record)
    {
        var typeInfo = record.MemberTypeInfo!;
        for (int i = 0; i < record.MemberValues.Count; i++)
        {
            object? value = record.MemberValues[i];
            NrbfBinaryType binaryType = typeInfo.BinaryTypes[i];
            if (binaryType == NrbfBinaryType.Primitive && value is not NrbfRecord)
            {
                var primType = (NrbfPrimitiveType)typeInfo.AdditionalInfos[i]!;
                WritePrimitive(bw, primType, value);
            }
            else if (value is NrbfRecord sub)
            {
                WriteRecord(bw, sub);
            }
        }
    }

    private void WriteBinaryArrayRecord(BinaryWriter bw, NrbfRecord record)
    {
        bw.Write(record.ObjectId);
        bw.Write((byte)record.ArrayType);
        bw.Write(record.Rank);
        foreach (int len in record.Lengths)
            bw.Write(len);
        bw.Write((byte)record.ArrayBinaryType);
        if (record.ArrayBinaryType == NrbfBinaryType.Class)
        {
            WriteLengthPrefixedString(bw, record.ArrayInfo!.TypeName);
            bw.Write(record.ArrayInfo.LibraryId);
        }
        foreach (var v in record.MemberValues)
            if (v is NrbfRecord sub)
                WriteRecord(bw, sub);
    }

    private static void WritePrimitive(BinaryWriter bw, NrbfPrimitiveType type, object? value)
    {
        switch (type)
        {
            case NrbfPrimitiveType.Boolean:
                bw.Write((byte)((bool)value! ? 1 : 0));
                break;
            case NrbfPrimitiveType.Byte:
                bw.Write(Convert.ToByte(value));
                break;
            case NrbfPrimitiveType.Int32:
                bw.Write(Convert.ToInt32(value));
                break;
            case NrbfPrimitiveType.Single:
                bw.Write(Convert.ToSingle(value));
                break;
            case NrbfPrimitiveType.Double:
                bw.Write(Convert.ToDouble(value));
                break;
            case NrbfPrimitiveType.Int64:
                bw.Write(Convert.ToInt64(value));
                break;
            default:
                throw new InvalidDataException($"Cannot serialize primitive {type}");
        }
    }
}
