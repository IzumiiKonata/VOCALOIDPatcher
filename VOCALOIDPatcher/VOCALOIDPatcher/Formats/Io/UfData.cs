using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using VOCALOIDPatcher.Formats.Model;
using VOCALOIDPatcher.Formats.Process;
using VOCALOIDPatcher.Formats.Util;

namespace VOCALOIDPatcher.Formats.Io;

public static class UfData
{
    public const int DataVersion = 1;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        IncludeFields = false,
    };

    public static Project Parse(IReadOnlyList<ImportFile> files, ImportParams parms)
    {
        var file = files[0];
        var text = Texts.ReadText(file.Content);
        var document = JsonSerializer.Deserialize<UfDocument>(text, Json) ?? new UfDocument();
        return ParseDocument(document, files, parms);
    }

    public static Project ParseDocument(UfDocument document, IReadOnlyList<ImportFile> inputFiles, ImportParams parms)
    {
        var warnings = new List<ImportWarning>();
        if (document.FormatVersion > DataVersion)
            warnings.Add(new ImportWarning.IncompatibleFormatSerializationVersion(
                DataVersion.ToString(), document.FormatVersion.ToString()));

        var project = document.Project ?? new UfProject();
        return new Project(
            Format.UfData,
            inputFiles,
            project.Name,
            project.Tracks.Select((track, index) => ParseTrack(index, track, parms)).ToList(),
            project.TimeSignatures.Select(ParseTimeSignature).ToList(),
            project.Tempos.Select(ParseTempo).ToList(),
            project.MeasurePrefix,
            warnings);
    }

    private static Track ParseTrack(int index, UfTrack track, ImportParams parms)
    {
        var notes = track.Notes
            .Select((note, noteIndex) => new Note(noteIndex, note.Key, note.Lyric, note.TickOn, note.TickOff, note.Phoneme))
            .ToList();

        Pitch? pitch = null;
        if (!parms.SimpleImport && track.Pitch is { } p && p.Ticks.Count > 0)
        {
            var data = new List<(long, double?)>(p.Ticks.Count);
            for (int i = 0; i < p.Ticks.Count; i++)
                data.Add((p.Ticks[i], i < p.Values.Count ? p.Values[i] : null));
            pitch = new Pitch(data, p.IsAbsolute);
        }

        return new Track(index, track.Name, notes, pitch).ValidateNotes();
    }

    private static TimeSignature ParseTimeSignature(UfTimeSignature ts) =>
        new(ts.MeasurePosition, ts.Numerator, ts.Denominator);

    private static Tempo ParseTempo(UfTempo tempo) => new(tempo.TickPosition, tempo.Bpm);

    public static ExportResult Generate(Project project, IReadOnlyList<FeatureConfig> features)
    {
        var document = GenerateDocument(project, features);
        var text = JsonSerializer.Serialize(document, Json);
        var notifications = new List<ExportNotification>();
        if (features.Contains(Feature.ConvertPitch))
            notifications.Add(ExportNotification.PitchDataExported);

        return new ExportResult(Encoding.UTF8.GetBytes(text), FormatRegistry.Get(Format.UfData).GetFileName(project.Name), notifications);
    }

    public static UfDocument GenerateDocument(Project project, IReadOnlyList<FeatureConfig> features) => new()
    {
        FormatVersion = DataVersion,
        Project = new UfProject
        {
            Name = project.Name,
            Tracks = project.Tracks.Select(t => GenerateTrack(t, features)).ToList(),
            TimeSignatures = project.TimeSignatures.Select(GenerateTimeSignature).ToList(),
            Tempos = project.Tempos.Select(GenerateTempo).ToList(),
            MeasurePrefix = project.MeasurePrefix,
        },
    };

    private static UfTrack GenerateTrack(Track track, IReadOnlyList<FeatureConfig> features)
    {
        var notes = track.Notes
            .Select(n => new UfNote { Key = n.Key, Lyric = n.Lyric, TickOn = n.TickOn, TickOff = n.TickOff, Phoneme = n.Phoneme })
            .ToList();

        var pitch = new UfPitch();
        if (features.Contains(Feature.ConvertPitch) && track.Pitch is { } source)
        {
            pitch.Ticks = source.Data.Select(p => p.Tick).ToList();
            pitch.Values = source.Data.Select(p => p.Value).ToList();
            pitch.IsAbsolute = source.IsAbsolute;
        }

        return new UfTrack { Name = track.Name, Notes = notes, Pitch = pitch };
    }

    private static UfTimeSignature GenerateTimeSignature(TimeSignature ts) => new()
    {
        MeasurePosition = ts.MeasurePosition,
        Numerator = ts.Numerator,
        Denominator = ts.Denominator,
    };

    private static UfTempo GenerateTempo(Tempo tempo) => new() { TickPosition = tempo.TickPosition, Bpm = tempo.Bpm };
}

public sealed class UfDocument
{
    public int FormatVersion { get; set; }
    public UfProject? Project { get; set; }
}

public sealed class UfProject
{
    public string Name { get; set; } = "";
    public List<UfTrack> Tracks { get; set; } = new();
    public List<UfTimeSignature> TimeSignatures { get; set; } = new();
    public List<UfTempo> Tempos { get; set; } = new();
    public int MeasurePrefix { get; set; }
}

public sealed class UfTrack
{
    public string Name { get; set; } = "";
    public List<UfNote> Notes { get; set; } = new();
    public UfPitch? Pitch { get; set; }
}

public sealed class UfNote
{
    public int Key { get; set; }
    public string Lyric { get; set; } = "";
    public long TickOn { get; set; }
    public long TickOff { get; set; }
    public string? Phoneme { get; set; }
}

public sealed class UfPitch
{
    public List<long> Ticks { get; set; } = new();
    public List<double?> Values { get; set; } = new();
    public bool IsAbsolute { get; set; }
}

public sealed class UfTempo
{
    public long TickPosition { get; set; }
    public double Bpm { get; set; }
}

public sealed class UfTimeSignature
{
    public int MeasurePosition { get; set; }
    public int Numerator { get; set; }
    public int Denominator { get; set; }
}
