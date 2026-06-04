using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Model;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.MusicXml;

public sealed class MusicXmlGenerator
{
    private const double TickRate = 2.0;
    private static readonly int TicksInBeat = (int)Math.Round(Constants.TicksInBeat * TickRate);

    public XElement GenerateProject(Project project)
    {
        var singingTracks = project.TrackList.OfType<SingingTrack>()
            .Select(ScaleTrack).ToList();
        var scaledTempos = project.SongTempoList
            .Select(t => new SongTempo((int)(t.Position * TickRate), t.Bpm)).ToList();

        var partList = new XElement("part-list");
        var root = new XElement("score-partwise",
            new XAttribute("version", "4.0"),
            new XElement("identification",
                new XElement("encoding", new XElement("software", "LibreSVIP"))),
            partList);

        for (int i = 0; i < singingTracks.Count; i++)
        {
            var track = singingTracks[i];
            var keyTicks = GetKeyTicks(i, track, scaledTempos);
            if (keyTicks.Count == 0)
                continue;
            var measures = GetMeasures(keyTicks, project.TimeSignatureList);
            string partId = $"P{i + 1}";
            partList.Add(new XElement("score-part",
                new XAttribute("id", partId),
                new XElement("part-name", string.IsNullOrEmpty(track.Title) ? $"Track {i + 1}" : track.Title)));
            root.Add(GeneratePart(measures, i, partId));
        }
        return root;
    }

    private static SingingTrack ScaleTrack(SingingTrack track) => new()
    {
        Title = track.Title,
        NoteList = track.NoteList.Select(n => new Note
        {
            StartPos = (int)(n.StartPos * TickRate),
            Length = (int)(n.Length * TickRate),
            KeyNumber = n.KeyNumber,
            Lyric = n.Lyric,
            Pronunciation = n.Pronunciation,
        }).ToList(),
    };

    private static List<KeyTick> GetKeyTicks(int trackIndex, SingingTrack track, List<SongTempo> tempos)
    {
        var result = new List<KeyTick>();
        result.AddRange(track.NoteList.Select(n => new KeyTick { Tick = n.EndPos, NoteEnd = n }));
        if (trackIndex == 0)
            result.AddRange(tempos.Select(t => new KeyTick { Tick = t.Position, Tempo = t }));
        result.AddRange(track.NoteList.Select(n => new KeyTick { Tick = n.StartPos, NoteStart = n }));
        return result.OrderBy(k => k.Tick).ToList();
    }

    private static List<MXmlMeasure> GetMeasures(List<KeyTick> keyTicks, List<TimeSignature> timeSignatures)
    {
        var measureBorderTicks = new List<int> { 0 };
        double measure = 0.0;
        double tick = 0.0;
        var prevTs = new TimeSignature();
        int ticksInMeasure = (int)Math.Round(prevTs.BarLength(TicksInBeat));
        foreach (var ts in timeSignatures)
        {
            int previousMeasure = (int)measure;
            ticksInMeasure = (int)Math.Round(prevTs.BarLength(TicksInBeat));
            tick += ticksInMeasure * (ts.BarIndex - measure);
            measure = ts.BarIndex;
            int currentMeasure = (int)measure;
            for (int j = 0; j < currentMeasure - previousMeasure; j++)
                measureBorderTicks.Add(measureBorderTicks[^1] + ticksInMeasure);
            prevTs = ts;
        }
        int lastTick = keyTicks[^1].Tick;
        ticksInMeasure = (int)Math.Round(prevTs.BarLength(TicksInBeat));
        if (lastTick >= tick + ticksInMeasure)
        {
            int previousMeasure = (int)measure;
            double tickDiff = lastTick - tick;
            measure += tickDiff / ticksInMeasure;
            int currentMeasure = (int)measure;
            for (int j = 0; j < currentMeasure - previousMeasure; j++)
                measureBorderTicks.Add(measureBorderTicks[^1] + ticksInMeasure);
        }
        measureBorderTicks.Add(measureBorderTicks[^1] + (int)Math.Round(prevTs.BarLength(TicksInBeat)));

        var measures = new List<MXmlMeasure>();
        KeyTick? ongoingNote = null;
        int ongoingHead = 0;
        for (int b = 0; b < measureBorderTicks.Count - 1; b++)
        {
            int start = measureBorderTicks[b];
            int end = measureBorderTicks[b + 1];
            var group = keyTicks.Where(k => k.NoteEnd != null
                ? start < k.Tick && k.Tick <= end
                : start <= k.Tick && k.Tick < end).ToList();

            int currentInMeasure = 0;
            var contents = new List<MXmlMeasureContent>();
            foreach (var keyTick in group)
            {
                int relative = keyTick.Tick - start;
                if (relative > currentInMeasure)
                {
                    if (ongoingNote == null)
                        contents.Add(MXmlMeasureContent.WithRest(relative - currentInMeasure));
                    currentInMeasure = relative;
                }
                if (keyTick.Tempo != null)
                {
                    if (ongoingNote != null)
                    {
                        contents.Add(MXmlMeasureContent.WithNote(keyTick.Tick - ongoingHead, ongoingNote.NoteStart!,
                            ongoingNote.NoteStart!.StartPos == ongoingHead ? MXmlNoteType.Begin : MXmlNoteType.Middle));
                        ongoingHead = keyTick.Tick;
                    }
                    contents.Add(MXmlMeasureContent.WithTempo(keyTick.Tempo.Bpm));
                }
                else if (keyTick.NoteStart != null)
                {
                    ongoingNote = keyTick;
                    ongoingHead = keyTick.Tick;
                }
                else if (keyTick.NoteEnd != null && ongoingNote != null)
                {
                    contents.Add(MXmlMeasureContent.WithNote(keyTick.NoteEnd.EndPos - ongoingHead, keyTick.NoteEnd,
                        ongoingNote.NoteStart!.StartPos == ongoingHead ? MXmlNoteType.Single : MXmlNoteType.End));
                    ongoingNote = null;
                }
            }
            int restLength = end - start - currentInMeasure;
            if (restLength > 0)
            {
                if (ongoingNote == null)
                {
                    contents.Add(MXmlMeasureContent.WithRest(restLength));
                }
                else
                {
                    contents.Add(MXmlMeasureContent.WithNote(end - ongoingHead, ongoingNote.NoteStart!,
                        ongoingNote.NoteStart!.StartPos == ongoingHead ? MXmlNoteType.Begin : MXmlNoteType.Middle));
                    ongoingHead = end;
                }
            }
            measures.Add(new MXmlMeasure
            {
                TickStart = start,
                Length = end - start,
                TimeSignature = timeSignatures.FirstOrDefault(ts => ts.BarIndex == b),
                Contents = contents,
            });
        }
        return measures;
    }

    private static XElement GeneratePart(List<MXmlMeasure> measures, int trackIndex, string partId)
    {
        var partNode = new XElement("part", new XAttribute("id", partId));
        XElement? prevNote = null;
        bool prevIsSlur = false;
        int slurNumber = 1;
        for (int i = 0; i < measures.Count; i++)
        {
            slurNumber = 1;
            var measure = measures[i];
            var measureNode = new XElement("measure", new XAttribute("number", (i + 1).ToString(CultureInfo.InvariantCulture)));
            var attributes = new XElement("attributes",
                new XElement("divisions", TicksInBeat.ToString(CultureInfo.InvariantCulture)));
            if (measure.TimeSignature != null)
                attributes.Add(new XElement("time",
                    new XElement("beats", measure.TimeSignature.Numerator),
                    new XElement("beat-type", measure.TimeSignature.Denominator)));
            measureNode.Add(attributes);

            foreach (var content in measure.Contents)
            {
                if (content.Bpm != null)
                {
                    measureNode.Add(GenerateTempoNode(content.Bpm.Value));
                }
                else if (content.NoteType != null && content.Note != null)
                {
                    bool isSlur = content.Note.Lyric == "-";
                    if (prevNote != null)
                    {
                        if (isSlur && !prevIsSlur)
                            AddSlur(prevNote, "start", slurNumber);
                        else if (prevIsSlur)
                        {
                            int prevSlurNum = ExistingSlurNumber(prevNote) ?? slurNumber;
                            if (isSlur)
                                AddSlur(prevNote, "continue", prevSlurNum);
                            else
                            {
                                AddSlur(prevNote, "stop", prevSlurNum);
                                slurNumber++;
                            }
                        }
                    }
                    var note = GenerateNoteNode(content, trackIndex);
                    if (note != null)
                    {
                        measureNode.Add(note);
                        prevNote = note;
                        prevIsSlur = isSlur;
                    }
                }
                else
                {
                    if (prevNote != null && prevIsSlur)
                    {
                        AddSlur(prevNote, "stop", ExistingSlurNumber(prevNote) ?? slurNumber);
                        prevNote = null;
                        prevIsSlur = false;
                    }
                    measureNode.Add(GenerateRestNode(content));
                }
            }
            partNode.Add(measureNode);
        }
        if (prevNote != null && prevIsSlur)
            AddSlur(prevNote, "stop", ExistingSlurNumber(prevNote) ?? slurNumber);
        return partNode;
    }

    private static XElement GenerateTempoNode(double bpm) =>
        new("direction",
            new XElement("direction-type",
                new XElement("metronome",
                    new XElement("beat-unit", "quarter"),
                    new XElement("per-minute", bpm.ToString(CultureInfo.InvariantCulture)))),
            new XElement("sound", new XAttribute("tempo", bpm.ToString(CultureInfo.InvariantCulture))));

    private static XElement GenerateRestNode(MXmlMeasureContent content) =>
        new("note",
            new XElement("rest"),
            new XElement("duration", content.Duration.ToString(CultureInfo.InvariantCulture)));

    private static XElement? GenerateNoteNode(MXmlMeasureContent content, int trackIndex)
    {
        if (content.Note == null || content.NoteType == null)
            return null;
        string keyStr = MusicMath.Midi2Note(content.Note.KeyNumber);
        var octaveMatch = System.Text.RegularExpressions.Regex.Match(keyStr, @"\d+$");
        var stepMatch = System.Text.RegularExpressions.Regex.Match(keyStr, @"^[A-G]");
        if (!octaveMatch.Success || !stepMatch.Success)
            return null;
        bool sharp = keyStr.Contains('#');

        var pitch = new XElement("pitch", new XElement("step", stepMatch.Value));
        if (sharp)
            pitch.Add(new XElement("alter", "1"));
        pitch.Add(new XElement("octave", octaveMatch.Value));

        var noteNode = new XElement("note", pitch,
            new XElement("duration", content.Duration.ToString(CultureInfo.InvariantCulture)));

        string? tieType = content.NoteType switch
        {
            MXmlNoteType.Begin => "start",
            MXmlNoteType.End => "stop",
            _ => null,
        };
        if (tieType != null)
        {
            noteNode.Add(new XElement("tie", new XAttribute("type", tieType)));
            noteNode.Add(new XElement("notations", new XElement("tied", new XAttribute("type", tieType))));
        }
        noteNode.Add(new XElement("voice", (trackIndex + 2).ToString(CultureInfo.InvariantCulture)));
        noteNode.Add(new XElement("lyric",
            new XElement("syllabic", content.NoteType.ToString()!.ToLowerInvariant()),
            new XElement("text", content.Note.Lyric)));
        return noteNode;
    }

    private static void AddSlur(XElement noteNode, string type, int number)
    {
        var notations = noteNode.Elements().FirstOrDefault(e => e.Name.LocalName == "notations");
        if (notations == null)
        {
            notations = new XElement("notations");
            noteNode.Add(notations);
        }
        notations.Add(new XElement("slur", new XAttribute("type", type), new XAttribute("number", number.ToString(CultureInfo.InvariantCulture))));
    }

    private static int? ExistingSlurNumber(XElement noteNode)
    {
        var slur = noteNode.Descendants().FirstOrDefault(e => e.Name.LocalName == "slur");
        if (slur?.Attribute("number")?.Value is { } v && int.TryParse(v, out int n))
            return n;
        return null;
    }
}
