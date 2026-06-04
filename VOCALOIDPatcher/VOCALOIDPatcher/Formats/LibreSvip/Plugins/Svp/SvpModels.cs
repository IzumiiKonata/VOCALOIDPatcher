using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Svp;

public sealed class SVMeter
{
    [JsonPropertyName("index")] public int Index { get; set; }
    [JsonPropertyName("numerator")] public int Numerator { get; set; } = 4;
    [JsonPropertyName("denominator")] public int Denominator { get; set; } = 4;
}

public sealed class SVTempo
{
    [JsonPropertyName("bpm")] public double Bpm { get; set; }
    [JsonPropertyName("position")] public long Position { get; set; }
}

public sealed class SVTime
{
    [JsonPropertyName("meter")] public List<SVMeter> Meter { get; set; } = new();
    [JsonPropertyName("tempo")] public List<SVTempo> Tempo { get; set; } = new();
}

public sealed class SVNote
{
    [JsonPropertyName("onset")] public long Onset { get; set; }
    [JsonPropertyName("duration")] public long Duration { get; set; }
    [JsonPropertyName("lyrics")] public string Lyrics { get; set; } = "";
    [JsonPropertyName("phonemes")] public string Phonemes { get; set; } = "";
    [JsonPropertyName("pitch")] public int Pitch { get; set; }
}

public sealed class SVGroup
{
    [JsonPropertyName("name")] public string Name { get; set; } = "main";
    [JsonPropertyName("uuid")] public string Uuid { get; set; } = "";
    [JsonPropertyName("notes")] public List<SVNote> Notes { get; set; } = new();
}

public sealed class SVDatabase
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}

public sealed class SVAudio
{
    [JsonPropertyName("filename")] public string Filename { get; set; } = "";
    [JsonPropertyName("duration")] public double Duration { get; set; }
}

public sealed class SVRef
{
    [JsonPropertyName("audio")] public SVAudio? Audio { get; set; }
    [JsonPropertyName("blickOffset")] public long BlickOffset { get; set; }
    [JsonPropertyName("pitchOffset")] public int PitchOffset { get; set; }
    [JsonPropertyName("database")] public SVDatabase Database { get; set; } = new();
    [JsonPropertyName("groupID")] public string GroupId { get; set; } = "";
    [JsonPropertyName("isInstrumental")] public bool IsInstrumental { get; set; }
}

public sealed class SVMixer
{
    [JsonPropertyName("pan")] public double Pan { get; set; }
    [JsonPropertyName("mute")] public bool Mute { get; set; }
    [JsonPropertyName("solo")] public bool Solo { get; set; }
    [JsonPropertyName("gainDecibel")] public double GainDecibel { get; set; }
}

public sealed class SVTrack
{
    [JsonPropertyName("name")] public string Name { get; set; } = "Track 1";
    [JsonPropertyName("dispColor")] public string DispColor { get; set; } = "ff7db235";
    [JsonPropertyName("mixer")] public SVMixer Mixer { get; set; } = new();
    [JsonPropertyName("mainGroup")] public SVGroup MainGroup { get; set; } = new();
    [JsonPropertyName("mainRef")] public SVRef MainRef { get; set; } = new();
    [JsonPropertyName("groups")] public List<SVRef> Groups { get; set; } = new();
    [JsonPropertyName("renderEnabled")] public bool RenderEnabled { get; set; } = true;
}

public sealed class SVProject
{
    [JsonPropertyName("version")] public int Version { get; set; } = 100;
    [JsonPropertyName("time")] public SVTime Time { get; set; } = new();
    [JsonPropertyName("library")] public List<SVGroup> Library { get; set; } = new();
    [JsonPropertyName("tracks")] public List<SVTrack> Tracks { get; set; } = new();
    [JsonPropertyName("instantModeEnabled")] public bool? InstantModeEnabled { get; set; }
}
