using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Svip;

internal enum NrbfRecordType
{
    SerializedStreamHeader = 0,
    ClassWithId = 1,
    SystemClassWithMembers = 2,
    ClassWithMembers = 3,
    SystemClassWithMembersAndTypes = 4,
    ClassWithMembersAndTypes = 5,
    BinaryObjectString = 6,
    BinaryArray = 7,
    MemberPrimitiveTyped = 8,
    MemberReference = 9,
    ObjectNull = 10,
    MessageEnd = 11,
    BinaryLibrary = 12,
    ObjectNullMultiple256 = 13,
    ObjectNullMultiple = 14,
    ArraySinglePrimitive = 15,
    ArraySingleObject = 16,
    ArraySingleString = 17,
}

internal enum NrbfPrimitiveType
{
    Boolean = 1,
    Byte = 2,
    Char = 3,
    Decimal = 5,
    Double = 6,
    Int16 = 7,
    Int32 = 8,
    Int64 = 9,
    SByte = 10,
    Single = 11,
    TimeSpan = 12,
    DateTime = 13,
    UInt16 = 14,
    UInt32 = 15,
    UInt64 = 16,
    Null = 17,
    String = 18,
}

internal enum NrbfBinaryType
{
    Primitive = 0,
    String = 1,
    Object = 2,
    SystemClass = 3,
    Class = 4,
    ObjectArray = 5,
    StringArray = 6,
    PrimitiveArray = 7,
}

internal enum NrbfBinaryArrayType
{
    Single = 0,
    Jagged = 1,
    Rectangular = 2,
    SingleOffset = 3,
    JaggedOffset = 4,
    RectangularOffset = 5,
}

internal sealed class NrbfClassTypeInfo
{
    public string TypeName = "";
    public int LibraryId;
}

internal sealed class NrbfMemberTypeInfo
{
    public readonly List<NrbfBinaryType> BinaryTypes = new();
    public readonly List<object?> AdditionalInfos = new();
}

internal sealed class NrbfClassInfo
{
    public int ObjectId;
    public string Name = "";
    public readonly List<string> MemberNames = new();
}

internal sealed class NrbfRecord
{
    public NrbfRecordType RecordType;
    public NrbfClassInfo? ClassInfo;
    public NrbfMemberTypeInfo? MemberTypeInfo;
    public int LibraryId;
    public readonly List<object?> MemberValues = new();

    public int ObjectId;
    public int RootId;

    public string StringValue = "";
    public string LibraryName = "";
    public int IdRef;
    public int NullCount;

    public NrbfBinaryArrayType ArrayType;
    public int Rank;
    public readonly List<int> Lengths = new();
    public NrbfBinaryType ArrayBinaryType;
    public NrbfClassTypeInfo? ArrayInfo;
    public NrbfPrimitiveType ArrayPrimitiveType;
    public int ArrayLength;
}

internal sealed class NrbfReference
{
    public int IdRef;
    public NrbfRecord? RealObj;
}
