using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using VOCALOIDPatcher.Formats.Model;
using VOCALOIDPatcher.Formats.Process;
using VOCALOIDPatcher.Formats.Process.Pitch;
using VOCALOIDPatcher.Formats.Util;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace VOCALOIDPatcher.Formats.Io;

public static class Ustx
{
    private const string PitchCurveAbbr = "pitd";
    private static readonly Regex PhonemeRegex = new(@"\[([^\[\]]*)\]$", RegexOptions.Compiled);

    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly ISerializer YamlSerializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    public static Model.Project Parse(IReadOnlyList<ImportFile> files, ImportParams parms)
    {
        var file = files[0];
        var project = YamlDeserializer.Deserialize<UstxProject>(Texts.ReadText(file.Content));
        var tempos = ParseTempos(project);
        var timeSignatures = ParseTimeSignatures(project);
        return new Model.Project(
            Format.Ustx,
            files,
            project.Name ?? file.NameWithoutExtension,
            ParseTracks(project, parms, tempos),
            timeSignatures,
            tempos,
            0,
            new List<ImportWarning>());
    }

    private static List<TimeSignature> ParseTimeSignatures(UstxProject project)
    {
        if (project.TimeSignatures is { Count: > 0 } list)
            return list.Select(t => new TimeSignature(t.BarPosition, t.BeatPerBar, t.BeatUnit)).ToList();
        return new List<TimeSignature> { new(0, project.BeatPerBar ?? 4, project.BeatUnit ?? 4) };
    }

    private static List<Tempo> ParseTempos(UstxProject project)
    {
        if (project.Tempos is { Count: > 0 } list)
            return list.Select(t => new Tempo(t.Position, t.Bpm)).ToList();
        return new List<Tempo> { new(0, project.Bpm ?? 120.0) };
    }

    private static List<Track> ParseTracks(UstxProject project, ImportParams parms, IReadOnlyList<Tempo> tempos)
    {
        var trackMap = new Dictionary<int, Track>();
        for (int index = 0; index < project.Tracks.Count; index++)
            trackMap[index] = new Track(index, project.Tracks[index].TrackName ?? $"Track {index + 1}", new List<Note>());

        foreach (var voicePart in project.VoiceParts)
        {
            if (!trackMap.TryGetValue(voicePart.TrackNo, out var track))
                continue;

            long tickPrefix = voicePart.Position;
            var notes = voicePart.Notes.Select(n =>
            {
                var (lyric, phoneme) = SplitLyric(n.Lyric ?? "");
                return new Note(0, n.Tone, lyric, n.Position + tickPrefix, n.Position + n.Duration + tickPrefix, phoneme);
            }).ToList();

            var notePitches = parms.SimpleImport ? null : voicePart.Notes.Select(ParseNotePitch).ToList();
            var (validatedNotes, validatedNotePitches) = GetValidatedNotes(notes, notePitches);

            List<OpenUtauPartPitchData.Point>? pitchCurve = null;
            if (!parms.SimpleImport)
            {
                var curve = voicePart.Curves?.FirstOrDefault(c => c.Abbr == PitchCurveAbbr);
                if (curve != null)
                    pitchCurve = curve.Xs.Zip(curve.Ys, (x, y) => new OpenUtauPartPitchData.Point(x + tickPrefix, (int)y)).ToList();
            }

            Model.Pitch? pitch = null;
            if ((validatedNotePitches is { Count: > 0 }) || pitchCurve != null)
            {
                var partPitchData = new OpenUtauPartPitchData(
                    pitchCurve ?? new List<OpenUtauPartPitchData.Point>(),
                    validatedNotePitches ?? new List<OpenUtauNotePitchData>());
                pitch = OpenUtauPitchConversion.PitchFromUstxPart(validatedNotes, partPitchData, tempos);
            }

            var mergedPitch = OpenUtauPitchConversion.MergePitchFromUstxParts(track.Pitch, pitch);
            trackMap[voicePart.TrackNo] = track with
            {
                Notes = track.Notes.Concat(validatedNotes).ToList(),
                Pitch = mergedPitch,
            };
        }

        return trackMap.Values
            .Select(t => t with
            {
                Notes = t.Notes.Select((note, index) => note with { Id = index }).ToList(),
                Pitch = t.Pitch.ReduceRepeatedPitchPointsFromUstxTrack(),
            })
            .OrderBy(t => t.Id)
            .ToList();
    }

    private static (string Lyric, string? Phoneme) SplitLyric(string rawLyrics)
    {
        var match = PhonemeRegex.Match(rawLyrics);
        if (!match.Success)
            return (rawLyrics, null);
        string cleaned = match.Value.Trim('[', ']');
        string before = rawLyrics[..match.Index].Trim();
        return (before.Length == 0 ? cleaned : before, cleaned);
    }

    private static OpenUtauNotePitchData ParseNotePitch(UstxNote note)
    {
        var points = (note.Pitch?.Data ?? new List<UstxDatum>())
            .Select(d => new OpenUtauNotePitchData.Point(d.X, d.Y, OpenUtauNotePitchData.ShapeFromText(d.Shape)))
            .ToList();
        var v = note.Vibrato ?? new UstxVibrato();
        var vibrato = new UtauNoteVibratoParams(v.Length, v.Period, v.Depth, v.In, v.Out, v.Shift, v.Drift);
        return new OpenUtauNotePitchData(points, vibrato);
    }

    private static (List<Note>, List<OpenUtauNotePitchData>?) GetValidatedNotes(List<Note> notes, List<OpenUtauNotePitchData>? notePitches)
    {
        var validatedNotes = new List<Note>();
        var validatedNotePitches = notePitches != null ? new List<OpenUtauNotePitchData>() : null;
        long pos = 0L;
        for (int i = 0; i < notes.Count; i++)
        {
            var note = notes[i];
            if (note.TickOn >= pos)
            {
                validatedNotes.Add(note);
                if (notePitches != null && i < notePitches.Count)
                    validatedNotePitches!.Add(notePitches[i]);
                pos = note.TickOff;
            }
        }

        return (validatedNotes, validatedNotePitches);
    }

    public static ExportResult Generate(Model.Project project, IReadOnlyList<FeatureConfig> features)
    {
        var content = GenerateContent(project, features);
        var notifications = new List<ExportNotification>();
        if (features.Contains(Feature.ConvertPitch))
            notifications.Add(ExportNotification.PitchDataExported);
        return new ExportResult(Encoding.UTF8.GetBytes(content), FormatRegistry.Get(Format.Ustx).GetFileName(project.Name), notifications);
    }

    private static string GenerateContent(Model.Project project, IReadOnlyList<FeatureConfig> features)
    {
        var template = YamlDeserializer.Deserialize<UstxProject>(Resources.UstxTemplate);
        var trackTemplate = template.Tracks[0];
        var voicePartTemplate = template.VoiceParts[0];

        template.Name = project.Name;
        template.Bpm = project.Tempos[0].Bpm;
        template.BeatPerBar = project.TimeSignatures[0].Numerator;
        template.BeatUnit = project.TimeSignatures[0].Denominator;
        template.Tempos = project.Tempos.Select(t => new UstxTempo { Position = t.TickPosition, Bpm = t.Bpm }).ToList();
        template.TimeSignatures = project.TimeSignatures
            .Select(t => new UstxTimeSignature { BarPosition = t.MeasurePosition, BeatPerBar = t.Numerator, BeatUnit = t.Denominator })
            .ToList();
        template.Tracks = project.Tracks.Select(t => CloneTrack(trackTemplate, t.Name)).ToList();
        template.VoiceParts = project.Tracks.Select(t => GenerateVoicePart(voicePartTemplate, t, features)).ToList();

        return YamlSerializer.Serialize(template);
    }

    private static UstxTrack CloneTrack(UstxTrack template, string name) => new()
    {
        Phonemizer = template.Phonemizer,
        Mute = template.Mute,
        Solo = template.Solo,
        Volume = template.Volume,
        TrackName = name,
    };

    private static UstxVoicePart GenerateVoicePart(UstxVoicePart template, Track track, IReadOnlyList<FeatureConfig> features)
    {
        var noteTemplate = template.Notes[0];
        var notes = new List<UstxNote>();
        if (track.Notes.Count > 0)
            notes.Add(GenerateNote(noteTemplate, null, track.Notes[0]));
        for (int i = 0; i + 1 < track.Notes.Count; i++)
            notes.Add(GenerateNote(noteTemplate, track.Notes[i], track.Notes[i + 1]));

        var curves = new List<UstxCurve>();
        if (features.Contains(Feature.ConvertPitch))
        {
            var points = track.Pitch.ToOpenUtauPitchData(track.Notes);
            if (points.Count > 0)
                curves.Add(new UstxCurve
                {
                    Xs = points.Select(p => p.Tick).ToList(),
                    Ys = points.Select(p => p.Value).ToList(),
                    Abbr = PitchCurveAbbr,
                });
        }

        return new UstxVoicePart
        {
            Name = track.Name,
            Comment = template.Comment,
            TrackNo = track.Id,
            Position = 0L,
            Notes = notes,
            Curves = curves,
        };
    }

    private static UstxNote GenerateNote(UstxNote template, Note? lastNote, Note thisNote)
    {
        double firstPitchPointValue = lastNote?.TickOff == thisNote.TickOn ? (lastNote!.Key - thisNote.Key) * 10.0 : 0.0;
        var templateData = template.Pitch?.Data ?? new List<UstxDatum>();
        var pitchPoints = templateData
            .Select((datum, index) => new UstxDatum
            {
                X = datum.X,
                Y = index == 0 ? firstPitchPointValue : datum.Y,
                Shape = datum.Shape,
            })
            .ToList();

        var lyricBuilder = new StringBuilder(thisNote.Lyric);
        if (!string.IsNullOrWhiteSpace(thisNote.Phoneme))
            lyricBuilder.Append($" [{thisNote.Phoneme}]");

        var tv = template.Vibrato ?? new UstxVibrato();
        return new UstxNote
        {
            Position = thisNote.TickOn,
            Duration = thisNote.Length,
            Tone = thisNote.Key,
            Pitch = new UstxPitch { Data = pitchPoints, SnapFirst = template.Pitch?.SnapFirst ?? true },
            Lyric = lyricBuilder.ToString(),
            Vibrato = new UstxVibrato
            {
                Length = tv.Length,
                Period = tv.Period,
                Depth = tv.Depth,
                In = tv.In,
                Out = tv.Out,
                Shift = tv.Shift,
                Drift = tv.Drift,
            },
        };
    }

    public sealed class UstxProject
    {
        public string? Name { get; set; }
        public string? Comment { get; set; }
        public string? OutputDir { get; set; }
        public string? CacheDir { get; set; }
        public double UstxVersion { get; set; }
        public double? Bpm { get; set; }
        public int? BeatPerBar { get; set; }
        public int? BeatUnit { get; set; }
        public int? Resolution { get; set; }
        public List<UstxTimeSignature>? TimeSignatures { get; set; }
        public List<UstxTempo>? Tempos { get; set; }
        public Dictionary<string, UstxExpression>? Expressions { get; set; }
        public List<UstxTrack> Tracks { get; set; } = new();
        public List<UstxVoicePart> VoiceParts { get; set; } = new();
    }

    public sealed class UstxExpression
    {
        public string? Name { get; set; }
        public string? Abbr { get; set; }
        public string? Type { get; set; }
        public int Min { get; set; }
        public int Max { get; set; }
        public int DefaultValue { get; set; }
        public bool IsFlag { get; set; }
        public string? Flag { get; set; }
        public List<string>? Options { get; set; }
    }

    public sealed class UstxTrack
    {
        public string? Phonemizer { get; set; }
        public bool Mute { get; set; }
        public bool Solo { get; set; }
        public double Volume { get; set; }
        public string? TrackName { get; set; }
    }

    public sealed class UstxVoicePart
    {
        public string? Name { get; set; }
        public string? Comment { get; set; }
        public int TrackNo { get; set; }
        public long Position { get; set; }
        public List<UstxNote> Notes { get; set; } = new();
        public List<UstxCurve>? Curves { get; set; }
    }

    public sealed class UstxNote
    {
        public long Position { get; set; }
        public long Duration { get; set; }
        public int Tone { get; set; }
        public string? Lyric { get; set; }
        public UstxPitch? Pitch { get; set; }
        public UstxVibrato? Vibrato { get; set; }
    }

    public sealed class UstxPitch
    {
        public List<UstxDatum> Data { get; set; } = new();
        public bool SnapFirst { get; set; }
    }

    public sealed class UstxDatum
    {
        public double X { get; set; }
        public double Y { get; set; }
        public string? Shape { get; set; }
    }

    public sealed class UstxVibrato
    {
        public double Length { get; set; }
        public double Period { get; set; }
        public double Depth { get; set; }
        public double In { get; set; }
        public double Out { get; set; }
        public double Shift { get; set; }
        public double Drift { get; set; }
    }

    public sealed class UstxCurve
    {
        public List<long> Xs { get; set; } = new();
        public List<double> Ys { get; set; } = new();
        public string? Abbr { get; set; }
    }

    public sealed class UstxTempo
    {
        public long Position { get; set; }
        public double Bpm { get; set; }
    }

    public sealed class UstxTimeSignature
    {
        public int BarPosition { get; set; }
        public int BeatPerBar { get; set; }
        public int BeatUnit { get; set; }
    }
}
