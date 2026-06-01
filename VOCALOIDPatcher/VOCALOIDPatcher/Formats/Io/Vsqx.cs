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

public static class Vsqx
{
    private const double BpmRate = 100.0;
    private const int MinMeasureOffset = 1;

    public static Model.Project Parse(IReadOnlyList<ImportFile> files, ImportParams parms)
    {
        var file = files[0];
        var text = Texts.ReadText(file.Content);
        if (text.Contains("xmlns=\"http://www.yamaha.co.jp/vocaloid/schema/vsq3/\""))
            return Parse(file, files, text, TagNames.Vsq3, parms);
        if (text.Contains("xmlns=\"http://www.yamaha.co.jp/vocaloid/schema/vsq4/\""))
            return Parse(file, files, text, TagNames.Vsq4, parms);
        throw new IllegalFileException.UnknownVsqVersion();
    }

    private static Model.Project Parse(ImportFile file, IReadOnlyList<ImportFile> files, string textRead, TagNames tagNames, ImportParams parms)
    {
        var warnings = new List<ImportWarning>();
        var document = new XmlDocument();
        document.LoadXml(textRead);
        var root = document.DocumentElement ?? throw new IllegalFileException.XmlRootNotFound();

        var masterTrack = root.GetSingleElementByTagName(tagNames.MasterTrack);
        var preMeasureNode = masterTrack.GetSingleElementByTagName(tagNames.PreMeasure);
        if (!int.TryParse(preMeasureNode.InnerValue(), out var measurePrefix))
            throw new IllegalFileException.XmlElementValueIllegal(tagNames.PreMeasure);

        var (tickPrefix, timeSignatures) = ParseTimeSignatures(masterTrack, tagNames, measurePrefix, warnings);
        var tempos = ParseTempos(masterTrack, tagNames, tickPrefix, warnings);
        var tracks = root.GetElementListByTagName(tagNames.VsTrack)
            .Select((element, index) => ParseTrack(element, index, tagNames, tickPrefix, parms))
            .ToList();

        return new Model.Project(Format.Vsqx, files, file.NameWithoutExtension, tracks, timeSignatures, tempos, measurePrefix, warnings);
    }

    private static (long, List<TimeSignature>) ParseTimeSignatures(XmlElement masterTrack, TagNames tagNames, int measurePrefix, List<ImportWarning> warnings)
    {
        var rawTimeSignatures = masterTrack.GetElementListByTagName(tagNames.TimeSig, allowEmpty: false)
            .Select(e =>
            {
                if (!int.TryParse(e.GetSingleElementByTagNameOrNull(tagNames.PosMes)?.InnerValueOrNull(), out var posMes))
                    return (TimeSignature?)null;
                if (!int.TryParse(e.GetSingleElementByTagNameOrNull(tagNames.Nume)?.InnerValueOrNull(), out var nume))
                    return null;
                if (!int.TryParse(e.GetSingleElementByTagNameOrNull(tagNames.Denomi)?.InnerValueOrNull(), out var denomi))
                    return null;
                return new TimeSignature(posMes, nume, denomi);
            })
            .Where(t => t != null)
            .Select(t => t!)
            .ToList();
        if (rawTimeSignatures.Count == 0)
        {
            warnings.Add(new ImportWarning.TimeSignatureNotFound());
            rawTimeSignatures.Add(TimeSignature.Default);
        }

        long tickPrefix = GetTickPrefix(rawTimeSignatures, measurePrefix);
        var timeSignatures = rawTimeSignatures.Select(t => t with { MeasurePosition = t.MeasurePosition - measurePrefix }).ToList();
        int firstIndex = timeSignatures.FindLastIndex(t => t.MeasurePosition <= 0);
        for (int i = 0; i < firstIndex; i++)
        {
            warnings.Add(new ImportWarning.TimeSignatureIgnoredInPreMeasure(timeSignatures[0]));
            timeSignatures.RemoveAt(0);
        }

        timeSignatures[0] = timeSignatures[0] with { MeasurePosition = 0 };
        return (tickPrefix, timeSignatures);
    }

    private static long GetTickPrefix(IReadOnlyList<TimeSignature> timeSignatures, int measurePrefix)
    {
        var counter = new TickCounter();
        foreach (var ts in timeSignatures.Where(t => t.MeasurePosition < measurePrefix))
            counter.GoToMeasure(ts);
        counter.GoToMeasure(measurePrefix);
        return counter.Tick;
    }

    private static List<Tempo> ParseTempos(XmlElement masterTrack, TagNames tagNames, long tickPrefix, List<ImportWarning> warnings)
    {
        var tempos = masterTrack.GetElementListByTagName(tagNames.Tempo, allowEmpty: false)
            .Select(e =>
            {
                if (!long.TryParse(e.GetSingleElementByTagNameOrNull(tagNames.PosTick)?.InnerValueOrNull(), out var posTick))
                    return (Tempo?)null;
                var bpmStr = e.GetSingleElementByTagNameOrNull(tagNames.Bpm)?.InnerValueOrNull();
                if (!double.TryParse(bpmStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var bpm))
                    return null;
                return new Tempo(posTick - tickPrefix, bpm / BpmRate);
            })
            .Where(t => t != null)
            .Select(t => t!)
            .ToList();
        if (tempos.Count == 0)
        {
            warnings.Add(new ImportWarning.TempoNotFound());
            tempos.Add(Tempo.Default);
        }

        int firstIndex = tempos.FindLastIndex(t => t.TickPosition <= 0);
        for (int i = 0; i < firstIndex; i++)
        {
            warnings.Add(new ImportWarning.TempoIgnoredInPreMeasure(tempos[0]));
            tempos.RemoveAt(0);
        }

        tempos[0] = tempos[0] with { TickPosition = 0 };
        return tempos;
    }

    private static Track ParseTrack(XmlElement trackNode, int id, TagNames tagNames, long tickPrefix, ImportParams parms)
    {
        string trackName = trackNode.GetSingleElementByTagNameOrNull(tagNames.TrackName)?.InnerValueOrNull() ?? $"Track {id + 1}";
        var partNodes = trackNode.GetElementListByTagName(tagNames.MusicalPart);

        var noteWithOffsets = new List<(long TickOffset, XmlElement NoteNode)>();
        foreach (var partNode in partNodes)
        {
            long tickOffset = long.Parse(partNode.GetSingleElementByTagName(tagNames.PosTick).InnerValue()) - tickPrefix;
            foreach (var noteNode in partNode.GetElementListByTagName(tagNames.Note))
                noteWithOffsets.Add((tickOffset, noteNode));
        }

        var notes = noteWithOffsets.Select((pair, index) =>
        {
            var (tickOffset, noteNode) = pair;
            int key = int.Parse(noteNode.GetSingleElementByTagName(tagNames.NoteNum).InnerValue());
            long tickOn = long.Parse(noteNode.GetSingleElementByTagName(tagNames.PosTick).InnerValue());
            long length = long.Parse(noteNode.GetSingleElementByTagName(tagNames.Duration).InnerValue());
            string lyric = noteNode.GetSingleElementByTagNameOrNull(tagNames.Lyric)?.InnerValueOrNull() ?? parms.DefaultLyric;
            string? xSampa = noteNode.GetSingleElementByTagNameOrNull(tagNames.XSampa)?.InnerValueOrNull();
            return new Note(index, key, lyric, tickOn + tickOffset, tickOn + tickOffset + length, xSampa);
        }).ToList();

        Model.Pitch? pitch = null;
        if (!parms.SimpleImport)
        {
            var pitchByParts = partNodes.Select(partNode =>
            {
                long tickOffset = long.Parse(partNode.GetSingleElementByTagName(tagNames.PosTick).InnerValue()) - tickPrefix;
                var controlNodes = partNode.GetElementListByTagName(tagNames.MCtrl);
                var pbs = controlNodes
                    .Where(c => c.GetSingleElementByTagName(tagNames.Attr).GetAttribute(tagNames.Id) == tagNames.PbsName)
                    .Select(c => new VocaloidPartPitchData.Event(
                        long.Parse(c.GetSingleElementByTagName(tagNames.PosTick).InnerValue()),
                        int.Parse(c.GetSingleElementByTagName(tagNames.Attr).InnerValue())))
                    .ToList();
                var pit = controlNodes
                    .Where(c => c.GetSingleElementByTagName(tagNames.Attr).GetAttribute(tagNames.Id) == tagNames.PitName)
                    .Select(c => new VocaloidPartPitchData.Event(
                        long.Parse(c.GetSingleElementByTagName(tagNames.PosTick).InnerValue()),
                        int.Parse(c.GetSingleElementByTagName(tagNames.Attr).InnerValue())))
                    .ToList();
                return new VocaloidPartPitchData(tickOffset, pit, pbs);
            }).ToList();
            pitch = VocaloidPitchConversion.PitchFromVocaloidParts(pitchByParts);
        }

        return new Track(id, trackName, notes, pitch).ValidateNotes();
    }

    public static ExportResult Generate(Model.Project project, IReadOnlyList<FeatureConfig> features)
    {
        var document = GenerateContent(project, features);
        var settings = new XmlWriterSettings { Indent = false, OmitXmlDeclaration = false };
        var builder = new StringBuilder();
        using (var writer = XmlWriter.Create(builder, settings))
            document.Save(writer);
        var content = builder.ToString().Replace(" xmlns=\"\"", "");

        var notifications = new List<ExportNotification>();
        if (!project.HasXSampaData)
            notifications.Add(ExportNotification.PhonemeResetRequiredV4);
        if (features.Contains(Feature.ConvertPitch))
            notifications.Add(ExportNotification.PitchDataExported);

        return new ExportResult(Encoding.UTF8.GetBytes(content), FormatRegistry.Get(Format.Vsqx).GetFileName(project.Name), notifications);
    }

    private static XmlDocument GenerateContent(Model.Project project, IReadOnlyList<FeatureConfig> features)
    {
        var tagNames = TagNames.Vsq4;
        var document = new XmlDocument();
        document.LoadXml(Resources.VsqxTemplate);
        var root = document.DocumentElement!;

        var mixer = root.GetSingleElementByTagName(tagNames.Mixer);
        var masterTrack = root.GetSingleElementByTagName(tagNames.MasterTrack);

        int measurePrefix = Math.Max(project.MeasurePrefix, MinMeasureOffset);
        masterTrack.SetSingleChildValue(tagNames.PreMeasure, measurePrefix);
        long tickPrefix = project.TimeSignatures[0].TicksInMeasure * (long)measurePrefix;

        SetupTempoNodes(masterTrack, tagNames, project.Tempos, tickPrefix);
        SetupTimeSignatureNodes(masterTrack, tagNames, project.TimeSignatures, measurePrefix);

        var emptyTrack = root.GetSingleElementByTagName(tagNames.VsTrack);
        var emptyUnit = mixer.GetSingleElementByTagName(tagNames.VsUnit);
        var track = emptyTrack;
        var unit = emptyUnit;
        for (int trackIndex = 0; trackIndex < project.Tracks.Count; trackIndex++)
        {
            var newTrack = GenerateNewTrackNode(emptyTrack, tagNames, trackIndex, project, tickPrefix, document, features);
            track.InsertAfterThis(newTrack);
            track = newTrack;

            var newUnit = emptyUnit.CloneElement();
            newUnit.SetSingleChildValue(tagNames.TrackNum, trackIndex);
            unit.InsertAfterThis(newUnit);
            unit = newUnit;
        }

        root.RemoveChild(emptyTrack);
        mixer.RemoveChild(emptyUnit);
        return document;
    }

    private static void SetupTempoNodes(XmlElement masterTrack, TagNames tagNames, IReadOnlyList<Tempo> models, long tickPrefix)
    {
        var empty = masterTrack.GetSingleElementByTagName(tagNames.Tempo);
        var previous = empty;
        previous.SetSingleChildValue(tagNames.Bpm, (int)(models[0].Bpm * BpmRate));
        foreach (var model in models.Skip(1))
        {
            long tickPosition = model.TickPosition == 0L ? 0L : model.TickPosition + tickPrefix;
            var newNode = empty.CloneElement();
            newNode.SetSingleChildValue(tagNames.PosTick, tickPosition);
            newNode.SetSingleChildValue(tagNames.Bpm, (int)(model.Bpm * BpmRate));
            previous.InsertAfterThis(newNode);
            previous = newNode;
        }
    }

    private static void SetupTimeSignatureNodes(XmlElement masterTrack, TagNames tagNames, IReadOnlyList<TimeSignature> models, int measurePrefix)
    {
        var empty = masterTrack.GetSingleElementByTagName(tagNames.TimeSig);
        var previous = empty;
        previous.SetSingleChildValue(tagNames.Nume, models[0].Numerator);
        previous.SetSingleChildValue(tagNames.Denomi, models[0].Denominator);
        foreach (var model in models.Skip(1))
        {
            int measurePosition = model.MeasurePosition == 0 ? 0 : model.MeasurePosition + measurePrefix;
            var newNode = empty.CloneElement();
            newNode.SetSingleChildValue(tagNames.PosMes, measurePosition);
            newNode.SetSingleChildValue(tagNames.Nume, model.Numerator);
            newNode.SetSingleChildValue(tagNames.Denomi, model.Denominator);
            previous.InsertAfterThis(newNode);
            previous = newNode;
        }
    }

    private static XmlElement GenerateNewTrackNode(XmlElement emptyTrack, TagNames tagNames, int trackIndex, Model.Project project, long tickPrefix, XmlDocument document, IReadOnlyList<FeatureConfig> features)
    {
        var trackModel = project.Tracks[trackIndex];
        var newTrack = emptyTrack.CloneElement();
        newTrack.SetSingleChildValue(tagNames.TrackNum, trackIndex);
        newTrack.SetSingleChildValue(tagNames.TrackName, trackModel.Name);

        var part = newTrack.GetSingleElementByTagName(tagNames.MusicalPart);
        part.SetSingleChildValue(tagNames.PosTick, tickPrefix);
        part.SetSingleChildValue(tagNames.PlayTime, trackModel.Notes.Count > 0 ? trackModel.Notes[^1].TickOff : 0);

        SetupPitchControllingNodes(features.Contains(Feature.ConvertPitch), part, trackModel, tagNames);

        var emptyNote = part.GetSingleElementByTagName(tagNames.Note);
        var note = emptyNote;
        foreach (var model in trackModel.Notes)
        {
            var newNote = GenerateNewNote(emptyNote, tagNames, model, document);
            note.InsertAfterThis(newNote);
            note = newNote;
        }

        part.RemoveChild(emptyNote);
        if (trackModel.Notes.Count == 0)
            newTrack.RemoveChild(part);
        return newTrack;
    }

    private static XmlElement GenerateNewNote(XmlElement emptyNote, TagNames tagNames, Note model, XmlDocument document)
    {
        var newNote = emptyNote.CloneElement();
        newNote.SetSingleChildValue(tagNames.PosTick, model.TickOn);
        newNote.SetSingleChildValue(tagNames.Duration, model.Length);
        newNote.SetSingleChildValue(tagNames.NoteNum, model.Key);
        var lyricNode = newNote.GetSingleElementByTagName(tagNames.Lyric);
        lyricNode.Clear();
        lyricNode.AppendChild(document.CreateCDataSection(model.Lyric));
        if (!string.IsNullOrEmpty(model.Phoneme))
        {
            var xSampaNode = newNote.GetSingleElementByTagName(tagNames.XSampa);
            xSampaNode.Clear();
            xSampaNode.AppendChild(document.CreateCDataSection(model.Phoneme));
            xSampaNode.SetAttribute(tagNames.XSampaLock, "1");
        }

        return newNote;
    }

    private static void SetupPitchControllingNodes(bool convert, XmlElement part, Track trackModel, TagNames tagNames)
    {
        var emptyControl = part.GetSingleElementByTagName(tagNames.MCtrl);
        var pitchRawData = trackModel.Pitch?.GenerateForVocaloid(trackModel.Notes);
        if (!convert || pitchRawData == null)
        {
            part.RemoveChild(emptyControl);
            return;
        }

        var eventsWithName = pitchRawData.Pbs.Select(e => (Event: e, Name: tagNames.PbsName))
            .Concat(pitchRawData.Pit.OrderBy(e => e.Pos).Select(e => (Event: e, Name: tagNames.PitName)))
            .ToList();

        var currentElement = emptyControl;
        foreach (var (ev, name) in eventsWithName)
        {
            var newControlNode = emptyControl.CloneElement();
            newControlNode.SetSingleChildValue(tagNames.PosTick, ev.Pos);
            newControlNode.GetSingleElementByTagName(tagNames.Attr).SetAttribute(tagNames.Id, name);
            newControlNode.SetSingleChildValue(tagNames.Attr, ev.Value);
            currentElement.InsertAfterThis(newControlNode);
            currentElement = newControlNode;
        }

        part.RemoveChild(emptyControl);
    }

    private sealed class TagNames
    {
        public string MasterTrack = "masterTrack";
        public string PreMeasure = "preMeasure";
        public string TimeSig = "timeSig";
        public string PosMes = "posMes";
        public string Nume = "nume";
        public string Denomi = "denomi";
        public string Tempo = "tempo";
        public string PosTick = "posTick";
        public string Bpm = "bpm";
        public string VsTrack = "vsTrack";
        public string TrackName = "trackName";
        public string MusicalPart = "musicalPart";
        public string Note = "note";
        public string Duration = "durTick";
        public string NoteNum = "noteNum";
        public string Lyric = "lyric";
        public string XSampa = "phnms";
        public string XSampaLock = "lock";
        public string Mixer = "mixer";
        public string VsUnit = "vsUnit";
        public string TrackNum = "vsTrackNo";
        public string PlayTime = "playTime";
        public string MCtrl = "mCtrl";
        public string Attr = "attr";
        public string Id = "id";
        public string PbsName = "PBS";
        public string PitName = "PIT";

        public static readonly TagNames Vsq3 = new();

        public static readonly TagNames Vsq4 = new()
        {
            PosMes = "m",
            Nume = "nu",
            Denomi = "de",
            PosTick = "t",
            Bpm = "v",
            TrackName = "name",
            MusicalPart = "vsPart",
            Duration = "dur",
            NoteNum = "n",
            Lyric = "y",
            TrackNum = "tNo",
            XSampa = "p",
            MCtrl = "cc",
            Attr = "v",
            PbsName = "S",
            PitName = "P",
        };
    }
}
