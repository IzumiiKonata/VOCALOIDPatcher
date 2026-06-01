using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using VOCALOIDPatcher.Formats.Exceptions;
using VOCALOIDPatcher.Formats.Model;
using VOCALOIDPatcher.Formats.Process;
using VOCALOIDPatcher.Formats.Process.Pitch;
using VOCALOIDPatcher.Formats.Util;

namespace VOCALOIDPatcher.Formats.Io;

public static class VsqLike
{
    private const long MaxVsqOutputTick = 4096L * Constants.TicksInFullNote;
    private const int MinMeasureOffset = 1;
    private const int MaxMeasureOffset = 8;
    private static readonly Regex SectionTitleRegex = new(@"^\[.*\]$", RegexOptions.Compiled);

    public static bool MatchFile(ImportFile file)
    {
        if (file.ExtensionName != "mid")
            return false;
        try
        {
            var midi = MidiFile.Parse(file.Content);
            var tracksAsText = Mid.ExtractVsqTextsFromMetaEvents(midi.Tracks).Where(s => s.Length > 0).ToList();
            if (tracksAsText.Count == 0)
                return false;
            return tracksAsText.Any(track => Texts.LinesNotBlank(track).Any(l => SectionTitleRegex.IsMatch(l)));
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static Model.Project Parse(IReadOnlyList<ImportFile> files, Format format, ImportParams parms)
    {
        var file = files[0];
        var midi = MidiFile.Parse(file.Content);
        int timeDivision = midi.TicksPerBeat;
        var warnings = new List<ImportWarning>();
        var tracksAsText = Mid.ExtractVsqTextsFromMetaEvents(midi.Tracks).Where(s => s.Length > 0).ToList();
        int measurePrefix = GetMeasurePrefix(tracksAsText[0]);
        var (tempos, timeSignatures, tickPrefix) = Mid.ParseMasterTrack(timeDivision, midi.Tracks[0], measurePrefix, warnings);

        var tracks = tracksAsText.Select((trackText, index) => ParseTrack(trackText, index, tickPrefix, parms)).ToList();

        return new Model.Project(format, files, file.NameWithoutExtension, tracks, timeSignatures, tempos, measurePrefix, warnings);
    }

    private static int GetMeasurePrefix(string firstTrack)
    {
        const string parameterName = "PreMeasure";
        foreach (var line in Texts.LinesNotBlank(firstTrack))
            if (line.Contains(parameterName))
                return int.TryParse(line.Replace($"{parameterName}=", ""), out var v) ? v : 0;
        return 0;
    }

    private sealed class Section
    {
        public List<(string Key, string Value)> Entries { get; } = new();
        private readonly Dictionary<string, string> _map = new();

        public void Add(string key, string value)
        {
            Entries.Add((key, value));
            _map[key] = value;
        }

        public string? Get(string key) => _map.TryGetValue(key, out var v) ? v : null;
    }

    private static Track ParseTrack(string trackAsText, int trackId, long tickPrefix, ImportParams parms)
    {
        var lines = Texts.LinesNotBlank(trackAsText);
        var titleWithIndexes = new List<(string Title, int Index)>();
        for (int i = 0; i < lines.Count; i++)
            if (SectionTitleRegex.IsMatch(lines[i]))
                titleWithIndexes.Add((lines[i][1..^1], i));

        var sectionMap = new Dictionary<string, Section>();
        if (titleWithIndexes.Count > 0)
        {
            for (int i = 0; i < titleWithIndexes.Count - 1; i++)
                AddSection(sectionMap, titleWithIndexes[i].Title, lines, titleWithIndexes[i].Index + 1, titleWithIndexes[i + 1].Index);
            var last = titleWithIndexes[^1];
            AddSection(sectionMap, last.Title, lines, last.Index, lines.Count);
        }

        string name = sectionMap.TryGetValue("Common", out var common)
            ? common.Get("Name") ?? ""
            : $"Track {trackId + 1}";

        if (!sectionMap.TryGetValue("EventList", out var eventList))
            return new Track(trackId, name, new List<Note>());

        var notes = new List<Note>();
        foreach (var (key, value) in eventList.Entries)
        {
            if (!long.TryParse(key, out var rawTick))
                continue;
            long tickPosition = rawTick - tickPrefix;
            if (!sectionMap.TryGetValue(value, out var section))
                continue;
            if (section.Get("Type") != "Anote")
                continue;
            if (!long.TryParse(section.Get("Length"), out var length))
                continue;
            if (!int.TryParse(section.Get("Note#"), out var key2))
                continue;

            string lyric = parms.DefaultLyric;
            string? xSampa = null;
            var lyricHandleKey = section.Get("LyricHandle");
            if (lyricHandleKey != null && sectionMap.TryGetValue(lyricHandleKey, out var lyricHandle))
            {
                var l0 = lyricHandle.Get("L0")?.Split(',');
                if (l0 is { Length: >= 2 })
                {
                    lyric = l0[0].Trim('"');
                    xSampa = l0[1].Trim('"');
                }
            }

            notes.Add(new Note(0, key2, lyric, tickPosition, tickPosition + length, xSampa));
        }

        Model.Pitch? pitch = parms.SimpleImport ? null : ParsePitchData(sectionMap, tickPrefix);
        return new Track(trackId, name, notes, pitch).ValidateNotes();
    }

    private static void AddSection(Dictionary<string, Section> map, string title, IReadOnlyList<string> lines, int start, int end)
    {
        var section = new Section();
        for (int i = start; i < end; i++)
        {
            var (key, value) = lines[i].SplitFirst("=");
            section.Add(key, value);
        }

        map[title] = section;
    }

    private static Model.Pitch? ParsePitchData(Dictionary<string, Section> sectionMap, long tickPrefix)
    {
        var pit = new List<VocaloidPartPitchData.Event>();
        if (sectionMap.TryGetValue("PitchBendBPList", out var pitSection))
            foreach (var (key, value) in pitSection.Entries)
                if (long.TryParse(key, out var pos) && int.TryParse(value, out var v))
                    pit.Add(new VocaloidPartPitchData.Event(pos - tickPrefix, v));

        var pbs = new List<VocaloidPartPitchData.Event>();
        if (sectionMap.TryGetValue("PitchBendSensBPList", out var pbsSection))
            foreach (var (key, value) in pbsSection.Entries)
                if (long.TryParse(key, out var pos) && int.TryParse(value, out var v))
                    pbs.Add(new VocaloidPartPitchData.Event(pos - tickPrefix, v));

        return VocaloidPitchConversion.PitchFromVocaloidParts(new List<VocaloidPartPitchData>
        {
            new(0, pit, pbs),
        });
    }

    public static ExportResult Generate(Model.Project project, IReadOnlyList<FeatureConfig> features, Format format)
    {
        var limited = project.LengthLimited(MaxVsqOutputTick) with
        {
            MeasurePrefix = Math.Clamp(project.MeasurePrefix, MinMeasureOffset, MaxMeasureOffset),
        };
        var projectFixed = limited.WithoutEmptyTracks() ?? throw new EmptyProjectException();

        var content = Mid.GenerateContent(projectFixed,
            (track, tickPrefix, measurePrefix) => GenerateTrack(track, tickPrefix, measurePrefix, projectFixed, features));

        var notifications = new List<ExportNotification>();
        if (!projectFixed.HasXSampaData)
            notifications.Add(ExportNotification.PhonemeResetRequiredVsq);
        if (features.Contains(Feature.ConvertPitch))
            notifications.Add(ExportNotification.PitchDataExported);

        return new ExportResult(content, FormatRegistry.Get(format).GetFileName(projectFixed.Name), notifications);
    }

    private static List<string> GeneratePitchTexts(Model.Pitch pitch, int tickPrefix, IReadOnlyList<Note> notes)
    {
        var result = new List<string>();
        var pitchRawData = pitch.GenerateForVocaloid(notes);
        if (pitchRawData == null)
            return result;

        if (pitchRawData.Pit.Count > 0)
        {
            result.Add("[PitchBendBPList]");
            foreach (var e in pitchRawData.Pit)
                result.Add($"{e.Pos + tickPrefix}={e.Value}");
        }

        if (pitchRawData.Pbs.Count > 0)
        {
            result.Add("[PitchBendSensBPList]");
            foreach (var e in pitchRawData.Pbs)
                result.Add($"{e.Pos + tickPrefix}={e.Value}");
        }

        return result;
    }

    private static string GenerateTrackText(Track track, int tickPrefix, int measurePrefix, Model.Project project, IReadOnlyList<FeatureConfig> features)
    {
        var notesLines = new List<string>();
        var lyricsLines = new List<string>();
        var tickLists = track.Notes.Select(n => n.TickOn + tickPrefix).ToList();
        foreach (var note in track.Notes)
        {
            int number = note.Id + 1;
            notesLines.Add($"[ID#{number.PadStartZero(4)}]");
            notesLines.Add("Type=Anote");
            notesLines.Add($"Length={note.Length}");
            notesLines.Add($"Note#={note.Key}");
            notesLines.Add("Dynamics=64");
            notesLines.Add("PMBendDepth=0");
            notesLines.Add("PMBendLength=0");
            notesLines.Add("PMbPortamentoUse=0");
            notesLines.Add("DEMdecGainRate=0");
            notesLines.Add("DEMaccent=0");
            notesLines.Add($"LyricHandle=h#{number.PadStartZero(4)}");

            lyricsLines.Add($"[h#{number.PadStartZero(4)}]");
            var cleanedPhonemes = note.Phoneme?.Split(' ').Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
            if (cleanedPhonemes is { Count: 0 })
                cleanedPhonemes = null;
            var phonemes = cleanedPhonemes ?? new List<string> { "a" };
            string phonemesValue = string.Join(" ", phonemes);
            var lockPhonemes = phonemes.Select(_ => "0").ToList();
            if (cleanedPhonemes != null)
                lockPhonemes[phonemes.Count - 1] = "1";
            string lockPhonemesValue = string.Join(",", lockPhonemes);
            lyricsLines.Add($"L0=\"{note.Lyric}\",\"{phonemesValue}\",0.000000,64,{lockPhonemesValue}");
        }

        var output = new List<string>
        {
            "[Common]",
            "Version=DSB301",
            $"Name={track.Name}",
            "Color=181,162,123",
            "DynamicsMode=1",
            "PlayMode=1",
        };

        if (track.Id == 0)
        {
            output.Add("[Master]");
            output.Add($"PreMeasure={measurePrefix}");
            output.Add("[Mixer]");
            output.Add("MasterFeder=0");
            output.Add("MasterPanpot=0");
            output.Add("MasterMute=0");
            output.Add("OutputMode=0");
            output.Add($"Tracks={project.Tracks.Count}");
            for (int i = 0; i < project.Tracks.Count; i++)
            {
                output.Add($"Feder{i}=0");
                output.Add($"Panpot{i}=0");
                output.Add($"Mute{i}=0");
                output.Add($"Solo{i}=0");
            }
        }

        output.Add("[EventList]");
        output.Add("0=ID#0000");
        for (int index = 0; index < tickLists.Count; index++)
            output.Add($"{tickLists[index]}=ID#{(index + 1).PadStartZero(4)}");
        output.Add($"{track.Notes[^1].TickOff + tickPrefix}=EOS");
        output.Add("[ID#0000]");
        output.Add("Type=Singer");
        output.Add("IconHandle=h#0000");
        output.AddRange(notesLines);
        output.Add("[h#0000]");
        output.Add("IconID=$07010000");
        output.Add("IDS=Miku");
        output.Add("Original=0");
        output.Add("Caption=");
        output.Add("Length=1");
        output.Add("Language=0");
        output.Add("Program=0");
        output.AddRange(lyricsLines);
        if (features.Contains(Feature.ConvertPitch) && track.Pitch != null)
            output.AddRange(GeneratePitchTexts(track.Pitch, tickPrefix, track.Notes));

        return string.Join("\n", output);
    }

    private static List<byte> GenerateTrack(Track track, int tickPrefix, int measurePrefix, Model.Project project, IReadOnlyList<FeatureConfig> features)
    {
        var bytes = new List<byte>();
        bytes.Add(0x00);
        bytes.AddRange(MidiUtil.MetaType.TrackName.EventHeaderBytes());
        bytes.AddString(track.Name, Mid.IsLittleEndian, lengthInVariableLength: true);

        var sjis = Texts.ShiftJis();
        var textBytes = sjis.GetBytes(GenerateTrackText(track, tickPrefix, measurePrefix, project, features)).ToList();
        var textEvents = new List<List<byte>>();
        while (textBytes.Count > 0)
        {
            int id = textEvents.Count;
            int idStringLength = (int)Math.Log(Math.Max(id, 1), 10000) + 1 * 4;
            string idString = id.PadStartZero(idStringLength);
            var header = Encoding.UTF8.GetBytes($"DM:{idString}:");
            int availableByteSize = 127 - header.Length;
            var chunk = header.ToList();
            chunk.AddRange(textBytes.Take(availableByteSize));
            textEvents.Add(chunk);
            textBytes = textBytes.Skip(availableByteSize).ToList();
        }

        foreach (var textEvent in textEvents)
        {
            bytes.Add(0x00);
            bytes.AddRange(MidiUtil.MetaType.Text.EventHeaderBytes());
            bytes.AddBlock(textEvent, Mid.IsLittleEndian, lengthInVariableLength: true);
        }

        bytes.Add(0x00);
        bytes.AddRange(MidiUtil.MetaType.EndOfTrack.EventHeaderBytes());
        bytes.Add(0x00);
        return bytes;
    }
}
