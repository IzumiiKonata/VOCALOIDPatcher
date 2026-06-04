using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Framework;
using VOCALOIDPatcher.Formats.LibreSvip.Model;
using VOCALOIDPatcher.Formats.LibreSvip.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Vsqx;

public sealed class VsqxConverter : FormatConverter
{
    private const double BpmRate = 100.0;
    private static readonly XNamespace Ns = "http://www.yamaha.co.jp/vocaloid/schema/vsq4/";

    public bool ImportInstrumental { get; set; } = true;

    public override bool CanLoad => true;
    public override bool CanDump => true;

    public override Project Load(byte[] content)
    {
        var root = LoadXml(TextHelper.DetectAndDecode(content));
        if (root.Name.LocalName != "vsq4")
            throw new InvalidDataException("仅支持 VOCALOID4 (vsq4) 的 vsqx");

        var master = root.Child("masterTrack") ?? throw new InvalidDataException("缺少 masterTrack");
        int preMeasure = int.TryParse(master.ChildText("preMeasure"), out int pm) ? pm : 1;

        var rawTimeSignatures = master.Children("timeSig").Select(ts => new TimeSignature(
            ParseInt(ts.ChildText("m")), ParseInt(ts.ChildText("nu"), 4), ParseInt(ts.ChildText("de"), 4))).ToList();
        if (rawTimeSignatures.Count == 0)
            rawTimeSignatures.Add(new TimeSignature());
        var (tickPrefix, timeSignatures) = ParseTimeSignatures(rawTimeSignatures, preMeasure);

        var rawTempos = master.Children("tempo").Select(t => new SongTempo(
            ParseInt(t.ChildText("t")), ParseInt(t.ChildText("v")) / BpmRate)).ToList();
        if (rawTempos.Count == 0)
            rawTempos.Add(new SongTempo(0, Constants.DefaultBpm));
        var tempos = TickCounter.SkipTempoList(rawTempos, tickPrefix);

        var muteSolo = new Dictionary<int, (bool Mute, bool Solo)>();
        var mixer = root.Child("mixer");
        if (mixer != null)
            foreach (var unit in mixer.Children("vsUnit"))
                muteSolo[ParseInt(unit.ChildText("tNo"))] = (ParseInt(unit.ChildText("m")) != 0, ParseInt(unit.ChildText("s")) != 0);

        var trackList = new List<Track>();
        foreach (var vsTrack in root.Children("vsTrack"))
        {
            int tNo = ParseInt(vsTrack.ChildText("tNo"));
            var singing = new SingingTrack { Title = vsTrack.ChildText("name") ?? $"Track {tNo + 1}" };
            if (muteSolo.TryGetValue(tNo, out var ms))
            {
                singing.Mute = ms.Mute;
                singing.Solo = ms.Solo;
            }
            foreach (var part in vsTrack.Children("vsPart"))
            {
                int partPos = ParseInt(part.ChildText("t"));
                int offset = partPos - tickPrefix;
                foreach (var note in part.Children("note"))
                {
                    var phnms = note.Child("p")?.Value;
                    if (phnms is "Asp" or "Sil" or "?")
                        continue;
                    string lyric = (note.ChildText("y") ?? Constants.DefaultEnglishLyric).ToLowerInvariant();
                    singing.NoteList.Add(new Note
                    {
                        StartPos = ParseInt(note.ChildText("t")) + offset,
                        Length = ParseInt(note.ChildText("dur")),
                        KeyNumber = ParseInt(note.ChildText("n"), 60),
                        Lyric = lyric,
                    });
                }
            }
            trackList.Add(singing);
        }

        return new Project
        {
            SongTempoList = tempos,
            TimeSignatureList = timeSignatures,
            TrackList = trackList,
        };
    }

    private static (int, List<TimeSignature>) ParseTimeSignatures(List<TimeSignature> tsList, int measurePrefix)
    {
        int tickPrefix = 0;
        int measure = 0;
        foreach (var ts in tsList)
        {
            int measureDiff = ts.BarIndex - measure;
            tickPrefix += measureDiff * (int)Math.Round(ts.BarLength());
            measure += ts.BarIndex;
        }
        tickPrefix += (measurePrefix - measure) * (int)Math.Round(tsList[^1].BarLength());
        return (tickPrefix, TickCounter.SkipBeatList(tsList, measurePrefix));
    }

    public override byte[] Dump(Project project)
    {
        int firstBarLength = (int)Math.Round(project.TimeSignatureList[0].BarLength());
        int tickPrefix = firstBarLength;

        var master = new XElement(Ns + "masterTrack",
            CData("seqName", "Untitled0"),
            CData("comment", "New VSQ File"),
            new XElement(Ns + "resolution", 480),
            new XElement(Ns + "preMeasure", 1));
        foreach (var ts in TickCounter.ShiftBeatList(project.TimeSignatureList, 1))
            master.Add(new XElement(Ns + "timeSig",
                new XElement(Ns + "m", ts.BarIndex),
                new XElement(Ns + "nu", ts.Numerator),
                new XElement(Ns + "de", ts.Denominator)));
        foreach (var tempo in TickCounter.SkipTempoList(project.SongTempoList, tickPrefix))
            master.Add(new XElement(Ns + "tempo",
                new XElement(Ns + "t", tempo.Position),
                new XElement(Ns + "v", (int)Math.Round(tempo.Bpm * BpmRate))));

        var mixer = new XElement(Ns + "mixer",
            new XElement(Ns + "masterUnit",
                new XElement(Ns + "oGin", 0), new XElement(Ns + "rLvl", 0), new XElement(Ns + "vol", 0)));

        var root = new XElement(Ns + "vsq4",
            CData("vender", "Yamaha Corporation"),
            CData("version", "4.0.0.3"),
            new XElement(Ns + "vVoiceTable",
                new XElement(Ns + "vVoice",
                    new XElement(Ns + "bs", 0), new XElement(Ns + "pc", 0),
                    CData("id", "BCNHC6KMM5RTC5GB"), CData("name", "singer"))));

        var singingTracks = project.TrackList.OfType<SingingTrack>().ToList();
        for (int i = 0; i < singingTracks.Count; i++)
        {
            var track = singingTracks[i];
            mixer.Add(new XElement(Ns + "vsUnit",
                new XElement(Ns + "tNo", i),
                new XElement(Ns + "iGin", 0),
                new XElement(Ns + "m", track.Mute ? 1 : 0),
                new XElement(Ns + "s", track.Solo ? 1 : 0),
                new XElement(Ns + "pan", 64),
                new XElement(Ns + "vol", 0)));
            var vsTrack = new XElement(Ns + "vsTrack",
                new XElement(Ns + "tNo", i),
                CData("name", track.Title),
                CData("comment", "Track"));
            if (track.NoteList.Count > 0)
            {
                var part = new XElement(Ns + "vsPart",
                    new XElement(Ns + "t", tickPrefix),
                    new XElement(Ns + "playTime", track.NoteList[^1].EndPos),
                    CData("name", "New Part"),
                    CData("comment", "New Musical Part"),
                    new XElement(Ns + "singer", new XElement(Ns + "t", 0), new XElement(Ns + "bs", 0), new XElement(Ns + "pc", 0)));
                foreach (var note in track.NoteList)
                {
                    var noteNode = new XElement(Ns + "note",
                        new XElement(Ns + "t", note.StartPos),
                        new XElement(Ns + "dur", note.Length),
                        new XElement(Ns + "n", Math.Clamp(note.KeyNumber, 0, 127)),
                        new XElement(Ns + "v", 64),
                        CData("y", note.Lyric));
                    if (!string.IsNullOrEmpty(note.Pronunciation))
                        noteNode.Add(CData("p", note.Pronunciation));
                    part.Add(noteNode);
                }
                vsTrack.Add(part);
            }
            root.Add(vsTrack);
        }

        root.Add(mixer);
        root.Add(master);

        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", "no"), root);
        using var ms = new MemoryStream();
        var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };
        using (var writer = XmlWriter.Create(ms, settings))
            doc.Save(writer);
        return ms.ToArray();
    }

    private static XElement CData(string name, string value) =>
        new(Ns + name, new XCData(value ?? ""));

    private static int ParseInt(string? text, int fallback = 0) =>
        int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;

    private static XElement LoadXml(string text)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
        using var reader = XmlReader.Create(new StringReader(text), settings);
        return XDocument.Load(reader).Root!;
    }
}
