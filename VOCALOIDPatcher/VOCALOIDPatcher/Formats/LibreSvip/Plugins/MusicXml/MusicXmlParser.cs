using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Model;
using VOCALOIDPatcher.Formats.LibreSvip.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.MusicXml;

public sealed class MusicXmlParser
{
    private readonly bool _importTempo;

    public MusicXmlParser(bool importTempo) => _importTempo = importTempo;

    public Project ParseProject(XElement scoreRoot)
    {
        var parts = scoreRoot.Children("part").ToList();
        var master = parts.FirstOrDefault(p => p.Children("measure").Any())
            ?? throw new InvalidOperationException("MusicXML 没有小节");
        int divisions = ReadDivisions(master);
        decimal rate = (decimal)Constants.TicksInBeat / divisions;

        var timeSignatures = ParseTimeSignatures(master);
        if (timeSignatures.Count == 0)
            timeSignatures.Add(new TimeSignature());
        var tempos = _importTempo ? ParseTempos(master, divisions) : new List<SongTempo>();
        if (tempos.Count == 0)
            tempos.Add(new SongTempo());

        var partNames = ParsePartNames(scoreRoot);
        var tracks = new List<Track>();
        for (int i = 0; i < parts.Count; i++)
        {
            string? id = parts[i].Attribute("id")?.Value;
            string name = id != null && partNames.TryGetValue(id, out var n) && !string.IsNullOrEmpty(n)
                ? n
                : $"Track {i + 1}";
            tracks.Add(ParseTrack(parts[i], name, rate));
        }

        return new Project
        {
            TrackList = tracks,
            TimeSignatureList = timeSignatures,
            SongTempoList = tempos,
        };
    }

    private static int ReadDivisions(XElement part)
    {
        foreach (var measure in part.Children("measure"))
        {
            var divText = measure.Child("attributes")?.ChildText("divisions");
            if (divText != null && int.TryParse(divText.Trim(), out int d) && d > 0)
                return d;
        }
        return Constants.TicksInBeat;
    }

    private static List<TimeSignature> ParseTimeSignatures(XElement part)
    {
        var result = new List<TimeSignature>();
        int index = 0;
        foreach (var measure in part.Children("measure"))
        {
            var time = measure.Child("attributes")?.Child("time");
            if (time != null)
            {
                string? beats = time.ChildText("beats");
                string? beatType = time.ChildText("beat-type");
                if (int.TryParse(beats, out int num) && int.TryParse(beatType, out int den))
                {
                    int barIndex = int.TryParse(time.Attribute("number")?.Value, out int bn) ? bn : index;
                    result.Add(new TimeSignature(barIndex, num, den));
                }
            }
            index++;
        }
        return result;
    }

    private List<SongTempo> ParseTempos(XElement part, int divisions)
    {
        var tempos = new List<SongTempo>();
        decimal rate = (decimal)Constants.TicksInBeat / divisions;
        int measureStart = 0;
        var currentTs = new TimeSignature();
        foreach (var measure in part.Children("measure"))
        {
            int cursor = 0;
            var time = measure.Child("attributes")?.Child("time");
            if (time != null && int.TryParse(time.ChildText("beats"), out int num) && int.TryParse(time.ChildText("beat-type"), out int den))
                currentTs = new TimeSignature(0, num, den);
            foreach (var child in measure.Elements())
            {
                string tag = child.Name.LocalName;
                if (tag == "note")
                {
                    bool isChord = child.Child("chord") != null;
                    bool isGrace = child.Child("grace") != null;
                    if (!isChord && !isGrace)
                        cursor += (int)(Dec(child.ChildText("duration")) * rate);
                }
                else if (tag == "backup")
                {
                    cursor -= (int)(Dec(child.ChildText("duration")) * rate);
                }
                else if (tag == "forward")
                {
                    cursor += (int)(Dec(child.ChildText("duration")) * rate);
                }
                else if (tag == "direction")
                {
                    int offset = (int)(Dec(child.ChildText("offset")) * rate);
                    string? tempo = child.Child("sound")?.Attribute("tempo")?.Value;
                    if (tempo != null && double.TryParse(tempo, NumberStyles.Float, CultureInfo.InvariantCulture, out double bpm))
                        tempos.Add(new SongTempo(measureStart + cursor + offset, bpm));
                }
                else if (tag == "sound")
                {
                    string? tempo = child.Attribute("tempo")?.Value;
                    if (tempo != null && double.TryParse(tempo, NumberStyles.Float, CultureInfo.InvariantCulture, out double bpm))
                        tempos.Add(new SongTempo(measureStart + cursor, bpm));
                }
            }
            measureStart += (int)Math.Round(currentTs.BarLength());
        }
        tempos.Sort((a, b) => a.Position.CompareTo(b.Position));
        var deduped = new List<SongTempo>();
        foreach (var t in tempos)
        {
            if (deduped.Count > 0 && deduped[^1].Position == t.Position)
                deduped[^1] = t;
            else
                deduped.Add(t);
        }
        return deduped;
    }

    private static Dictionary<string, string> ParsePartNames(XElement scoreRoot)
    {
        var result = new Dictionary<string, string>();
        var partList = scoreRoot.Child("part-list");
        if (partList == null)
            return result;
        foreach (var scorePart in partList.Children("score-part"))
        {
            string? id = scorePart.Attribute("id")?.Value;
            string? name = scorePart.ChildText("part-name");
            if (id != null && name != null)
                result[id] = name;
        }
        return result;
    }

    private static Track ParseTrack(XElement part, string trackName, decimal rate)
    {
        var notes = new List<Note>();
        bool isInsideNote = false;
        int tickPosition = 0;
        int previousTickPosition = 0;
        Note? incompleteLyricNote = null;

        foreach (var measure in part.Children("measure"))
        {
            foreach (var node in measure.Elements())
            {
                string tag = node.Name.LocalName;
                if (tag == "backup")
                {
                    int dur = (int)(Dec(node.ChildText("duration")) * rate);
                    tickPosition -= dur;
                    previousTickPosition = tickPosition;
                    continue;
                }
                if (tag == "forward")
                {
                    int dur = (int)(Dec(node.ChildText("duration")) * rate);
                    tickPosition += dur;
                    previousTickPosition = tickPosition;
                    continue;
                }
                if (tag != "note")
                    continue;

                string? durText = node.ChildText("duration");
                if (durText == null)
                {
                    if (node.Child("grace") != null)
                        continue;
                    continue;
                }
                int duration = (int)(Dec(durText) * rate);
                if (duration <= 0)
                    continue;

                if (node.Child("rest") != null)
                {
                    tickPosition += duration;
                    continue;
                }

                var pitch = node.Child("pitch");
                if (pitch == null)
                    continue;
                string step = pitch.ChildText("step") ?? "C";
                string octave = pitch.ChildText("octave") ?? "4";
                int alter = int.TryParse(pitch.ChildText("alter"), out int a) ? a : 0;
                int key = MusicMath.Note2Midi($"{step}{octave}") + alter;

                bool isSlurContinuation = node.Children("notations")
                    .SelectMany(nt => nt.Children("slur"))
                    .Any(s => s.Attribute("type")?.Value is "continue" or "stop");
                string lyric;
                var lyricNode = node.Child("lyric");
                if (isSlurContinuation)
                    lyric = "-";
                else if (lyricNode?.ChildText("text") is { Length: > 0 } lyricText)
                    lyric = lyricText;
                else
                    lyric = Constants.DefaultPhoneme;

                if (node.Child("chord") != null)
                    tickPosition = previousTickPosition;

                if (!isInsideNote)
                {
                    var note = new Note { KeyNumber = key, Lyric = lyric, StartPos = tickPosition, Length = duration };
                    if (lyricNode != null)
                    {
                        string syllabic = lyricNode.ChildText("syllabic") ?? "single";
                        if (syllabic == "begin")
                            incompleteLyricNote = note;
                        else if (syllabic == "end" && incompleteLyricNote != null)
                        {
                            incompleteLyricNote.Lyric += lyric;
                            incompleteLyricNote = null;
                            note.Lyric = "+";
                        }
                        else if (syllabic == "middle" && incompleteLyricNote != null)
                        {
                            incompleteLyricNote.Lyric += lyric;
                            note.Lyric = "+";
                        }
                    }
                    notes.Add(note);
                }
                else
                {
                    notes[^1].Length += duration;
                }

                previousTickPosition = tickPosition;
                tickPosition += duration;

                string? tieType = node.Child("tie")?.Attribute("type")?.Value;
                if (tieType == "start")
                    isInsideNote = true;
                else if (tieType == "stop")
                    isInsideNote = false;
            }
        }

        notes = notes.OrderBy(n => n.StartPos).ThenBy(n => n.KeyNumber).ToList();
        return new SingingTrack { Title = trackName, NoteList = notes };
    }

    private static decimal Dec(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;
        return decimal.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out decimal d) ? d : 0;
    }
}
