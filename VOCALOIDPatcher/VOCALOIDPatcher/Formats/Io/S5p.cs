using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using VOCALOIDPatcher.Formats.Model;
using VOCALOIDPatcher.Formats.Process;
using VOCALOIDPatcher.Formats.Process.Pitch;
using VOCALOIDPatcher.Formats.Util;

namespace VOCALOIDPatcher.Formats.Io;

public static class S5p
{
    private const long TickRate = 1470000L;
    private const long DefaultInterval = 5512500L;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public static Model.Project Parse(IReadOnlyList<ImportFile> files, ImportParams parms)
    {
        var file = files[0];
        var raw = Texts.ReadText(file.Content);
        int index = raw.LastIndexOf('}');
        var text = index >= 0 ? raw[..(index + 1)] : raw;
        var project = JsonSerializer.Deserialize<S5pProject>(text, Json) ?? new S5pProject();

        var warnings = new List<ImportWarning>();
        var timeSignatures = project.Meter
            .Select(m => new TimeSignature(m.Measure, m.BeatPerMeasure, m.BeatGranularity))
            .ToList();
        if (timeSignatures.Count == 0)
        {
            timeSignatures.Add(TimeSignature.Default);
            warnings.Add(new ImportWarning.TimeSignatureNotFound());
        }

        var tempos = project.Tempo
            .Select(t => new Tempo(t.Position / TickRate, t.BeatPerMinute))
            .ToList();
        if (tempos.Count == 0)
        {
            tempos.Add(Tempo.Default);
            warnings.Add(new ImportWarning.TempoNotFound());
        }

        var tracks = project.Tracks
            .Select((track, i) => new Track(
                    i,
                    track.Name ?? $"Track {i + 1}",
                    ParseNotes(track, parms.DefaultLyric),
                    parms.SimpleImport ? null : ParsePitch(track))
                .ValidateNotes())
            .ToList();

        return new Model.Project(Format.S5P, files, file.NameWithoutExtension, tracks, timeSignatures, tempos, 0, warnings);
    }

    private static List<Note> ParseNotes(S5pTrack track, string defaultLyric) =>
        track.Notes.Where(n => n != null).Select(n =>
        {
            long tickOn = n!.Onset / TickRate;
            string lyric = string.IsNullOrWhiteSpace(n.Lyric) ? defaultLyric : n.Lyric!;
            return new Note(0, n.Pitch, lyric, tickOn, tickOn + n.Duration / TickRate);
        }).ToList();

    private static Model.Pitch? ParsePitch(S5pTrack track)
    {
        var pitchDelta = track.Parameters?.PitchDelta;
        if (pitchDelta == null)
            return new Model.Pitch(new List<(long, double?)>(), false);

        long interval = track.Parameters!.Interval ?? DefaultInterval;
        double intervalRatio = interval / (double)TickRate;
        var points = new List<(long, double?)>();
        for (int i = 0; i + 1 < pitchDelta.Count; i += 2)
        {
            long tick = (long)Math.Round(pitchDelta[i] * intervalRatio);
            points.Add((tick, pitchDelta[i + 1] / 100.0));
        }

        return points.Count > 0 ? new Model.Pitch(points, false) : null;
    }

    public static ExportResult Generate(Model.Project project, IReadOnlyList<FeatureConfig> features)
    {
        var s5p = JsonSerializer.Deserialize<S5pProject>(Resources.S5pTemplate, Json)!;
        s5p.Meter = project.TimeSignatures
            .Select(t => new S5pMeter { Measure = t.MeasurePosition, BeatPerMeasure = t.Numerator, BeatGranularity = t.Denominator })
            .ToList();
        s5p.Tempo = project.Tempos
            .Select(t => new S5pTempo { Position = t.TickPosition * TickRate, BeatPerMinute = t.Bpm })
            .ToList();

        var emptyTrack = s5p.Tracks[0];
        s5p.Tracks = project.Tracks.Select(t => GenerateTrack(t, emptyTrack, features)).ToList();

        var text = JsonSerializer.Serialize(s5p, Json);
        var notifications = new List<ExportNotification>();
        if (features.Contains(Feature.ConvertPitch))
            notifications.Add(ExportNotification.PitchDataExported);

        return new ExportResult(Encoding.UTF8.GetBytes(text), FormatRegistry.Get(Format.S5P).GetFileName(project.Name), notifications);
    }

    private static S5pTrack GenerateTrack(Track track, S5pTrack emptyTrack, IReadOnlyList<FeatureConfig> features)
    {
        var parameters = emptyTrack.Parameters != null ? Clone(emptyTrack.Parameters) : new S5pParameters();
        parameters.Interval = DefaultInterval;
        parameters.PitchDelta = GeneratePitchData(track, features, DefaultInterval);

        return new S5pTrack
        {
            Name = track.Name,
            DbName = emptyTrack.DbName,
            Color = emptyTrack.Color,
            DisplayOrder = track.Id,
            DbDefaults = emptyTrack.DbDefaults,
            Mixer = emptyTrack.Mixer,
            Notes = track.Notes.Select(n => new S5pNote
            {
                Onset = n.TickOn * TickRate,
                Duration = n.Length * TickRate,
                Lyric = n.Lyric,
                Pitch = n.Key,
            }).Cast<S5pNote?>().ToList(),
            Parameters = parameters,
        };
    }

    private static List<double> GeneratePitchData(Track track, IReadOnlyList<FeatureConfig> features, long interval)
    {
        if (!features.Contains(Feature.ConvertPitch) || track.Pitch == null)
            return new List<double>();

        var relative = track.Pitch.GetRelativeData(track.Notes);
        if (relative == null)
            return new List<double>();

        double intervalRatio = interval / (double)TickRate;
        var result = new List<double>();
        foreach (var (tick, value) in relative)
        {
            result.Add(tick / intervalRatio);
            result.Add(value * 100);
        }

        return result;
    }

    private static S5pParameters Clone(S5pParameters source) => new()
    {
        Breathiness = source.Breathiness,
        Gender = source.Gender,
        Interval = source.Interval,
        Loudness = source.Loudness,
        PitchDelta = source.PitchDelta,
        Tension = source.Tension,
        VibratoEnv = source.VibratoEnv,
        Voicing = source.Voicing,
    };

    private sealed class S5pProject
    {
        public JsonElement? Instrumental { get; set; }
        public List<S5pMeter> Meter { get; set; } = new();
        public JsonElement? Mixer { get; set; }
        public List<S5pTempo> Tempo { get; set; } = new();
        public List<S5pTrack> Tracks { get; set; } = new();
        public int? Version { get; set; }
    }

    private sealed class S5pMeter
    {
        public int BeatGranularity { get; set; }
        public int BeatPerMeasure { get; set; }
        public int Measure { get; set; }
    }

    private sealed class S5pTempo
    {
        public double BeatPerMinute { get; set; }
        public long Position { get; set; }
    }

    private sealed class S5pTrack
    {
        public string? Color { get; set; }
        public JsonElement? DbDefaults { get; set; }
        public string? DbName { get; set; }
        public int? DisplayOrder { get; set; }
        public JsonElement? Mixer { get; set; }
        public string? Name { get; set; }
        public List<S5pNote?> Notes { get; set; } = new();
        public S5pParameters? Parameters { get; set; }
    }

    private sealed class S5pNote
    {
        public string? Comment { get; set; }
        public double? DF0Jitter { get; set; }
        public long Duration { get; set; }
        public string? Lyric { get; set; }
        public long Onset { get; set; }
        public int Pitch { get; set; }
    }

    private sealed class S5pParameters
    {
        public List<double>? Breathiness { get; set; }
        public List<double>? Gender { get; set; }
        public long? Interval { get; set; }
        public List<double>? Loudness { get; set; }
        public List<double>? PitchDelta { get; set; }
        public List<double>? Tension { get; set; }
        public List<double>? VibratoEnv { get; set; }
        public List<double>? Voicing { get; set; }
    }
}
