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
    private string[] _breathLyrics = { "Asp", "AP" };
    private string[] _silenceLyrics = { "R", "SP" };

    public UstxParser(bool importInstrumental) => _importInstrumental = importInstrumental;

    public Project ParseProject(USTXProject project)
    {
        var tempos = ParseTempos(project.Tempos);
        var timeSignatures = ParseTimeSignatures(project.TimeSignatures);
        var tracks = ParseTracks(project.Tracks, project.VoiceParts);
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

        foreach (var part in voiceParts)
        {
            if (part.TrackNo < 0 || part.TrackNo >= trackList.Count)
                continue;
            var singing = trackList[part.TrackNo];
            if (string.IsNullOrEmpty(singing.Title))
                singing.Title = part.Name;
            bool monosyllabic = IsMonosyllabic(tracks[part.TrackNo].Phonemizer);
            singing.NoteList.AddRange(ParseNotes(part.Notes, part.Position, monosyllabic));
        }

        return trackList.Where(t => t.NoteList.Count > 0).Cast<Track>().ToList();
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
