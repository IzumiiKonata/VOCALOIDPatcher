using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using VOCALOIDPatcher.Formats.Exceptions;
using VOCALOIDPatcher.Formats.Model;
using VOCALOIDPatcher.Formats.Process;

namespace VOCALOIDPatcher.Formats.Io;

public static class Ppsf
{
    private const double BpmRate = 10000.0;
    private const string JsonPath = "ppsf.json";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public static Model.Project Parse(IReadOnlyList<ImportFile> files, ImportParams parms)
    {
        var file = files[0];
        var content = ReadContent(file.Content);
        var warnings = new List<ImportWarning>();
        var project = content.Ppsf.Project;

        string name = string.IsNullOrWhiteSpace(project.Name) ? file.NameWithoutExtension : project.Name!;

        var timeSignatures = ParseTimeSignatures(project.Meter);
        if (timeSignatures.Count == 0)
        {
            timeSignatures.Add(TimeSignature.Default);
            warnings.Add(new ImportWarning.TimeSignatureNotFound());
        }

        var tempos = ParseTempos(project.Tempo);
        if (tempos.Count == 0)
        {
            tempos.Add(Tempo.Default);
            warnings.Add(new ImportWarning.TempoNotFound());
        }

        var tracks = project.DvlTrack.Select((track, i) => ParseTrack(i, track, parms.DefaultLyric)).ToList();

        return new Model.Project(Format.Ppsf, files, name, tracks, timeSignatures, tempos, 0, warnings);
    }

    private static List<TimeSignature> ParseTimeSignatures(PpsfMeter meter)
    {
        var first = new TimeSignature(0, meter.Const.Nume, meter.Const.Denomi);
        if (!meter.UseSequence)
            return new List<TimeSignature> { first };

        var sequence = (meter.Sequence ?? new List<PpsfMeterSequenceEvent>())
            .Select(e => new TimeSignature(e.Measure, e.Nume, e.Denomi))
            .ToList();
        if (sequence.All(t => t.MeasurePosition != 0))
            return new List<TimeSignature> { first }.Concat(sequence).ToList();
        return sequence;
    }

    private static List<Tempo> ParseTempos(PpsfTempo tempo)
    {
        var first = new Tempo(0, tempo.Const / BpmRate);
        if (!tempo.UseSequence)
            return new List<Tempo> { first };

        var sequence = (tempo.Sequence ?? new List<PpsfTempoSequenceEvent>())
            .Select(e => new Tempo(e.Tick, e.Value / BpmRate))
            .ToList();
        if (sequence.All(t => t.TickPosition != 0L))
            return new List<Tempo> { first }.Concat(sequence).ToList();
        return sequence;
    }

    private static Track ParseTrack(int index, PpsfDvlTrack dvlTrack, string defaultLyric)
    {
        string name = dvlTrack.Name ?? $"Track {index + 1}";
        var notes = dvlTrack.Events.Where(e => e.Enabled != false).Select(e =>
        {
            string lyric = string.IsNullOrWhiteSpace(e.Lyric) ? defaultLyric : e.Lyric!;
            return new Note(0, e.NoteNumber, lyric, e.Pos, e.Pos + e.Length);
        }).ToList();
        return new Track(index, name, notes).ValidateNotes();
    }

    private static PpsfProject ReadContent(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        ZipArchive zip;
        try
        {
            zip = new ZipArchive(stream, ZipArchiveMode.Read);
        }
        catch (System.Exception)
        {
            throw new UnsupportedLegacyPpsfError();
        }

        using (zip)
        {
            var entry = zip.GetEntry(JsonPath) ?? zip.Entries.FirstOrDefault(e => e.FullName.EndsWith(JsonPath))
                ?? throw new UnsupportedLegacyPpsfError();
            using var entryStream = entry.Open();
            using var reader = new StreamReader(entryStream);
            return JsonSerializer.Deserialize<PpsfProject>(reader.ReadToEnd(), Json)!;
        }
    }

    private sealed class PpsfProject
    {
        [JsonPropertyName("ppsf")] public PpsfRoot Ppsf { get; set; } = new();
    }

    private sealed class PpsfRoot
    {
        [JsonPropertyName("project")] public PpsfInnerProject Project { get; set; } = new();
    }

    private sealed class PpsfInnerProject
    {
        [JsonPropertyName("dvl_track")] public List<PpsfDvlTrack> DvlTrack { get; set; } = new();
        [JsonPropertyName("meter")] public PpsfMeter Meter { get; set; } = new();
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("tempo")] public PpsfTempo Tempo { get; set; } = new();
    }

    private sealed class PpsfDvlTrack
    {
        [JsonPropertyName("enabled")] public bool? Enabled { get; set; }
        [JsonPropertyName("events")] public List<PpsfEvent> Events { get; set; } = new();
        [JsonPropertyName("name")] public string? Name { get; set; }
    }

    private sealed class PpsfMeter
    {
        [JsonPropertyName("const")] public PpsfMeterConst Const { get; set; } = new();
        [JsonPropertyName("sequence")] public List<PpsfMeterSequenceEvent>? Sequence { get; set; }
        [JsonPropertyName("use_sequence")] public bool UseSequence { get; set; }
    }

    private sealed class PpsfTempo
    {
        [JsonPropertyName("const")] public int Const { get; set; }
        [JsonPropertyName("sequence")] public List<PpsfTempoSequenceEvent>? Sequence { get; set; }
        [JsonPropertyName("use_sequence")] public bool UseSequence { get; set; }
    }

    private sealed class PpsfEvent
    {
        [JsonPropertyName("enabled")] public bool? Enabled { get; set; }
        [JsonPropertyName("length")] public long Length { get; set; }
        [JsonPropertyName("lyric")] public string? Lyric { get; set; }
        [JsonPropertyName("note_number")] public int NoteNumber { get; set; }
        [JsonPropertyName("pos")] public long Pos { get; set; }
    }

    private sealed class PpsfMeterConst
    {
        [JsonPropertyName("denomi")] public int Denomi { get; set; }
        [JsonPropertyName("nume")] public int Nume { get; set; }
    }

    private sealed class PpsfMeterSequenceEvent
    {
        [JsonPropertyName("denomi")] public int Denomi { get; set; }
        [JsonPropertyName("nume")] public int Nume { get; set; }
        [JsonPropertyName("measure")] public int Measure { get; set; }
    }

    private sealed class PpsfTempoSequenceEvent
    {
        [JsonPropertyName("tick")] public int Tick { get; set; }
        [JsonPropertyName("value")] public int Value { get; set; }
    }
}
