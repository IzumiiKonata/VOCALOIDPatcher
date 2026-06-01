using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using VOCALOIDPatcher.Formats.Exceptions;
using VOCALOIDPatcher.Formats.Model;
using VOCALOIDPatcher.Formats.Util;

namespace VOCALOIDPatcher.Formats.Io;

public static class MusicXml
{
    private const double DefaultTickRateCevio = 2.0;

    public static Model.Project Parse(IReadOnlyList<ImportFile> files, ImportParams parms)
    {
        var file = files[0];
        var document = new XmlDocument();
        document.LoadXml(Texts.ReadText(file.Content));
        var root = document.DocumentElement ?? throw new IllegalFileException.XmlRootNotFound();
        var partNodes = root.GetElementListByTagName("part");

        var warnings = new List<ImportWarning>();
        var masterTrack = partNodes.FirstOrDefault(p => p.GetElementListByTagName("measure").Count > 0)
                          ?? throw new IllegalFileException.XmlElementNotFound("measure");
        var masterResult = ParseMasterTrack(masterTrack, warnings);
        var timeSignatures = masterResult.TimeSignatures.Count > 0 ? masterResult.TimeSignatures : new List<TimeSignature> { TimeSignature.Default };
        var tempos = masterResult.TempoWithMeasureIndexes.Count > 0
            ? masterResult.TempoWithMeasureIndexes.Select(p => p.Tempo).ToList()
            : new List<Tempo> { Tempo.Default };

        var tracks = partNodes.Select((element, index) => ParseTrack(index, element, masterResult, parms.DefaultLyric)).ToList();

        return new Model.Project(Format.MusicXml, files, file.NameWithoutExtension, tracks, timeSignatures, tempos, 0, warnings);
    }

    private sealed class MasterTrackParseResult
    {
        public List<(int Index, Tempo Tempo)> TempoWithMeasureIndexes { get; init; } = new();
        public List<TimeSignature> TimeSignatures { get; init; } = new();
        public double ImportTickRate { get; init; }
        public List<long> MeasureBorders { get; init; } = new();
    }

    private static MasterTrackParseResult ParseMasterTrack(XmlElement partNode, List<ImportWarning> warnings)
    {
        var measureNodes = partNode.GetElementListByTagName("measure");
        long divisions = long.Parse(measureNodes[0].GetElementListByTagName("attributes")
            .SelectMany(a => a.GetElementListByTagName("divisions")).First().InnerValue());
        double importTickRate = Constants.TicksInBeat / (double)divisions;

        var tempos = new List<(int, Tempo)>();
        var timeSignatures = new List<TimeSignature>();
        var measureBorders = new List<long> { 0L };
        long tickPosition = 0L;
        var currentTimeSignature = TimeSignature.Default;

        for (int index = 0; index < measureNodes.Count; index++)
        {
            var measureNode = measureNodes[index];
            var timeNode = measureNode.GetElementListByTagName("attributes")
                .SelectMany(a => a.GetElementListByTagName("time")).FirstOrDefault();
            TimeSignature timeSignature;
            if (timeNode != null)
            {
                timeSignature = new TimeSignature(index,
                    int.Parse(timeNode.GetSingleElementByTagName("beats").InnerValue()),
                    int.Parse(timeNode.GetSingleElementByTagName("beat-type").InnerValue()));
                timeSignatures.Add(timeSignature);
                currentTimeSignature = timeSignature;
            }
            else
            {
                timeSignature = currentTimeSignature;
            }

            var soundNode = measureNode.GetElementListByTagName("sound").FirstOrDefault(s => s.HasAttribute("tempo"));
            if (soundNode != null)
                tempos.Add((index, new Tempo(tickPosition, double.Parse(soundNode.GetAttribute("tempo"), CultureInfo.InvariantCulture))));

            tickPosition += timeSignature.TicksInMeasure;
            measureBorders.Add(tickPosition);
        }

        if (timeSignatures.Count == 0)
            warnings.Add(new ImportWarning.TimeSignatureNotFound());
        if (tempos.Count == 0)
            warnings.Add(new ImportWarning.TempoNotFound());

        return new MasterTrackParseResult
        {
            TempoWithMeasureIndexes = tempos,
            TimeSignatures = timeSignatures,
            ImportTickRate = importTickRate,
            MeasureBorders = measureBorders,
        };
    }

    private static Track ParseTrack(int trackIndex, XmlElement partNode, MasterTrackParseResult masterResult, string defaultLyric)
    {
        var notes = new List<Note>();
        bool isInsideNote = false;
        double importTickRate = masterResult.ImportTickRate;
        var measureNodes = partNode.GetElementListByTagName("measure");

        for (int index = 0; index < measureNodes.Count; index++)
        {
            long tickPosition = masterResult.MeasureBorders[index];
            foreach (var noteNode in measureNodes[index].GetElementListByTagName("note"))
            {
                var durationStr = noteNode.GetSingleElementByTagNameOrNull("duration")?.InnerValueOrNull();
                long duration;
                if (durationStr != null && long.TryParse(durationStr, out var rawDuration))
                {
                    duration = (long)(rawDuration * importTickRate);
                }
                else if (noteNode.GetSingleElementByTagNameOrNull("grace") != null)
                {
                    continue;
                }
                else
                {
                    throw new IllegalFileException.XmlElementNotFound("duration");
                }

                if (noteNode.GetElementListByTagName("rest").Count > 0)
                {
                    tickPosition += duration;
                    continue;
                }

                int key = Constants.DefaultKey;
                var pitchNode = noteNode.GetSingleElementByTagNameOrNull("pitch");
                if (pitchNode != null)
                {
                    string step = pitchNode.GetSingleElementByTagName("step").InnerValue();
                    int? alter = int.TryParse(pitchNode.GetSingleElementByTagNameOrNull("alter")?.InnerValueOrNull(), out var a) ? a : null;
                    int relativeKey = step switch
                    {
                        "C" => 0,
                        "D" => 2,
                        "E" => 4,
                        "F" => 5,
                        "G" => 7,
                        "A" => 9,
                        "B" => 11,
                        _ => throw new InvalidOperationException(),
                    } + (alter ?? 0);
                    int octave = int.Parse(pitchNode.GetSingleElementByTagName("octave").InnerValue()) + 1;
                    key = octave * Constants.KeyInOctave + relativeKey;
                }

                string lyric = noteNode.GetSingleElementByTagNameOrNull("lyric")?.GetSingleElementByTagNameOrNull("text")?.InnerValueOrNull() ?? defaultLyric;

                Note note;
                if (!isInsideNote)
                {
                    note = new Note(0, key, lyric, tickPosition, tickPosition + duration);
                }
                else
                {
                    var last = notes[^1];
                    notes.RemoveAt(notes.Count - 1);
                    note = last with { TickOff = last.TickOff + duration };
                }

                tickPosition += duration;
                notes.Add(note);

                switch (noteNode.GetSingleElementByTagNameOrNull("tie")?.GetAttribute("type"))
                {
                    case "start":
                        isInsideNote = true;
                        break;
                    case "stop":
                        isInsideNote = false;
                        break;
                }
            }
        }

        return new Track(trackIndex, $"Track {trackIndex + 1}", notes);
    }

    public static ExportResult Generate(Model.Project project, IReadOnlyList<FeatureConfig> features)
    {
        var projectWithTickRate = ApplyTickRate(project);
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            foreach (var track in projectWithTickRate.Tracks)
            {
                string content = GenerateTrackContent(projectWithTickRate, track);
                string safeName = Texts.GetSafeFileName(track.Name);
                string fileName = $"{project.Name}_{track.Id + 1}_{safeName}.{FormatRegistry.Get(Format.MusicXml).Extension}";
                var entry = zip.CreateEntry(fileName);
                using var entryStream = entry.Open();
                var bytes = Encoding.UTF8.GetBytes(content);
                entryStream.Write(bytes, 0, bytes.Length);
            }
        }

        return new ExportResult(stream.ToArray(), project.Name + ".zip", new List<ExportNotification>());
    }

    private static string GenerateTrackContent(Model.Project project, Track track)
    {
        var keyTicks = GetKeyTicks(project, track);
        var measures = GetMeasures(keyTicks, project.TimeSignatures);

        var document = new XmlDocument();
        document.LoadXml(Resources.MusicXmlTemplate);
        var root = document.DocumentElement!;
        var partNode = root.GetSingleElementByTagName("part");
        var firstMeasureNode = partNode.GetSingleElementByTagName("measure");
        partNode.RemoveChild(firstMeasureNode);

        for (int index = 0; index < measures.Count; index++)
        {
            var measureNode = GenerateMeasureNode(document, measures[index], index, index == 0 ? firstMeasureNode : null);
            partNode.AppendChild(measureNode);
        }

        var settings = new XmlWriterSettings { Indent = false, OmitXmlDeclaration = false };
        var builder = new StringBuilder();
        using (var writer = XmlWriter.Create(builder, settings))
            document.Save(writer);
        return builder.ToString();
    }

    private static XmlElement GenerateMeasureNode(XmlDocument document, MXmlMeasure measure, int index, XmlElement? baseMeasureNode)
    {
        var node = baseMeasureNode?.CloneElement() ?? document.CreateElement("measure");
        node.SetAttribute("number", (index + 1).ToString(CultureInfo.InvariantCulture));
        if (measure.TimeSignature != null)
            node.AppendChild(GenerateTimeSignatureNode(document, measure.TimeSignature));

        foreach (var content in measure.Contents)
        {
            switch (content)
            {
                case MXmlTempo tempo:
                    foreach (var n in GenerateNodesForTempo(document, tempo))
                        node.AppendChild(n);
                    break;
                case MXmlRest rest:
                    node.AppendChild(GenerateRestNode(document, rest));
                    break;
                case MXmlNote noteContent:
                    node.AppendChild(GenerateNoteNode(document, noteContent));
                    break;
            }
        }

        return node;
    }

    private static XmlElement GenerateTimeSignatureNode(XmlDocument document, TimeSignature timeSignature)
    {
        var attributesNode = document.CreateElement("attributes");
        document.AppendNewChildTo(attributesNode, "time", timeNode =>
        {
            document.AppendNewChildTo(timeNode, "beats", e => e.AppendText(timeSignature.Numerator.ToString(CultureInfo.InvariantCulture)));
            document.AppendNewChildTo(timeNode, "beat-type", e => e.AppendText(timeSignature.Denominator.ToString(CultureInfo.InvariantCulture)));
        });
        return attributesNode;
    }

    private static List<XmlElement> GenerateNodesForTempo(XmlDocument document, MXmlTempo tempo)
    {
        string bpmText = tempo.Bpm.ToString(CultureInfo.InvariantCulture);
        var soundNode = document.CreateElement("sound");
        soundNode.SetAttribute("tempo", bpmText);
        var directionNode = document.CreateElement("direction");
        document.AppendNewChildTo(directionNode, "direction-type", directionTypeNode =>
            document.AppendNewChildTo(directionTypeNode, "metronome", metronomeNode =>
            {
                document.AppendNewChildTo(metronomeNode, "beat-unit", e => e.AppendText("quarter"));
                document.AppendNewChildTo(metronomeNode, "per-minute", e => e.AppendText(bpmText));
            }));
        directionNode.AppendChild(soundNode.CloneElement());
        return new List<XmlElement> { soundNode, directionNode };
    }

    private static XmlElement GenerateRestNode(XmlDocument document, MXmlRest rest)
    {
        var noteNode = document.CreateElement("note");
        document.AppendNewChildTo(noteNode, "rest", _ => { });
        document.AppendNewChildTo(noteNode, "duration", e => e.AppendText(rest.Duration.ToString(CultureInfo.InvariantCulture)));
        return noteNode;
    }

    private static XmlElement GenerateNoteNode(XmlDocument document, MXmlNote note)
    {
        var noteNode = document.CreateElement("note");
        document.AppendNewChildTo(noteNode, "pitch", pitchNode =>
        {
            int octave = note.Note.Key / Constants.KeyInOctave - 1;
            var (step, alter) = (note.Note.Key % Constants.KeyInOctave) switch
            {
                0 => ("C", 0),
                1 => ("C", 1),
                2 => ("D", 0),
                3 => ("D", 1),
                4 => ("E", 0),
                5 => ("F", 0),
                6 => ("F", 1),
                7 => ("G", 0),
                8 => ("G", 1),
                9 => ("A", 0),
                10 => ("A", 1),
                11 => ("B", 0),
                _ => throw new InvalidOperationException(),
            };
            document.AppendNewChildTo(pitchNode, "step", e => e.AppendText(step));
            if (alter == 1)
                document.AppendNewChildTo(pitchNode, "alter", e => e.AppendText("1"));
            document.AppendNewChildTo(pitchNode, "octave", e => e.AppendText(octave.ToString(CultureInfo.InvariantCulture)));
        });
        document.AppendNewChildTo(noteNode, "duration", e => e.AppendText(note.Duration.ToString(CultureInfo.InvariantCulture)));

        string? tieType = note.Type switch
        {
            NoteType.Begin => "start",
            NoteType.End => "stop",
            _ => null,
        };
        if (tieType != null)
        {
            document.AppendNewChildTo(noteNode, "tie", e => e.SetAttribute("type", tieType));
            document.AppendNewChildTo(noteNode, "notations", notationsNode =>
                document.AppendNewChildTo(notationsNode, "tied", e => e.SetAttribute("type", tieType)));
        }

        AppendLyricNode(document, noteNode, note.Type, note.Note.Lyric);
        return noteNode;
    }

    private static void AppendLyricNode(XmlDocument document, XmlElement noteNode, NoteType type, string lyric)
    {
        document.AppendNewChildTo(noteNode, "lyric", lyricNode =>
        {
            document.AppendNewChildTo(lyricNode, "syllabic", e => e.AppendText(type switch
            {
                NoteType.Begin => "begin",
                NoteType.Middle => "middle",
                NoteType.End => "end",
                _ => "single",
            }));
            document.AppendNewChildTo(lyricNode, "text", e =>
            {
                if (type == NoteType.Begin || type == NoteType.Single)
                    e.AppendText(lyric);
            });
        });
    }

    private static Model.Project ApplyTickRate(Model.Project project) => project with
    {
        Tempos = project.Tempos.Select(t => t with { TickPosition = (long)(t.TickPosition * DefaultTickRateCevio) }).ToList(),
        Tracks = project.Tracks.Select(track => track with
        {
            Notes = track.Notes.Select(n => n with
            {
                TickOn = (long)(n.TickOn * DefaultTickRateCevio),
                TickOff = (long)(n.TickOff * DefaultTickRateCevio),
            }).ToList(),
        }).ToList(),
    };

    private static List<KeyTick> GetKeyTicks(Model.Project project, Track track)
    {
        var noteEnds = track.Notes.Select(n => (KeyTick)new KeyTickNoteEnd(n.TickOff, n));
        var tempos = project.Tempos.Select(t => (KeyTick)new KeyTickTempo(t.TickPosition, t));
        var noteStarts = track.Notes.Select(n => (KeyTick)new KeyTickNoteStart(n.TickOn, n));
        return noteEnds.Concat(tempos).Concat(noteStarts).OrderBy(k => k.Tick).ToList();
    }

    private static List<MXmlMeasure> GetMeasures(List<KeyTick> keyTicks, IReadOnlyList<TimeSignature> timeSignatures)
    {
        var tickCounter = new TickCounter(1.0, (long)(Constants.TicksInFullNote * DefaultTickRateCevio));
        var measureBorderTicks = new List<long> { 0L };
        foreach (var timeSignature in timeSignatures)
        {
            int previousMeasure = tickCounter.Measure;
            long ticksInMeasure = tickCounter.TicksInMeasure;
            tickCounter.GoToMeasure(timeSignature);
            int currentMeasure = tickCounter.Measure;
            for (int i = 0; i < currentMeasure - previousMeasure; i++)
                measureBorderTicks.Add(measureBorderTicks[^1] + ticksInMeasure);
        }

        long lastTick = keyTicks[^1].Tick;
        if (lastTick >= tickCounter.Tick + tickCounter.TicksInMeasure)
        {
            int previousMeasure = tickCounter.Measure;
            long ticksInMeasure = tickCounter.TicksInMeasure;
            tickCounter.GoToTick(lastTick);
            int currentMeasure = tickCounter.Measure;
            for (int i = 0; i < currentMeasure - previousMeasure; i++)
                measureBorderTicks.Add(measureBorderTicks[^1] + ticksInMeasure);
        }

        measureBorderTicks.Add(measureBorderTicks[^1] + tickCounter.TicksInMeasure);

        var keyTicksWithMeasureBorders = new List<((long First, long Second) Border, List<KeyTick> Group)>();
        for (int i = 0; i < measureBorderTicks.Count - 1; i++)
        {
            long first = measureBorderTicks[i];
            long second = measureBorderTicks[i + 1];
            var group = keyTicks.Where(k => k is KeyTickNoteEnd
                ? k.Tick > first && k.Tick <= second
                : k.Tick >= first && k.Tick < second).ToList();
            keyTicksWithMeasureBorders.Add(((first, second), group));
        }

        var contentGroupBorderPairMap = new List<((long First, long Second) Border, List<MXmlMeasureContent> Contents)>();
        (Note Note, long Head)? ongoingNoteWithCurrentHead = null;
        foreach (var (border, keyTickGroup) in keyTicksWithMeasureBorders)
        {
            long currentTickInMeasure = 0L;
            var currentContentGroup = new List<MXmlMeasureContent>();
            foreach (var keyTick in keyTickGroup)
            {
                long keyTickRelative = keyTick.Tick - border.First;
                if (keyTickRelative > currentTickInMeasure)
                {
                    if (ongoingNoteWithCurrentHead == null)
                        currentContentGroup.Add(new MXmlRest(keyTickRelative - currentTickInMeasure));
                    currentTickInMeasure = keyTickRelative;
                }

                switch (keyTick)
                {
                    case KeyTickTempo t:
                        if (ongoingNoteWithCurrentHead == null)
                        {
                            currentContentGroup.Add(new MXmlTempo(t.Tempo.Bpm));
                        }
                        else
                        {
                            var (note, head) = ongoingNoteWithCurrentHead.Value;
                            currentContentGroup.Add(new MXmlNote(keyTick.Tick - head, note, note.TickOn == head ? NoteType.Begin : NoteType.Middle));
                            ongoingNoteWithCurrentHead = (note, keyTick.Tick);
                            currentContentGroup.Add(new MXmlTempo(t.Tempo.Bpm));
                        }

                        break;
                    case KeyTickNoteStart s:
                        ongoingNoteWithCurrentHead = (s.Note, keyTick.Tick);
                        break;
                    case KeyTickNoteEnd e:
                        var (ongoingNote, ongoingHead) = ongoingNoteWithCurrentHead!.Value;
                        currentContentGroup.Add(new MXmlNote(e.Note.TickOff - ongoingHead, e.Note, ongoingNote.TickOn == ongoingHead ? NoteType.Single : NoteType.End));
                        ongoingNoteWithCurrentHead = null;
                        break;
                }
            }

            long restLength = border.Second - border.First - currentTickInMeasure;
            if (restLength > 0)
            {
                if (ongoingNoteWithCurrentHead == null)
                {
                    currentContentGroup.Add(new MXmlRest(restLength));
                }
                else
                {
                    var (note, head) = ongoingNoteWithCurrentHead.Value;
                    currentContentGroup.Add(new MXmlNote(border.Second - head, note, note.TickOn == head ? NoteType.Begin : NoteType.Middle));
                    ongoingNoteWithCurrentHead = (note, border.Second);
                }
            }

            contentGroupBorderPairMap.Add((border, currentContentGroup));
        }

        return contentGroupBorderPairMap
            .OrderBy(p => p.Border.First)
            .Select((pair, index) => new MXmlMeasure(
                pair.Border.First,
                pair.Border.Second - pair.Border.First,
                timeSignatures.FirstOrDefault(t => t.MeasurePosition == index),
                pair.Contents))
            .ToList();
    }

    private abstract class KeyTick
    {
        protected KeyTick(long tick) => Tick = tick;
        public long Tick { get; }
    }

    private sealed class KeyTickTempo : KeyTick
    {
        public KeyTickTempo(long tick, Tempo tempo) : base(tick) => Tempo = tempo;
        public Tempo Tempo { get; }
    }

    private sealed class KeyTickNoteStart : KeyTick
    {
        public KeyTickNoteStart(long tick, Note note) : base(tick) => Note = note;
        public Note Note { get; }
    }

    private sealed class KeyTickNoteEnd : KeyTick
    {
        public KeyTickNoteEnd(long tick, Note note) : base(tick) => Note = note;
        public Note Note { get; }
    }

    private sealed record MXmlMeasure(long TickStart, long Length, TimeSignature? TimeSignature, List<MXmlMeasureContent> Contents);

    private abstract record MXmlMeasureContent;

    private sealed record MXmlTempo(double Bpm) : MXmlMeasureContent;

    private sealed record MXmlRest(long Duration) : MXmlMeasureContent;

    private sealed record MXmlNote(long Duration, Note Note, NoteType Type) : MXmlMeasureContent;

    private enum NoteType
    {
        Begin,
        Middle,
        End,
        Single,
    }
}
