using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Model;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Acep;

public sealed class AcepParser
{
    private static readonly Regex LatinSpanRe = new(@"#(\d+)$", RegexOptions.Compiled);

    private readonly AcepInputOptions _options;
    private int _contentVersion;
    private TimeSynchronizer _synchronizer = new(new List<SongTempo> { new() });
    private int _firstBarTicks;

    public AcepParser(AcepInputOptions options) => _options = options;

    public Project ParseProject(AcepProject aceProject)
    {
        var project = new Project();
        _contentVersion = aceProject.Version;
        project.TimeSignatureList = ParseTimeSignatures(aceProject.TimeSignatures);
        if (project.TimeSignatureList.Count == 0)
            project.TimeSignatureList.Add(new TimeSignature(0, aceProject.BeatsPerBar, 4));
        _firstBarTicks = (int)project.TimeSignatureList[0].BarLength();
        project.SongTempoList = TickCounter.ShiftTempoList(ParseTempos(aceProject.Tempos), _firstBarTicks);
        if (project.SongTempoList.Count == 0)
            project.SongTempoList.Add(new SongTempo());
        _synchronizer = new TimeSynchronizer(project.SongTempoList);
        foreach (var aceTrack in aceProject.Tracks)
            aceTrack.Gain += aceProject.Master.Gain;
        foreach (var aceTrack in aceProject.Tracks)
        {
            var track = ParseTrack(aceTrack);
            if (track != null)
                project.TrackList.Add(track);
        }
        return project;
    }

    private static List<TimeSignature> ParseTimeSignatures(List<AcepTimeSignature> sigs) =>
        sigs.Select(s => new TimeSignature(s.BarPos, s.Numerator, s.Denominator)).ToList();

    private static List<SongTempo> ParseTempos(List<AcepTempo> tempos) =>
        tempos.Select(t => new SongTempo(t.Position, t.Bpm)).ToList();

    private Track? ParseTrack(AcepTrack aceTrack)
    {
        Track track;
        if (_options.ImportInstrumentalTrack && aceTrack is AcepAudioTrack audioTrack && audioTrack.Patterns.Count > 0)
        {
            var pattern = audioTrack.Patterns[0];
            track = new InstrumentalTrack
            {
                AudioFilePath = pattern.Path,
                Offset = (int)(_contentVersion < 7
                    ? pattern.Pos
                    : _synchronizer.GetActualTicksFromSecs(pattern.Pos)),
            };
        }
        else if (aceTrack is AcepVocalTrack vocalTrack)
        {
            string singerName = "";
            if (vocalTrack.Singers.Count > 0 && vocalTrack.Singers[0].Singer.SingerId is int id
                && AcepSingers.Id2Singer.TryGetValue(id, out var name))
                singerName = name;
            var singingTrack = new SingingTrack { AiSingerName = singerName };
            var aceNoteList = new List<AcepNote>();
            var aceParams = new AcepParams();
            foreach (var pattern in vocalTrack.Patterns.OrderBy(p => p.ClipPos))
            {
                var aceNotes = pattern.Notes.Where(note =>
                    note.Pos + pattern.Pos >= 0
                    && pattern.ClipPos <= note.Pos
                    && note.Pos < pattern.ClipPos + pattern.ClipDur).ToList();
                if (aceNotes.Count == 0)
                    continue;
                AcepNote? prevAceNote = null;
                foreach (var aceNote in aceNotes)
                {
                    aceNote.Dur = (int)Math.Min(aceNote.Dur, pattern.ClipPos + pattern.ClipDur - aceNote.Pos);
                    aceNote.Pos += (int)pattern.Pos;
                    if (prevAceNote != null && prevAceNote.Pos + prevAceNote.Dur > aceNote.Pos)
                        prevAceNote.Dur = aceNote.Pos - prevAceNote.Pos;
                    prevAceNote = aceNote;
                }
                aceNoteList.AddRange(aceNotes);

                MergeCurves(pattern, pattern.Parameters.PitchDelta, aceParams.PitchDelta);
                MergeCurves(pattern, pattern.Parameters.Breathiness, aceParams.Breathiness);
                MergeCurves(pattern, pattern.Parameters.Gender, aceParams.Gender);
                MergeCurves(pattern, pattern.Parameters.Energy, aceParams.Energy);
                MergeCurves(pattern, pattern.Parameters.Tension, aceParams.Tension);
                if (_options.BreathNormalization.Enabled)
                    MergeCurves(pattern, pattern.Parameters.RealBreathiness, aceParams.RealBreathiness);
                if (_options.TensionNormalization.Enabled)
                    MergeCurves(pattern, pattern.Parameters.RealTension, aceParams.RealTension);
                if (_options.EnergyNormalization.Enabled)
                    MergeCurves(pattern, pattern.Parameters.RealEnergy, aceParams.RealEnergy);
            }
            aceNoteList.Sort((a, b) => a.Pos.CompareTo(b.Pos));
            SortByOffset(aceParams.PitchDelta);
            SortByOffset(aceParams.Breathiness);
            SortByOffset(aceParams.Gender);
            SortByOffset(aceParams.Energy);
            SortByOffset(aceParams.Tension);
            if (_options.BreathNormalization.Enabled)
                SortByOffset(aceParams.RealBreathiness);
            if (_options.TensionNormalization.Enabled)
                SortByOffset(aceParams.RealTension);
            if (_options.EnergyNormalization.Enabled)
                SortByOffset(aceParams.RealEnergy);
            singingTrack.NoteList = aceNoteList.Select(ParseNote).ToList();
            singingTrack.EditedParams = ParseParams(aceParams, aceNoteList);
            track = singingTrack;
        }
        else
        {
            return null;
        }
        track.Title = aceTrack.Name;
        track.Mute = aceTrack.Mute;
        track.Solo = aceTrack.Solo;
        track.Volume = Math.Pow(10, aceTrack.Gain / 20);
        return track;
    }

    private void MergeCurves(AcepVocalPattern pattern, AcepParamCurveList src, AcepParamCurveList dst)
    {
        var aceCurves = src.Root.Where(curve =>
            curve.Offset + pattern.Pos >= -_firstBarTicks
            && curve.Offset + curve.Values.Count > pattern.ClipPos
            && curve.Offset < pattern.ClipPos + pattern.ClipDur).ToList();
        foreach (var aceCurve in aceCurves)
        {
            int maxLength = (int)(pattern.ClipPos + pattern.ClipDur - aceCurve.Offset);
            if (maxLength < aceCurve.Values.Count)
                aceCurve.Values = aceCurve.Values.Take(maxLength).ToList();
            aceCurve.Offset += (int)pattern.Pos;
        }
        dst.Root.AddRange(aceCurves);
    }

    private static void SortByOffset(AcepParamCurveList list) =>
        list.Root.Sort((a, b) => a.Offset.CompareTo(b.Offset));

    private Note ParseNote(AcepNote aceNote)
    {
        string? pronunciation = null;
        var note = new Note
        {
            KeyNumber = aceNote.Pitch,
            StartPos = aceNote.Pos,
            Length = aceNote.Dur,
            Lyric = aceNote.Lyric,
        };
        var language = aceNote.LanguageEnum;
        if (IsLatinLanguage(language))
        {
            var match = LatinSpanRe.Match(note.Lyric);
            if (match.Success)
            {
                int spanIndex = int.Parse(match.Groups[1].Value);
                note.Lyric = spanIndex == 1 ? LatinSpanRe.Replace(note.Lyric, "") : "+";
            }
        }
        if (!string.IsNullOrEmpty(aceNote.Syllable) && aceNote.Syllable != aceNote.FreezedDefaultSyllable)
            note.Pronunciation = aceNote.Syllable;
        else if (pronunciation == null || (!aceNote.Lyric.Contains('-') && aceNote.Pronunciation != pronunciation))
            note.Pronunciation = aceNote.Pronunciation;
        if (aceNote.BrLen > 0)
            note.HeadTag = "V";
        if (aceNote.HeadConsonants != null && aceNote.HeadConsonants.Count > 0)
        {
            note.EditedPhones = new Phones
            {
                HeadLengthInSecs = _contentVersion < 7
                    ? _synchronizer.GetDurationSecsFromTicks(note.StartPos - (int)aceNote.HeadConsonants[0], note.StartPos)
                    : aceNote.HeadConsonants[0],
            };
        }
        return note;
    }

    private static bool IsLatinLanguage(AcepLyricsLanguage language) =>
        language is AcepLyricsLanguage.English or AcepLyricsLanguage.Spanish
            or AcepLyricsLanguage.Portuguese or AcepLyricsLanguage.French or AcepLyricsLanguage.Italian;

    private Params ParseParams(AcepParams aceParams, List<AcepNote> aceNoteList)
    {
        if (_options.BreathNormalization.Enabled)
            ApplyNormalization(aceParams, aceParams.RealBreathiness, _options.BreathNormalization,
                v => aceParams.Breathiness = aceParams.Breathiness.Plus(v, 1.0, x => x >= 0 ? x * 1.5 : x * 0.8));
        if (_options.TensionNormalization.Enabled)
            ApplyNormalization(aceParams, aceParams.RealTension, _options.TensionNormalization,
                v => aceParams.Tension = aceParams.Tension.Plus(v, 1.0, x => x >= 0 ? x * 0.5 : x * 0.3));
        if (_options.EnergyNormalization.Enabled)
            ApplyNormalization(aceParams, aceParams.RealEnergy, _options.EnergyNormalization,
                v => aceParams.Energy = aceParams.Energy.Plus(v, 1.0, x => x));

        var parameters = new Params();
        if (_options.ImportPitch)
            parameters.Pitch = ParsePitchCurve(aceParams.PitchDelta, aceNoteList);
        if (_options.ImportBreath)
            parameters.Breath = ParseParamCurve(aceParams.Breathiness, LinearTransform(0.2, 1, 2.5));
        if (_options.ImportGender)
            parameters.Gender = ParseParamCurve(aceParams.Gender, LinearTransform(-1, 0, 1));
        if (_options.ImportTension && _options.ImportEnergy)
        {
            var transform = LinearTransform(0, 1, 2);
            parameters.Volume = ParseParamCurve(aceParams.Energy,
                x => (int)Math.Round(_options.EnergyCoefficient * transform(x)));
            var remainingEnergy = new AcepParamCurveList
            {
                Root = aceParams.Energy.Root.Select(part => new AcepParamCurve
                {
                    CurveType = part.CurveType,
                    Offset = part.Offset,
                    Values = part.Values.Select(v =>
                        v.HasValue ? (double?)((v.Value - 1) * (1 - _options.EnergyCoefficient) + 1) : null).ToList(),
                }).ToList(),
            };
            var energyPlusTension = remainingEnergy.Plus(aceParams.Tension, 1.0,
                x => x >= 1 ? (x - 1) * 0.5 : (x - 1) * 0.3);
            parameters.Strength = ParseParamCurve(energyPlusTension,
                x => (int)Math.Round(_options.EnergyCoefficient * transform(x)));
        }
        else if (_options.ImportTension)
        {
            parameters.Strength = ParseParamCurve(aceParams.Tension, LinearTransform(0.7, 1, 1.5));
        }
        else if (_options.ImportEnergy)
        {
            var transform = LinearTransform(0, 1, 2);
            parameters.Volume = ParseParamCurve(aceParams.Energy,
                x => (int)Math.Round(_options.EnergyCoefficient * transform(x)));
            parameters.Strength = ParseParamCurve(aceParams.Energy,
                x => (int)Math.Round((1 - _options.EnergyCoefficient) * transform(x)));
        }
        return parameters;
    }

    private static void ApplyNormalization(AcepParams aceParams, AcepParamCurveList real,
        AcepNormalizationArgument arg, Action<AcepParamCurveList> apply)
    {
        var normalized = real.Exclude(x =>
            x + 1e-3 < arg.LowerThreshold || x - 1e-3 > arg.UpperThreshold);
        if (arg.NormalizeMethod == AcepNormalizationMethod.ZScore)
            normalized = normalized.ZScoreNormalize(arg.Scale, arg.Bias);
        else if (arg.NormalizeMethod == AcepNormalizationMethod.MinMax)
            normalized = normalized.MinMaxNormalize(arg.Scale, arg.Bias);
        apply(normalized);
    }

    private static Func<double, int> LinearTransform(double lowerBound, double middleValue, double upperBound) =>
        x => (int)Math.Round(MusicMath.Clamp(
            x >= middleValue
                ? (x - middleValue) / (upperBound - middleValue) * 1000
                : (x - middleValue) / (middleValue - lowerBound) * 1000,
            -1000, 1000));

    private ParamCurve ParsePitchCurve(AcepParamCurveList aceCurves, List<AcepNote> aceNoteList)
    {
        var curve = new ParamCurve();
        curve.Points.Add(Point.StartPoint());
        if (aceCurves.Root.Count > 0)
        {
            var basePitch = new AcepBasePitchCurve(aceNoteList, _synchronizer);
            foreach (var aceCurve in aceCurves.Root)
            {
                int pos = aceCurve.Offset;
                curve.Points.Add(new Point(pos + _firstBarTicks, -100));
                foreach (var value in aceCurve.Values)
                {
                    if (aceCurve.CurveType == "anchor")
                    {
                        curve.Points.Add(new Point(pos + _firstBarTicks, (int)Math.Round((value ?? 0.0) * 100)));
                    }
                    else if (value.HasValue && !double.IsNaN(value.Value))
                    {
                        double absSemitone = basePitch.SemitoneValueAt(
                            _synchronizer.GetActualSecsFromTicks(pos)) + value.Value;
                        curve.Points.Add(new Point(pos + _firstBarTicks, (int)Math.Round(absSemitone * 100)));
                    }
                    pos++;
                }
                curve.Points.Add(new Point(pos - 1 + _firstBarTicks, -100));
            }
        }
        curve.Points.Add(Point.EndPoint());
        if (_options.CurveSampleInterval > 0)
            curve = curve.ReduceSampleRate(_options.CurveSampleInterval, -100);
        return curve;
    }

    private ParamCurve ParseParamCurve(AcepParamCurveList aceCurves, Func<double, int> mappingFunc)
    {
        var curve = new ParamCurve();
        curve.Points.Add(Point.StartPoint(0));
        foreach (var aceCurve in aceCurves.Root)
        {
            int pos = aceCurve.Offset;
            curve.Points.Add(new Point(pos + _firstBarTicks, 0));
            foreach (var value in aceCurve.Values)
            {
                if (value.HasValue)
                    curve.Points.Add(new Point(pos + _firstBarTicks, mappingFunc(value.Value)));
                pos++;
            }
            curve.Points.Add(new Point(pos - 1 + _firstBarTicks, 0));
        }
        curve.Points.Add(Point.EndPoint(0));
        if (_options.CurveSampleInterval > 0)
            curve = curve.ReduceSampleRate(_options.CurveSampleInterval);
        return curve;
    }
}
