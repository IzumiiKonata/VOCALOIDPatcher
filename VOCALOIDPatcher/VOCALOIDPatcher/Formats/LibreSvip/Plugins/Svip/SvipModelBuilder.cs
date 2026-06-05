using System;
using System.Collections.Generic;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Svip;

internal sealed class SvipModelBuilder
{
    private readonly SvipNrbfReader _reader;

    public SvipModelBuilder(SvipNrbfReader reader)
    {
        _reader = reader;
    }

    private static XSObject? CreateByClassName(string className)
    {
        switch (className)
        {
            case "SingingTool.Model.AppModel": return new XSAppModel();
            case "SingingTool.Model.SingingGeneralConcept.BeatSize": return new XSBeatSize();
            case "SingingTool.Model.SingingGeneralConcept.SongBeat": return new XSSongBeat();
            case "SingingTool.Model.SingingGeneralConcept.SongTempo": return new XSSongTempo();
            case "SingingTool.Model.Line.LineParam": return new XSLineParam();
            case "SingingTool.Library.SerialOverlapableItemList": return new XSBufList("");
            case "System.Collections.Generic.List": return new XSBuf("");
            case "SingingTool.Model.SingingTrack": return new XSSingingTrack();
            case "SingingTool.Model.InstrumentTrack": return new XSInstrumentTrack();
            case "SingingTool.Model.VibratoPercentInfo": return new XSVibratoPercentInfo();
            case "SingingTool.Model.VibratoStyle": return new XSVibratoStyle();
            case "SingingTool.Model.NotePhoneInfo": return new XSNotePhoneInfo();
            case "SingingTool.Model.NoteHeadTag": return new XSNoteHeadTag();
            case "SingingTool.Model.Note": return new XSNote();
            case "SingingTool.Library.Audio.ReverbPreset": return new XSReverbPreset();
            default: return null;
        }
    }

    public XSAppModel? Build()
    {
        if (_reader.RootRecord == null)
            return null;
        return BuildObject(_reader.RootRecord) as XSAppModel;
    }

    private object? BuildObject(NrbfRecord? record)
    {
        if (record == null)
            return null;
        switch (record.RecordType)
        {
            case NrbfRecordType.ClassWithId:
            case NrbfRecordType.SystemClassWithMembers:
            case NrbfRecordType.ClassWithMembers:
            case NrbfRecordType.SystemClassWithMembersAndTypes:
            case NrbfRecordType.ClassWithMembersAndTypes:
                return BuildClass(record);
            case NrbfRecordType.BinaryArray:
                return BuildBinaryArray(record);
            case NrbfRecordType.ArraySinglePrimitive:
                if (record.ArrayPrimitiveType == NrbfPrimitiveType.Byte)
                    return BuildByteArray(record);
                return null;
            case NrbfRecordType.BinaryObjectString:
                return record.StringValue;
            case NrbfRecordType.ObjectNullMultiple:
            case NrbfRecordType.ObjectNullMultiple256:
                return new List<object?>(new object?[record.NullCount]);
            case NrbfRecordType.MemberReference:
                return BuildObject(_reader.ResolveRecord(record));
            default:
                return null;
        }
    }

    private byte[] BuildByteArray(NrbfRecord record)
    {
        var bytes = new byte[record.MemberValues.Count];
        for (int i = 0; i < record.MemberValues.Count; i++)
            bytes[i] = Convert.ToByte(record.MemberValues[i]);
        return bytes;
    }

    private List<object?> BuildBinaryArray(NrbfRecord record)
    {
        var results = new List<object?>();
        if (record.ArrayBinaryType == NrbfBinaryType.Class
            || record.ArrayBinaryType == NrbfBinaryType.SystemClass
            || record.ArrayBinaryType == NrbfBinaryType.Object)
        {
            foreach (var member in record.MemberValues)
            {
                if (member is not NrbfRecord elem)
                {
                    results.Add(null);
                    continue;
                }
                if (elem.RecordType == NrbfRecordType.ObjectNull)
                {
                    results.Add(null);
                }
                else if (elem.RecordType == NrbfRecordType.ObjectNullMultiple
                         || elem.RecordType == NrbfRecordType.ObjectNullMultiple256)
                {
                    for (int i = 0; i < elem.NullCount; i++)
                        results.Add(null);
                }
                else if (elem.RecordType == NrbfRecordType.MemberReference)
                {
                    results.Add(BuildObject(_reader.ResolveRecord(elem)));
                }
                else
                {
                    results.Add(BuildObject(elem));
                }
            }
        }
        return results;
    }

    private object? BuildClass(NrbfRecord record)
    {
        string fullName = record.ClassInfo!.Name;
        int tick = fullName.IndexOf("`1", StringComparison.Ordinal);
        string className = tick >= 0 ? fullName.Substring(0, tick) : fullName;

        XSObject? model = CreateByClassName(className);
        if (model == null)
            return null;

        var aliasToField = new Dictionary<string, XSField>();
        foreach (var f in model.Fields)
            aliasToField[f.Alias] = f;

        var names = record.ClassInfo.MemberNames;
        for (int i = 0; i < names.Count && i < record.MemberValues.Count; i++)
        {
            string memberName = names[i];
            object? raw = record.MemberValues[i];
            if (!aliasToField.TryGetValue(memberName, out var field))
                continue;

            object? value = BuildMemberValue(raw);
            ApplyField(model, field, value);
        }

        if (model is XSBuf buf)
        {
            if (buf.Items.Count > buf.Size)
                buf.Items.RemoveRange(buf.Size, buf.Items.Count - buf.Size);
        }
        if (model is XSLineParam lp)
            lp.InitFromBytes();

        return model;
    }

    private object? BuildMemberValue(object? raw)
    {
        if (raw is NrbfRecord rec)
        {
            if (rec.RecordType == NrbfRecordType.ObjectNull)
                return null;
            if (rec.RecordType == NrbfRecordType.MemberReference)
                return BuildObject(_reader.ResolveRecord(rec));
            if (rec.RecordType == NrbfRecordType.MemberPrimitiveTyped)
                return rec.MemberValues.Count > 0 ? rec.MemberValues[0] : null;
            return BuildObject(rec);
        }
        return raw;
    }

    private static void ApplyField(XSObject model, XSField field, object? value)
    {
        switch (field.Kind)
        {
            case XSFieldKind.List:
                if (value is List<object?> list)
                    field.Set(model, list);
                break;
            case XSFieldKind.EnumWrapper:
            case XSFieldKind.InlineWrapper:
                if (value is XSObject wrapper)
                    field.Set(model, wrapper);
                break;
            case XSFieldKind.Dataclass:
                if (value != null || field.Name is "note_phone_info" or "vibrato" or "vibrato_percent_info" or "edited_power_line" or "actual_project_file_path")
                    field.Set(model, value);
                break;
            case XSFieldKind.Bytes:
                field.Set(model, value as byte[] ?? Array.Empty<byte>());
                break;
            default:
                if (value != null)
                    field.Set(model, value);
                break;
        }
    }
}
