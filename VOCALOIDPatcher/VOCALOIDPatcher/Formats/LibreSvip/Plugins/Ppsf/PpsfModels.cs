using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ppsf;

public enum PpsfCurveType
{
    Border = 0,
    Normal = 1,
}

public enum PpsfLanguage
{
    Japanese = 0,
    English = 1,
    SimplifiedChinese = 4,
}

public sealed class PpsfCurvePointSeq
{
    [JsonPropertyName("border-type")] public int? BorderType { get; set; }
    [JsonPropertyName("note-index")] public int? NoteIndex { get; set; }
    [JsonPropertyName("region-index")] public int RegionIndex { get; set; }
    [JsonPropertyName("edited-by-user")] public bool? EditedByUser { get; set; } = false;
    [JsonPropertyName("seg-array-id")] public int? SegArrayId { get; set; }
    [JsonPropertyName("abs-value")] public int? AbsValue { get; set; }
}

public sealed class PpsfCurvePoint
{
    [JsonPropertyName("plugin-descriptor")] public string? PluginDescriptor { get; set; }
    [JsonPropertyName("sequence")] public List<PpsfCurvePointSeq> Sequence { get; set; } = new();
    [JsonPropertyName("sub-track-category")] public int SubTrackCategory { get; set; }
    [JsonPropertyName("sub-track-id")] public int SubTrackId { get; set; }
}

public sealed class PpsfSyllable
{
    [JsonPropertyName("footer-text")] public string FooterText { get; set; } = "";
    [JsonPropertyName("header-text")] public string HeaderText { get; set; } = "";
    [JsonPropertyName("is-list-end")] public bool IsListEnd { get; set; } = true;
    [JsonPropertyName("is-list-top")] public bool IsListTop { get; set; } = true;
    [JsonPropertyName("is-word-end")] public bool IsWordEnd { get; set; } = true;
    [JsonPropertyName("is-word-top")] public bool IsWordTop { get; set; } = true;
    [JsonPropertyName("lyric-text")] public string LyricText { get; set; } = "";
    [JsonPropertyName("symbols-text")] public string SymbolsText { get; set; } = "";
}

public sealed class PpsfNote
{
    [JsonPropertyName("language")] public PpsfLanguage Language { get; set; } = PpsfLanguage.Japanese;
    [JsonPropertyName("region-index")] public int RegionIndex { get; set; }
    [JsonPropertyName("syllables")] public List<PpsfSyllable> Syllables { get; set; } = new();
    [JsonPropertyName("event_index")] public int? EventIndex { get; set; }
    [JsonPropertyName("length")] public int? Length { get; set; }
    [JsonPropertyName("muted")] public bool? Muted { get; set; } = false;
    [JsonPropertyName("vibrato_preset_id")] public int? VibratoPresetId { get; set; } = 0;
    [JsonPropertyName("voice_color_id")] public int? VoiceColorId { get; set; } = 0;
    [JsonPropertyName("voice_release_id")] public int? VoiceReleaseId { get; set; } = 0;
    [JsonPropertyName("note_env_preset_id")] public int? NoteEnvPresetId { get; set; } = 2;
    [JsonPropertyName("note_gain_value")] public int? NoteGainValue { get; set; } = 0;
    [JsonPropertyName("note_param_edited_stats")] public int? NoteParamEditedStats { get; set; } = 0;
    [JsonPropertyName("portamento_length")] public int? PortamentoLength { get; set; } = 180;
    [JsonPropertyName("portamento_offset")] public int? PortamentoOffset { get; set; } = -120;
    [JsonPropertyName("vibrato_depth")] public int? VibratoDepth { get; set; } = 0;
    [JsonPropertyName("vibrato_rate")] public int? VibratoRate { get; set; } = 0;
    [JsonPropertyName("auto_ai_consonant_length_enabled")] public bool? AutoAiConsonantLengthEnabled { get; set; } = false;
    [JsonPropertyName("auto_ai_f0_enabled")] public bool? AutoAiF0Enabled { get; set; } = true;
    [JsonPropertyName("auto_ai_vibrato_depth")] public int? AutoAiVibratoDepth { get; set; } = 0;
    [JsonPropertyName("auto_ai_vibrato_enabled")] public bool? AutoAiVibratoEnabled { get; set; } = false;
    [JsonPropertyName("auto_ai_vibrato_offset_by_user")] public bool? AutoAiVibratoOffsetByUser { get; set; } = false;
}

public sealed class PpsfRegion
{
    [JsonPropertyName("auto-expand-left")] public bool? AutoExpandLeft { get; set; }
    [JsonPropertyName("auto-expand-right")] public bool? AutoExpandRight { get; set; }
    [JsonPropertyName("length")] public int Length { get; set; }
    [JsonPropertyName("muted")] public bool Muted { get; set; } = false;
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("position")] public int Position { get; set; }
    [JsonPropertyName("z-order")] public int ZOrder { get; set; } = 0;
    [JsonPropertyName("audio-event-index")] public int? AudioEventIndex { get; set; }
}

public sealed class PpsfSubTrack
{
    [JsonPropertyName("height")] public int Height { get; set; }
    [JsonPropertyName("plugin-descriptor")] public string? PluginDescriptor { get; set; }
    [JsonPropertyName("sub-track-category")] public int SubTrackCategory { get; set; }
    [JsonPropertyName("sub-track-id")] public int SubTrackId { get; set; }
}

public sealed class PpsfParamPoint
{
    [JsonPropertyName("curve_type")] public PpsfCurveType CurveType { get; set; } = PpsfCurveType.Normal;
    [JsonPropertyName("pos")] public int Pos { get; set; }
    [JsonPropertyName("value")] public int Value { get; set; }
    [JsonPropertyName("region-index")] public int? RegionIndex { get; set; }
    [JsonPropertyName("border-type")] public int? BorderType { get; set; }
    [JsonPropertyName("note-index")] public int? NoteIndex { get; set; }
    [JsonPropertyName("edited-by-user")] public bool? EditedByUser { get; set; }
    [JsonPropertyName("seg-array-id")] public int? SegArrayId { get; set; }
}

public sealed class PpsfBaseSequence
{
    [JsonPropertyName("constant")] public int Constant { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("sequence")] public List<PpsfParamPoint> Sequence { get; set; } = new();
    [JsonPropertyName("use_sequence")] public bool UseSequence { get; set; } = false;
}

public sealed class PpsfSeqParam
{
    [JsonPropertyName("base-sequence")] public PpsfBaseSequence? BaseSequence { get; set; }
    [JsonPropertyName("layers")] public List<object>? Layers { get; set; } = new();
}

public sealed class PpsfParameter
{
    [JsonPropertyName("constant")] public int Constant { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("sequence")] public List<PpsfParamPoint> Sequence { get; set; } = new();
    [JsonPropertyName("use_sequence")] public bool UseSequence { get; set; } = false;
    [JsonPropertyName("default")] public int? Default { get; set; }
    [JsonPropertyName("max")] public int? MaxValue { get; set; }
    [JsonPropertyName("min")] public int? MinValue { get; set; }
}

public sealed class PpsfFsmEffect
{
    [JsonPropertyName("parameters")] public List<PpsfParameter> Parameters { get; set; } = new();
    [JsonPropertyName("plugin-id")] public int PluginId { get; set; }
    [JsonPropertyName("plugin-name")] public string PluginName { get; set; } = "";
    [JsonPropertyName("power-state")] public bool PowerState { get; set; }
    [JsonPropertyName("program-name")] public string ProgramName { get; set; } = "";
    [JsonPropertyName("program-number")] public int ProgramNumber { get; set; }
    [JsonPropertyName("version")] public string Version { get; set; } = "";
}

public sealed class PpsfEventTrack
{
    [JsonPropertyName("curve-points")] public List<PpsfCurvePoint> CurvePoints { get; set; } = new();
    [JsonPropertyName("fsm-effects")] public List<PpsfFsmEffect>? FsmEffects { get; set; } = new();
    [JsonPropertyName("height")] public int Height { get; set; } = 64;
    [JsonPropertyName("index")] public int Index { get; set; }
    [JsonPropertyName("mute-solo")] public int MuteSolo { get; set; } = 0;
    [JsonPropertyName("notes")] public List<PpsfNote> Notes { get; set; } = new();
    [JsonPropertyName("nt-envelope-preset-id")] public int? NtEnvelopePresetId { get; set; }
    [JsonPropertyName("regions")] public List<PpsfRegion> Regions { get; set; } = new();
    [JsonPropertyName("sub-tracks")] public List<PpsfSubTrack> SubTracks { get; set; } = new();
    [JsonPropertyName("total-height")] public int TotalHeight { get; set; } = 64;
    [JsonPropertyName("track-type")] public int TrackType { get; set; }
    [JsonPropertyName("vertical-scale")] public double VerticalScale { get; set; } = 1;
    [JsonPropertyName("vertical-scroll")] public int VerticalScroll { get; set; } = 0;
}

public sealed class PpsfTempoTrack
{
    [JsonPropertyName("height")] public int Height { get; set; } = 0;
    [JsonPropertyName("vertical-scale")] public double VerticalScale { get; set; } = 2;
    [JsonPropertyName("vertical-scroll")] public int VerticalScroll { get; set; } = 0;
}

public sealed class PpsfTrackEditor
{
    [JsonPropertyName("event-tracks")] public List<PpsfEventTrack> EventTracks { get; set; } = new();
    [JsonPropertyName("header-width")] public int HeaderWidth { get; set; } = 264;
    [JsonPropertyName("height")] public int Height { get; set; } = 720;
    [JsonPropertyName("horizontal-scale")] public double HorizontalScale { get; set; } = 0.08;
    [JsonPropertyName("horizontal-scroll")] public int HorizontalScroll { get; set; } = 0;
    [JsonPropertyName("tempo-track")] public PpsfTempoTrack TempoTrack { get; set; } = new();
    [JsonPropertyName("user-markers")] public List<object>? UserMarkers { get; set; } = new();
    [JsonPropertyName("width")] public int Width { get; set; } = 1024;
    [JsonPropertyName("x")] public int X { get; set; } = 100;
    [JsonPropertyName("y")] public int Y { get; set; } = 100;
}

public sealed class PpsfLoopPoint
{
    [JsonPropertyName("begin")] public int Begin { get; set; } = 0;
    [JsonPropertyName("enable")] public bool? Enabled { get; set; } = false;
    [JsonPropertyName("end")] public int End { get; set; } = 7680;
}

public sealed class PpsfMetronome
{
    [JsonPropertyName("enable")] public bool? Enabled { get; set; } = false;
    [JsonPropertyName("wav")] public string Wav { get; set; } = "null";
}

public sealed class PpsfGuiSettings
{
    [JsonPropertyName("loop_point")] public PpsfLoopPoint? LoopPoint { get; set; } = new();
    [JsonPropertyName("metronome")] public PpsfMetronome? Metronome { get; set; } = new();
    [JsonPropertyName("ambient-enabled")] public bool? AmbientEnabled { get; set; }
    [JsonPropertyName("file-fullpath")] public string FileFullpath { get; set; } = "";
    [JsonPropertyName("playback-position")] public int PlaybackPosition { get; set; } = 0;
    [JsonPropertyName("project-length")] public int ProjectLength { get; set; } = 0;
    [JsonPropertyName("track-editor")] public PpsfTrackEditor TrackEditor { get; set; } = new();
    [JsonPropertyName("auto-connect-interval-msec")] public int? AutoConnectIntervalMsec { get; set; }
    [JsonPropertyName("is-saved-by-nt2")] public bool? IsSavedByNt2 { get; set; }
}

public sealed class PpsfFileAudioData
{
    [JsonPropertyName("file_path")] public string FilePath { get; set; } = "";
    [JsonPropertyName("tempo")] public int Tempo { get; set; } = 0;
}

public sealed class PpsfAudioTrackEvent
{
    [JsonPropertyName("file_audio_data")] public PpsfFileAudioData FileAudioData { get; set; } = new();
    [JsonPropertyName("playback_offset_sample")] public int PlaybackOffsetSample { get; set; } = 0;
    [JsonPropertyName("tick_length")] public int TickLength { get; set; } = 0;
    [JsonPropertyName("tick_pos")] public int TickPos { get; set; }
    [JsonPropertyName("enable")] public bool? Enabled { get; set; } = true;
}

public sealed class PpsfMixerSeq
{
    [JsonPropertyName("constant")] public int Constant { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("sequence")] public List<PpsfParamPoint> Sequence { get; set; } = new();
    [JsonPropertyName("use_sequence")] public bool UseSequence { get; set; } = false;
}

public sealed class PpsfMixerParam
{
    [JsonPropertyName("base-sequence")] public PpsfMixerSeq? BaseSequence { get; set; }
    [JsonPropertyName("layers")] public List<object>? Layers { get; set; } = new();
}

public sealed class PpsfMixer
{
    [JsonPropertyName("gain")] public PpsfMixerParam Gain { get; set; } = new() { BaseSequence = new PpsfMixerSeq { Constant = 750000, Name = "Gain" } };
    [JsonPropertyName("mixer_type")] public string MixerType { get; set; } = "stereo";
    [JsonPropertyName("panpot")] public PpsfMixerParam Panpot { get; set; } = new() { BaseSequence = new PpsfMixerSeq { Constant = 0, Name = "Panpot" } };
}

public sealed class PpsfAudioTrackItem
{
    [JsonPropertyName("block_size")] public int BlockSize { get; set; } = 279;
    [JsonPropertyName("enable")] public bool? Enabled { get; set; } = true;
    [JsonPropertyName("events")] public List<PpsfAudioTrackEvent> Events { get; set; } = new();
    [JsonPropertyName("input_channel")] public int InputChannel { get; set; } = 0;
    [JsonPropertyName("mixer")] public PpsfMixer Mixer { get; set; } = new();
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("output_channel")] public int OutputChannel { get; set; } = 2;
    [JsonPropertyName("sampling_rate")] public int SamplingRate { get; set; } = 48000;
}

public sealed class PpsfBaseMeter
{
    [JsonPropertyName("denomi")] public int Denomi { get; set; } = 4;
    [JsonPropertyName("nume")] public int Nume { get; set; } = 4;
}

public sealed class PpsfMeter
{
    [JsonPropertyName("denomi")] public int Denomi { get; set; } = 4;
    [JsonPropertyName("nume")] public int Nume { get; set; } = 4;
    [JsonPropertyName("measure")] public int Measure { get; set; }
}

public sealed class PpsfMeters
{
    [JsonPropertyName("const")] public PpsfBaseMeter Const { get; set; } = new();
    [JsonPropertyName("sequence")] public List<PpsfMeter> Sequence { get; set; } = new();
    [JsonPropertyName("use_sequence")] public bool UseSequence { get; set; } = false;
}

public sealed class PpsfSingerParam
{
    [JsonPropertyName("breathiness")] public double Breathiness { get; set; } = 0;
    [JsonPropertyName("brightness")] public double Brightness { get; set; } = 0;
    [JsonPropertyName("clearness")] public double Clearness { get; set; } = 0;
    [JsonPropertyName("gender_factor")] public double GenderFactor { get; set; } = 0;
    [JsonPropertyName("opening")] public double Opening { get; set; } = 0;
}

public sealed class PpsfSinger
{
    [JsonPropertyName("character_name")] public string CharacterName { get; set; } = "miku";
    [JsonPropertyName("do_extrapolation")] public bool DoExtrapolation { get; set; } = false;
    [JsonPropertyName("frame_shift")] public double FrameShift { get; set; } = 0.001;
    [JsonPropertyName("gender")] public int Gender { get; set; } = 0;
    [JsonPropertyName("language_id")] public PpsfLanguage LanguageId { get; set; } = PpsfLanguage.Japanese;
    [JsonPropertyName("library_id")] public string LibraryId { get; set; } = System.Guid.NewGuid().ToString("D");
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("singer_name")] public string SingerName { get; set; } = "HATSUNE MIKU NT Original";
    [JsonPropertyName("stationaly_type")] public string StationalyType { get; set; } = "ParamedNDR";
    [JsonPropertyName("synthesis_version")] public string SynthesisVersion { get; set; } = "V20200915_H";
    [JsonPropertyName("tail_silent")] public double TailSilent { get; set; } = 0.05;
    [JsonPropertyName("vprm_morph_mode")] public string VprmMorphMode { get; set; } = "Linear";
}

public sealed class PpsfEnvelope
{
    [JsonPropertyName("length")] public int Length { get; set; } = 0;
    [JsonPropertyName("offset")] public int Offset { get; set; } = 0;
    [JsonPropertyName("points")] public List<PpsfParamPoint> Points { get; set; } = new();
    [JsonPropertyName("use_length")] public bool UseLength { get; set; } = true;
}

public sealed class PpsfDvlTrackEvent
{
    [JsonPropertyName("adjust_speed")] public bool? AdjustSpeed { get; set; } = false;
    [JsonPropertyName("attack_speed_rate")] public int AttackSpeedRate { get; set; } = 800000;
    [JsonPropertyName("consonant_rate")] public int ConsonantRate { get; set; } = 950000;
    [JsonPropertyName("consonant_speed_rate")] public int ConsonantSpeedRate { get; set; } = 1000000;
    [JsonPropertyName("enable")] public bool? Enabled { get; set; } = true;
    [JsonPropertyName("length")] public int Length { get; set; }
    [JsonPropertyName("lyric")] public string Lyric { get; set; } = "";
    [JsonPropertyName("note_number")] public int NoteNumber { get; set; }
    [JsonPropertyName("note_off_pit_envelope")] public PpsfEnvelope? NoteOffPitEnvelope { get; set; } = new();
    [JsonPropertyName("note_on_pit_envelope")] public PpsfEnvelope? NoteOnPitEnvelope { get; set; } = new();
    [JsonPropertyName("portamento_envelope")] public PpsfEnvelope? PortamentoEnvelope { get; set; } = new() { Length = 0, Offset = 0 };
    [JsonPropertyName("vib_depth")] public PpsfEnvelope? VibDepth { get; set; }
    [JsonPropertyName("vib_rate")] public PpsfEnvelope? VibRate { get; set; }
    [JsonPropertyName("vib_setting_id")] public int? VibSettingId { get; set; }
    [JsonPropertyName("portamento_type")] public int PortamentoType { get; set; } = 5;
    [JsonPropertyName("pos")] public int Pos { get; set; }
    [JsonPropertyName("protected")] public bool Protected { get; set; } = false;
    [JsonPropertyName("release_speed_rate")] public int ReleaseSpeedRate { get; set; } = 1400000;
    [JsonPropertyName("symbols")] public string Symbols { get; set; } = "";
    [JsonPropertyName("vcl_like_note_off")] public bool? VclLikeNoteOff { get; set; } = false;
    [JsonPropertyName("is_consonant_length_by_user")] public bool? IsConsonantLengthByUser { get; set; } = false;
    [JsonPropertyName("portamento_length")] public int PortamentoLength { get; set; } = 180;
    [JsonPropertyName("portamento_offset")] public int PortamentoOffset { get; set; } = -120;

    public int EndPos => Pos + Length;
}

public sealed class PpsfDvlTrackItem
{
    [JsonPropertyName("enable")] public bool? Enabled { get; set; } = true;
    [JsonPropertyName("events")] public List<PpsfDvlTrackEvent> Events { get; set; } = new();
    [JsonPropertyName("mixer")] public PpsfMixer Mixer { get; set; } = new();
    [JsonPropertyName("name")] public string Name { get; set; } = "HATSUNE MIKU NT Original";
    [JsonPropertyName("parameters")] public List<PpsfSeqParam> Parameters { get; set; } = new();
    [JsonPropertyName("plugin_output_bus_index")] public int PluginOutputBusIndex { get; set; } = 0;
    [JsonPropertyName("singer")] public PpsfSinger Singer { get; set; } = new();
}

public sealed class PpsfTempo
{
    [JsonPropertyName("curve_type")] public PpsfCurveType CurveType { get; set; } = PpsfCurveType.Normal;
    [JsonPropertyName("tick")] public int Tick { get; set; }
    [JsonPropertyName("value")] public int Value { get; set; }
}

public sealed class PpsfTempos
{
    [JsonPropertyName("const")] public int Const { get; set; } = 1200000;
    [JsonPropertyName("sequence")] public List<PpsfTempo> Sequence { get; set; } = new();
    [JsonPropertyName("use_sequence")] public bool UseSequence { get; set; } = false;
}

public sealed class PpsfInnerProject
{
    [JsonPropertyName("audio_track")] public List<PpsfAudioTrackItem> AudioTrack { get; set; } = new();
    [JsonPropertyName("block_size")] public int BlockSize { get; set; } = 279;
    [JsonPropertyName("loop_point")] public PpsfLoopPoint? LoopPoint { get; set; }
    [JsonPropertyName("meter")] public PpsfMeters Meter { get; set; } = new();
    [JsonPropertyName("metronome")] public PpsfMetronome? Metronome { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("sampling_rate")] public int SamplingRate { get; set; } = 48000;
    [JsonPropertyName("tempo")] public PpsfTempos Tempo { get; set; } = new();
    [JsonPropertyName("dvl_track")] public List<PpsfDvlTrackItem>? DvlTrack { get; set; }
}

public sealed class PpsfRoot
{
    [JsonPropertyName("app_ver")] public string AppVer { get; set; } = "3.0.0.0";
    [JsonPropertyName("gui_settings")] public PpsfGuiSettings GuiSettings { get; set; } = new();
    [JsonPropertyName("ppsf_ver")] public string PpsfVer { get; set; } = "3.2";
    [JsonPropertyName("project")] public PpsfInnerProject Project { get; set; } = new();
}

public sealed class PpsfProject
{
    [JsonPropertyName("ppsf")] public PpsfRoot Ppsf { get; set; } = new();
}
