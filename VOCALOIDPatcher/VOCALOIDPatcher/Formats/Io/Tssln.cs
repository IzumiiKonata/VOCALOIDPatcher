using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using VOCALOIDPatcher.Formats.Exceptions;
using VOCALOIDPatcher.Formats.Model;
using VOCALOIDPatcher.Formats.Process;
using VOCALOIDPatcher.Formats.Util;

namespace VOCALOIDPatcher.Formats.Io;

public static class Tssln
{
    private const double TickRate = 2.0;
    private static readonly Regex PhonemePartPattern = new(@"\[.+?\]", RegexOptions.Compiled);

    public static Model.Project Parse(IReadOnlyList<ImportFile> files, ImportParams parms)
    {
        var file = files[0];
        var valueTree = ValueTree.Parse(file.Content);
        if (valueTree.Type != "TSSolution")
            throw new IllegalFileException.IllegalTsslnFile();

        var trackTrees = valueTree.Children.First(c => c.Type == "Tracks").Children
            .Where(c => c.Get("Type")?.AsInt() == 0)
            .ToList();

        var (tempos, timeSignatures) = ParseMasterTrack(trackTrees[0]);
        var tracks = ParseTracks(trackTrees, parms);

        return new Model.Project(Format.Tssln, files, file.NameWithoutExtension, tracks, timeSignatures, tempos, 1, new List<ImportWarning>());
    }

    private static ValueTree ParsePluginData(byte[] pluginData)
    {
        var tree = ValueTree.Parse(pluginData);
        if (tree.Type.StartsWith("VST"))
        {
            var actualData = new byte[pluginData.Length - 48];
            Array.Copy(pluginData, 48, actualData, 0, actualData.Length);
            return ValueTree.Parse(actualData);
        }

        return tree;
    }

    private static List<Track> ParseTracks(List<ValueTree> trackTrees, ImportParams parms) =>
        trackTrees.Select((trackTree, trackIndex) =>
        {
            string trackName = trackTree.Get("Name")!.AsString();
            var pluginDataTree = ParsePluginData(trackTree.Get("PluginData")!.AsBytes());
            if (pluginDataTree.Type != "StateInformation")
                throw new IllegalFileException.IllegalTsslnFile();

            var songTree = pluginDataTree.Children.First(c => c.Type == "Song");
            var scoreTree = songTree.Children.First(c => c.Type == "Score");

            var notes = new List<Note>();
            int noteIndex = 0;
            foreach (var noteTree in scoreTree.Children)
            {
                if (noteTree.Type != "Note")
                {
                    noteIndex++;
                    continue;
                }

                int pitchStep = noteTree.Get("PitchStep")!.AsInt();
                int pitchOctave = noteTree.Get("PitchOctave")!.AsInt();
                string rawLyric = noteTree.Get("Lyric")!.AsString();
                string cleanedLyric = PhonemePartPattern.Replace(rawLyric, "");
                string lyric = string.IsNullOrWhiteSpace(cleanedLyric) ? parms.DefaultLyric : cleanedLyric;
                string phoneme = noteTree.Get("Phoneme")!.AsString().Replace(",", "");
                int tickOn = noteTree.Get("Clock")!.AsInt();
                int tickOff = tickOn + noteTree.Get("Duration")!.AsInt();

                notes.Add(new Note(noteIndex, pitchOctave * 12 + pitchStep + 12, lyric, (long)(tickOn / TickRate), (long)(tickOff / TickRate), phoneme));
                noteIndex++;
            }

            return new Track(trackIndex, trackName, notes);
        }).ToList();

    private static (List<Tempo>, List<TimeSignature>) ParseMasterTrack(ValueTree trackTree)
    {
        var pluginDataTree = ParsePluginData(trackTree.Get("PluginData")!.AsBytes());
        if (pluginDataTree.Type != "StateInformation")
            throw new IllegalFileException.IllegalTsslnFile();

        var songTree = pluginDataTree.Children.First(c => c.Type == "Song");
        var tempoTree = songTree.Children.First(c => c.Type == "Tempo");

        var tempos = tempoTree.Children.Select(c => new Tempo((long)(c.Get("Clock")!.AsInt() / TickRate), c.Get("Tempo")!.AsDouble())).ToList();

        var beatTree = songTree.Children.First(c => c.Type == "Beat");
        var timeSignatures = new List<TimeSignature>();
        int currentBeatIndex = 0;
        int currentMeasureIndex = 0;
        double beatLength = 4.0;

        foreach (var tree in beatTree.Children.OrderBy(c => c.Get("Clock")!.AsInt()))
        {
            int numerator = tree.Get("Beats")!.AsInt();
            int denominator = tree.Get("BeatType")!.AsInt();
            int clock = tree.Get("Clock")!.AsInt();
            int beatIndex = (int)Math.Floor(clock / TickRate / Constants.TicksInBeat);
            int beatNum = beatIndex - currentBeatIndex;
            if (beatNum < 0)
                throw new IllegalFileException.IllegalTsslnFile();

            int measureIndex = (int)(currentMeasureIndex + beatNum / beatLength);
            beatLength = numerator / (double)denominator * 4;
            currentBeatIndex = beatIndex;
            currentMeasureIndex = measureIndex;
            timeSignatures.Add(new TimeSignature(measureIndex, numerator, denominator));
        }

        return (tempos, timeSignatures);
    }

    public static ExportResult Generate(Model.Project project, IReadOnlyList<FeatureConfig> features)
    {
        var baseTree = ValueTree.Parse(Resources.TsslnTemplateBytes());
        var tracksTree = baseTree.Children.First(c => c.Type == "Tracks");
        var baseTrack = tracksTree.Children.First(c => c.Get("Type")?.AsInt() == 0);

        tracksTree.Children = GenerateTracks(baseTrack, project);

        return new ExportResult(baseTree.Dump(), project.Name + ".tssln", new List<ExportNotification>());
    }

    private static List<ValueTree> GenerateTracks(ValueTree baseTrack, Model.Project project) =>
        project.Tracks.Select(track =>
        {
            var trackTree = baseTrack.Clone();
            trackTree.Set("Name", Variant.FromString(track.Name));

            var pluginDataTree = ValueTree.Parse(trackTree.Get("PluginData")!.AsBytes());
            var songTree = pluginDataTree.Children.First(c => c.Type == "Song");
            var tempoTree = songTree.Children.First(c => c.Type == "Tempo");
            var beatTree = songTree.Children.First(c => c.Type == "Beat");

            tempoTree.Children = GenerateTempos(project.Tempos);
            beatTree.Children = GenerateTimeSignatures(project.TimeSignatures);

            var scoreTree = songTree.Children.First(c => c.Type == "Score");
            var newChildren = scoreTree.Children.Select(c => c.Clone()).ToList();
            newChildren.AddRange(GenerateNotes(track));
            scoreTree.Children = newChildren;

            trackTree.Set("PluginData", Variant.FromBytes(pluginDataTree.Dump()));
            return trackTree;
        }).ToList();

    private static List<ValueTree> GenerateTempos(IReadOnlyList<Tempo> tempos) =>
        tempos.Select(t =>
        {
            var tree = new ValueTree { Type = "Sound" };
            tree.Set("Clock", Variant.FromInt((int)(t.TickPosition * TickRate)));
            tree.Set("Tempo", Variant.FromDouble(t.Bpm));
            return tree;
        }).ToList();

    private static List<ValueTree> GenerateTimeSignatures(IReadOnlyList<TimeSignature> timeSignatures)
    {
        var result = new List<ValueTree>();
        int currentBeat = 0;
        int currentMeasure = 0;
        double beatLength = 4.0;

        foreach (var ts in timeSignatures)
        {
            var tree = new ValueTree { Type = "Time" };
            int numMeasure = ts.MeasurePosition - currentMeasure;
            double numBeat = numMeasure * beatLength;
            currentBeat += (int)numBeat;
            beatLength = ts.Numerator / (double)ts.Denominator * 4;
            currentMeasure = ts.MeasurePosition;

            tree.Set("Clock", Variant.FromInt((int)(currentBeat * Constants.TicksInBeat * TickRate)));
            tree.Set("Beats", Variant.FromInt(ts.Numerator));
            tree.Set("BeatType", Variant.FromInt(ts.Denominator));
            result.Add(tree);
        }

        return result;
    }

    private static List<ValueTree> GenerateNotes(Track track) =>
        track.Notes.Select(n =>
        {
            var tree = new ValueTree { Type = "Note" };
            tree.Set("PitchStep", Variant.FromInt(n.Key % 12));
            tree.Set("PitchOctave", Variant.FromInt(n.Key / 12 - 1));
            tree.Set("Lyric", Variant.FromString(n.Lyric));
            tree.Set("Phoneme", Variant.FromString(n.Phoneme ?? ""));
            tree.Set("Clock", Variant.FromInt((int)(n.TickOn * TickRate)));
            tree.Set("Syllabic", Variant.FromInt(0));
            tree.Set("Duration", Variant.FromInt((int)((n.TickOff - n.TickOn) * TickRate)));
            return tree;
        }).ToList();
}
