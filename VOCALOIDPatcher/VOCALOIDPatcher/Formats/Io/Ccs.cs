using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml;
using VOCALOIDPatcher.Formats.Exceptions;
using VOCALOIDPatcher.Formats.Model;
using VOCALOIDPatcher.Formats.Process;
using VOCALOIDPatcher.Formats.Process.Pitch;
using VOCALOIDPatcher.Formats.Util;

namespace VOCALOIDPatcher.Formats.Io;

public static class Ccs
{
    private const double TickRate = 2.0;
    private const int OctaveOffset = -1;
    private const int FixedMeasurePrefix = 1;

    public static Model.Project Parse(IReadOnlyList<ImportFile> files, ImportParams parms)
    {
        var file = files[0];
        var document = new XmlDocument();
        document.LoadXml(Texts.ReadText(file.Content));

        var scenarioNode = document.DocumentElement ?? throw new IllegalFileException.XmlRootNotFound();
        var sceneNode = scenarioNode.GetSingleElementByTagName("Sequence").GetSingleElementByTagName("Scene");

        var unitNodes = sceneNode.GetSingleElementByTagName("Units")
            .GetElementListByTagName("Unit")
            .Where(n => n.GetAttribute("Category") == "SingerSong")
            .ToList();
        var groupNodes = sceneNode.GetSingleElementByTagName("Groups")
            .GetElementListByTagName("Group")
            .Where(n => n.GetAttribute("Category") == "SingerSong")
            .ToList();

        var results = unitNodes.Select((unitNode, index) =>
        {
            var groupId = unitNode.GetAttributeOrNull("Group");
            var group = groupId != null ? groupNodes.FirstOrDefault(g => g.GetAttribute("Id") == groupId) : null;
            var trackName = group?.GetAttributeOrNull("Name");
            return ParseTrack(index, unitNode, trackName, parms);
        }).ToList();

        var tracks = results.Select(r => r.Track).ToList();
        var warnings = new List<ImportWarning>();
        var tempos = MergeTempos(results, warnings);
        var timeSignatures = MergeTimeSignatures(results, warnings);

        return new Model.Project(Format.Ccs, files, file.NameWithoutExtension, tracks, timeSignatures, tempos, FixedMeasurePrefix, warnings);
    }

    private static List<Tempo> MergeTempos(List<TrackParseResult> results, List<ImportWarning> warnings)
    {
        var tempos = (results.FirstOrDefault(r => r.Tempos.Count > 0)?.Tempos
                      ?? new List<Tempo> { Tempo.Default }.Also(() => warnings.Add(new ImportWarning.TempoNotFound())))
            .ToList();

        foreach (var result in results)
            foreach (var ignored in result.Tempos.Where(t => !tempos.Contains(t)))
                warnings.Add(new ImportWarning.TempoIgnoredInTrack(result.Track, ignored));

        int firstTempoIndex = tempos.FindLastIndex(t => t.TickPosition <= 0);
        for (int i = 0; i < firstTempoIndex; i++)
        {
            var removed = tempos[0];
            tempos.RemoveAt(0);
            warnings.Add(new ImportWarning.TempoIgnoredInPreMeasure(removed));
        }

        tempos[0] = tempos[0] with { TickPosition = 0 };
        return tempos;
    }

    private static List<TimeSignature> MergeTimeSignatures(List<TrackParseResult> results, List<ImportWarning> warnings)
    {
        var timeSignatures = (results.FirstOrDefault(r => r.TimeSignatures.Count > 0)?.TimeSignatures
                              ?? new List<TimeSignature> { TimeSignature.Default }.Also(() => warnings.Add(new ImportWarning.TimeSignatureNotFound())))
            .ToList();

        foreach (var result in results)
            foreach (var ignored in result.TimeSignatures.Where(t => !timeSignatures.Contains(t)))
                warnings.Add(new ImportWarning.TimeSignatureIgnoredInTrack(result.Track, ignored));

        int firstIndex = timeSignatures.FindLastIndex(t => t.MeasurePosition <= 0);
        for (int i = 0; i < firstIndex; i++)
        {
            var removed = timeSignatures[0];
            timeSignatures.RemoveAt(0);
            warnings.Add(new ImportWarning.TimeSignatureIgnoredInPreMeasure(removed));
        }

        timeSignatures[0] = timeSignatures[0] with { MeasurePosition = 0 };
        return timeSignatures;
    }

    private static TrackParseResult ParseTrack(int index, XmlElement unitNode, string? name, ImportParams parms)
    {
        var songNode = unitNode.GetSingleElementByTagNameOrNull("Song");
        var timeNodes = songNode?.GetSingleElementByTagNameOrNull("Beat")?.GetElementListByTagName("Time") ?? new List<XmlElement>();

        var tickCounter = new TickCounter(TickRate);
        var timeSignatures = new List<TimeSignature>();
        foreach (var timeNode in timeNodes)
        {
            if (!long.TryParse(timeNode.GetAttributeOrNull("Clock"), out var tick))
                continue;
            if (!int.TryParse(timeNode.GetAttributeOrNull("Beats"), out var numerator))
                continue;
            if (!int.TryParse(timeNode.GetAttributeOrNull("BeatType"), out var denominator))
                continue;

            tickCounter.GoToTick(tick, numerator, denominator);
            timeSignatures.Add(new TimeSignature(tickCounter.Measure, numerator, denominator));
        }

        long tickPrefix = GetTickPrefix(timeSignatures, FixedMeasurePrefix);
        timeSignatures = timeSignatures.Select(t => t with { MeasurePosition = t.MeasurePosition - FixedMeasurePrefix }).ToList();

        var tempos = (songNode?.GetSingleElementByTagNameOrNull("Tempo")?.GetElementListByTagName("Sound") ?? new List<XmlElement>())
            .Select(e =>
            {
                if (!long.TryParse(e.GetAttributeOrNull("Clock"), out var clock))
                    return (Tempo?)null;
                if (!double.TryParse(e.GetAttributeOrNull("Tempo"), NumberStyles.Float, CultureInfo.InvariantCulture, out var bpm))
                    return null;
                return new Tempo((long)(clock / TickRate) - tickPrefix, bpm);
            })
            .Where(t => t != null)
            .Select(t => t!)
            .ToList();

        var notes = (songNode?.GetSingleElementByTagNameOrNull("Score")?.GetElementListByTagName("Note") ?? new List<XmlElement>())
            .Select((element, noteIndex) =>
            {
                long tickOn = (long)(element.GetRequiredAttributeAsLong("Clock") / TickRate) - tickPrefix;
                long tickOff = tickOn + (long)(element.GetRequiredAttributeAsLong("Duration") / TickRate);
                int pitchStep = element.GetRequiredAttributeAsInteger("PitchStep");
                int pitchOctave = element.GetRequiredAttributeAsInteger("PitchOctave") - OctaveOffset;
                int key = pitchStep + pitchOctave * Constants.KeyInOctave;
                string lyric = element.GetRequiredAttribute("Lyric");
                string? phoneme = element.HasAttribute("Phonetic") ? element.GetRequiredAttribute("Phonetic").Replace(",", " ") : null;
                return new Note(noteIndex, key, lyric, tickOn, tickOff, phoneme);
            })
            .ToList();

        Model.Pitch? pitch = null;
        if (!parms.SimpleImport)
        {
            var dataNodes = songNode?.GetSingleElementByTagNameOrNull("Parameter")?.GetSingleElementByTagNameOrNull("LogF0")?.GetElementListByTagName("Data")
                            ?? new List<XmlElement>();
            var events = dataNodes.Select(ParsePitchData).Where(e => e != null).Select(e => e!).ToList();
            pitch = CevioPitchConversion.PitchFromCevioTrack(new CevioTrackPitchData(events, tempos, tickPrefix));
        }

        var trackName = name ?? $"Track {index + 1}";
        var track = new Track(index, trackName, notes, pitch).ValidateNotes();
        return new TrackParseResult(track, tempos, timeSignatures);
    }

    private static CevioTrackPitchData.Event? ParsePitchData(XmlElement dataElement)
    {
        long? index = long.TryParse(dataElement.GetAttributeOrNull("Index"), out var i) ? i : null;
        long? repeat = long.TryParse(dataElement.GetAttributeOrNull("Repeat"), out var r) ? r : null;
        var inner = dataElement.InnerValueOrNull();
        if (inner == null || !double.TryParse(inner, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return null;
        return new CevioTrackPitchData.Event(index, repeat, value);
    }

    private static long GetTickPrefix(IReadOnlyList<TimeSignature> timeSignatures, int measurePrefix)
    {
        var counter = new TickCounter();
        foreach (var ts in timeSignatures.Where(t => t.MeasurePosition < measurePrefix))
            counter.GoToMeasure(ts);
        counter.GoToMeasure(measurePrefix);
        return counter.Tick;
    }

    private sealed record TrackParseResult(Track Track, List<Tempo> Tempos, List<TimeSignature> TimeSignatures);

    public static ExportResult Generate(Model.Project project, IReadOnlyList<FeatureConfig> features)
    {
        var document = GenerateContent(project, features);
        var settings = new XmlWriterSettings { Indent = false, OmitXmlDeclaration = false };
        var builder = new StringBuilder();
        using (var writer = XmlWriter.Create(builder, settings))
            document.Save(writer);

        var notifications = new List<ExportNotification>();
        if (features.Contains(Feature.ConvertPitch))
            notifications.Add(ExportNotification.PitchDataExported);

        return new ExportResult(Encoding.UTF8.GetBytes(builder.ToString()), FormatRegistry.Get(Format.Ccs).GetFileName(project.Name), notifications);
    }

    private static XmlDocument GenerateContent(Model.Project project, IReadOnlyList<FeatureConfig> features)
    {
        var document = new XmlDocument();
        document.LoadXml(Resources.CcsTemplate);
        var scenarioNode = document.DocumentElement!;
        var sceneNode = scenarioNode.GetSingleElementByTagName("Sequence").GetSingleElementByTagName("Scene");

        var unitsNode = sceneNode.GetSingleElementByTagName("Units");
        var emptyUnitNode = unitsNode.GetSingleElementByTagName("Unit");
        unitsNode.RemoveChild(emptyUnitNode);
        var groupsNode = sceneNode.GetSingleElementByTagName("Groups");
        var emptyGroupNode = groupsNode.GetSingleElementByTagName("Group");
        groupsNode.RemoveChild(emptyGroupNode);

        long tickPrefix = (long)(project.TimeSignatures[0].TicksInMeasure * TickRate * FixedMeasurePrefix);

        var temposNode = emptyUnitNode.GetSingleElementByTagName("Song").GetSingleElementByTagName("Tempo");
        SetupTempoNodes(temposNode, project.Tempos, tickPrefix);

        var beatsNode = emptyUnitNode.GetSingleElementByTagName("Song").GetSingleElementByTagName("Beat");
        SetupBeatNodes(beatsNode, project.TimeSignatures, tickPrefix);

        foreach (var model in project.Tracks)
        {
            var newUnit = emptyUnitNode.CloneElement();
            var newGroup = emptyGroupNode.CloneElement();

            string id = Guid.NewGuid().ToString();
            newUnit.SetAttribute("Group", id);
            newGroup.SetAttribute("Id", id);
            newGroup.SetAttribute("Name", model.Name);
            SetupNotes(document, newUnit, model.Notes, tickPrefix);

            if (features.Contains(Feature.ConvertPitch))
                SetupPitchData(document, newUnit, model, project.Tempos, tickPrefix);

            unitsNode.AppendChild(newUnit);
            groupsNode.AppendChild(newGroup);
        }

        return document;
    }

    private static void SetupTempoNodes(XmlElement temposNode, IReadOnlyList<Tempo> models, long tickPrefix)
    {
        var previous = temposNode.GetSingleElementByTagName("Sound");
        previous.SetAttribute("Tempo", models[0].Bpm.ToFixed(2));
        foreach (var model in models.Skip(1))
        {
            var newNode = previous.CloneElement();
            newNode.SetAttribute("Tempo", model.Bpm.ToFixed(2));
            newNode.SetAttribute("Clock", ((long)(model.TickPosition * TickRate + tickPrefix)).ToString(CultureInfo.InvariantCulture));
            previous.InsertAfterThis(newNode);
            previous = newNode;
        }
    }

    private static void SetupBeatNodes(XmlElement beatsNode, IReadOnlyList<TimeSignature> models, long tickPrefix)
    {
        var previous = beatsNode.GetSingleElementByTagName("Time");
        previous.SetAttribute("Beats", models[0].Numerator.ToString(CultureInfo.InvariantCulture));
        previous.SetAttribute("BeatType", models[0].Denominator.ToString(CultureInfo.InvariantCulture));
        var counter = new TickCounter(TickRate);
        counter.GoToMeasure(models[0]);
        foreach (var model in models.Skip(1))
        {
            var newNode = previous.CloneElement();
            counter.GoToMeasure(model);
            newNode.SetAttribute("Clock", (counter.OutputTick + tickPrefix).ToString(CultureInfo.InvariantCulture));
            newNode.SetAttribute("Beats", model.Numerator.ToString(CultureInfo.InvariantCulture));
            newNode.SetAttribute("BeatType", model.Denominator.ToString(CultureInfo.InvariantCulture));
            previous.InsertAfterThis(newNode);
            previous = newNode;
        }
    }

    private static void SetupNotes(XmlDocument document, XmlElement unitNode, IReadOnlyList<Note> models, long tickPrefix)
    {
        var score = unitNode.GetSingleElementByTagName("Song").GetSingleElementByTagName("Score");
        foreach (var note in models)
        {
            var newNote = document.CreateElement("Note");
            newNote.SetAttribute("Clock", ((long)(note.TickOn * TickRate + tickPrefix)).ToString(CultureInfo.InvariantCulture));
            newNote.SetAttribute("PitchStep", (note.Key % Constants.KeyInOctave).ToString(CultureInfo.InvariantCulture));
            newNote.SetAttribute("PitchOctave", (note.Key / Constants.KeyInOctave + OctaveOffset).ToString(CultureInfo.InvariantCulture));
            newNote.SetAttribute("Duration", ((long)(note.Length * TickRate)).ToString(CultureInfo.InvariantCulture));
            newNote.SetAttribute("Lyric", note.Lyric);
            if (!string.IsNullOrWhiteSpace(note.Phoneme))
                newNote.SetAttribute("Phonetic", note.Phoneme!.Replace(" ", ","));
            score.AppendChild(newNote);
        }
    }

    private static void SetupPitchData(XmlDocument document, XmlElement unitNode, Track trackModel, IReadOnlyList<Tempo> tempos, long tickPrefix)
    {
        var data = trackModel.Pitch?.GenerateForCevio(trackModel.Notes, tempos, (long)(tickPrefix / TickRate));
        if (data == null)
            return;

        var dataNodes = data.Events.Select(e =>
        {
            var node = document.CreateElement("Data");
            if (e.Index != null)
                node.SetAttribute("Index", e.Index.Value.ToString(CultureInfo.InvariantCulture));
            if (e.Repeat != null)
                node.SetAttribute("Repeat", e.Repeat.Value.ToString(CultureInfo.InvariantCulture));
            node.AppendText(e.Value.ToString(CultureInfo.InvariantCulture));
            return node;
        }).ToList();

        var songNode = unitNode.GetSingleElementByTagName("Song");
        document.AppendNewChildTo(songNode, "Parameter", parameterNode =>
            document.AppendNewChildTo(parameterNode, "LogF0", logF0Node =>
            {
                logF0Node.SetAttribute("Length", data.GetLength().ToString(CultureInfo.InvariantCulture));
                foreach (var node in dataNodes)
                    logF0Node.AppendChild(node);
            }));
    }
}

internal static class CcsExtensions
{
    public static List<T> Also<T>(this List<T> list, Action action)
    {
        action();
        return list;
    }
}
