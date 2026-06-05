using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Model;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Svip;

internal sealed class SvipGenerator
{
    private const int ValueListVersionSongTempo = 2;
    private const int ValueListVersionSongBeat = 2;
    private const int ValueListVersionSongITrack = 4;
    private const int ValueListVersionSongNote = 1054;

    private const double MinNoteDuration = 0.045;
    private const double MaxNoteDuration = 20.0;

    private static readonly Regex VersionPattern = new(@"^SVIP\d\.\d\.\d$", RegexOptions.Compiled);

    private readonly string _singer;
    private readonly int _tempoOption;
    private readonly bool _isPower;

    private bool _isAbsoluteTimeMode;
    private TimeSynchronizer _synchronizer = null!;
    private int _firstBarTick;

    public SvipGenerator(string singer, int tempoOption, bool isPower)
    {
        _singer = singer;
        _tempoOption = tempoOption;
        _isPower = isPower;
    }

    public (string version, XSAppModel model) GenerateProject(Project project)
    {
        string version = VersionPattern.IsMatch(project.Version) ? project.Version : "SVIP6.0.0";
        var model = new XSAppModel();

        var firstSignature = project.TimeSignatureList.Count > 0
            ? project.TimeSignatureList[0]
            : new TimeSignature();
        _firstBarTick = (int)Math.Round(firstSignature.BarLength());

        _isAbsoluteTimeMode = project.SongTempoList.Any(t => t.Bpm < 20 || t.Bpm > 300);
        _synchronizer = new TimeSynchronizer(
            project.SongTempoList,
            _firstBarTick,
            _isAbsoluteTimeMode,
            _tempoOption);

        var beatItems = model.BeatList.Buf.Items;
        if (_isAbsoluteTimeMode
            || project.TimeSignatureList.Any(b => b.Numerator > 255 || b.Denominator > 32))
        {
            beatItems.Add(new XSSongBeat { BarIndex = 0, BeatSize = new XSBeatSize(4, 4) });
        }
        else
        {
            foreach (var beat in project.TimeSignatureList)
                beatItems.Add(GenerateTimeSignature(beat));
        }
        model.BeatList.Buf.Size = beatItems.Count;
        model.BeatList.Buf.Version = ValueListVersionSongBeat;
        model.BeatList.Buf1 = model.BeatList.Buf;

        var tempoItems = model.TempoList.Buf.Items;
        if (_isAbsoluteTimeMode)
        {
            tempoItems.Add(new XSSongTempo { Pos = 0, Tempo = _tempoOption * 100 });
        }
        else
        {
            foreach (var tempo in project.SongTempoList)
                tempoItems.Add(GenerateSongTempo(tempo));
        }
        model.TempoList.Buf.Size = tempoItems.Count;
        model.TempoList.Buf.Version = ValueListVersionSongTempo;
        model.TempoList.Buf1 = model.TempoList.Buf;

        var trackItems = new List<XSObject?>();
        foreach (var track in project.TrackList)
        {
            var generated = GenerateTrack(track);
            if (generated != null)
                trackItems.Add(generated);
        }
        model.TrackList.Size = trackItems.Count;
        model.TrackList.Version = ValueListVersionSongITrack;
        model.TrackList.Items.Clear();
        model.TrackList.Items.AddRange(trackItems);

        return (version, model);
    }

    private static XSSongTempo GenerateSongTempo(SongTempo tempo) =>
        new() { Pos = tempo.Position, Tempo = (int)Math.Round(tempo.Bpm * 100) };

    private static XSSongBeat GenerateTimeSignature(TimeSignature signature) =>
        new()
        {
            BarIndex = signature.BarIndex,
            BeatSize = new XSBeatSize(signature.Numerator, signature.Denominator),
        };

    private XSITrack? GenerateTrack(Track track)
    {
        XSITrack? result;
        if (track is SingingTrack singing)
        {
            string singerId = SvipSingers.GetId(singing.AiSingerName);
            if (singerId == "")
                singerId = SvipSingers.GetId(_singer);
            var singingTrack = new XSSingingTrack
            {
                AiSingerId = singerId,
                ReverbPreset = new XSReverbPreset(
                    SvipReverbPresets.GetEnum(singing.ReverbPreset, XSReverbPresetEnum.None)),
            };

            var noteItems = singingTrack.NoteList.Buf.Items;
            foreach (var note in singing.NoteList)
            {
                var newNote = GenerateNote(note);
                if (newNote != null)
                    noteItems.Add(newNote);
            }
            singingTrack.NoteList.Buf.Size = noteItems.Count;
            singingTrack.NoteList.Buf.Version = ValueListVersionSongNote;
            singingTrack.NoteList.Buf1 = singingTrack.NoteList.Buf;

            var pars = GenerateParams(singing.EditedParams);
            singingTrack.EditedPitchLine = pars["Pitch"];
            singingTrack.EditedVolumeLine = pars["Volume"];
            singingTrack.EditedBreathLine = pars["Breath"];
            singingTrack.EditedGenderLine = pars["Gender"];
            if (_isPower)
                singingTrack.EditedPowerLine = pars["Strength"];
            result = singingTrack;
        }
        else if (track is InstrumentalTrack instrumental)
        {
            result = new XSInstrumentTrack
            {
                InstrumentFilePath = instrumental.AudioFilePath,
                OffsetInPos = instrumental.Offset,
            };
        }
        else
        {
            return null;
        }

        result.Name = track.Title;
        result.Mute = track.Mute;
        result.Solo = track.Solo;
        result.Volume = track.Volume;
        result.Pan = (float)track.Pan;
        return result;
    }

    private XSNote? GenerateNote(Note note)
    {
        if (string.IsNullOrEmpty(note.Lyric) && string.IsNullOrEmpty(note.Pronunciation))
            return null;
        var xsNote = new XSNote
        {
            StartPos = (int)Math.Round(_synchronizer.GetActualTicksFromTicks(note.StartPos)),
            KeyIndex = note.KeyNumber + 12,
            HeadTag = new XSNoteHeadTag(SvipNoteHeadTags.GetEnum(note.HeadTag)),
            Lyric = SvipText.StartsWithChinese(note.Lyric) ? note.Lyric : Constants.DefaultChineseLyric,
            Pronouncing = note.Pronunciation ?? "",
        };
        xsNote.WidthPos =
            (int)Math.Round(_synchronizer.GetActualTicksFromTicks(note.EndPos)) - xsNote.StartPos;
        if (note.EditedPhones != null)
            xsNote.NotePhoneInfo = GeneratePhones(note.EditedPhones);
        if (note.Vibrato != null)
        {
            var (percent, vibrato) = GenerateVibrato(note.Vibrato);
            xsNote.VibratoPercentInfo = percent;
            xsNote.Vibrato = vibrato;
        }
        return xsNote;
    }

    private static XSNotePhoneInfo GeneratePhones(Phones editedPhones) => new()
    {
        HeadPhoneTimeInSec = (float)editedPhones.HeadLengthInSecs,
        MidPartOverTailPartRatio = (float)editedPhones.MidRatioOverTail,
    };

    private (XSVibratoPercentInfo, XSVibratoStyle) GenerateVibrato(VibratoParam vibrato)
    {
        var percent = new XSVibratoPercentInfo
        {
            StartPercent = (float)vibrato.StartPercent,
            EndPercent = (float)vibrato.EndPercent,
        };
        var style = new XSVibratoStyle
        {
            IsAntiPhase = vibrato.IsAntiPhase,
            AmpLine = GenerateParamCurve(vibrato.Amplitude, null, -1, 100001, 0, false),
            FreqLine = GenerateParamCurve(vibrato.Frequency, null, -1, 100001, 0, false),
        };
        return (percent, style);
    }

    private Dictionary<string, XSLineParam> GenerateParams(Params editedParams) => new()
    {
        ["Pitch"] = GenerateParamCurve(editedParams.Pitch, x => x > -100 ? x + 1150 : -100),
        ["Volume"] = GenerateParamCurve(editedParams.Volume, null),
        ["Breath"] = GenerateParamCurve(editedParams.Breath, null),
        ["Gender"] = GenerateParamCurve(editedParams.Gender, null),
        ["Strength"] = GenerateParamCurve(editedParams.Strength, null),
    };

    private XSLineParam GenerateParamCurve(
        ParamCurve paramCurve,
        Func<double, double>? op,
        int left = -192000,
        int right = 1073741823,
        int termination = 0,
        bool isTicks = true)
    {
        op ??= x => x;
        var line = new XSLineParam();
        foreach (var p in paramCurve.Points)
        {
            if (left <= p.X && p.X <= right)
            {
                int pos;
                if (_isAbsoluteTimeMode && isTicks && p.X != left && p.X != right)
                {
                    pos = (int)Math.Round(
                        _synchronizer.GetActualTicksFromTicks(p.X - _firstBarTick)) + _firstBarTick;
                }
                else
                {
                    pos = p.X;
                }
                line.Nodes.Add(new XSLineParamNode(pos, (int)op(p.Y)));
            }
        }
        if (line.Nodes.Count == 0 || line.Nodes[0].Pos > left)
            line.Nodes.Insert(0, new XSLineParamNode(left, termination));
        if (line.Nodes.Count == 0 || line.Nodes[^1].Pos < right)
            line.Nodes.Add(new XSLineParamNode(right, termination));
        line.ConvertToParam();
        return line;
    }
}
