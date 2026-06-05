using System;
using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Model;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Acep;

public sealed class AcepGenerator
{
    private readonly AcepOutputOptions _options;
    private int _firstBarTicks;
    private TimeSynchronizer _synchronizer = new(new List<SongTempo> { new() });
    private List<AcepNote> _aceNoteList = new();
    private int _patternStart;
    private readonly Random _random = new();

    public AcepGenerator(AcepOutputOptions options) => _options = options;

    public AcepProject GenerateProject(Project project)
    {
        var aceProject = new AcepProject();
        if (project.TimeSignatureList.Count == 0)
            project.TimeSignatureList.Add(new TimeSignature());
        _firstBarTicks = (int)project.TimeSignatureList[0].BarLength();
        int denominator = project.TimeSignatureList[0].Denominator;
        int numerator = project.TimeSignatureList[0].Numerator;
        aceProject.BeatsPerBar = numerator * 4 / denominator;
        if (denominator <= 32 && numerator <= 32)
            aceProject.TimeSignatures = new List<AcepTimeSignature>
            {
                new() { BarPos = 0, Numerator = numerator, Denominator = denominator },
            };
        _synchronizer = new TimeSynchronizer(project.SongTempoList, _firstBarTicks);
        aceProject.Tempos = GenerateTempos(project.SongTempoList);

        foreach (var track in project.TrackList)
        {
            var aceTrack = GenerateTrack(track);
            if (aceTrack != null)
                aceProject.Tracks.Add(aceTrack);
        }
        aceProject.Duration = aceProject.Tracks.OfType<AcepVocalTrack>()
            .Select(t => t.Length()).DefaultIfEmpty(0).Max() + 115200;
        int colorCount = AcepColorPool.Count();
        int colorIndex = _random.Next(0, colorCount);
        foreach (var aceTrack in aceProject.Tracks)
        {
            aceTrack.Color = AcepColorPool.Get(colorIndex);
            colorIndex = (colorIndex + 1) % colorCount;
        }
        aceProject.ColorIndex = colorIndex;
        return aceProject;
    }

    private List<AcepTempo> GenerateTempos(List<SongTempo> tempos)
    {
        var skipped = TickCounter.SkipTempoList(tempos, _firstBarTicks);
        return skipped.Select(t => new AcepTempo { Bpm = t.Bpm, Position = t.Position }).ToList();
    }

    private AcepTrack? GenerateTrack(Track track)
    {
        AcepTrack aceTrack;
        if (track is InstrumentalTrack instrumental)
        {
            var audioTrack = new AcepAudioTrack();
            var audioPattern = new AcepAudioPattern
            {
                Path = instrumental.AudioFilePath,
                Pos = _synchronizer.GetActualSecsFromTicks(instrumental.Offset),
            };
            audioTrack.Patterns.Add(audioPattern);
            aceTrack = audioTrack;
        }
        else if (track is SingingTrack singing)
        {
            var vocalTrack = new AcepVocalTrack { Language = AcepLyricsLanguageHelper.ToCode(_options.LyricLanguage) };
            var singerConfig = new AcepSingerConfig();
            if (AcepSingers.Singer2Id.ContainsKey(singing.AiSingerName)
                && AcepSingers.Singer2Seed.ContainsKey(singing.AiSingerName))
            {
                singerConfig.Singer.SingerId = AcepSingers.Singer2Id.TryGetValue(singing.AiSingerName, out var sid)
                    ? sid : AcepSingers.DefaultSingerId;
                singerConfig.Singer.Composition.Add(new AcepSeedComposition
                {
                    Code = AcepSingers.Singer2Seed.TryGetValue(singing.AiSingerName, out var seed)
                        ? seed : AcepSingers.DefaultSeed,
                });
            }
            else
            {
                singerConfig.Singer.SingerId = AcepSingers.DefaultSingerId;
                singerConfig.Singer.Composition.Add(new AcepSeedComposition { Code = AcepSingers.DefaultSeed });
            }
            vocalTrack.Singers.Add(singerConfig);
            if (singing.NoteList.Count > 0)
            {
                var buffer = new List<Note> { singing.NoteList[0] };
                void GenerateVocalPattern()
                {
                    _patternStart = (int)Math.Round(Math.Max(0, buffer[0].StartPos - 240.0));
                    _aceNoteList = buffer.Where(n => !string.IsNullOrEmpty(n.Lyric)).Select(GenerateNote).ToList();
                    var vocalPattern = new AcepVocalPattern
                    {
                        Pos = _patternStart,
                        Dur = Math.Round((double)buffer[^1].EndPos) - _patternStart,
                        Notes = _aceNoteList,
                    };
                    vocalPattern.ClipDur = vocalPattern.Dur;
                    buffer.Clear();
                    if (_options.Breath > 0)
                        AdjustBreathTags(vocalPattern.Notes);
                    vocalPattern.Parameters = GenerateParams(singing.EditedParams);
                    vocalTrack.Patterns.Add(vocalPattern);
                }

                for (int i = 0; i + 1 < singing.NoteList.Count; i++)
                {
                    int prevEnd = singing.NoteList[i].EndPos;
                    int curStart = singing.NoteList[i + 1].StartPos;
                    if (curStart - prevEnd > _options.SplitThreshold * Constants.TicksInBeat
                        && _options.SplitThreshold * Constants.TicksInBeat > 0)
                        GenerateVocalPattern();
                    buffer.Add(singing.NoteList[i + 1]);
                }
                if (buffer.Count > 0)
                    GenerateVocalPattern();
            }
            aceTrack = vocalTrack;
        }
        else
        {
            return null;
        }
        aceTrack.Name = track.Title;
        aceTrack.Mute = track.Mute;
        aceTrack.Solo = track.Solo;
        aceTrack.Pan = track.Pan;
        aceTrack.Gain = Math.Min(6.0, 20 * Math.Log10(track.Volume));
        return aceTrack;
    }

    private void AdjustBreathTags(List<AcepNote> notes)
    {
        for (int i = 1; i < notes.Count; i++)
        {
            double breath = notes[i].BrLen;
            if (breath == 0)
                continue;
            double distance = _synchronizer.GetDurationSecsFromTicks(notes[i - 1].Pos, notes[i].Pos);
            double actualBreath = Math.Min(distance / 2, breath);
            notes[i - 1].Dur = Math.Min(
                notes[i - 1].Dur,
                (int)_synchronizer.GetActualTicksFromSecsOffset(notes[i - 1].Pos, distance - actualBreath)
                    - notes[i - 1].Pos);
            notes[i].BrLen -= actualBreath;
        }
    }

    private AcepNote GenerateNote(Note note)
    {
        var aceNote = new AcepNote
        {
            Pos = (int)Math.Round((double)note.StartPos) - _patternStart,
            Dur = note.Length,
            Pitch = note.KeyNumber,
            Lyric = note.Lyric,
            Language = AcepLyricsLanguageHelper.ToCode(_options.LyricLanguage),
        };

        if (!note.Lyric.Contains('-') && !note.Lyric.Contains('+'))
        {
            if (note.Pronunciation != null)
                aceNote.Syllable = note.Pronunciation;
            if (note.EditedPhones != null && note.EditedPhones.HeadLengthInSecs >= 0)
                aceNote.HeadConsonants = new List<double> { note.EditedPhones.HeadLengthInSecs };
            else if (_options.DefaultConsonantLength != 0)
                aceNote.HeadConsonants = new List<double> { _options.DefaultConsonantLength };
        }
        else if (IsLatinLanguage(_options.LyricLanguage) && aceNote.Lyric == "+" && _aceNoteList.Count > 0)
        {
            aceNote.Pronunciation = "-";
            int lastIndex = Search.FindLastIndex(_aceNoteList, n => n.Lyric != "-");
            if (lastIndex >= 0)
            {
                var lastAceNote = _aceNoteList[lastIndex];
                int hashPos = lastAceNote.Lyric.IndexOf('#');
                string lyric = hashPos >= 0 ? lastAceNote.Lyric.Substring(0, hashPos) : lastAceNote.Lyric;
                string index = hashPos >= 0 ? lastAceNote.Lyric.Substring(hashPos + 1) : "";
                if (hashPos >= 0 && index.Length > 0 && index.All(char.IsDigit))
                {
                    aceNote.Lyric = $"{lyric}#{int.Parse(index) + 1}";
                }
                else
                {
                    lastAceNote.Lyric = $"{lyric}#1";
                    aceNote.Lyric = $"{lyric}#2";
                }
            }
        }
        else
        {
            aceNote.Lyric = aceNote.Pronunciation = "-";
        }

        if (note.HeadTag == "V" && _options.Breath > 0)
        {
            double breathStartInSecs = _synchronizer.GetActualSecsFromTicks(note.StartPos) - _options.Breath / 1000.0;
            double breathStartInTicks = _synchronizer.GetActualTicksFromSecs(breathStartInSecs);
            aceNote.BrLen = Math.Round(note.StartPos - breathStartInTicks);
        }
        return aceNote;
    }

    private static bool IsLatinLanguage(AcepLyricsLanguage language) =>
        language is AcepLyricsLanguage.English or AcepLyricsLanguage.Spanish
            or AcepLyricsLanguage.Portuguese or AcepLyricsLanguage.French or AcepLyricsLanguage.Italian;

    private static Func<double, double> LinearTransform(double lowerBound, double middleValue, double upperBound) =>
        x => x >= 0
            ? x * (upperBound - middleValue) / 1000 + middleValue
            : x * (middleValue - lowerBound) / 1000 + middleValue;

    private AcepParams GenerateParams(Params parameters)
    {
        var result = new AcepParams
        {
            Breathiness = GenerateParamCurves(parameters.Breath, LinearTransform(0.2, 1, 2.5)),
            Gender = GenerateParamCurves(parameters.Gender, LinearTransform(-1, 0, 1)),
        };
        result.PitchDelta = GeneratePitchCurves(parameters.Pitch);
        if (_options.MapStrengthInfo == StrengthMappingOption.Both)
        {
            var energyTransform = LinearTransform(0, 1, 2);
            var tensionTransform = LinearTransform(0.7, 1, 1.5);
            result.Energy = GenerateParamCurves(parameters.Strength, x => energyTransform(x / 2));
            result.Tension = GenerateParamCurves(parameters.Strength, x => tensionTransform(x / 2));
        }
        else if (_options.MapStrengthInfo == StrengthMappingOption.Energy)
        {
            result.Energy = GenerateParamCurves(parameters.Strength, LinearTransform(0, 1, 2));
        }
        else if (_options.MapStrengthInfo == StrengthMappingOption.Tension)
        {
            result.Tension = GenerateParamCurves(parameters.Strength, LinearTransform(0.7, 1, 1.5));
        }
        return result;
    }

    private AcepParamCurveList GeneratePitchCurves(ParamCurve curve)
    {
        var aceCurves = new AcepParamCurveList();
        if (_aceNoteList.Count == 0)
            return aceCurves;
        var basePitch = new AcepBasePitchCurve(_aceNoteList, _synchronizer, _patternStart);
        int leftBound = Math.Max(0, _patternStart + _aceNoteList[0].Pos - 240);
        int rightBound = Math.Max(0,
            _patternStart + _aceNoteList[^1].Pos + _aceNoteList[^1].Dur + 120);

        var segments = SelectSegments(curve, leftBound, rightBound);
        foreach (var segment in segments)
        {
            var (startPoint, endPoint) = BoundPoints(segment, leftBound, rightBound);
            if (startPoint.X == endPoint.X)
                continue;
            var aceCurve = new AcepParamCurve { Offset = (int)Math.Round((double)(startPoint.X - _firstBarTicks - _patternStart)) };
            int curveEnd = (int)Math.Round((double)(endPoint.X - _firstBarTicks - _patternStart));
            double tickStep = (double)(endPoint.X - startPoint.X) / (curveEnd - aceCurve.Offset);
            double tick = startPoint.X;
            while (tick < _firstBarTicks)
            {
                aceCurve.Offset += 1;
                tick += tickStep;
            }
            tick = Math.Max(_firstBarTicks, tick);
            tickStep = (endPoint.X - tick) / (curveEnd - aceCurve.Offset);
            while (tick < leftBound)
            {
                aceCurve.Offset += 1;
                tick += tickStep;
            }
            int pos = aceCurve.Offset;
            while (pos <= Math.Min(rightBound, curveEnd))
            {
                double second = _synchronizer.GetActualSecsFromTicks((int)Math.Round(tick - _firstBarTicks));
                aceCurve.Values.Add(GetValueFromSegment(segment, tick) / 100 - basePitch.SemitoneValueAt(second));
                pos++;
                tick += tickStep;
            }
            aceCurves.Root.Add(aceCurve);
        }
        return aceCurves;
    }

    private AcepParamCurveList GenerateParamCurves(ParamCurve curve, Func<double, double> mappingFunc)
    {
        var aceCurves = new AcepParamCurveList();
        if (_aceNoteList.Count == 0)
            return aceCurves;
        int leftBound = Math.Max(0, _patternStart + _aceNoteList[0].Pos - 240);
        int rightBound = Math.Max(0,
            _patternStart + _aceNoteList[^1].Pos + _aceNoteList[^1].Dur + 120);
        var segments = SelectSegments(curve, leftBound, rightBound);
        foreach (var segment in segments)
        {
            var (startPoint, endPoint) = BoundPoints(segment, leftBound, rightBound);
            var aceCurve = new AcepParamCurve { Offset = (int)Math.Round((double)(startPoint.X - _firstBarTicks - _patternStart)) };
            int curveEnd = (int)Math.Round((double)(endPoint.X - _firstBarTicks - _patternStart));
            double tickStep = (double)(endPoint.X - startPoint.X) / (curveEnd - aceCurve.Offset);
            double tick = startPoint.X;
            while (tick < _firstBarTicks)
            {
                aceCurve.Offset += 1;
                tick += tickStep;
            }
            tick = Math.Max(_firstBarTicks, tick);
            tickStep = (endPoint.X - tick) / (curveEnd - aceCurve.Offset);
            while (tick < leftBound)
            {
                aceCurve.Offset += 1;
                tick += tickStep;
            }
            int pos = aceCurve.Offset;
            while (pos <= Math.Min(rightBound, curveEnd))
            {
                aceCurve.Values.Add(mappingFunc(GetValueFromSegment(segment, tick)));
                pos++;
                tick += tickStep;
            }
            aceCurves.Root.Add(aceCurve);
        }
        return aceCurves;
    }

    private List<List<Point>> SelectSegments(ParamCurve curve, int leftBound, int rightBound)
    {
        var segments = new List<List<Point>>();
        foreach (var seg in curve.SplitIntoSegments(-100))
        {
            if (seg[^1].X < _firstBarTicks)
                continue;
            int startTicks = seg[0].X > _firstBarTicks ? seg[0].X - _firstBarTicks : 0;
            int endTicks = seg[^1].X - _firstBarTicks;
            if (startTicks <= rightBound && endTicks >= leftBound)
                segments.Add(seg);
        }
        return segments;
    }

    private (Point Start, Point End) BoundPoints(List<Point> segment, int leftBound, int rightBound)
    {
        int startIdx = Search.FindLastIndex(segment, p => 0 <= p.X - _firstBarTicks && p.X - _firstBarTicks <= leftBound);
        Point startPoint = startIdx >= 0 ? segment[startIdx] : segment[0];
        int endIdx = Search.FindIndex(segment, p => rightBound <= p.X - _firstBarTicks);
        Point endPoint = endIdx >= 0 ? segment[endIdx] : segment[^1];
        return (startPoint, endPoint);
    }

    private static double GetValueFromSegment(List<Point> segment, double ticks)
    {
        int leftIdx = Search.FindLastIndex(segment, p => p.X <= ticks);
        if (leftIdx < 0)
            return segment[0].Y;
        int rightIdx = Search.FindIndex(segment, p => p.X > ticks);
        if (rightIdx < 0)
            return segment[^1].Y;
        var leftPoint = segment[leftIdx];
        var rightPoint = segment[rightIdx];
        double ratio = (ticks - leftPoint.X) / (rightPoint.X - leftPoint.X);
        return (1 - ratio) * leftPoint.Y + ratio * rightPoint.Y;
    }
}
