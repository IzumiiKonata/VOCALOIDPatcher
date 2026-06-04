using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using VOCALOIDPatcher.Formats.LibreSvip.Core;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.VvProj;

public sealed class VoiceVoxSinger
{
    [JsonPropertyName("engineId")] public string EngineId { get; set; } = "074fc39e-678b-4c13-8916-ffca8d505d1d";
    [JsonPropertyName("styleId")] public int StyleId { get; set; } = 3002;
}

public sealed class VoiceVoxTalk
{
    [JsonPropertyName("audioKeys")] public List<string> AudioKeys { get; set; } = new();
    [JsonPropertyName("audioItems")] public Dictionary<string, JsonElement> AudioItems { get; set; } = new();
}

public sealed class VoiceVoxTempo
{
    [JsonPropertyName("position")] public int Position { get; set; }
    [JsonPropertyName("bpm")] public int Bpm { get; set; }
}

public sealed class VoiceVoxTimeSignature
{
    [JsonPropertyName("measureNumber")] public int MeasureNumber { get; set; }
    [JsonPropertyName("beats")] public int Beats { get; set; }
    [JsonPropertyName("beatType")] public int BeatType { get; set; }
}

public sealed class VoiceVoxNote
{
    [JsonPropertyName("id")] public string Id { get; set; } = Guid.NewGuid().ToString();
    [JsonPropertyName("position")] public int Position { get; set; }
    [JsonPropertyName("duration")] public int Duration { get; set; }
    [JsonPropertyName("noteNumber")] public int NoteNumber { get; set; }
    [JsonPropertyName("lyric")] public string Lyric { get; set; } = "";
}

public sealed class VoiceVoxTrack
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("singer")] public VoiceVoxSinger Singer { get; set; } = new();
    [JsonPropertyName("keyRangeAdjustment")] public int KeyRangeAdjustment { get; set; }
    [JsonPropertyName("volumeRangeAdjustment")] public int VolumeRangeAdjustment { get; set; }
    [JsonPropertyName("notes")] public List<VoiceVoxNote> Notes { get; set; } = new();
    [JsonPropertyName("pitchEditData")] public List<double> PitchEditData { get; set; } = new();
    [JsonPropertyName("solo")] public bool Solo { get; set; }
    [JsonPropertyName("mute")] public bool Mute { get; set; }
    [JsonPropertyName("gain")] public double Gain { get; set; } = 1.0;
    [JsonPropertyName("pan")] public double Pan { get; set; }
}

public sealed class VoiceVoxSong
{
    [JsonPropertyName("tpqn")] public int Tpqn { get; set; } = Constants.TicksInBeat;
    [JsonPropertyName("tempos")] public List<VoiceVoxTempo> Tempos { get; set; } = new();
    [JsonPropertyName("timeSignatures")] public List<VoiceVoxTimeSignature> TimeSignatures { get; set; } = new();
    [JsonPropertyName("tracks")] public Dictionary<string, VoiceVoxTrack> Tracks { get; set; } = new();
    [JsonPropertyName("trackOrder")] public List<string> TrackOrder { get; set; } = new();
}

public sealed class VoiceVoxProject
{
    [JsonPropertyName("appVersion")] public string AppVersion { get; set; } = "0.21.1";
    [JsonPropertyName("talk")] public VoiceVoxTalk Talk { get; set; } = new();
    [JsonPropertyName("song")] public VoiceVoxSong Song { get; set; } = new();
}
