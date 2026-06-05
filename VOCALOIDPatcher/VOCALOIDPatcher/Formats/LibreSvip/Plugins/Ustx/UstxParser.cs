using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Model;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ustx;

public sealed class UstxParser
{
    private static readonly Regex PhoneticHint = new(@"\[(.*?)\]", RegexOptions.Compiled);
    private static readonly string[] MonosyllabicLanguages =
        { "chinese", "japanese", "korean", "cantonese", "vietnamese" };

    private readonly bool _importInstrumental;
    private readonly bool _importPitch;
    private string[] _breathLyrics = { "Asp", "AP" };
    private string[] _silenceLyrics = { "R", "SP" };
    private BasePitchGenerator _basePitchGenerator = null!;

    public UstxParser(bool importInstrumental, bool importPitch = true)
    {
        _importInstrumental = importInstrumental;
        _importPitch = importPitch;
    }

    public Project ParseProject(USTXProject project)
    {
        _basePitchGenerator = new BasePitchGenerator(project);
        var tempos = ParseTempos(project.Tempos);
        var timeSignatures = ParseTimeSignatures(project.TimeSignatures);
        var tracks = ParseTracks(project.Tracks, project.VoiceParts);
        foreach (var track in tracks)
        {
            if (track is SingingTrack singing)
                singing.EditedParams.Pitch.Points.Add(Point.EndPoint());
        }
        tracks.AddRange(ParseWaveParts(project.Tracks, project.WaveParts));
        return new Project
        {
            SongTempoList = tempos,
            TimeSignatureList = timeSignatures,
            TrackList = tracks,
        };
    }

    private static List<SongTempo> ParseTempos(List<UTempo> tempos)
    {
        var result = tempos
            .Select(t => new SongTempo(t.Position > 0 ? t.Position + 1920 : t.Position, t.Bpm))
            .ToList();
        if (result.Count == 0)
            result.Add(new SongTempo(0, Constants.DefaultBpm));
        return result;
    }

    private static List<TimeSignature> ParseTimeSignatures(List<UTimeSignature> timeSignatures)
    {
        var result = timeSignatures
            .Select(t => new TimeSignature(t.BarPosition, t.BeatPerBar, t.BeatUnit))
            .ToList();
        if (result.Count == 0)
            result.Add(new TimeSignature());
        return result;
    }

    private static bool IsMonosyllabic(string? phonemizer)
    {
        if (phonemizer == null)
            return false;
        string lower = phonemizer.ToLowerInvariant();
        return MonosyllabicLanguages.Any(lang => lower.Contains(lang));
    }

    private List<Track> ParseTracks(List<UTrack> tracks, List<UVoicePart> voiceParts)
    {
        var trackList = tracks.Select((t, i) => new SingingTrack
        {
            Volume = ParseVolume(t.Volume),
            Solo = t.Solo,
            Mute = t.Mute,
            AiSingerName = t.Singer ?? "",
            Title = t.TrackName ?? $"Track {i + 1}",
        }).ToList();

        if (_importPitch)
            foreach (var track in trackList)
                track.EditedParams.Pitch.Points.Add(Point.StartPoint());

        foreach (var part in voiceParts)
        {
            if (part.TrackNo < 0 || part.TrackNo >= trackList.Count)
                continue;
            var singing = trackList[part.TrackNo];
            if (string.IsNullOrEmpty(singing.Title))
                singing.Title = part.Name;
            bool monosyllabic = IsMonosyllabic(tracks[part.TrackNo].Phonemizer);
            singing.NoteList.AddRange(ParseNotes(part.Notes, part.Position, monosyllabic));
            if (_importPitch)
                singing.EditedParams.Pitch.Points.AddRange(ParsePitch(part));
        }

        return trackList.Where(t => t.NoteList.Count > 0).Cast<Track>().ToList();
    }

    private List<Point> ParsePitch(UVoicePart part)
    {
        const int pitchStart = BasePitchGenerator.PitchStart;
        const int pitchInterval = BasePitchGenerator.PitchInterval;
        const int firstBarLength = 1920;

        var pitches = _basePitchGenerator.BasePitch(part);

        UCurve? curve = null;
        foreach (var c in part.Curves)
        {
            if (c.Abbr == "pitd")
            {
                curve = c;
                break;
            }
        }
        if (curve != null && !curve.IsEmpty)
            for (int i = 0; i < pitches.Count; i++)
                pitches[i] += curve.Sample(pitchStart + i * pitchInterval);

        var pointList = new List<Point>
        {
            new(firstBarLength + part.Position, -100),
        };
        for (int i = 0; i < pitches.Count; i++)
            pointList.Add(new Point(
                firstBarLength + part.Position + i * pitchInterval,
                (int)pitches[i]));
        pointList.Add(new Point(
            firstBarLength + part.Position + pitches.Count * pitchInterval,
            -100));
        return pointList;
    }

    private List<Note> ParseNotes(List<UNote> notes, int tickPrefix, bool monosyllabic)
    {
        var result = new List<Note>();
        UNote? prev = null;
        foreach (var ustxNote in notes)
        {
            string lyric = ustxNote.Lyric;
            if (lyric.StartsWith("+", StringComparison.Ordinal))
                lyric = monosyllabic || lyric == "+~" || lyric == "+*" ? "-" : "+";
            var note = new Note
            {
                KeyNumber = ustxNote.Tone,
                Lyric = lyric,
                StartPos = ustxNote.Position + tickPrefix,
                Length = ustxNote.Duration,
            };
            if (!string.IsNullOrEmpty(ustxNote.Lyric))
            {
                var hint = PhoneticHint.Match(ustxNote.Lyric);
                if (hint.Success)
                    note.Pronunciation = hint.Groups[1].Value;
                else if (!ustxNote.Lyric.StartsWith("+", StringComparison.Ordinal))
                    note.Pronunciation = ustxNote.Lyric.StartsWith("?", StringComparison.Ordinal)
                        ? ustxNote.Lyric[1..]
                        : ustxNote.Lyric;
            }
            if (prev != null && prev.Position + prev.Duration == ustxNote.Position
                && _breathLyrics.Contains(prev.Lyric))
                note.HeadTag = "V";
            if (!_breathLyrics.Contains(ustxNote.Lyric) && !_silenceLyrics.Contains(ustxNote.Lyric))
                result.Add(note);
            prev = ustxNote;
        }
        return result;
    }

    private List<Track> ParseWaveParts(List<UTrack> tracks, List<UWavePart> waveParts)
    {
        var result = new List<Track>();
        if (!_importInstrumental)
            return result;
        foreach (var wavePart in waveParts)
        {
            var track = wavePart.TrackNo >= 0 && wavePart.TrackNo < tracks.Count ? tracks[wavePart.TrackNo] : new UTrack();
            result.Add(new InstrumentalTrack
            {
                AudioFilePath = wavePart.RelativePath,
                Offset = wavePart.Position,
                Title = wavePart.Name,
                Mute = track.Mute,
                Solo = track.Solo,
                Volume = ParseVolume(track.Volume),
            });
        }
        return result;
    }

    private static double ParseVolume(double volume) =>
        Math.Min(MusicMath.DbToFloat(volume, usingAmplitude: false), 2);
}
