using System;
using System.Collections.Generic;
using VOCALOIDPatcher.Formats.LibreSvip.Model;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Svip;

internal sealed class SvipParser
{
    private readonly bool _importPitch;
    private readonly bool _importVolume;
    private readonly bool _importBreath;
    private readonly bool _importGender;
    private readonly bool _importStrength;
    private readonly bool _importInstrumentalTrack;

    public SvipParser(
        bool importPitch,
        bool importVolume,
        bool importBreath,
        bool importGender,
        bool importStrength,
        bool importInstrumentalTrack)
    {
        _importPitch = importPitch;
        _importVolume = importVolume;
        _importBreath = importBreath;
        _importGender = importGender;
        _importStrength = importStrength;
        _importInstrumentalTrack = importInstrumentalTrack;
    }

    public Project ParseProject(string version, XSAppModel model)
    {
        var project = new Project { Version = version };
        foreach (var tempo in model.TempoList.Buf.Items)
            if (tempo is XSSongTempo t)
                project.SongTempoList.Add(ParseSongTempo(t));
        foreach (var beat in model.BeatList.Buf.Items)
            if (beat is XSSongBeat b)
                project.TimeSignatureList.Add(ParseTimeSignature(b));
        foreach (var track in model.TrackList.Items)
        {
            var parsed = ParseTrack(track);
            if (parsed != null)
                project.TrackList.Add(parsed);
        }
        return project;
    }

    private static SongTempo ParseSongTempo(XSSongTempo tempo) =>
        new(tempo.Pos, tempo.Tempo / 100.0);

    private static TimeSignature ParseTimeSignature(XSSongBeat beat) =>
        new(beat.BarIndex, beat.BeatSize.X, beat.BeatSize.Y);

    private Track? ParseTrack(XSObject? track)
    {
        Track result;
        if (track is XSSingingTrack singing)
        {
            var singingTrack = new SingingTrack
            {
                AiSingerName = SvipSingers.GetName(singing.AiSingerId),
                ReverbPreset = SvipReverbPresets.GetName(singing.ReverbPreset.Value) ?? "",
            };
            foreach (var note in singing.NoteList.Buf.Items)
                if (note is XSNote n)
                    singingTrack.NoteList.Add(ParseNote(n));
            singingTrack.EditedParams = ParseParams(singing);
            result = singingTrack;
        }
        else if (_importInstrumentalTrack && track is XSInstrumentTrack instrument)
        {
            result = new InstrumentalTrack
            {
                AudioFilePath = instrument.InstrumentFilePath,
                Offset = instrument.OffsetInPos,
            };
        }
        else
        {
            return null;
        }

        var baseTrack = (XSITrack)track;
        result.Title = baseTrack.Name;
        result.Mute = baseTrack.Mute;
        result.Solo = baseTrack.Solo;
        result.Volume = baseTrack.Volume;
        result.Pan = baseTrack.Pan;
        return result;
    }

    private Params ParseParams(XSSingingTrack track)
    {
        var pars = new Params();
        if (_importPitch && track.EditedPitchLine.Nodes.Count > 0)
            pars.Pitch = ParseParamCurve(track.EditedPitchLine, x => x > 1050 ? x - 1150 : -100);
        if (_importVolume && track.EditedVolumeLine.Nodes.Count > 0)
            pars.Breath = ParseParamCurve(track.EditedVolumeLine, null);
        if (_importBreath && track.EditedBreathLine.Nodes.Count > 0)
            pars.Breath = ParseParamCurve(track.EditedBreathLine, null);
        if (_importGender && track.EditedGenderLine.Nodes.Count > 0)
            pars.Gender = ParseParamCurve(track.EditedGenderLine, null);
        if (_importStrength && track.EditedPowerLine != null && track.EditedPowerLine.Nodes.Count > 0)
            pars.Strength = ParseParamCurve(track.EditedPowerLine, null);
        return pars;
    }

    private Note ParseNote(XSNote note)
    {
        var result = new Note
        {
            StartPos = note.StartPos,
            Length = note.WidthPos,
            KeyNumber = note.KeyIndex - 12,
            HeadTag = SvipNoteHeadTags.GetTag(note.HeadTag.Value),
            Lyric = SvipText.CleanseText(note.Lyric),
        };
        if (!string.IsNullOrEmpty(note.Pronouncing))
            result.Pronunciation = note.Pronouncing;
        if (note.NotePhoneInfo != null)
            result.EditedPhones = ParsePhones(note.NotePhoneInfo);
        if (note.Vibrato != null)
            result.Vibrato = ParseVibrato(note);
        return result;
    }

    private VibratoParam ParseVibrato(XSNote note)
    {
        var vibrato = new VibratoParam();
        if (note.VibratoPercentInfo != null)
        {
            vibrato.StartPercent = note.VibratoPercentInfo.StartPercent;
            vibrato.EndPercent = note.VibratoPercentInfo.EndPercent;
        }
        else if (note.VibratoPercent > 0)
        {
            vibrato.StartPercent = 1.0 - note.VibratoPercent / 100.0;
            vibrato.EndPercent = 1.0;
        }
        if (note.Vibrato != null)
        {
            vibrato.IsAntiPhase = note.Vibrato.IsAntiPhase;
            vibrato.Amplitude = ParseParamCurve(note.Vibrato.AmpLine, null);
            vibrato.Frequency = ParseParamCurve(note.Vibrato.FreqLine, null);
        }
        return vibrato;
    }

    private static Phones ParsePhones(XSNotePhoneInfo phone) => new()
    {
        HeadLengthInSecs = phone.HeadPhoneTimeInSec,
        MidRatioOverTail = phone.MidPartOverTailPartRatio,
    };

    private static ParamCurve ParseParamCurve(XSLineParam line, Func<double, double>? op)
    {
        op ??= x => x;
        var curve = new ParamCurve();
        foreach (var node in line.Nodes)
            curve.Points.Add(new Point(node.Pos, (int)op(node.Value)));
        return curve;
    }
}
