using System;
using System.Collections.Generic;
using System.Linq;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Svip;

internal enum XSFieldKind
{
    Bool,
    Int32,
    Single,
    Double,
    String,
    Bytes,
    Dataclass,
    EnumWrapper,
    InlineWrapper,
    List,
}

internal sealed class XSField
{
    public string Name = "";
    public string Alias = "";
    public int Order;
    public XSFieldKind Kind;
    public string? ElementClassName;
    public bool UpdatesSubcon;
    public Func<XSObject, object?> Get = _ => null;
    public Action<XSObject, object?> Set = (_, _) => { };
}

internal abstract class XSObject
{
    public abstract string ClassName { get; }
    public abstract IReadOnlyList<XSField> Fields { get; }
}

internal sealed class XSLineParamNode
{
    public int Pos;
    public int Value;

    public XSLineParamNode() { }
    public XSLineParamNode(int pos, int value) { Pos = pos; Value = value; }
}

internal sealed class XSLineParam : XSObject
{
    public byte[] LineParam = Array.Empty<byte>();
    public readonly List<XSLineParamNode> Nodes = new();

    public override string ClassName => "SingingTool.Model.Line.LineParam";

    private static readonly XSField[] _fields =
    {
        new()
        {
            Name = "line_param", Alias = "LineParam", Order = 0, Kind = XSFieldKind.Bytes,
            Get = o => ((XSLineParam)o).LineParam,
            Set = (o, v) => ((XSLineParam)o).LineParam = (byte[])(v ?? Array.Empty<byte>()),
        },
    };

    public override IReadOnlyList<XSField> Fields => _fields;

    public void InitFromBytes()
    {
        Nodes.Clear();
        if (LineParam.Length >= 4)
        {
            int nodeCount = BitConverter.ToInt32(LineParam, 0);
            int offset = 4;
            for (int i = 0; i < nodeCount; i++)
            {
                int pos = BitConverter.ToInt32(LineParam, offset);
                int value = BitConverter.ToInt32(LineParam, offset + 4);
                offset += 8;
                Nodes.Add(new XSLineParamNode(pos, value));
            }
        }
    }

    public void ConvertToParam()
    {
        var buffer = new List<byte>();
        buffer.AddRange(BitConverter.GetBytes(Nodes.Count));
        foreach (var node in Nodes)
        {
            buffer.AddRange(BitConverter.GetBytes(node.Pos));
            buffer.AddRange(BitConverter.GetBytes(node.Value));
        }
        int expectedLen = Math.Max(64, buffer.Count <= 1 ? 64 : 1 << (int)Math.Ceiling(Math.Log2(buffer.Count)));
        while (buffer.Count < expectedLen)
            buffer.Add(0);
        LineParam = buffer.ToArray();
    }
}

internal sealed class XSVibratoStyle : XSObject
{
    public XSLineParam AmpLine = new();
    public XSLineParam FreqLine = new();
    public bool IsAntiPhase;

    public override string ClassName => "SingingTool.Model.VibratoStyle";

    private static readonly XSField[] _fields =
    {
        new() { Name = "amp_line", Alias = "_ampLine", Order = 0, Kind = XSFieldKind.Dataclass,
            Get = o => ((XSVibratoStyle)o).AmpLine, Set = (o, v) => ((XSVibratoStyle)o).AmpLine = (XSLineParam)v! },
        new() { Name = "freq_line", Alias = "_freqLine", Order = 1, Kind = XSFieldKind.Dataclass,
            Get = o => ((XSVibratoStyle)o).FreqLine, Set = (o, v) => ((XSVibratoStyle)o).FreqLine = (XSLineParam)v! },
        new() { Name = "is_anti_phase", Alias = "<IsAntiPhase>k__BackingField", Order = 2, Kind = XSFieldKind.Bool,
            Get = o => ((XSVibratoStyle)o).IsAntiPhase, Set = (o, v) => ((XSVibratoStyle)o).IsAntiPhase = (bool)v! },
    };

    public override IReadOnlyList<XSField> Fields => _fields;
}

internal sealed class XSVibratoPercentInfo : XSObject
{
    public float StartPercent;
    public float EndPercent = 100;

    public override string ClassName => "SingingTool.Model.VibratoPercentInfo";

    private static readonly XSField[] _fields =
    {
        new() { Name = "start_percent", Alias = "_startPercent", Order = 0, Kind = XSFieldKind.Single,
            Get = o => ((XSVibratoPercentInfo)o).StartPercent, Set = (o, v) => ((XSVibratoPercentInfo)o).StartPercent = Convert.ToSingle(v) },
        new() { Name = "end_percent", Alias = "_endPercent", Order = 1, Kind = XSFieldKind.Single,
            Get = o => ((XSVibratoPercentInfo)o).EndPercent, Set = (o, v) => ((XSVibratoPercentInfo)o).EndPercent = Convert.ToSingle(v) },
    };

    public override IReadOnlyList<XSField> Fields => _fields;
}

internal enum XSReverbPresetEnum
{
    None = -1,
    Default = 0,
    SmallHall1 = 1,
    SmallHall2 = 2,
    MediumHall1 = 3,
    MediumHall2 = 4,
    LargeHall1 = 5,
    LargeHall2 = 6,
    SmallRoom1 = 7,
    SmallRoom2 = 8,
    MediumRoom1 = 9,
    MediumRoom2 = 10,
    LargeRoom1 = 11,
    LargeRoom2 = 12,
    MediumEr1 = 13,
    MediumEr2 = 14,
    PlateHigh = 15,
    PlateLow = 16,
    LongReverb1 = 17,
    LongReverb2 = 18,
}

internal sealed class XSReverbPreset : XSObject
{
    public XSReverbPresetEnum Value = XSReverbPresetEnum.None;

    public XSReverbPreset() { }
    public XSReverbPreset(XSReverbPresetEnum value) { Value = value; }

    public override string ClassName => "SingingTool.Library.Audio.ReverbPreset";

    private static readonly XSField[] _fields =
    {
        new() { Name = "value", Alias = "value__", Order = 0, Kind = XSFieldKind.Int32,
            Get = o => (int)((XSReverbPreset)o).Value, Set = (o, v) => ((XSReverbPreset)o).Value = (XSReverbPresetEnum)Convert.ToInt32(v) },
    };

    public override IReadOnlyList<XSField> Fields => _fields;
}

internal sealed class XSBeatSize : XSObject
{
    public int X;
    public int Y;

    public XSBeatSize() { }
    public XSBeatSize(int x, int y) { X = x; Y = y; }

    public override string ClassName => "SingingTool.Model.SingingGeneralConcept.BeatSize";

    private static readonly XSField[] _fields =
    {
        new() { Name = "x", Alias = "_x", Order = 0, Kind = XSFieldKind.Int32,
            Get = o => ((XSBeatSize)o).X, Set = (o, v) => ((XSBeatSize)o).X = Convert.ToInt32(v) },
        new() { Name = "y", Alias = "_y", Order = 1, Kind = XSFieldKind.Int32,
            Get = o => ((XSBeatSize)o).Y, Set = (o, v) => ((XSBeatSize)o).Y = Convert.ToInt32(v) },
    };

    public override IReadOnlyList<XSField> Fields => _fields;
}

internal sealed class XSSongBeat : XSObject
{
    public int BarIndex;
    public XSBeatSize BeatSize = new();
    public bool Overlapped;

    public override string ClassName => "SingingTool.Model.SingingGeneralConcept.SongBeat";

    private static readonly XSField[] _fields =
    {
        new() { Name = "bar_index", Alias = "_barIndex", Order = 0, Kind = XSFieldKind.Int32,
            Get = o => ((XSSongBeat)o).BarIndex, Set = (o, v) => ((XSSongBeat)o).BarIndex = Convert.ToInt32(v) },
        new() { Name = "beat_size", Alias = "_beatSize", Order = 1, Kind = XSFieldKind.Dataclass,
            Get = o => ((XSSongBeat)o).BeatSize, Set = (o, v) => ((XSSongBeat)o).BeatSize = (XSBeatSize)v! },
        new() { Name = "overlapped", Alias = "<Overlaped>k__BackingField", Order = 6, Kind = XSFieldKind.Bool,
            Get = o => ((XSSongBeat)o).Overlapped, Set = (o, v) => ((XSSongBeat)o).Overlapped = (bool)v! },
    };

    public override IReadOnlyList<XSField> Fields => _fields;
}

internal sealed class XSSongTempo : XSObject
{
    public int Pos;
    public int Tempo = 120;
    public bool Overlapped;

    public override string ClassName => "SingingTool.Model.SingingGeneralConcept.SongTempo";

    private static readonly XSField[] _fields =
    {
        new() { Name = "pos", Alias = "_pos", Order = 0, Kind = XSFieldKind.Int32,
            Get = o => ((XSSongTempo)o).Pos, Set = (o, v) => ((XSSongTempo)o).Pos = Convert.ToInt32(v) },
        new() { Name = "tempo", Alias = "_tempo", Order = 1, Kind = XSFieldKind.Int32,
            Get = o => ((XSSongTempo)o).Tempo, Set = (o, v) => ((XSSongTempo)o).Tempo = Convert.ToInt32(v) },
        new() { Name = "overlapped", Alias = "<Overlaped>k__BackingField", Order = 6, Kind = XSFieldKind.Bool,
            Get = o => ((XSSongTempo)o).Overlapped, Set = (o, v) => ((XSSongTempo)o).Overlapped = (bool)v! },
    };

    public override IReadOnlyList<XSField> Fields => _fields;
}

internal enum XSNoteHeadTagEnum
{
    NoTag = 0,
    SilTag = 1,
    SpTag = 2,
}

internal sealed class XSNoteHeadTag : XSObject
{
    public XSNoteHeadTagEnum Value = XSNoteHeadTagEnum.NoTag;

    public XSNoteHeadTag() { }
    public XSNoteHeadTag(XSNoteHeadTagEnum value) { Value = value; }

    public override string ClassName => "SingingTool.Model.NoteHeadTag";

    private static readonly XSField[] _fields =
    {
        new() { Name = "value", Alias = "value__", Order = 0, Kind = XSFieldKind.Int32,
            Get = o => (int)((XSNoteHeadTag)o).Value, Set = (o, v) => ((XSNoteHeadTag)o).Value = (XSNoteHeadTagEnum)Convert.ToInt32(v) },
    };

    public override IReadOnlyList<XSField> Fields => _fields;
}

internal sealed class XSNotePhoneInfo : XSObject
{
    public float HeadPhoneTimeInSec;
    public float MidPartOverTailPartRatio;

    public override string ClassName => "SingingTool.Model.NotePhoneInfo";

    private static readonly XSField[] _fields =
    {
        new() { Name = "head_phone_time_in_sec", Alias = "<HeadPhoneTimeInSec>k__BackingField", Order = 0, Kind = XSFieldKind.Single,
            Get = o => ((XSNotePhoneInfo)o).HeadPhoneTimeInSec, Set = (o, v) => ((XSNotePhoneInfo)o).HeadPhoneTimeInSec = Convert.ToSingle(v) },
        new() { Name = "mid_part_over_tail_part_ratio", Alias = "<MidPartOverTailPartRatio>k__BackingField", Order = 1, Kind = XSFieldKind.Single,
            Get = o => ((XSNotePhoneInfo)o).MidPartOverTailPartRatio, Set = (o, v) => ((XSNotePhoneInfo)o).MidPartOverTailPartRatio = Convert.ToSingle(v) },
    };

    public override IReadOnlyList<XSField> Fields => _fields;
}

internal sealed class XSNote : XSObject
{
    public int StartPos;
    public int WidthPos = 480;
    public int KeyIndex = 60;
    public string Lyric = "";
    public string Pronouncing = "";
    public XSNoteHeadTag HeadTag = new();
    public bool Overlapped;
    public XSNotePhoneInfo? NotePhoneInfo;
    public int VibratoPercent;
    public XSVibratoStyle? Vibrato;
    public XSVibratoPercentInfo? VibratoPercentInfo;

    public override string ClassName => "SingingTool.Model.Note";

    private static readonly XSField[] _fields =
    {
        new() { Name = "start_pos", Alias = "_startPos", Order = 0, Kind = XSFieldKind.Int32,
            Get = o => ((XSNote)o).StartPos, Set = (o, v) => ((XSNote)o).StartPos = Convert.ToInt32(v) },
        new() { Name = "width_pos", Alias = "_widthPos", Order = 1, Kind = XSFieldKind.Int32,
            Get = o => ((XSNote)o).WidthPos, Set = (o, v) => ((XSNote)o).WidthPos = Convert.ToInt32(v) },
        new() { Name = "key_index", Alias = "_keyIndex", Order = 2, Kind = XSFieldKind.Int32,
            Get = o => ((XSNote)o).KeyIndex, Set = (o, v) => ((XSNote)o).KeyIndex = Convert.ToInt32(v) },
        new() { Name = "lyric", Alias = "_lyric", Order = 3, Kind = XSFieldKind.String,
            Get = o => ((XSNote)o).Lyric, Set = (o, v) => ((XSNote)o).Lyric = (string)(v ?? "") },
        new() { Name = "pronouncing", Alias = "_pronouncing", Order = 4, Kind = XSFieldKind.String,
            Get = o => ((XSNote)o).Pronouncing, Set = (o, v) => ((XSNote)o).Pronouncing = (string)(v ?? "") },
        new() { Name = "head_tag", Alias = "_headTag", Order = 5, Kind = XSFieldKind.InlineWrapper,
            Get = o => ((XSNote)o).HeadTag, Set = (o, v) => ((XSNote)o).HeadTag = (XSNoteHeadTag)v! },
        new() { Name = "overlapped", Alias = "<Overlaped>k__BackingField", Order = 6, Kind = XSFieldKind.Bool,
            Get = o => ((XSNote)o).Overlapped, Set = (o, v) => ((XSNote)o).Overlapped = (bool)v! },
        new() { Name = "note_phone_info", Alias = "<NotePhoneInfo>k__BackingField", Order = 7, Kind = XSFieldKind.Dataclass,
            ElementClassName = "SingingTool.Model.NotePhoneInfo",
            Get = o => ((XSNote)o).NotePhoneInfo, Set = (o, v) => ((XSNote)o).NotePhoneInfo = (XSNotePhoneInfo?)v },
        new() { Name = "vibrato_percent", Alias = "<VibratoPercent>k__BackingField", Order = 8, Kind = XSFieldKind.Int32,
            Get = o => ((XSNote)o).VibratoPercent, Set = (o, v) => ((XSNote)o).VibratoPercent = Convert.ToInt32(v) },
        new() { Name = "vibrato", Alias = "<Vibrato>k__BackingField", Order = 9, Kind = XSFieldKind.Dataclass,
            ElementClassName = "SingingTool.Model.VibratoStyle",
            Get = o => ((XSNote)o).Vibrato, Set = (o, v) => ((XSNote)o).Vibrato = (XSVibratoStyle?)v },
        new() { Name = "vibrato_percent_info", Alias = "<VibratoPercentInfo>k__BackingField", Order = 10, Kind = XSFieldKind.Dataclass,
            ElementClassName = "SingingTool.Model.VibratoPercentInfo",
            Get = o => ((XSNote)o).VibratoPercentInfo, Set = (o, v) => ((XSNote)o).VibratoPercentInfo = (XSVibratoPercentInfo?)v },
    };

    public override IReadOnlyList<XSField> Fields => _fields;
}

internal sealed class XSBuf : XSObject
{
    public string ElementClass;
    public readonly List<XSObject?> Items = new();
    public int Size;
    public int Version;

    public override string ClassName => "System.Collections.Generic.List";

    private readonly XSField[] _fields;

    public XSBuf(string elementClass)
    {
        ElementClass = elementClass;
        _fields = new[]
        {
            new XSField { Name = "items", Alias = "_items", Order = 0, Kind = XSFieldKind.List, ElementClassName = elementClass, UpdatesSubcon = true,
                Get = o => ((XSBuf)o).Items, Set = (o, v) => SetItems((XSBuf)o, v) },
            new XSField { Name = "size", Alias = "_size", Order = 1, Kind = XSFieldKind.Int32,
                Get = o => ((XSBuf)o).Size, Set = (o, v) => ((XSBuf)o).Size = Convert.ToInt32(v) },
            new XSField { Name = "version", Alias = "_version", Order = 2, Kind = XSFieldKind.Int32,
                Get = o => ((XSBuf)o).Version, Set = (o, v) => ((XSBuf)o).Version = Convert.ToInt32(v) },
        };
    }

    private static void SetItems(XSBuf buf, object? v)
    {
        buf.Items.Clear();
        if (v is IEnumerable<object?> items)
            buf.Items.AddRange(items.Select(x => x as XSObject));
    }

    public override IReadOnlyList<XSField> Fields => _fields;
}

internal sealed class XSBufList : XSObject
{
    public string ElementClass;
    public XSBuf Buf;
    public XSBuf Buf1;

    public XSBufList(string elementClass)
    {
        ElementClass = elementClass;
        Buf = new XSBuf(elementClass);
        Buf1 = Buf;
        _fields = new[]
        {
            new XSField { Name = "buf", Alias = "_buf", Order = 0, Kind = XSFieldKind.Dataclass, ElementClassName = elementClass, UpdatesSubcon = true,
                Get = o => ((XSBufList)o).Buf, Set = (o, v) => ((XSBufList)o).Buf = (XSBuf)v! },
            new XSField { Name = "buf_1", Alias = "SerialItemList`1+_buf", Order = 1, Kind = XSFieldKind.Dataclass, ElementClassName = elementClass, UpdatesSubcon = true,
                Get = o => ((XSBufList)o).Buf1, Set = (o, v) => ((XSBufList)o).Buf1 = (XSBuf)v! },
        };
    }

    public override string ClassName => "SingingTool.Library.SerialOverlapableItemList";

    private readonly XSField[] _fields;
    public override IReadOnlyList<XSField> Fields => _fields;
}

internal abstract class XSITrack : XSObject
{
    public float Pan;
    public string Name = "";
    public bool Mute;
    public bool Solo;
    public double Volume = 0.7;

    public override string ClassName => "SingingTool.Model.ITrack";
}

internal sealed class XSSingingTrack : XSITrack
{
    public XSBufList NoteList = new("SingingTool.Model.Note");
    public bool NeedRefreshBaseMetadataFlag;
    public XSLineParam EditedPitchLine = new();
    public XSLineParam EditedVolumeLine = new();
    public XSLineParam EditedBreathLine = new();
    public XSLineParam EditedGenderLine = new();
    public XSLineParam? EditedPowerLine;
    public XSReverbPreset ReverbPreset = new();
    public string AiSingerId = "";

    public override string ClassName => "SingingTool.Model.SingingTrack";

    private static readonly XSField[] _fields =
    {
        new() { Name = "note_list", Alias = "_noteList", Order = 0, Kind = XSFieldKind.Dataclass, ElementClassName = "SingingTool.Model.Note", UpdatesSubcon = true,
            Get = o => ((XSSingingTrack)o).NoteList, Set = (o, v) => ((XSSingingTrack)o).NoteList = (XSBufList)v! },
        new() { Name = "need_refresh_base_metadata_flag", Alias = "_needRefreshBaseMetadataFlag", Order = 1, Kind = XSFieldKind.Bool,
            Get = o => ((XSSingingTrack)o).NeedRefreshBaseMetadataFlag, Set = (o, v) => ((XSSingingTrack)o).NeedRefreshBaseMetadataFlag = (bool)v! },
        new() { Name = "edited_pitch_line", Alias = "_editedPitchLine", Order = 2, Kind = XSFieldKind.Dataclass,
            Get = o => ((XSSingingTrack)o).EditedPitchLine, Set = (o, v) => ((XSSingingTrack)o).EditedPitchLine = (XSLineParam)v! },
        new() { Name = "edited_volume_line", Alias = "_editedVolumeLine", Order = 3, Kind = XSFieldKind.Dataclass,
            Get = o => ((XSSingingTrack)o).EditedVolumeLine, Set = (o, v) => ((XSSingingTrack)o).EditedVolumeLine = (XSLineParam)v! },
        new() { Name = "edited_breath_line", Alias = "_editedBreathLine", Order = 4, Kind = XSFieldKind.Dataclass,
            Get = o => ((XSSingingTrack)o).EditedBreathLine, Set = (o, v) => ((XSSingingTrack)o).EditedBreathLine = (XSLineParam)v! },
        new() { Name = "edited_gender_line", Alias = "_editedGenderLine", Order = 5, Kind = XSFieldKind.Dataclass,
            Get = o => ((XSSingingTrack)o).EditedGenderLine, Set = (o, v) => ((XSSingingTrack)o).EditedGenderLine = (XSLineParam)v! },
        new() { Name = "edited_power_line", Alias = "_editedPowerLine", Order = 6, Kind = XSFieldKind.Dataclass,
            Get = o => ((XSSingingTrack)o).EditedPowerLine, Set = (o, v) => ((XSSingingTrack)o).EditedPowerLine = (XSLineParam?)v },
        new() { Name = "reverb_preset", Alias = "_reverbPreset", Order = 7, Kind = XSFieldKind.InlineWrapper,
            Get = o => ((XSSingingTrack)o).ReverbPreset, Set = (o, v) => ((XSSingingTrack)o).ReverbPreset = (XSReverbPreset)v! },
        new() { Name = "volume", Alias = "_volume", Order = 8, Kind = XSFieldKind.Double,
            Get = o => ((XSSingingTrack)o).Volume, Set = (o, v) => ((XSSingingTrack)o).Volume = Convert.ToDouble(v) },
        new() { Name = "pan", Alias = "_pan", Order = 9, Kind = XSFieldKind.Double,
            Get = o => ((XSSingingTrack)o).Pan, Set = (o, v) => ((XSSingingTrack)o).Pan = Convert.ToSingle(v) },
        new() { Name = "name", Alias = "_name", Order = 10, Kind = XSFieldKind.String,
            Get = o => ((XSSingingTrack)o).Name, Set = (o, v) => ((XSSingingTrack)o).Name = (string)(v ?? "") },
        new() { Name = "mute", Alias = "_mute", Order = 11, Kind = XSFieldKind.Bool,
            Get = o => ((XSSingingTrack)o).Mute, Set = (o, v) => ((XSSingingTrack)o).Mute = (bool)v! },
        new() { Name = "solo", Alias = "_solo", Order = 12, Kind = XSFieldKind.Bool,
            Get = o => ((XSSingingTrack)o).Solo, Set = (o, v) => ((XSSingingTrack)o).Solo = (bool)v! },
        new() { Name = "ai_singer_id", Alias = "<AISingerId>k__BackingField", Order = 13, Kind = XSFieldKind.String,
            Get = o => ((XSSingingTrack)o).AiSingerId, Set = (o, v) => ((XSSingingTrack)o).AiSingerId = (string)(v ?? "") },
    };

    public override IReadOnlyList<XSField> Fields => _fields;
}

internal sealed class XSInstrumentTrack : XSITrack
{
    public float SampleRate = 48000;
    public int SampleCount;
    public int ChannelCount;
    public int OffsetInPos;
    public string InstrumentFilePath = "";

    public XSInstrumentTrack() { Volume = 0.3; }

    public override string ClassName => "SingingTool.Model.InstrumentTrack";

    private static readonly XSField[] _fields =
    {
        new() { Name = "volume", Alias = "_volume", Order = 8, Kind = XSFieldKind.Double,
            Get = o => ((XSInstrumentTrack)o).Volume, Set = (o, v) => ((XSInstrumentTrack)o).Volume = Convert.ToDouble(v) },
        new() { Name = "pan", Alias = "_pan", Order = 9, Kind = XSFieldKind.Double,
            Get = o => ((XSInstrumentTrack)o).Pan, Set = (o, v) => ((XSInstrumentTrack)o).Pan = Convert.ToSingle(v) },
        new() { Name = "name", Alias = "_name", Order = 10, Kind = XSFieldKind.String,
            Get = o => ((XSInstrumentTrack)o).Name, Set = (o, v) => ((XSInstrumentTrack)o).Name = (string)(v ?? "") },
        new() { Name = "mute", Alias = "_mute", Order = 11, Kind = XSFieldKind.Bool,
            Get = o => ((XSInstrumentTrack)o).Mute, Set = (o, v) => ((XSInstrumentTrack)o).Mute = (bool)v! },
        new() { Name = "solo", Alias = "_solo", Order = 12, Kind = XSFieldKind.Bool,
            Get = o => ((XSInstrumentTrack)o).Solo, Set = (o, v) => ((XSInstrumentTrack)o).Solo = (bool)v! },
        new() { Name = "sample_rate", Alias = "<SampleRate>k__BackingField", Order = 14, Kind = XSFieldKind.Double,
            Get = o => ((XSInstrumentTrack)o).SampleRate, Set = (o, v) => ((XSInstrumentTrack)o).SampleRate = Convert.ToSingle(v) },
        new() { Name = "sample_count", Alias = "<SampleCount>k__BackingField", Order = 15, Kind = XSFieldKind.Int32,
            Get = o => ((XSInstrumentTrack)o).SampleCount, Set = (o, v) => ((XSInstrumentTrack)o).SampleCount = Convert.ToInt32(v) },
        new() { Name = "channel_count", Alias = "<ChannelCount>k__BackingField", Order = 16, Kind = XSFieldKind.Int32,
            Get = o => ((XSInstrumentTrack)o).ChannelCount, Set = (o, v) => ((XSInstrumentTrack)o).ChannelCount = Convert.ToInt32(v) },
        new() { Name = "offset_in_pos", Alias = "<OffsetInPos>k__BackingField", Order = 17, Kind = XSFieldKind.Int32,
            Get = o => ((XSInstrumentTrack)o).OffsetInPos, Set = (o, v) => ((XSInstrumentTrack)o).OffsetInPos = Convert.ToInt32(v) },
        new() { Name = "instrument_file_path", Alias = "<InstrumentFilePath>k__BackingField", Order = 18, Kind = XSFieldKind.String,
            Get = o => ((XSInstrumentTrack)o).InstrumentFilePath, Set = (o, v) => ((XSInstrumentTrack)o).InstrumentFilePath = (string)(v ?? "") },
    };

    public override IReadOnlyList<XSField> Fields => _fields;
}

internal sealed class XSAppModel : XSObject
{
    public string ProjectFilePath = "";
    public XSBufList TempoList = new("SingingTool.Model.SingingGeneralConcept.SongTempo");
    public XSBufList BeatList = new("SingingTool.Model.SingingGeneralConcept.SongBeat");
    public XSBuf TrackList = new("SingingTool.Model.ITrack");
    public int Quantize = 8;
    public bool IsTriplet;
    public bool IsNumericalKeyName = true;
    public int FirstNumericalKeyNameAtIndex;
    public string? ActualProjectFilePath;

    public override string ClassName => "SingingTool.Model.AppModel";

    private static readonly XSField[] _fields =
    {
        new() { Name = "project_file_path", Alias = "<ProjectFilePath>k__BackingField", Order = 0, Kind = XSFieldKind.String,
            Get = o => ((XSAppModel)o).ProjectFilePath, Set = (o, v) => ((XSAppModel)o).ProjectFilePath = (string)(v ?? "") },
        new() { Name = "tempo_list", Alias = "_tempoList", Order = 1, Kind = XSFieldKind.Dataclass, ElementClassName = "SingingTool.Model.SingingGeneralConcept.SongTempo", UpdatesSubcon = true,
            Get = o => ((XSAppModel)o).TempoList, Set = (o, v) => ((XSAppModel)o).TempoList = (XSBufList)v! },
        new() { Name = "beat_list", Alias = "_beatList", Order = 2, Kind = XSFieldKind.Dataclass, ElementClassName = "SingingTool.Model.SingingGeneralConcept.SongBeat", UpdatesSubcon = true,
            Get = o => ((XSAppModel)o).BeatList, Set = (o, v) => ((XSAppModel)o).BeatList = (XSBufList)v! },
        new() { Name = "track_list", Alias = "_trackList", Order = 3, Kind = XSFieldKind.Dataclass, ElementClassName = "SingingTool.Model.ITrack", UpdatesSubcon = true,
            Get = o => ((XSAppModel)o).TrackList, Set = (o, v) => ((XSAppModel)o).TrackList = (XSBuf)v! },
        new() { Name = "quantize", Alias = "_quantize", Order = 4, Kind = XSFieldKind.Int32,
            Get = o => ((XSAppModel)o).Quantize, Set = (o, v) => ((XSAppModel)o).Quantize = Convert.ToInt32(v) },
        new() { Name = "is_triplet", Alias = "_isTriplet", Order = 5, Kind = XSFieldKind.Bool,
            Get = o => ((XSAppModel)o).IsTriplet, Set = (o, v) => ((XSAppModel)o).IsTriplet = (bool)v! },
        new() { Name = "is_numerical_key_name", Alias = "_isNumerialKeyName", Order = 6, Kind = XSFieldKind.Bool,
            Get = o => ((XSAppModel)o).IsNumericalKeyName, Set = (o, v) => ((XSAppModel)o).IsNumericalKeyName = (bool)v! },
        new() { Name = "first_numerical_key_name_at_index", Alias = "_firstNumerialKeyNameAtIndex", Order = 7, Kind = XSFieldKind.Int32,
            Get = o => ((XSAppModel)o).FirstNumericalKeyNameAtIndex, Set = (o, v) => ((XSAppModel)o).FirstNumericalKeyNameAtIndex = Convert.ToInt32(v) },
        new() { Name = "actual_project_file_path", Alias = "<ActualProjectFilePath>k__BackingField", Order = 8, Kind = XSFieldKind.String,
            Get = o => ((XSAppModel)o).ActualProjectFilePath, Set = (o, v) => ((XSAppModel)o).ActualProjectFilePath = (string?)v },
    };

    public override IReadOnlyList<XSField> Fields => _fields;
}
