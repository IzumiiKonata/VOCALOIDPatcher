using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using VOCALOIDPatcher.Formats.Model;
using VOCALOIDPatcher.Formats.Process;
using VOCALOIDPatcher.Formats.Util;

namespace VOCALOIDPatcher.Formats.Io;

public static class Ust
{
    private const string LineSeparator = "\r\n";
    private const double MaxAcceptedBpm = 10000.0;

    public static Project Parse(IReadOnlyList<ImportFile> files, ImportParams parms)
    {
        var results = files.Select(ParseFile).ToList();
        string projectName = files.Count == 1
            ? files[0].NameWithoutExtension
            : $"{files[0].NameWithoutExtension} ({files.Count - 1} more)";

        var tracks = results
            .Select((r, index) => new Track(index, r.File.NameWithoutExtension, r.Notes).ValidateNotes())
            .ToList();

        var warnings = new List<ImportWarning>();
        List<Tempo> tempos;
        var withTempo = results.FirstOrDefault(r => r.Tempos.Count > 0);
        if (withTempo == null)
        {
            tempos = new List<Tempo> { Tempo.Default };
            warnings.Add(new ImportWarning.TempoNotFound());
        }
        else
        {
            var first = withTempo.Tempos[0];
            if (first.TickPosition == 0L && first.Bpm > MaxAcceptedBpm)
            {
                warnings.Add(new ImportWarning.DefaultTempoFixed(first.Bpm));
                tempos = new List<Tempo> { Tempo.Default };
                tempos.AddRange(withTempo.Tempos.Skip(1));
            }
            else
            {
                tempos = withTempo.Tempos.ToList();
            }
        }

        foreach (var result in results)
            foreach (var ignored in result.Tempos.Where(t => !tempos.Contains(t)))
                warnings.Add(new ImportWarning.TempoIgnoredInFile(result.File, ignored));

        return new Project(Format.Ust, files, projectName, tracks,
            new List<TimeSignature> { TimeSignature.Default }, tempos, 0, warnings);
    }

    private static FileResult ParseFile(ImportFile file)
    {
        var lines = Texts.LinesNotBlank(Texts.DetectAndDecode(file.Content));
        var notes = new List<Note>();
        var tempos = new Dictionary<long, double>();

        bool isHeader = true;
        long time = 0;
        int? pendingKey = null;
        string? pendingLyric = null;
        long? pendingOn = null;
        long? pendingOff = null;
        double? pendingBpm = null;

        foreach (var line in lines)
        {
            var tempoStr = TryGetValue(line, "Tempo");
            if (tempoStr != null &&
                double.TryParse(tempoStr.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var bpm))
            {
                if (isHeader)
                    tempos[0] = bpm;
                else if (pendingOn is { } tick)
                    tempos[tick] = bpm;
                else
                    pendingBpm = bpm;
            }

            if (line.Contains("[#0000]"))
                isHeader = false;

            if (line.Contains("[#"))
            {
                if (pendingKey != null && pendingLyric != null && pendingOn != null && pendingOff != null)
                    notes.Add(new Note(notes.Count, pendingKey.Value, pendingLyric, pendingOn.Value, pendingOff.Value));

                pendingKey = null;
                pendingLyric = null;
                pendingOn = null;
                pendingOff = null;
            }

            var lengthStr = TryGetValue(line, "Length");
            if (lengthStr != null &&
                double.TryParse(lengthStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var lengthValue))
            {
                pendingOn = time;
                if (pendingBpm is { } pending)
                {
                    tempos[time] = pending;
                    pendingBpm = null;
                }

                time += (long)Math.Round(lengthValue);
                pendingOff = time;
            }

            var lyric = TryGetValue(line, "Lyric");
            if (lyric is not null and not "R" and not "r")
                pendingLyric = lyric;

            var noteNum = TryGetValue(line, "NoteNum");
            if (noteNum != null && int.TryParse(noteNum, out var key))
                pendingKey = key;
        }

        var tempoList = tempos.Select(kv => new Tempo(kv.Key, kv.Value)).OrderBy(t => t.TickPosition).ToList();
        return new FileResult(file, notes, tempoList);
    }

    public static ExportResult Generate(Project project, IReadOnlyList<FeatureConfig> features)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            var sjis = Texts.ShiftJis();
            foreach (var track in project.Tracks)
            {
                string content = GenerateTrackContent(project, track);
                string safeName = Texts.GetSafeFileName(track.Name);
                string fileName = $"{project.Name}_{track.Id + 1}_{safeName}.ust";
                var entry = zip.CreateEntry(fileName);
                using var entryStream = entry.Open();
                var bytes = sjis.GetBytes(content);
                entryStream.Write(bytes, 0, bytes.Length);
            }
        }

        var notifications = new List<ExportNotification>();
        if (project.TimeSignatures.Any(t => t.Numerator != Constants.DefaultMeterHigh || t.Denominator != Constants.DefaultMeterLow))
            notifications.Add(ExportNotification.TimeSignatureIgnored);

        return new ExportResult(stream.ToArray(), project.Name + ".zip", notifications);
    }

    private static string GenerateTrackContent(Project project, Track track)
    {
        var builder = new StringBuilder();
        void AppendLine(string line) => builder.Append(line).Append(LineSeparator);

        double firstBpm = project.Tempos.Count > 0 ? project.Tempos[0].Bpm : Constants.DefaultBpm;
        AppendLine("[#VERSION]");
        AppendLine("UST Version1.2");
        AppendLine("[#SETTING]");
        AppendLine("Tempo=" + ToFixed(firstBpm, 2));
        AppendLine("Tracks=1");
        AppendLine("ProjectName=" + track.Name);
        AppendLine("Mode2=True");

        long tickPos = 0;
        int restCount = 0;
        int? nextTempoIndex = 1 < project.Tempos.Count ? 1 : null;

        Tempo? GetNextTempo() => nextTempoIndex is { } i ? project.Tempos[i] : null;
        void IncreaseNextTempoIndex()
        {
            if (nextTempoIndex is { } i)
                nextTempoIndex = i + 1 < project.Tempos.Count ? i + 1 : null;
        }

        foreach (var note in track.Notes)
        {
            if (tickPos < note.TickOn)
            {
                var nextTempo = GetNextTempo();
                long restOn = tickPos;
                string? restBpm = null;
                if (nextTempo != null && nextTempo.TickPosition >= restOn && nextTempo.TickPosition < note.TickOn)
                {
                    AppendLine("[#" + PadNumber(note.Id + restCount) + "]");
                    AppendLine("Length=" + (nextTempo.TickPosition - restOn));
                    AppendLine("Lyric=R");
                    AppendLine("NoteNum=60");
                    AppendLine("PreUtterance=");
                    restCount++;
                    restOn = nextTempo.TickPosition;
                    restBpm = ToFixed(nextTempo.Bpm, 2);
                    IncreaseNextTempoIndex();
                }

                AppendLine("[#" + PadNumber(note.Id + restCount) + "]");
                AppendLine("Length=" + (note.TickOn - restOn));
                AppendLine("Lyric=R");
                AppendLine("NoteNum=60");
                if (restBpm != null)
                    AppendLine("Tempo=" + restBpm);
                AppendLine("PreUtterance=");
                restCount++;
            }

            var noteTempo = GetNextTempo();
            string? noteBpm = null;
            if (noteTempo != null && noteTempo.TickPosition >= note.TickOn && noteTempo.TickPosition < note.TickOff)
            {
                noteBpm = ToFixed(noteTempo.Bpm, 2);
                IncreaseNextTempoIndex();
            }

            AppendLine("[#" + PadNumber(note.Id + restCount) + "]");
            AppendLine("Length=" + note.Length);
            AppendLine("Lyric=" + note.Lyric);
            AppendLine("NoteNum=" + note.Key);
            if (noteBpm != null)
                AppendLine("Tempo=" + noteBpm);
            AppendLine("PreUtterance=");

            tickPos = note.TickOff;
        }

        AppendLine("[#TRACKEND]");
        return builder.ToString();
    }

    private static string? TryGetValue(string line, string key)
    {
        if (!line.StartsWith(key + "=", StringComparison.Ordinal))
            return null;
        int index = line.IndexOf('=');
        if (index < 0 || index >= line.Length - 1)
            return null;
        var value = line[(index + 1)..];
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string ToFixed(double value, int digits) =>
        value.ToString("F" + digits, CultureInfo.InvariantCulture);

    private static string PadNumber(int value) => value.ToString(CultureInfo.InvariantCulture).PadLeft(4, '0');

    private sealed class FileResult
    {
        public FileResult(ImportFile file, List<Note> notes, List<Tempo> tempos)
        {
            File = file;
            Notes = notes;
            Tempos = tempos;
        }

        public ImportFile File { get; }
        public List<Note> Notes { get; }
        public List<Tempo> Tempos { get; }
    }
}
