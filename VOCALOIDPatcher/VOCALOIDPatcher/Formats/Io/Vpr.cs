using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using VOCALOIDPatcher.Formats.Model;
using VOCALOIDPatcher.Formats.Process;
using VOCALOIDPatcher.Formats.Process.Pitch;
using VOCALOIDPatcher.Formats.Util;

namespace VOCALOIDPatcher.Formats.Io;

public static class Vpr
{
    private const double BpmRate = 100.0;
    private const string EntryPath = "Project/sequence.json";
    private const string PitchBendName = "pitchBend";
    private const string PitchBendSensitivityName = "pitchBendSens";

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
        var content = ReadContent(file.Content);
        var warnings = new List<ImportWarning>();

        var tracks = content.Tracks.Select((track, index) => ParseTrack(track, index, parms)).ToList();

        var timeSignatures = content.MasterTrack?.TimeSig?.Events?
            .Select(e => new TimeSignature(e.Bar, e.Numer, e.Denom)).ToList();
        if (timeSignatures == null || timeSignatures.Count == 0)
        {
            timeSignatures = new List<TimeSignature> { TimeSignature.Default };
            warnings.Add(new ImportWarning.TimeSignatureNotFound());
        }

        var tempos = content.MasterTrack?.Tempo?.Events?
            .Select(e => new Tempo(e.Pos, e.Value / BpmRate)).ToList();
        if (tempos == null || tempos.Count == 0)
        {
            tempos = new List<Tempo> { Tempo.Default };
            warnings.Add(new ImportWarning.TempoNotFound());
        }

        return new Model.Project(Format.VPR, files, content.Title ?? file.NameWithoutExtension, tracks, timeSignatures, tempos, 0, warnings);
    }

    private static Track ParseTrack(VprTrack track, int trackIndex, ImportParams parms)
    {
        var noteWithOffsets = track.Parts.SelectMany(part => part.Notes.Select(n => (part.Pos, Note: n))).ToList();
        var notes = noteWithOffsets.Select((pair, index) =>
        {
            var (offset, n) = pair;
            string lyric = string.IsNullOrWhiteSpace(n.Lyric) ? parms.DefaultLyric : n.Lyric!;
            return new Note(index, n.Number, lyric, offset + n.Pos, offset + n.Pos + n.Duration, n.Phoneme);
        }).ToList();

        Model.Pitch? pitch = parms.SimpleImport ? null : ParsePitchData(track);
        return new Track(trackIndex, track.Name ?? $"Track {trackIndex + 1}", notes, pitch).ValidateNotes();
    }

    private static Model.Pitch? ParsePitchData(VprTrack track)
    {
        var dataByParts = track.Parts.Select(part => new VocaloidPartPitchData(
            part.Pos,
            GetControllerEvents(part, PitchBendName).Select(e => new VocaloidPartPitchData.Event(e.Pos, (int)e.Value)).ToList(),
            GetControllerEvents(part, PitchBendSensitivityName).Select(e => new VocaloidPartPitchData.Event(e.Pos, (int)e.Value)).ToList())).ToList();
        return VocaloidPitchConversion.PitchFromVocaloidParts(dataByParts);
    }

    private static List<VprControllerEvent> GetControllerEvents(VprPart part, string name) =>
        part.Controllers?.FirstOrDefault(c => c.Name == name)?.Events ?? new List<VprControllerEvent>();

    private static VprProject ReadContent(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        var entry = zip.Entries.FirstOrDefault(e => e.FullName.Replace('\\', '/').EndsWith("sequence.json"))
                    ?? zip.Entries.First();
        using var entryStream = entry.Open();
        using var reader = new StreamReader(entryStream);
        return JsonSerializer.Deserialize<VprProject>(reader.ReadToEnd(), Json)!;
    }

    public static ExportResult Generate(Model.Project project, IReadOnlyList<FeatureConfig> features)
    {
        var jsonText = GenerateContent(project, features);
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            var entry = zip.CreateEntry(EntryPath);
            using var entryStream = entry.Open();
            var bytes = Encoding.UTF8.GetBytes(jsonText);
            entryStream.Write(bytes, 0, bytes.Length);
        }

        var notifications = new List<ExportNotification>();
        if (!project.HasXSampaData)
            notifications.Add(ExportNotification.PhonemeResetRequiredV5);
        if (features.Contains(Feature.ConvertPitch))
            notifications.Add(ExportNotification.PitchDataExported);

        return new ExportResult(stream.ToArray(), FormatRegistry.Get(Format.VPR).GetFileName(project.Name), notifications);
    }

    private static string GenerateContent(Model.Project project, IReadOnlyList<FeatureConfig> features)
    {
        var vpr = JsonSerializer.Deserialize<VprProject>(Resources.VprTemplate, Json)!;
        long endTick = 0L;
        vpr.Title = project.Name;

        var tickCounter = new TickCounter();
        var timeSigEvents = project.TimeSignatures.Select(t =>
        {
            tickCounter.GoToMeasure(t);
            return new VprTimeSigEvent { Bar = t.MeasurePosition, Denom = t.Denominator, Numer = t.Numerator };
        }).ToList();
        vpr.MasterTrack!.TimeSig!.Events = timeSigEvents;
        endTick = System.Math.Max(endTick, tickCounter.OutputTick);

        var tempoEvents = project.Tempos.Select(t => new VprTempoEvent { Pos = t.TickPosition, Value = (int)(t.Bpm * BpmRate) }).ToList();
        vpr.MasterTrack!.Tempo!.Events = tempoEvents;
        endTick = System.Math.Max(endTick, tempoEvents.Count > 0 ? tempoEvents.Max(e => e.Pos) : 0);

        var emptyTrack = vpr.Tracks[0];
        var emptyPart = emptyTrack.Parts[0];
        var emptyNote = emptyPart.Notes[0];

        var tracks = project.Tracks.Select(track =>
        {
            var notes = track.Notes.Select(n => new VprNote
            {
                Pos = n.TickOn,
                Duration = n.Length,
                Number = n.Key,
                Lyric = n.Lyric,
                Phoneme = string.IsNullOrEmpty(n.Phoneme) ? emptyNote.Phoneme : n.Phoneme,
                IsProtected = !string.IsNullOrEmpty(n.Phoneme),
                Exp = emptyNote.Exp,
                SingingSkill = emptyNote.SingingSkill,
                Vibrato = emptyNote.Vibrato,
                Velocity = emptyNote.Velocity,
            }).ToList();

            var controllers = features.Contains(Feature.ConvertPitch) ? GeneratePitchData(track) : null;
            var parts = new List<VprPart>();
            if (track.Notes.Count > 0)
            {
                long duration = track.Notes[^1].TickOff;
                parts.Add(new VprPart
                {
                    Duration = duration,
                    MidiEffects = emptyPart.MidiEffects,
                    Notes = notes,
                    Pos = emptyPart.Pos,
                    StyleName = emptyPart.StyleName,
                    Voice = emptyPart.Voice,
                    Controllers = controllers,
                });
            }

            return new VprTrack
            {
                BusNo = emptyTrack.BusNo,
                Color = emptyTrack.Color,
                Height = emptyTrack.Height,
                IsFolded = emptyTrack.IsFolded,
                IsMuted = emptyTrack.IsMuted,
                IsSoloMode = emptyTrack.IsSoloMode,
                Name = track.Name,
                Panpot = emptyTrack.Panpot,
                Parts = parts,
                Type = emptyTrack.Type,
                Volume = emptyTrack.Volume,
            };
        }).ToList();

        vpr.Tracks = tracks;
        endTick = System.Math.Max(endTick, tracks.Count > 0 ? tracks.Max(t => t.Parts.Count > 0 ? t.Parts[0].Duration : 0) : 0);
        vpr.MasterTrack!.Loop!.End = endTick;

        return JsonSerializer.Serialize(vpr, Json);
    }

    private static List<VprController>? GeneratePitchData(Track track)
    {
        var pitchRawData = track.Pitch?.GenerateForVocaloid(track.Notes);
        if (pitchRawData == null)
            return null;

        var controllers = new List<VprController>();
        if (pitchRawData.Pbs.Count > 0)
            controllers.Add(new VprController
            {
                Name = PitchBendSensitivityName,
                Events = pitchRawData.Pbs.Select(e => new VprControllerEvent { Pos = e.Pos, Value = e.Value }).ToList(),
            });
        if (pitchRawData.Pit.Count > 0)
            controllers.Add(new VprController
            {
                Name = PitchBendName,
                Events = pitchRawData.Pit.Select(e => new VprControllerEvent { Pos = e.Pos, Value = e.Value }).ToList(),
            });

        return controllers.Count > 0 ? controllers : null;
    }

    private sealed class VprProject
    {
        public VprMasterTrack? MasterTrack { get; set; }
        public string? Title { get; set; }
        public List<VprTrack> Tracks { get; set; } = new();
        public string? Vender { get; set; }
        public JsonElement? Version { get; set; }
        public JsonElement? Voices { get; set; }
    }

    private sealed class VprMasterTrack
    {
        public VprLoop? Loop { get; set; }
        public int? SamplingRate { get; set; }
        public VprTempo? Tempo { get; set; }
        public VprTimeSig? TimeSig { get; set; }
        public JsonElement? Volume { get; set; }
    }

    private sealed class VprTempo
    {
        public List<VprTempoEvent> Events { get; set; } = new();
        public JsonElement? Global { get; set; }
        public double? Height { get; set; }
        public bool? IsFolded { get; set; }
    }

    private sealed class VprTempoEvent
    {
        public long Pos { get; set; }
        public int Value { get; set; }
    }

    private sealed class VprTimeSig
    {
        public List<VprTimeSigEvent> Events { get; set; } = new();
        public bool? IsFolded { get; set; }
    }

    private sealed class VprTimeSigEvent
    {
        public int Bar { get; set; }
        public int Denom { get; set; }
        public int Numer { get; set; }
    }

    private sealed class VprTrack
    {
        public int? BusNo { get; set; }
        public long? Color { get; set; }
        public double? Height { get; set; }
        public bool? IsFolded { get; set; }
        public bool? IsMuted { get; set; }
        public bool? IsSoloMode { get; set; }
        public string? Name { get; set; }
        public JsonElement? Panpot { get; set; }
        public List<VprPart> Parts { get; set; } = new();
        public int? Type { get; set; }
        public JsonElement? Volume { get; set; }
    }

    private sealed class VprLoop
    {
        public long? Begin { get; set; }
        public long? End { get; set; }
        public bool? IsEnabled { get; set; }
    }

    private sealed class VprPart
    {
        public long Duration { get; set; }
        public JsonElement? MidiEffects { get; set; }
        public List<VprNote> Notes { get; set; } = new();
        public long Pos { get; set; }
        public string? StyleName { get; set; }
        public JsonElement? Voice { get; set; }
        public List<VprController>? Controllers { get; set; }
    }

    private sealed class VprController
    {
        public string Name { get; set; } = "";
        public List<VprControllerEvent> Events { get; set; } = new();
    }

    private sealed class VprControllerEvent
    {
        public long Pos { get; set; }
        public long Value { get; set; }
    }

    private sealed class VprNote
    {
        public long Duration { get; set; }
        public JsonElement? Exp { get; set; }
        public bool? IsProtected { get; set; }
        public string? Lyric { get; set; }
        public int Number { get; set; }
        public string? Phoneme { get; set; }
        public long Pos { get; set; }
        public JsonElement? SingingSkill { get; set; }
        public int? Velocity { get; set; }
        public JsonElement? Vibrato { get; set; }
    }
}
