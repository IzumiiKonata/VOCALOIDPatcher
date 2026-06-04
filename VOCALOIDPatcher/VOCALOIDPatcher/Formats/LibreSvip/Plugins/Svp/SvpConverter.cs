using System;
using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Framework;
using VOCALOIDPatcher.Formats.LibreSvip.Model;
using VOCALOIDPatcher.Formats.LibreSvip.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Svp;

public sealed class SvpConverter : FormatConverter
{
    private const long TickRate = 1470000;

    private static readonly HashSet<char> SymbolBlacklist = new(
        "()[]{}（）<>《》―—*×!！?？:：·•。,，;；^`\"‘’“”=、_$%~@#…&￥");

    public bool ImportInstrumental { get; set; } = true;

    private int _firstBarTick;

    public override bool CanLoad => true;
    public override bool CanDump => true;

    private static int PositionToTicks(long position) => (int)Math.Round(position / (double)TickRate);
    private static long TicksToPosition(int ticks) => (long)ticks * TickRate;

    public override Project Load(byte[] content)
    {
        string text = TextHelper.DetectAndDecode(content).Trim('\0').Trim();
        var svp = JsonHelper.Deserialize<SVProject>(text);

        var timeSignatures = TickCounter.ShiftBeatList(
            svp.Time.Meter.Select(m => new TimeSignature(m.Index, m.Numerator, m.Denominator)).ToList(), 1);
        if (timeSignatures.Count == 0)
            timeSignatures.Add(new TimeSignature());
        _firstBarTick = (int)Math.Round(timeSignatures[0].BarLength());
        var tempos = TickCounter.ShiftTempoList(
            svp.Time.Tempo.Select(t => new SongTempo(PositionToTicks(t.Position), t.Bpm)).ToList(), _firstBarTick);
        if (tempos.Count == 0)
            tempos.Add(new SongTempo());

        var library = new Dictionary<string, SVGroup>();
        foreach (var group in svp.Library)
            library[group.Uuid] = group;
        var splitCounts = new Dictionary<string, int>();

        var trackList = new List<Track>();
        var groupTracks = new List<Track>();
        foreach (var svTrack in svp.Tracks)
        {
            if (svTrack.MainRef.IsInstrumental)
            {
                if (ImportInstrumental && svTrack.MainRef.Audio != null)
                    trackList.Add(new InstrumentalTrack
                    {
                        Title = svTrack.Name,
                        AudioFilePath = svTrack.MainRef.Audio.Filename,
                        Offset = PositionToTicks(svTrack.MainRef.BlickOffset),
                        Mute = svTrack.Mixer.Mute,
                        Solo = svTrack.Mixer.Solo,
                        Pan = svTrack.Mixer.Pan,
                        Volume = ParseVolume(svTrack.Mixer.GainDecibel),
                    });
                continue;
            }

            trackList.Add(new SingingTrack
            {
                Title = svTrack.Name,
                AiSingerName = svTrack.MainRef.Database.Name,
                Mute = svTrack.Mixer.Mute,
                Solo = svTrack.Mixer.Solo,
                Pan = svTrack.Mixer.Pan,
                Volume = ParseVolume(svTrack.Mixer.GainDecibel),
                NoteList = ParseNotes(svTrack.MainGroup.Notes, 0, 0),
            });

            foreach (var svRef in svTrack.Groups)
            {
                if (!library.TryGetValue(svRef.GroupId, out var group))
                    continue;
                splitCounts.TryGetValue(svRef.GroupId, out int count);
                splitCounts[svRef.GroupId] = count + 1;
                groupTracks.Add(new SingingTrack
                {
                    Title = $"{group.Name} ({count + 1})",
                    AiSingerName = svRef.Database.Name,
                    NoteList = ParseNotes(group.Notes, svRef.BlickOffset, svRef.PitchOffset),
                });
            }
        }
        trackList.AddRange(groupTracks);

        return new Project
        {
            TimeSignatureList = timeSignatures,
            SongTempoList = tempos,
            TrackList = trackList,
        };
    }

    private static List<Note> ParseNotes(List<SVNote> notes, long blickOffset, int pitchOffset)
    {
        var result = new List<Note>();
        foreach (var svNote in notes)
        {
            long onset = svNote.Onset + blickOffset;
            if (onset < 0)
                continue;
            int start = PositionToTicks(onset);
            result.Add(new Note
            {
                StartPos = start,
                Length = PositionToTicks(onset + svNote.Duration) - start,
                KeyNumber = svNote.Pitch + pitchOffset,
                Lyric = NormalizeLyric(svNote.Lyrics),
                Pronunciation = string.IsNullOrEmpty(svNote.Phonemes) ? null : svNote.Phonemes,
            });
        }
        return result;
    }

    private static string NormalizeLyric(string lyric)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in lyric)
            if (!SymbolBlacklist.Contains(c))
                sb.Append(c);
        return sb.ToString().Trim();
    }

    private static double ParseVolume(double gain) =>
        gain >= 0 ? Math.Min(gain / MusicMath.RatioToDb(4) + 1.0, 2.0) : MusicMath.DbToFloat(gain);

    public override byte[] Dump(Project project)
    {
        _firstBarTick = (int)Math.Round(project.TimeSignatureList[0].BarLength());
        var svp = new SVProject();
        svp.Time.Meter = TickCounter.SkipBeatList(project.TimeSignatureList, 1)
            .Select(ts => new SVMeter { Index = ts.BarIndex, Numerator = ts.Numerator, Denominator = ts.Denominator })
            .ToList();
        if (svp.Time.Meter.Count == 0)
            svp.Time.Meter.Add(new SVMeter { Index = 0, Numerator = 4, Denominator = 4 });
        svp.Time.Tempo = TickCounter.SkipTempoList(project.SongTempoList, _firstBarTick)
            .Select(t => new SVTempo { Position = TicksToPosition(t.Position), Bpm = t.Bpm })
            .ToList();
        if (svp.Time.Tempo.Count == 0)
            svp.Time.Tempo.Add(new SVTempo { Position = 0, Bpm = Constants.DefaultBpm });

        foreach (var track in project.TrackList)
        {
            if (track is SingingTrack singing)
            {
                svp.Tracks.Add(new SVTrack
                {
                    Name = singing.Title,
                    Mixer = new SVMixer { GainDecibel = GenerateVolume(singing.Volume), Pan = singing.Pan, Mute = singing.Mute, Solo = singing.Solo },
                    MainRef = new SVRef { IsInstrumental = false, Database = new SVDatabase { Name = singing.AiSingerName }, GroupId = Guid.NewGuid().ToString() },
                    MainGroup = new SVGroup
                    {
                        Uuid = Guid.NewGuid().ToString(),
                        Notes = singing.NoteList.Select(n => new SVNote
                        {
                            Onset = TicksToPosition(n.StartPos),
                            Duration = TicksToPosition(n.EndPos) - TicksToPosition(n.StartPos),
                            Lyrics = n.Lyric,
                            Phonemes = n.Pronunciation ?? "",
                            Pitch = n.KeyNumber,
                        }).ToList(),
                    },
                });
            }
            else if (track is InstrumentalTrack instrumental)
            {
                svp.Tracks.Add(new SVTrack
                {
                    Name = instrumental.Title,
                    Mixer = new SVMixer { Mute = instrumental.Mute, Solo = instrumental.Solo },
                    MainRef = new SVRef
                    {
                        IsInstrumental = true,
                        BlickOffset = TicksToPosition(instrumental.Offset),
                        Audio = new SVAudio { Filename = instrumental.AudioFilePath, Duration = 0 },
                        GroupId = Guid.NewGuid().ToString(),
                    },
                });
            }
        }

        string json = JsonHelper.Serialize(svp);
        return TextHelper.EncodeUtf8(json).Concat(new byte[] { 0 }).ToArray();
    }

    private static double GenerateVolume(double volume) =>
        Math.Max(MusicMath.RatioToDb(Math.Max(volume, 0.06)), -24.0);
}
