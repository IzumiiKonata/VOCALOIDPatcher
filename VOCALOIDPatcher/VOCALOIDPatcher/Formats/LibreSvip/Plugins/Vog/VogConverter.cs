using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Framework;
using VOCALOIDPatcher.Formats.LibreSvip.Model;
using VOCALOIDPatcher.Formats.LibreSvip.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Vog;

public sealed class VogConverter : FormatConverter
{
    public double Tempo { get; set; } = Constants.DefaultBpm;
    public string SingerName { get; set; } = "Doaz";

    public override bool CanLoad => true;
    public override bool CanDump => true;

    public override Project Load(byte[] content)
    {
        using var archive = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read);
        var entry = archive.GetEntry("chart.json") ?? throw new InvalidDataException("vog 缺少 chart.json");
        using var stream = entry.Open();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var vogen = JsonHelper.Deserialize<VogenProject>(ms.ToArray());

        var (numerator, denominator) = ParseTimeSig(vogen.TimeSig0);
        return new Project
        {
            SongTempoList = new List<SongTempo> { new(0, vogen.Bpm0) },
            TimeSignatureList = new List<TimeSignature> { new(0, numerator, denominator) },
            TrackList = vogen.Utts.Select(ParseTrack).Cast<Track>().ToList(),
        };
    }

    private static (int, int) ParseTimeSig(string timeSig)
    {
        var parts = timeSig.Split('/');
        int numerator = parts.Length > 0 && int.TryParse(parts[0], out var n) ? n : 4;
        int denominator = parts.Length > 1 && int.TryParse(parts[1], out var d) ? d : 4;
        return (numerator, denominator);
    }

    private static SingingTrack ParseTrack(VogenTrack utt) => new()
    {
        Title = utt.Name ?? "",
        AiSingerName = utt.SingerId ?? "",
        NoteList = utt.Notes.Select(note => new Note
        {
            StartPos = note.On ?? 0,
            Length = note.Dur ?? 0,
            Lyric = note.Lyric ?? "",
            Pronunciation = note.Rom,
            KeyNumber = note.Pitch ?? 0,
        }).ToList(),
    };

    public override byte[] Dump(Project project)
    {
        var source = project.SongTempoList.Count != 1
            ? ResetTimeAxis.Reset(project, Tempo)
            : project;

        var timeSignature = source.TimeSignatureList.Count > 0 ? source.TimeSignatureList[0] : new TimeSignature();
        var vogen = new VogenProject
        {
            Bpm0 = source.SongTempoList.Count > 0 ? source.SongTempoList[0].Bpm : Constants.DefaultBpm,
            TimeSig0 = $"{timeSignature.Numerator}/{timeSignature.Denominator}",
            Utts = source.TrackList.OfType<SingingTrack>().Select(track => new VogenTrack
            {
                Name = track.Title,
                SingerId = string.IsNullOrWhiteSpace(SingerName) ? track.AiSingerName : SingerName,
                Notes = track.NoteList.Select(note => new VogenNote
                {
                    On = note.StartPos,
                    Dur = note.Length,
                    Lyric = note.Lyric,
                    Rom = note.Pronunciation ?? note.Lyric,
                    Pitch = note.KeyNumber,
                }).ToList(),
            }).ToList(),
        };

        string chart = JsonHelper.Serialize(vogen);
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry("chart.json");
            using var entryStream = entry.Open();
            var bytes = TextHelper.EncodeUtf8(chart);
            entryStream.Write(bytes, 0, bytes.Length);
        }
        return ms.ToArray();
    }
}
