using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Framework;
using VOCALOIDPatcher.Formats.LibreSvip.Model;
using VOCALOIDPatcher.Formats.LibreSvip.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Aisp;

public sealed class AispConverter : FormatConverter
{
    public bool ImportPitch { get; set; } = true;
    public bool ImportInstrumental { get; set; } = true;

    private int _firstBarLength;
    private TimeSynchronizer _synchronizer = new(new List<SongTempo> { new() });

    private static readonly JsonSerializerOptions Inner = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly JsonSerializerOptions Outer = BuildOuter();

    private static JsonSerializerOptions BuildOuter()
    {
        var options = new JsonSerializerOptions(Inner);
        options.Converters.Add(new AisTrackConverter(Inner));
        return options;
    }

    public override bool CanLoad => true;
    public override bool CanDump => true;

    public override Project Load(byte[] content)
    {
        string text = TextHelper.DetectAndDecode(content);
        int newline = text.IndexOf('\n');
        string headText = newline >= 0 ? text[..newline] : text;
        string bodyText = newline >= 0 ? text[(newline + 1)..] : "{}";
        var head = JsonSerializer.Deserialize<AISProjectHead>(headText.Trim(), Inner) ?? new AISProjectHead();
        var body = JsonSerializer.Deserialize<AISProjectBody>(bodyText.Trim(), Outer) ?? new AISProjectBody();

        var timeSignatures = head.Signature
            .Select(ts => new TimeSignature(ts.StartBar, ts.BeatZi, ts.BeatMu)).ToList();
        if (timeSignatures.Count == 0)
            timeSignatures.Add(new TimeSignature());
        _firstBarLength = (int)Math.Round(timeSignatures[0].BarLength());
        var tempos = head.Tempo.Select(t => new SongTempo(t.Start128 * 15, t.TempoFloat ?? Constants.DefaultBpm)).ToList();
        if (tempos.Count == 0)
            tempos.Add(new SongTempo());

        return new Project
        {
            TimeSignatureList = timeSignatures,
            SongTempoList = tempos,
            TrackList = ParseTracks(body.Tracks),
        };
    }

    private List<Track> ParseTracks(List<AISTrack> aisTracks)
    {
        var trackList = new List<Track>();
        foreach (var aisTrack in aisTracks)
        {
            if (aisTrack is AISSingVoiceTrack sing)
            {
                var noteList = new List<Note>();
                var pitchPoints = new List<Point> { Point.StartPoint() };
                foreach (var item in sing.Items)
                {
                    int tickPrefix = item.Start * 15;
                    var (itemNotes, itemPitch) = ParseNotes(item.Notes, tickPrefix);
                    noteList.AddRange(itemNotes);
                    pitchPoints.AddRange(itemPitch);
                }
                pitchPoints.Add(Point.EndPoint());
                var singingTrack = new SingingTrack
                {
                    Title = sing.Name ?? "",
                    AiSingerName = sing.SingerNameCn ?? "",
                    Mute = sing.Mute ?? false,
                    Solo = sing.Solo ?? false,
                    NoteList = noteList,
                };
                if (pitchPoints.Count > 2)
                    singingTrack.EditedParams.Pitch = new ParamCurve { Points = pitchPoints };
                trackList.Add(singingTrack);
            }
            else if (ImportInstrumental && aisTrack is AISAudioTrack audio)
            {
                int i = 1;
                foreach (var item in audio.Items)
                {
                    trackList.Add(new InstrumentalTrack
                    {
                        Title = $"{audio.Name} ({i})",
                        Mute = audio.Mute ?? false,
                        Solo = audio.Solo ?? false,
                        AudioFilePath = item.PathAudio ?? "",
                        Offset = item.Start * 15,
                    });
                    i++;
                }
            }
        }
        return trackList;
    }

    private (List<Note>, List<Point>) ParseNotes(List<AISNote> aisNotes, int tickPrefix)
    {
        var noteList = new List<Note>();
        var pitchPoints = new List<Point>();
        foreach (var aisNote in aisNotes)
        {
            var note = new Note
            {
                StartPos = aisNote.Start * 15 + tickPrefix,
                Length = aisNote.Length * 15,
                KeyNumber = aisNote.MidiNo + 12,
                Lyric = aisNote.Lyric ?? "",
                Pronunciation = aisNote.Pinyin,
            };
            if (ImportPitch && aisNote.Pit.Count > 0)
            {
                double tickStep = (double)note.Length / aisNote.Pit.Count;
                pitchPoints.Add(new Point(note.StartPos + _firstBarLength, -100));
                for (int i = 0; i < aisNote.Pit.Count; i++)
                    pitchPoints.Add(new Point(
                        (int)Math.Round(note.StartPos + tickStep * i) + _firstBarLength,
                        (int)Math.Round(note.KeyNumber * 100 + aisNote.Pit[i] * 10)));
                pitchPoints.Add(new Point(note.EndPos + _firstBarLength, -100));
            }
            noteList.Add(note);
        }
        return (noteList, pitchPoints);
    }

    public override byte[] Dump(Project project)
    {
        _synchronizer = new TimeSynchronizer(project.SongTempoList);
        _firstBarLength = (int)Math.Round(project.TimeSignatureList[0].BarLength());
        var aisTimeSignatures = project.TimeSignatureList
            .Select(ts => new AISTimeSignature { BeatZi = ts.Numerator, BeatMu = ts.Denominator, StartBar = ts.BarIndex })
            .ToList();
        var aisTempos = GenerateTempos(project.SongTempoList, project.TimeSignatureList);

        int maxEndTime = (int)Math.Round(project.TrackList.OfType<SingingTrack>()
            .SelectMany(t => t.NoteList).Select(n => (double)n.EndPos)
            .DefaultIfEmpty(project.SongTempoList[^1].Position).Max() / 15);
        double lastBarLen = 128.0 * aisTimeSignatures[^1].BeatZi / aisTimeSignatures[^1].BeatMu;
        int numBars = aisTempos[^1].StartBar + (int)Math.Floor((maxEndTime - aisTempos[^1].Start128) / lastBarLen);

        var head = new AISProjectHead
        {
            Signature = aisTimeSignatures,
            Tempo = aisTempos,
            Time = maxEndTime,
            Bar = Math.Max(numBars, 100),
        };
        var body = new AISProjectBody { Tracks = GenerateTracks(project.TrackList, project.TimeSignatureList) };

        string headJson = JsonSerializer.Serialize(head, Inner);
        string bodyJson = JsonSerializer.Serialize(body, Outer);
        return TextHelper.EncodeUtf8(headJson + "\n" + bodyJson);
    }

    private List<AISTempo> GenerateTempos(List<SongTempo> tempos, List<TimeSignature> timeSignatures)
    {
        var result = new List<AISTempo>();
        double prevBarLength = 1920.0;
        int prevBarIndex = 1;
        double curTick = 0.0;
        var tickIndexes = new List<int>();
        for (int i = 0; i < timeSignatures.Count; i++)
        {
            var ts = timeSignatures[i];
            if (ts.BarIndex > prevBarIndex && i != 0)
                curTick += prevBarLength * (ts.BarIndex - prevBarIndex);
            tickIndexes.Add((int)curTick);
            if (ts.BarIndex > prevBarIndex)
            {
                prevBarIndex = ts.BarIndex;
                prevBarLength = ts.BarLength();
            }
        }
        foreach (var tempo in tempos)
        {
            int tsIndex = Math.Min(Search.BisectLeft(tickIndexes, tempo.Position), tickIndexes.Count - 1);
            var ts = timeSignatures[tsIndex];
            double barLen = ts.BarLength();
            int startBar = Math.Max(ts.BarIndex + (int)Math.Floor((tempo.Position - tickIndexes[tsIndex] - _firstBarLength) / barLen), 0);
            int startBeat = (int)Math.Floor(Mod(tempo.Position - tickIndexes[tsIndex] - _firstBarLength, barLen) / (barLen / ts.Numerator));
            result.Add(new AISTempo
            {
                TempoFloat = tempo.Bpm,
                Start128 = (int)Math.Round(tempo.Position / 15.0),
                StartBar = startBar,
                StartBeatInBar = startBeat,
            });
        }
        return result;
    }

    private static double Mod(double a, double b) => a - b * Math.Floor(a / b);

    private List<AISTrack> GenerateTracks(List<Track> tracks, List<TimeSignature> timeSignatures)
    {
        var result = new List<AISTrack>();
        foreach (var track in tracks)
        {
            if (track is not SingingTrack singing)
                continue;
            var notes = GenerateNotes(singing, timeSignatures);
            if (notes.Count == 0)
                continue;
            result.Add(new AISSingVoiceTrack
            {
                Idx = result.Count,
                Name = singing.Title,
                Mute = singing.Mute,
                Solo = singing.Solo,
                SingerNameCn = singing.AiSingerName,
                Items = new List<AISSingVoicePattern>
                {
                    new()
                    {
                        Uid = tracks.Count + result.Count,
                        Start = 0,
                        Length = notes.Max(n => n.Start + n.Length) + _firstBarLength,
                        Notes = notes,
                    },
                },
            });
        }
        return result;
    }

    private List<AISNote> GenerateNotes(SingingTrack track, List<TimeSignature> timeSignatures)
    {
        var aisNotes = new List<AISNote>();
        PitchSimulator? pitchSimulator = null;
        foreach (var note in track.NoteList)
        {
            int noteStart = note.StartPos / 15;
            var aisNote = new AISNote
            {
                MidiNo = note.KeyNumber - 12,
                Start = noteStart,
                Length = note.EndPos / 15 - noteStart,
                Lyric = note.Lyric,
                Pinyin = string.IsNullOrEmpty(note.Pronunciation)
                    ? (string.IsNullOrEmpty(note.Lyric) ? Constants.DefaultPhoneme : note.Lyric)
                    : note.Pronunciation,
                Triple = false,
            };
            if (track.EditedParams.Pitch.Points.Count > 0)
            {
                if (pitchSimulator == null)
                {
                    pitchSimulator = new PitchSimulator(_synchronizer, PortamentoPitch.NoPortamento(), track.NoteList, timeSignatures);
                    pitchSimulator.MergePitchCurve(track.EditedParams.Pitch, _firstBarLength);
                }
                aisNote.Pit = GeneratePitch(pitchSimulator, note);
            }
            aisNotes.Add(aisNote);
        }
        return aisNotes;
    }

    private static List<double> GeneratePitch(PitchSimulator pitchSimulator, Note note)
    {
        double tickStep = note.Length / 500.0;
        var result = new List<double>(500);
        for (int i = 0; i < 500; i++)
        {
            double? pitchValue = pitchSimulator.PitchAtTicks(note.StartPos + (int)(tickStep * i));
            double pv = pitchValue ?? note.KeyNumber * 100;
            result.Add((pv - note.KeyNumber * 100) / 10);
        }
        return result;
    }
}
