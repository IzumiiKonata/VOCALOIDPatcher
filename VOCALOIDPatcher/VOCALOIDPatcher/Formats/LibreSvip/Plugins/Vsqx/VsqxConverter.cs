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
    private static readonly XNamespace Vsq4Ns = "http://www.yamaha.co.jp/vocaloid/schema/vsq4/";
    private static readonly XNamespace Vsq3Ns = "http://www.yamaha.co.jp/vocaloid/schema/vsq3/";

    public bool ImportInstrumental { get; set; } = true;
    public bool ImportPitch { get; set; } = true;
    public VsqxVersion Version { get; set; } = VsqxVersion.Vsq4;
    public VocaloidLanguage DefaultLanguage { get; set; } = VocaloidLanguage.SimplifiedChinese;

    public override bool CanLoad => true;
    public override bool CanDump => true;

    public override Project Load(byte[] content)
    {
        var root = LoadXml(TextHelper.DetectAndDecode(content));
        VsqxVersion version;
        if (root.Name.LocalName == "vsq4")
            version = VsqxVersion.Vsq4;
        else if (root.Name.LocalName == "vsq3")
            version = VsqxVersion.Vsq3;
        else
            throw new InvalidDataException("仅支持 VOCALOID3 (vsq3) 或 VOCALOID4 (vsq4) 的 vsqx");

        bool isVsq3 = version == VsqxVersion.Vsq3;
        string posTickName = isVsq3 ? "posTick" : "t";
        string durName = isVsq3 ? "durTick" : "dur";
        string noteNumName = isVsq3 ? "noteNum" : "n";
        string lyricName = isVsq3 ? "lyric" : "y";
        string phnmName = isVsq3 ? "phnms" : "p";
        string noteStyleName = isVsq3 ? "noteStyle" : "nStyle";
        string seqAttrName = isVsq3 ? "seqAttr" : "seq";
        string seqElemName = isVsq3 ? "elem" : "cc";
        string seqPosName = isVsq3 ? "posNrm" : "p";
        string seqValueName = isVsq3 ? "elv" : "v";
        string ccName = isVsq3 ? "mCtrl" : "cc";
        string ccPosName = isVsq3 ? "posTick" : "t";
        string ccValueName = isVsq3 ? "attr" : "v";
        string pitId = isVsq3 ? "PIT" : "P";
        string pbsId = isVsq3 ? "PBS" : "S";
        string trackNoName = isVsq3 ? "vsTrackNo" : "tNo";
        string trackNameName = isVsq3 ? "trackName" : "name";
        string unitTrackNoName = isVsq3 ? "vsTrackNo" : "tNo";
        string muteName = isVsq3 ? "mute" : "m";
        string soloName = isVsq3 ? "solo" : "s";
        string partName = isVsq3 ? "musicalPart" : "vsPart";
        string playTimeName = "playTime";

        var master = root.Child("masterTrack") ?? throw new InvalidDataException("缺少 masterTrack");
        int preMeasure = int.TryParse(master.ChildText("preMeasure"), out int pm) ? pm : 1;

        string tsMeasureName = isVsq3 ? "posMes" : "m";
        string tsNumeName = isVsq3 ? "nume" : "nu";
        string tsDenomiName = isVsq3 ? "denomi" : "de";
        string tempoPosName = isVsq3 ? "posTick" : "t";
        string tempoValueName = isVsq3 ? "bpm" : "v";

        var rawTimeSignatures = master.Children("timeSig").Select(ts => new TimeSignature(
            ParseInt(ts.ChildText(tsMeasureName)), ParseInt(ts.ChildText(tsNumeName), 4),
            ParseInt(ts.ChildText(tsDenomiName), 4))).ToList();
        if (rawTimeSignatures.Count == 0)
            rawTimeSignatures.Add(new TimeSignature());
        var (tickPrefix, timeSignatures) = ParseTimeSignatures(rawTimeSignatures, preMeasure);
        int firstBarLength = (int)Math.Round(rawTimeSignatures[0].BarLength());

        var rawTempos = master.Children("tempo").Select(t => new SongTempo(
            ParseInt(t.ChildText(tempoPosName)), ParseInt(t.ChildText(tempoValueName)) / BpmRate)).ToList();
        if (rawTempos.Count == 0)
            rawTempos.Add(new SongTempo(0, Constants.DefaultBpm));
        var tempos = TickCounter.SkipTempoList(rawTempos, tickPrefix);
        var synchronizer = new TimeSynchronizer(tempos);

        var muteSolo = new Dictionary<int, (bool Mute, bool Solo)>();
        var mixer = root.Child("mixer");
        if (mixer != null)
            foreach (var unit in mixer.Children("vsUnit"))
                muteSolo[ParseInt(unit.ChildText(unitTrackNoName))] =
                    (ParseInt(unit.ChildText(muteName)) != 0, ParseInt(unit.ChildText(soloName)) != 0);

        var trackList = new List<Track>();
        foreach (var vsTrack in root.Children("vsTrack"))
        {
            int tNo = ParseInt(vsTrack.ChildText(trackNoName));
            var singing = new SingingTrack { Title = vsTrack.ChildText(trackNameName) ?? $"Track {tNo + 1}" };
            if (muteSolo.TryGetValue(tNo, out var ms))
            {
                singing.Mute = ms.Mute;
                singing.Solo = ms.Solo;
            }
            foreach (var part in vsTrack.Children(partName))
            {
                int partPos = ParseInt(part.ChildText(posTickName));
                int offset = partPos - tickPrefix;
                int playTime = ParseInt(part.ChildText(playTimeName));
                var partNotes = new List<Note>();
                var vibrato = new VibratoData();
                foreach (var note in part.Children("note"))
                {
                    var phnmElem = note.Child(phnmName);
                    var phnms = phnmElem?.Value;
                    if (phnms is "Asp" or "Sil" or "?")
                        continue;
                    string lyric = (note.ChildText(lyricName) ?? Constants.DefaultEnglishLyric).ToLowerInvariant();
                    int startPos = ParseInt(note.ChildText(posTickName));
                    int length = ParseInt(note.ChildText(durName));
                    string? lockAttr = phnmElem?.Attribute("lock")?.Value;
                    var newNote = new Note
                    {
                        StartPos = startPos + offset,
                        Length = length,
                        KeyNumber = ParseInt(note.ChildText(noteNumName), 60),
                        Lyric = lyric,
                        Pronunciation = lockAttr == "1" ? phnms : null,
                    };
                    CollectVibrato(note, newNote, vibrato, synchronizer, noteStyleName, seqAttrName,
                        seqElemName, seqPosName, seqValueName);
                    partNotes.Add(newNote);
                }
                singing.NoteList.AddRange(partNotes);
                if (ImportPitch && partNotes.Count > 0)
                {
                    var pitch = ParsePartPitch(part, offset, partNotes, timeSignatures, synchronizer, firstBarLength,
                        ccName, ccPosName, ccValueName, pitId, pbsId, isVsq3);
                    if (pitch != null)
                    {
                        if (!vibrato.IsEmpty)
                            pitch = VsqxVibrato.Apply(pitch, vibrato, synchronizer, offset, offset, offset + playTime);
                        singing.EditedParams.Pitch.Points.AddRange(pitch.Points);
                    }
                }
            }
            if (singing.EditedParams.Pitch.Points.Count > 0)
            {
                singing.EditedParams.Pitch.Points.Insert(0, Point.StartPoint());
                singing.EditedParams.Pitch.Points.Add(Point.EndPoint());
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

    private static void CollectVibrato(XElement note, Note targetNote, VibratoData vibrato,
        TimeSynchronizer synchronizer, string noteStyleName, string seqAttrName, string seqElemName,
        string seqPosName, string seqValueName)
    {
        var style = note.Child(noteStyleName);
        if (style == null)
            return;
        var seqAttrs = style.Children(seqAttrName).ToList();
        if (seqAttrs.Count == 0)
            return;
        double startSecs = synchronizer.GetActualSecsFromTicks(targetNote.StartPos);
        double durationSecs = synchronizer.GetDurationSecsFromTicks(targetNote.StartPos, targetNote.EndPos);
        foreach (var seqAttr in seqAttrs)
        {
            string seqId = seqAttr.Attribute("id")?.Value ?? "";
            var elems = seqAttr.Children(seqElemName)
                .Select(e => new VibratoElem(ParseInt(e.ChildText(seqPosName)), ParseInt(e.ChildText(seqValueName))))
                .ToList();
            VsqxVibrato.CollectFromSeqAttr(vibrato, seqId, elems, startSecs, durationSecs);
        }
    }

    private static ParamCurve? ParsePartPitch(XElement part, int offset, List<Note> partNotes,
        List<TimeSignature> timeSignatures, TimeSynchronizer synchronizer, int firstBarLength,
        string ccName, string ccPosName, string ccValueName, string pitId, string pbsId, bool isVsq3)
    {
        var pitEvents = new List<ControllerEvent>();
        var pbsEvents = new List<ControllerEvent>();
        foreach (var cc in part.Children(ccName))
        {
            var v = cc.Child(ccValueName);
            if (v == null)
                continue;
            int pos = ParseInt(cc.ChildText(ccPosName));
            int value = ParseInt(v.Value);
            string id = v.Attribute("id")?.Value ?? "";
            if (id == pitId)
                pitEvents.Add(new ControllerEvent(pos, value));
            else if (id == pbsId)
                pbsEvents.Add(new ControllerEvent(pos, value));
        }
        if (pitEvents.Count == 0)
            return null;
        var pit = new ControllerCurve("pitch_bend", pitEvents, 0, -8192, 8191);
        var pbs = new ControllerCurve("pitch_bend_sens", pbsEvents, 2, 1, 24);
        var handler = new VocaloidPitchHandler(synchronizer, partNotes, timeSignatures, firstBarLength);
        return handler.ToAbsolutePitch(new List<PitchBendData> { new(pit, pbs) }, new List<int> { offset });
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
        bool isVsq3 = Version == VsqxVersion.Vsq3;
        XNamespace ns = isVsq3 ? Vsq3Ns : Vsq4Ns;

        int firstBarLength = (int)Math.Round(project.TimeSignatureList[0].BarLength());
        int tickPrefix = firstBarLength;
        var synchronizer = new TimeSynchronizer(project.SongTempoList.Count > 0
            ? project.SongTempoList : new List<SongTempo> { new() });

        string tsMeasureName = isVsq3 ? "posMes" : "m";
        string tsNumeName = isVsq3 ? "nume" : "nu";
        string tsDenomiName = isVsq3 ? "denomi" : "de";
        string tempoPosName = isVsq3 ? "posTick" : "t";
        string tempoValueName = isVsq3 ? "bpm" : "v";
        string trackNoName = isVsq3 ? "vsTrackNo" : "tNo";
        string trackNameName = isVsq3 ? "trackName" : "name";
        string unitTrackNoName = isVsq3 ? "vsTrackNo" : "tNo";
        string muteName = isVsq3 ? "mute" : "m";
        string soloName = isVsq3 ? "solo" : "s";
        string partName = isVsq3 ? "musicalPart" : "vsPart";
        string partNameName = isVsq3 ? "partName" : "name";
        string posTickName = isVsq3 ? "posTick" : "t";
        string durName = isVsq3 ? "durTick" : "dur";
        string noteNumName = isVsq3 ? "noteNum" : "n";
        string velocityName = isVsq3 ? "velocity" : "v";
        string lyricName = isVsq3 ? "lyric" : "y";
        string phnmName = isVsq3 ? "phnms" : "p";
        string ccName = isVsq3 ? "mCtrl" : "cc";
        string ccPosName = isVsq3 ? "posTick" : "t";
        string ccValueName = isVsq3 ? "attr" : "v";
        string singerPosName = isVsq3 ? "posTick" : "t";
        string singerBsName = isVsq3 ? "vBS" : "bs";
        string singerPcName = isVsq3 ? "vPC" : "pc";
        string pitId = isVsq3 ? "PIT" : "P";
        string pbsId = isVsq3 ? "PBS" : "S";
        string rootName = isVsq3 ? "vsq3" : "vsq4";
        string version = isVsq3 ? "3.0.0.0" : "4.0.0.3";

        XElement CData(string name, string value) => new(ns + name, new XCData(value ?? ""));

        var master = new XElement(ns + "masterTrack",
            CData("seqName", "Untitled0"),
            CData("comment", "New VSQ File"),
            new XElement(ns + "resolution", 480),
            new XElement(ns + "preMeasure", 1));
        foreach (var ts in TickCounter.ShiftBeatList(project.TimeSignatureList, 1))
            master.Add(new XElement(ns + "timeSig",
                new XElement(ns + tsMeasureName, ts.BarIndex),
                new XElement(ns + tsNumeName, ts.Numerator),
                new XElement(ns + tsDenomiName, ts.Denominator)));
        foreach (var tempo in TickCounter.SkipTempoList(project.SongTempoList, tickPrefix))
            master.Add(new XElement(ns + "tempo",
                new XElement(ns + tempoPosName, tempo.Position),
                new XElement(ns + tempoValueName, (int)Math.Round(tempo.Bpm * BpmRate))));

        var mixer = new XElement(ns + "mixer",
            new XElement(ns + "masterUnit",
                new XElement(ns + "oGin", 0), new XElement(ns + "rLvl", 0), new XElement(ns + "vol", 0)));

        var root = new XElement(ns + rootName,
            CData("vender", "Yamaha Corporation"),
            CData("version", version),
            new XElement(ns + "vVoiceTable",
                new XElement(ns + "vVoice",
                    new XElement(ns + "bs", 0), new XElement(ns + "pc", 0),
                    CData("id", "BCNHC6KMM5RTC5GB"), CData("name", "singer"))));

        var singingTracks = project.TrackList.OfType<SingingTrack>().ToList();
        for (int i = 0; i < singingTracks.Count; i++)
        {
            var track = singingTracks[i];
            mixer.Add(new XElement(ns + "vsUnit",
                new XElement(ns + unitTrackNoName, i),
                new XElement(ns + "iGin", 0),
                new XElement(ns + muteName, track.Mute ? 1 : 0),
                new XElement(ns + soloName, track.Solo ? 1 : 0),
                new XElement(ns + "pan", 64),
                new XElement(ns + "vol", 0)));
            var vsTrack = new XElement(ns + "vsTrack",
                new XElement(ns + trackNoName, i),
                CData(trackNameName, track.Title),
                CData("comment", "Track"));
            if (track.NoteList.Count > 0)
            {
                var part = new XElement(ns + partName,
                    new XElement(ns + posTickName, tickPrefix),
                    new XElement(ns + "playTime", track.NoteList[^1].EndPos),
                    CData(partNameName, "New Part"),
                    CData("comment", "New Musical Part"),
                    new XElement(ns + "singer",
                        new XElement(ns + singerPosName, 0),
                        new XElement(ns + singerBsName, (int)DefaultLanguage),
                        new XElement(ns + singerPcName, 0)));
                if (track.EditedParams.Pitch.Points.Count > 0)
                {
                    var handler = new VocaloidPitchHandler(synchronizer, track.NoteList, project.TimeSignatureList, firstBarLength);
                    var pb = handler.FromAbsolutePitch(track.EditedParams.Pitch);
                    var ccEvents = pb.Pit.Events.Select(e => (e.Pos, pitId, e.Value))
                        .Concat(pb.Pbs.Events.Select(e => (e.Pos, pbsId, e.Value)))
                        .OrderBy(e => e.Item1).ToList();
                    foreach (var (pos, id, value) in ccEvents)
                        part.Add(new XElement(ns + ccName,
                            new XElement(ns + ccPosName, pos),
                            new XElement(ns + ccValueName, new XAttribute("id", id), value)));
                }
                foreach (var note in track.NoteList)
                {
                    var (lyricOut, phoneme) = VsqxPhonemeGenerator.Generate(note.Lyric, DefaultLanguage);
                    var noteNode = new XElement(ns + "note",
                        new XElement(ns + posTickName, note.StartPos),
                        new XElement(ns + durName, note.Length),
                        new XElement(ns + noteNumName, Math.Clamp(note.KeyNumber, 0, 127)),
                        new XElement(ns + velocityName, 64),
                        CData(lyricName, lyricOut));
                    string phnmsValue = !string.IsNullOrEmpty(note.Pronunciation) ? note.Pronunciation : phoneme;
                    noteNode.Add(CData(phnmName, phnmsValue));
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

    private static int ParseInt(string? text, int fallback = 0) =>
        int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;

    private static XElement LoadXml(string text)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
        using var reader = XmlReader.Create(new StringReader(text), settings);
        return XDocument.Load(reader).Root!;
    }
}
