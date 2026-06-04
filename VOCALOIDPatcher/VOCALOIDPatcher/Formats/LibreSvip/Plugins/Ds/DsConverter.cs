using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Framework;
using VOCALOIDPatcher.Formats.LibreSvip.Model;
using VOCALOIDPatcher.Formats.LibreSvip.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ds;

public sealed class DsConverter : FormatConverter
{
    private static readonly Regex CentsRe = new(@"[+-]\d+$", RegexOptions.Compiled);

    public bool ImportPitch { get; set; } = true;
    public double Tempo { get; set; } = Constants.DefaultBpm;

    private TimeSynchronizer _synchronizer = new(new List<SongTempo> { new() });

    public override bool CanLoad => true;

    public override Project Load(byte[] content)
    {
        var items = JsonSerializer.Deserialize<List<DsItem>>(TextHelper.DetectAndDecode(content), JsonHelper.Default)
            ?? new List<DsItem>();
        var songTempoList = new List<SongTempo> { new(0, Tempo) };
        _synchronizer = new TimeSynchronizer(songTempoList);
        var track = new SingingTrack
        {
            NoteList = ParseNotes(items),
            EditedParams = new Params { Pitch = ParsePitch(items) },
        };
        return new Project
        {
            SongTempoList = songTempoList,
            TimeSignatureList = new List<TimeSignature> { new(0, 4, 4) },
            TrackList = new List<Track> { track },
        };
    }

    private List<Note> ParseNotes(List<DsItem> items)
    {
        var allNotes = new List<Note>();
        foreach (var item in items)
        {
            if (item.NoteDur == null || item.NoteSlur == null)
                continue;
            double curSecs = item.Offset;
            bool prevIsBreath = false;
            int lyricIndex = 0;
            var indexed = new List<(int Index, int Slur)>();
            for (int i = 0; i < item.NoteSlur.Count; i++)
                indexed.Add((i, item.NoteSlur[i]));
            foreach (var group in IterTools.SplitBefore(indexed, pair => pair.Slur == 0))
            {
                foreach (var (noteIndex, isSlur) in group)
                {
                    if (lyricIndex >= item.Text.Count || noteIndex >= item.NoteDur.Count || noteIndex >= item.NoteSeq.Count)
                        continue;
                    string text = item.Text[lyricIndex];
                    double noteDur = item.NoteDur[noteIndex];
                    string note = item.NoteSeq[noteIndex];
                    double curTime = _synchronizer.GetActualTicksFromSecs(curSecs);
                    double nextTime = _synchronizer.GetActualTicksFromSecs(curSecs + noteDur);
                    if (text == "SP")
                    {
                    }
                    else if (text == "AP")
                    {
                        prevIsBreath = true;
                    }
                    else
                    {
                        int midiKey = MusicMath.Note2Midi(CentsRe.Replace(note, ""));
                        if (isSlur == 0)
                        {
                            allNotes.Add(new Note
                            {
                                StartPos = (int)curTime,
                                Length = (int)nextTime - (int)curTime,
                                KeyNumber = midiKey,
                                Lyric = text,
                                HeadTag = prevIsBreath ? "V" : null,
                            });
                            prevIsBreath = false;
                        }
                        else
                        {
                            allNotes.Add(new Note
                            {
                                StartPos = (int)curTime,
                                Length = (int)nextTime - (int)curTime,
                                KeyNumber = midiKey,
                                Lyric = "-",
                            });
                        }
                    }
                    curSecs += noteDur;
                }
                lyricIndex++;
            }
        }
        return allNotes;
    }

    private ParamCurve ParsePitch(List<DsItem> items)
    {
        var points = new List<Point> { Point.StartPoint() };
        if (ImportPitch)
        {
            foreach (var item in items)
            {
                if (item.F0Timestep == null || item.F0Seq == null)
                    continue;
                double timestep = item.F0Timestep.Value;
                points.Add(new Point((int)Math.Round(_synchronizer.GetActualTicksFromSecs(item.Offset)) + 1920, -100));
                for (int i = 0; i < item.F0Seq.Count; i++)
                    points.Add(new Point(
                        (int)Math.Round(_synchronizer.GetActualTicksFromSecs(item.Offset + timestep * i)) + 1920,
                        (int)Math.Round(MusicMath.Hz2Midi(item.F0Seq[i]) * 100)));
                points.Add(new Point(
                    (int)Math.Round(_synchronizer.GetActualTicksFromSecs(item.Offset + timestep * (item.F0Seq.Count - 1))) + 1920,
                    -100));
            }
        }
        points.Add(Point.EndPoint());
        return new ParamCurve { Points = points };
    }
}
