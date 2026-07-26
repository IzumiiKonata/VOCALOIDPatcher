using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Framework;
using VOCALOIDPatcher.Formats.LibreSvip.Model;
using VOCALOIDPatcher.Formats.LibreSvip.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Svp;

public enum SvpVersionCompatibility
{
    Below190 = 100,
    Between1100And1112 = 135,
    Above200 = 182,
}

public enum SvpVibratoMode
{
    None,
    Always,
    Hybrid,
}

public enum SvpLanguage
{
    Mandarin,
    Cantonese,
    Japanese,
    English,
    Spanish,
    Korean,
    German,
    French,
    Portuguese,
}

public enum SvpPitchMode
{
    Full,
    Vibrato,
    Plain,
}

public enum SvpBreathMode
{
    Ignore,
    Keep,
    Convert,
}

public enum SvpGroupMode
{
    Split,
    Merge,
}

public sealed class SvpConverter : FormatConverter
{
    private const long TickRate = 1470000;

    private static readonly HashSet<char> SymbolBlacklist = new(
        "()[]{}（）<>《》―—*×!！?？:：·•。,，;；^`\"‘’“”=、_$%~@#…&￥");

    public bool ImportInstrumental { get; set; } = true;
    public bool ImportPitch { get; set; } = true;
    public bool ImportVolume { get; set; } = true;
    public bool ImportBreath { get; set; } = true;
    public bool ImportGender { get; set; } = true;
    public bool ImportStrength { get; set; } = true;
    public bool Instant { get; set; } = true;
    public SvpPitchMode PitchMode { get; set; } = SvpPitchMode.Plain;
    public SvpBreathMode Breath { get; set; } = SvpBreathMode.Convert;
    public SvpGroupMode Group { get; set; } = SvpGroupMode.Split;
    public SvpVersionCompatibility VersionCompatibility { get; set; } = SvpVersionCompatibility.Below190;
    public SvpVibratoMode Vibrato { get; set; } = SvpVibratoMode.None;
    public int DownSample { get; set; } = 20;
    public SvpLanguage LanguageOverride { get; set; } = SvpLanguage.Mandarin;

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
        var synchronizer = new TimeSynchronizer(tempos);

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

            var mainSinging = new SingingTrack
            {
                Title = svTrack.Name,
                AiSingerName = svTrack.MainRef.Database.Name,
                Mute = svTrack.Mixer.Mute,
                Solo = svTrack.Mixer.Solo,
                Pan = svTrack.Mixer.Pan,
                Volume = ParseVolume(svTrack.Mixer.GainDecibel),
                NoteList = ParseNotes(svTrack.MainGroup.Notes, 0, 0),
            };
            mainSinging.EditedParams = ParseParams(
                svTrack.MainGroup.Parameters,
                svTrack.MainGroup.Notes,
                synchronizer,
                svTrack.MainRef.Voice,
                svTrack.MainGroup.PitchControls,
                svTrack.MainRef.SystemPitchDelta,
                null);
            trackList.Add(mainSinging);

            foreach (var svRef in svTrack.Groups)
            {
                if (!library.TryGetValue(svRef.GroupId, out var group))
                    continue;
                splitCounts.TryGetValue(svRef.GroupId, out int count);
                splitCounts[svRef.GroupId] = count + 1;
                var groupNotes = ParseNotes(group.Notes, svRef.BlickOffset, svRef.PitchOffset);
                var groupSinging = new SingingTrack
                {
                    Title = $"{group.Name} ({count + 1})",
                    AiSingerName = svRef.Database.Name,
                    NoteList = groupNotes,
                };
                groupSinging.EditedParams = ParseParams(
                    group.Parameters,
                    group.Notes,
                    synchronizer,
                    svRef.Voice,
                    group.PitchControls,
                    svRef.SystemPitchDelta,
                    svTrack.MainGroup.Parameters,
                    svRef.BlickOffset,
                    svRef.PitchOffset);
                if (Group == SvpGroupMode.Merge && !HasOverlap(mainSinging.NoteList, groupNotes))
                    MergeTrack(mainSinging, groupSinging);
                else
                    groupTracks.Add(groupSinging);
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

    private List<Note> ParseNotes(List<SVNote> notes, long blickOffset, int pitchOffset)
    {
        var result = new List<Note>();
        for (int index = 0; index < notes.Count; index++)
        {
            var svNote = notes[index];
            bool isBreath = Regex.IsMatch(svNote.Lyrics, @"^\s*\.?\s*br(l?[1-9])?\s*$", RegexOptions.IgnoreCase);
            if (isBreath && Breath != SvpBreathMode.Keep)
                continue;
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
            if (Breath == SvpBreathMode.Convert && index > 0)
            {
                var previous = notes[index - 1];
                if (Regex.IsMatch(previous.Lyrics, @"^\s*\.?\s*br(l?[1-9])?\s*$", RegexOptions.IgnoreCase)
                    && PositionToTicks(svNote.Onset - previous.Onset - previous.Duration) < 120)
                    result[^1].HeadTag = "V";
            }
        }
        return result;
    }

    private ParamCurve? ParsePitch(
        SVParameters parameters,
        List<SVNote> svNotes,
        TimeSynchronizer synchronizer,
        SVNoteAttributes? voice,
        List<SVPitchControl>? svPitchControls = null,
        SVParamCurve? instantPitch = null,
        SVParameters? masterParameters = null,
        long blickOffset = 0,
        int pitchOffset = 0)
    {
        var validNotes = svNotes.Where(n => n.Onset + blickOffset >= 0).ToList();
        if (validNotes.Count == 0)
            return null;
        IParamExpression pitchDiff = new CurveGenerator(
            parameters.PitchDelta.Points.Select(p =>
                new Point(PositionToTicks(p.Offset + blickOffset), (int)Math.Round(p.Value))),
            Interp(parameters.PitchDelta.Mode), 0);
        if (masterParameters != null)
        {
            pitchDiff = new SumParamExpression(pitchDiff, new CurveGenerator(
                masterParameters.PitchDelta.Points.Select(p =>
                    new Point(PositionToTicks(p.Offset), (int)Math.Round(p.Value))),
                Interp(masterParameters.PitchDelta.Mode), 0));
        }
        if (Instant && instantPitch is { Points.Count: > 0 })
        {
            var instantDiff = new CurveGenerator(
                instantPitch.Points.Select(p =>
                    new Point(PositionToTicks(p.Offset + blickOffset), (int)Math.Round(p.Value))),
                Interp(instantPitch.Mode), 0);
            pitchDiff = new SumParamExpression(pitchDiff, new MaskedParamExpression(
                instantDiff,
                validNotes
                    .Where(note => note.InstantMode != false)
                    .Select(note => (
                        PositionToTicks(note.Onset + blickOffset),
                        PositionToTicks(note.Onset + blickOffset + note.Duration)))));
        }
        IParamExpression vibratoEnv = new CurveGenerator(
            parameters.VibratoEnv.Points.Select(p =>
                new Point(PositionToTicks(p.Offset + blickOffset), (int)Math.Round(p.Value * 1000))),
            Interp(parameters.VibratoEnv.Mode), 1000);
        if (masterParameters != null)
        {
            vibratoEnv = new SumParamExpression(vibratoEnv, new CurveGenerator(
                masterParameters.VibratoEnv.Points.Select(p =>
                    new Point(PositionToTicks(p.Offset), (int)Math.Round(p.Value * 1000))),
                Interp(masterParameters.VibratoEnv.Mode), 0));
        }
        var noteStructs = validNotes
            .Select(n => ToNoteStruct(n, synchronizer, voice, blickOffset, pitchOffset))
            .ToList();
        var pitchControls = ConvertPitchControls(svPitchControls, blickOffset, pitchOffset);
        var generator = new PitchGenerator(synchronizer, noteStructs, pitchDiff, vibratoEnv, pitchControls);
        var interval = new RangeInterval(
            validNotes.Select(n => (
                PositionToTicks(n.Onset + blickOffset),
                PositionToTicks(n.Onset + blickOffset + n.Duration)))).Expand(120);
        if (PitchMode != SvpPitchMode.Full)
        {
            var edited = new List<(int, int)>();
            foreach (var note in validNotes)
            {
                if (IsPitchEdited(note, voice, PitchMode == SvpPitchMode.Plain, Instant))
                {
                    int startTick = PositionToTicks(note.Onset + blickOffset);
                    int endTick = PositionToTicks(note.Onset + blickOffset + note.Duration);
                    var attributes = ResolveAttributes(note, voice);
                    double startSecs = synchronizer.GetActualSecsFromTicks(startTick)
                        - Math.Max(0, attributes.TransitionOffset) - 0.1;
                    double endSecs = synchronizer.GetActualSecsFromTicks(endTick) + 0.1;
                    edited.Add((
                        (int)Math.Round(synchronizer.GetActualTicksFromSecs(Math.Max(0, startSecs))),
                        (int)Math.Round(synchronizer.GetActualTicksFromSecs(endSecs))));
                }
            }
            edited.AddRange(CurveEditedRanges(parameters.PitchDelta, 0, blickOffset));
            edited.AddRange(CurveEditedRanges(parameters.VibratoEnv, 1, blickOffset));
            if (masterParameters != null)
            {
                edited.AddRange(CurveEditedRanges(masterParameters.PitchDelta, 0, 0));
                edited.AddRange(CurveEditedRanges(masterParameters.VibratoEnv, 1, 0));
            }
            if (svPitchControls != null)
                edited.AddRange(svPitchControls.Select(control =>
                {
                    int start = PositionToTicks(control.Pos + blickOffset);
                    int end = control.Points.Count == 0
                        ? start + 1
                        : start + PositionToTicks(control.Points[^1].Offset);
                    return (start, end);
                }));
            interval = interval.Intersect(new RangeInterval(edited));
        }

        var points = new List<Point> { Point.StartPoint() };
        foreach (var (start, end) in interval.Shift(_firstBarTick).SubRanges())
        {
            points.Add(new Point(start, -100));
            for (int i = start; i < end; i += 5)
                points.Add(new Point(i, (int)Math.Round(generator.ValueAtTicks(i - _firstBarTick))));
            points.Add(new Point(end, (int)Math.Round(generator.ValueAtTicks(end - _firstBarTick))));
            points.Add(new Point(end, -100));
        }
        points.Add(Point.EndPoint());
        return new ParamCurve { Points = points };
    }

    private Params ParseParams(
        SVParameters parameters,
        List<SVNote> notes,
        TimeSynchronizer synchronizer,
        SVNoteAttributes? voice,
        List<SVPitchControl>? pitchControls,
        SVParamCurve? instantPitch,
        SVParameters? masterParameters,
        long blickOffset = 0,
        int pitchOffset = 0)
    {
        var result = new Params();
        if (ImportPitch && notes.Count > 0)
            result.Pitch = ParsePitch(
                parameters, notes, synchronizer, voice, pitchControls, instantPitch,
                masterParameters, blickOffset, pitchOffset)
                ?? new ParamCurve();
        if (ImportVolume)
            result.Volume = ParseParamCurve(parameters.Loudness, masterParameters?.Loudness,
                blickOffset, voice?.ParamLoudness ?? 0,
                value => value >= 0
                    ? (int)Math.Round(value / 12.0 * 1000)
                    : (int)Math.Round(1000 * MusicMath.DbToFloat(value) - 1000));
        if (ImportBreath)
            result.Breath = ParseParamCurve(parameters.Breathiness, masterParameters?.Breathiness,
                blickOffset, voice?.ParamBreathiness ?? 0,
                value => (int)Math.Round(value * 1000));
        if (ImportGender)
            result.Gender = ParseParamCurve(parameters.Gender, masterParameters?.Gender,
                blickOffset, voice?.ParamGender ?? 0,
                value => (int)Math.Round(value * -1000));
        if (ImportStrength)
            result.Strength = ParseParamCurve(parameters.Tension, masterParameters?.Tension,
                blickOffset, voice?.ParamTension ?? 0,
                value => (int)Math.Round(value * 1000));
        return result;
    }

    private ParamCurve ParseParamCurve(
        SVParamCurve source,
        SVParamCurve? master,
        long blickOffset,
        double baseValue,
        Func<double, int> mapping)
    {
        int baseMapped = Math.Clamp(mapping(baseValue), -1000, 1000);
        var points = new List<Point> { new(Point.StartX, baseMapped) };
        if (source.Points.Count > 0 || master is { Points.Count: > 0 })
        {
            var groupGenerator = new CurveGenerator(
                source.Points.Select(point => new Point(
                    PositionToTicks(point.Offset + blickOffset) + _firstBarTick,
                    (int)Math.Round(point.Value * 1000))),
                Interp(source.Mode),
                0);
            var masterGenerator = master == null
                ? null
                : new CurveGenerator(
                    master.Points.Select(point => new Point(
                        PositionToTicks(point.Offset) + _firstBarTick,
                        (int)Math.Round(point.Value * 1000))),
                    Interp(master.Mode),
                    0);
            var positions = source.Points
                .Select(point => PositionToTicks(point.Offset + blickOffset) + _firstBarTick)
                .Concat(master?.Points.Select(point => PositionToTicks(point.Offset) + _firstBarTick)
                    ?? Enumerable.Empty<int>())
                .ToList();
            int start = positions.Min();
            int end = positions.Max();
            for (int pos = start; pos < end; pos += 5)
            {
                double raw = baseValue + groupGenerator.ValueAtTicks(pos) / 1000.0
                    + (masterGenerator?.ValueAtTicks(pos) ?? 0) / 1000.0;
                points.Add(new Point(pos, Math.Clamp(mapping(raw), -1000, 1000)));
            }
            double endRaw = baseValue + groupGenerator.ValueAtTicks(end) / 1000.0
                + (masterGenerator?.ValueAtTicks(end) ?? 0) / 1000.0;
            points.Add(new Point(end, Math.Clamp(mapping(endRaw), -1000, 1000)));
        }
        points.Add(new Point(Point.EndX, baseMapped));
        return new ParamCurve { Points = points };
    }

    private static bool IsPitchEdited(
        SVNote note,
        SVNoteAttributes? voice,
        bool regardDefaultVibratoAsUnedited,
        bool considerInstant)
    {
        var sources = new[] { note.Attributes, note.SystemAttributes, voice }
            .Where(source => source != null)
            .Cast<SVNoteAttributes>()
            .ToList();
        bool transitionEdited = sources.Any(attributes =>
            attributes.TF0Offset.HasValue
            || attributes.TF0Left.HasValue
            || attributes.TF0Right.HasValue
            || attributes.DF0Left.HasValue
            || attributes.DF0Right.HasValue);
        var resolved = ResolveAttributes(note, voice);
        if (considerInstant && note.InstantMode != false)
        {
            transitionEdited &= Math.Abs(resolved.PortamentoLeft - 0.07) >= 0.000001
                || Math.Abs(resolved.PortamentoRight - 0.07) >= 0.000001
                || Math.Abs(resolved.DepthLeft - 0.15) >= 0.000001
                || Math.Abs(resolved.DepthRight - 0.15) >= 0.000001;
        }
        bool vibratoEdited = Math.Abs(resolved.VibratoDepth) >= 0.000001;
        if (regardDefaultVibratoAsUnedited)
        {
            vibratoEdited &= sources.Any(attributes =>
                attributes.TF0VbrStart.HasValue
                || attributes.TF0VbrLeft.HasValue
                || attributes.TF0VbrRight.HasValue
                || attributes.DF0Vbr.HasValue
                || attributes.FF0Vbr.HasValue
                || attributes.PF0Vbr.HasValue);
        }
        return transitionEdited || vibratoEdited;
    }

    private static IEnumerable<(int Start, int End)> CurveEditedRanges(
        SVParamCurve curve,
        double defaultValue,
        long blickOffset)
    {
        const double tolerance = 0.000001;
        var points = curve.Points
            .Select(point => (
                Position: PositionToTicks(point.Offset + blickOffset),
                point.Value))
            .ToList();
        if (points.Count == 0)
            return Array.Empty<(int, int)>();
        if (points.Count == 1)
            return Math.Abs(points[0].Value - defaultValue) < tolerance
                ? Array.Empty<(int, int)>()
                : new[] { (0, int.MaxValue / 2) };
        var ranges = new List<(int, int)>();
        if (Math.Abs(points[0].Value - defaultValue) >= tolerance && points[0].Position > 0)
            ranges.Add((0, points[0].Position));
        int start = points[0].Position;
        int end = points[0].Position;
        for (int index = 1; index < points.Count; index++)
        {
            if (Math.Abs(points[index - 1].Value - defaultValue) < tolerance
                && Math.Abs(points[index].Value - defaultValue) < tolerance)
            {
                if (start < end)
                    ranges.Add((start, end));
                start = points[index].Position;
            }
            else
                end = points[index].Position;
        }
        if (start < end)
            ranges.Add((start, end));
        if (Math.Abs(points[^1].Value - defaultValue) >= tolerance)
            ranges.Add((points[^1].Position, int.MaxValue / 2));
        return ranges;
    }

    private static bool HasOverlap(List<Note> left, List<Note> right) =>
        left.Any(a => right.Any(b => a.StartPos < b.EndPos && b.StartPos < a.EndPos));

    private static void MergeTrack(SingingTrack target, SingingTrack source)
    {
        target.NoteList.AddRange(source.NoteList);
        target.NoteList.Sort((a, b) => a.StartPos.CompareTo(b.StartPos));
        MergeCurve(target.EditedParams.Pitch, source.EditedParams.Pitch, -100);
        MergeCurve(target.EditedParams.Volume, source.EditedParams.Volume, 0);
        MergeCurve(target.EditedParams.Breath, source.EditedParams.Breath, 0);
        MergeCurve(target.EditedParams.Gender, source.EditedParams.Gender, 0);
        MergeCurve(target.EditedParams.Strength, source.EditedParams.Strength, 0);
    }

    private static void MergeCurve(ParamCurve target, ParamCurve source, int termination)
    {
        var points = target.Points.Concat(source.Points)
            .Where(point => point.X != Point.StartX && point.X != Point.EndX)
            .OrderBy(point => point.X)
            .ToList();
        target.Points = new List<Point> { new(Point.StartX, termination) };
        target.Points.AddRange(points);
        target.Points.Add(new Point(Point.EndX, termination));
    }

    private static List<PitchControl>? ConvertPitchControls(
        List<SVPitchControl>? controls,
        long blickOffset,
        int pitchOffset)
    {
        if (controls == null || controls.Count == 0)
            return null;
        return controls.Select(control => new PitchControl
        {
            Pos = PositionToTicks(control.Pos + blickOffset),
            Pitch = control.Pitch + pitchOffset,
            Type = control.Type,
            Points = control.Points.Select(point =>
                new PitchControlPoint(PositionToTicks(point.Offset), point.Value)).ToList(),
        }).ToList();
    }

    private static NoteStruct ToNoteStruct(
        SVNote note,
        TimeSynchronizer synchronizer,
        SVNoteAttributes? voice,
        long blickOffset,
        int pitchOffset)
    {
        var a = ResolveAttributes(note, voice);
        return new NoteStruct(
            note.Pitch + pitchOffset,
            synchronizer.GetActualSecsFromTicks(PositionToTicks(note.Onset + blickOffset)),
            synchronizer.GetActualSecsFromTicks(PositionToTicks(note.Onset + blickOffset + note.Duration)),
            a.TransitionOffset, a.PortamentoLeft, a.PortamentoRight, a.DepthLeft, a.DepthRight,
            a.VibratoStart, a.VibratoLeft, a.VibratoRight, a.VibratoDepth, a.VibratoFrequency, a.VibratoPhase);
    }

    private static SVNoteAttributes ResolveAttributes(SVNote note, SVNoteAttributes? voice)
    {
        var noteAttributes = note.Attributes ?? new SVNoteAttributes();
        var systemAttributes = note.SystemAttributes;
        return new SVNoteAttributes
        {
            TF0Offset = noteAttributes.TF0Offset ?? systemAttributes?.TF0Offset ?? voice?.TF0Offset,
            TF0Left = noteAttributes.TF0Left ?? systemAttributes?.TF0Left ?? voice?.TF0Left,
            TF0Right = noteAttributes.TF0Right ?? systemAttributes?.TF0Right ?? voice?.TF0Right,
            DF0Left = noteAttributes.DF0Left ?? systemAttributes?.DF0Left ?? voice?.DF0Left,
            DF0Right = noteAttributes.DF0Right ?? systemAttributes?.DF0Right ?? voice?.DF0Right,
            TF0VbrStart = noteAttributes.TF0VbrStart ?? systemAttributes?.TF0VbrStart ?? voice?.TF0VbrStart,
            TF0VbrLeft = noteAttributes.TF0VbrLeft ?? systemAttributes?.TF0VbrLeft ?? voice?.TF0VbrLeft,
            TF0VbrRight = noteAttributes.TF0VbrRight ?? systemAttributes?.TF0VbrRight ?? voice?.TF0VbrRight,
            DF0Vbr = noteAttributes.DF0Vbr ?? systemAttributes?.DF0Vbr ?? voice?.DF0Vbr,
            FF0Vbr = noteAttributes.FF0Vbr ?? systemAttributes?.FF0Vbr ?? voice?.FF0Vbr,
            PF0Vbr = noteAttributes.PF0Vbr ?? systemAttributes?.PF0Vbr ?? voice?.PF0Vbr,
        };
    }

    private static InterpolationFunc Interp(string mode) => mode switch
    {
        "cosine" => MusicMath.CosineEasingInOutInterpolation,
        "cubic" => MusicMath.CubicInterpolation,
        _ => MusicMath.LinearInterpolation,
    };

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
        var svp = new SVProject
        {
            Version = (int)VersionCompatibility,
            InstantModeEnabled = VersionCompatibility == SvpVersionCompatibility.Below190 ? false : null,
        };
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

        var synchronizer = new TimeSynchronizer(project.SongTempoList, _firstBarTick);

        foreach (var track in project.TrackList)
        {
            if (track is SingingTrack singing)
            {
                var language = GetLanguagePreset(LanguageOverride);
                var reducedPitch = singing.EditedParams.Pitch.ReduceSampleRate(Math.Max(DownSample, 0), -100);
                var hybridIndexes = Vibrato == SvpVibratoMode.Hybrid
                    ? FindEditedVibratoNotes(reducedPitch, singing.NoteList, synchronizer)
                    : new HashSet<int>();
                var group = new SVGroup
                {
                    Uuid = Guid.NewGuid().ToString(),
                    Parameters = GenerateParams(singing.EditedParams, singing.NoteList, synchronizer, project.TimeSignatureList),
                };
                if (VersionCompatibility == SvpVersionCompatibility.Above200)
                {
                    group.Parameters.PitchDelta = new SVParamCurve();
                    group.PitchControls = GeneratePitchControls(reducedPitch);
                }
                group.Notes = singing.NoteList.Select((n, index) => new SVNote
                {
                    Onset = TicksToPosition(n.StartPos),
                    Duration = TicksToPosition(n.EndPos) - TicksToPosition(n.StartPos),
                    Lyrics = n.Lyric,
                    Phonemes = n.Pronunciation ?? "",
                    Pitch = n.KeyNumber,
                    InstantMode = VersionCompatibility == SvpVersionCompatibility.Between1100And1112 ? false : null,
                    Attributes = new SVNoteAttributes
                    {
                        DF0Vbr = Vibrato == SvpVibratoMode.None
                            || Vibrato == SvpVibratoMode.Hybrid && hybridIndexes.Contains(index)
                            ? 0.0
                            : null,
                    },
                }).ToList();
                svp.Tracks.Add(new SVTrack
                {
                    Name = singing.Title,
                    Mixer = new SVMixer { GainDecibel = GenerateVolume(singing.Volume), Pan = singing.Pan, Mute = singing.Mute, Solo = singing.Solo },
                    MainRef = new SVRef
                    {
                        IsInstrumental = false,
                        Database = new SVDatabase
                        {
                            Name = singing.AiSingerName,
                            LanguageOverride = language.Language,
                            PhonesetOverride = language.Phoneset,
                        },
                        GroupId = Guid.NewGuid().ToString(),
                    },
                    MainGroup = group,
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

    private static (string Language, string Phoneset) GetLanguagePreset(SvpLanguage language) => language switch
    {
        SvpLanguage.Cantonese => ("cantonese", "xsampa"),
        SvpLanguage.Japanese => ("japanese", "romaji"),
        SvpLanguage.English => ("english", "arpabet"),
        SvpLanguage.Spanish => ("spanish", "xsampa"),
        SvpLanguage.Korean => ("korean", "xsampa"),
        SvpLanguage.German => ("german", "xsampa"),
        SvpLanguage.French => ("french", "xsampa"),
        SvpLanguage.Portuguese => ("portuguese", "xsampa"),
        _ => ("mandarin", "xsampa"),
    };

    private HashSet<int> FindEditedVibratoNotes(
        ParamCurve curve,
        List<Note> notes,
        TimeSynchronizer synchronizer)
    {
        var result = new HashSet<int>();
        foreach (var point in curve.Points)
        {
            if (point.X < _firstBarTick || point.Y == -100)
                continue;
            int pos = point.X - _firstBarTick;
            int noteIndex = Search.FindLastIndex(notes, note => note.StartPos <= pos);
            if (noteIndex < 0)
                continue;
            var note = notes[noteIndex];
            if (pos < note.EndPos
                && synchronizer.GetDurationSecsFromTicks(note.StartPos, pos) > 0.25)
                result.Add(noteIndex);
        }
        return result;
    }

    private List<SVPitchControl> GeneratePitchControls(ParamCurve curve)
    {
        var result = new List<SVPitchControl>();
        var buffer = new List<Point>();
        void Flush()
        {
            if (buffer.Count == 0)
                return;
            long basePosition = TicksToPosition(buffer[0].X);
            double basePitch = buffer[0].Y / 100.0;
            result.Add(new SVPitchControl
            {
                Pos = basePosition,
                Pitch = basePitch,
                Id = (result.Count + 1).ToString(),
                Points = buffer.Select(point =>
                    new SvParamPoint(
                        TicksToPosition(point.X) - basePosition,
                        point.Y / 100.0 - basePitch)).ToList(),
            });
            buffer.Clear();
        }

        foreach (var point in curve.Points)
        {
            if (point.X < _firstBarTick)
                continue;
            var shifted = point.WithX(point.X - _firstBarTick);
            if (shifted.Y == -100)
                Flush();
            else
                buffer.Add(shifted);
        }
        Flush();
        return result;
    }

    private SVParameters GenerateParams(Params parameters, List<Note> noteList, TimeSynchronizer synchronizer,
        List<TimeSignature> timeSignatureList)
    {
        int downSample = Math.Max(DownSample, 0);
        var result = new SVParameters
        {
            Loudness = GenerateParamCurve(parameters.Volume.ReduceSampleRate(downSample), 0, 0.0, val =>
                val >= 0
                    ? val / 1000.0 * 12.0
                    : Math.Max(MusicMath.RatioToDb(val > -997 ? val / 1000.0 + 1.0 : 0.0039), -48.0)),
            Tension = GenerateParamCurve(parameters.Strength.ReduceSampleRate(downSample), 0, 0.0, val => 1000.0 / val),
            Breathiness = GenerateParamCurve(parameters.Breath.ReduceSampleRate(downSample), 0, 0.0, val => 1000.0 / val),
            Gender = GenerateParamCurve(parameters.Gender.ReduceSampleRate(downSample), 0, 0.0, val => -1000.0 / val),
        };
        result.PitchDelta = GeneratePitchCurve(parameters.Pitch.ReduceSampleRate(downSample, -100), noteList, synchronizer, timeSignatureList);
        return result;
    }

    private SVParamCurve GeneratePitchCurve(ParamCurve curve, List<Note> noteList, TimeSynchronizer synchronizer,
        List<TimeSignature> timeSignatureList)
    {
        var svCurve = new SVParamCurve();
        if (noteList.Count == 0)
            return svCurve;
        var simulator = new PitchSimulator(synchronizer, PortamentoPitch.NoPortamento(), noteList, timeSignatureList);
        var pointList = svCurve.Points;
        var buffer = new List<Point>();
        const int minInterval = 1;
        Point? lastPoint = null;
        foreach (var point in curve.Points)
        {
            if (point.X < _firstBarTick)
                continue;
            var shifted = point.WithX(point.X - _firstBarTick);
            if (shifted.Y == -100)
            {
                if (buffer.Count == 0)
                    continue;
                if (lastPoint is not { } lp || lp.X + minInterval < buffer[0].X)
                {
                    if (lastPoint is { } lp2 && lp2.X + 2 * minInterval < buffer[0].X)
                        pointList.Add(new SvParamPoint(TicksToPosition(lp2.X + minInterval), 0));
                    pointList.Add(new SvParamPoint(TicksToPosition(buffer[0].X - minInterval), 0));
                }
                foreach (var tmp in buffer)
                    pointList.Add(new SvParamPoint(
                        TicksToPosition(tmp.X),
                        GeneratePitchDiff(simulator, synchronizer, tmp.X, tmp.Y)));
                lastPoint = buffer[^1];
                buffer.Clear();
            }
            else
                buffer.Add(shifted);
        }
        if (lastPoint is { } last)
            pointList.Add(new SvParamPoint(TicksToPosition(last.X + minInterval), 0));
        return svCurve;
    }

    private static double GeneratePitchDiff(PitchSimulator simulator,
        TimeSynchronizer synchronizer, int pos, int pitch)
    {
        double? simulatedPitch = simulator.PitchAtSecs(synchronizer.GetActualSecsFromTicks(pos));
        if (simulatedPitch != null)
            return pitch - simulatedPitch.Value;
        return 0.0;
    }

    private SVParamCurve GenerateParamCurve(ParamCurve curve, int termination, double defaultValue,
        Func<int, double> mappingFunc)
    {
        var svCurve = new SVParamCurve();
        if (curve.Points.Count == 0)
            return svCurve;
        if (DownSample > 15)
            svCurve.Mode = "cubic";
        int skipped = 0;
        var pointList = svCurve.Points;
        var points = curve.Points;
        if (points[0].X == Point.StartX)
        {
            if (points.Count == 2 && points[1].X == Point.EndX)
            {
                if (points[0].Y != termination)
                    pointList.Add(new SvParamPoint(0, mappingFunc(points[0].Y)));
                return svCurve;
            }
            skipped = 1;
            int validIndex = Search.FindIndex(points, p => p.X >= _firstBarTick);
            if (validIndex != -1 && points.Count > validIndex + 1
                && (points[validIndex].Y != termination
                    || points[validIndex + 1].Y != termination
                    || points[validIndex + 1].X == Point.EndX))
            {
                skipped = validIndex + 1;
                pointList.Add(new SvParamPoint(
                    TicksToPosition(points[validIndex].X - _firstBarTick),
                    mappingFunc(points[validIndex].Y)));
            }
        }
        var buffer = new List<Point>();
        const int minInterval = 1;
        Point? lastPoint = null;
        for (int idx = skipped; idx < points.Count; idx++)
        {
            var point = points[idx];
            if (point.X < _firstBarTick || point.X == Point.EndX)
                continue;
            var shifted = point.WithX(point.X - _firstBarTick);
            if (shifted.Y == termination)
            {
                if (buffer.Count == 0)
                    continue;
                if (lastPoint is not { } lp || lp.X + minInterval < buffer[0].X)
                {
                    if (lastPoint is { } lp2 && lp2.X + 2 * minInterval < buffer[0].X)
                        pointList.Add(new SvParamPoint(TicksToPosition(lp2.X + minInterval), defaultValue));
                    pointList.Add(new SvParamPoint(TicksToPosition(buffer[0].X - minInterval), defaultValue));
                }
                foreach (var tmp in buffer)
                    pointList.Add(new SvParamPoint(TicksToPosition(tmp.X), mappingFunc(tmp.Y)));
                lastPoint = buffer[^1];
                buffer.Clear();
            }
            else
                buffer.Add(shifted);
        }
        if (buffer.Count == 0)
        {
            if (lastPoint is { } lpEnd)
                pointList.Add(new SvParamPoint(TicksToPosition(lpEnd.X + minInterval), defaultValue));
            return svCurve;
        }
        if (lastPoint is not { } lpr || lpr.X + minInterval < buffer[0].X)
        {
            if (lastPoint is { } lpr2 && lpr2.X + 2 * minInterval < buffer[0].X)
                pointList.Add(new SvParamPoint(TicksToPosition(lpr2.X + minInterval), defaultValue));
            pointList.Add(new SvParamPoint(TicksToPosition(buffer[0].X - minInterval), defaultValue));
        }
        foreach (var tmp in buffer)
            pointList.Add(new SvParamPoint(TicksToPosition(tmp.X), mappingFunc(tmp.Y)));
        var tail = buffer[^1];
        buffer.Clear();
        if (tail.Y == termination)
            pointList.Add(new SvParamPoint(TicksToPosition(tail.X + minInterval), defaultValue));
        return svCurve;
    }

    private static NoteStruct ToNoteStruct(Note note, TimeSynchronizer synchronizer) =>
        new(
            note.KeyNumber,
            synchronizer.GetActualSecsFromTicks(note.StartPos),
            synchronizer.GetActualSecsFromTicks(note.EndPos),
            0.0, 0.07, 0.07, 0.15, 0.15, 0.25, 0.2, 0.2, 1.0, 5.5, 0.0);
}
