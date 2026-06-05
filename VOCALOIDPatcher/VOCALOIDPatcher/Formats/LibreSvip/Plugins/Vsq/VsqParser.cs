using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Model;
using VOCALOIDPatcher.Formats.LibreSvip.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Vsq;

internal sealed class VsqParser
{
    private static readonly Regex BreathPattern = new("^br[1-5]$", RegexOptions.Compiled);

    private readonly bool _importPitch;
    private readonly bool _importVolume;
    private readonly bool _importBreath;
    private readonly bool _importGender;
    private readonly bool _importStrength;
    private readonly VsqBreathOption _breath;
    private readonly Encoding _lyricEncoding;

    private TimeSynchronizer _synchronizer = new(new List<SongTempo> { new() });
    private int _firstBarLength;
    private int _ticksPerBeat = Constants.TicksInBeat;
    private List<TimeSignature> _timeSignatures = new();

    public VsqParser(bool importPitch, bool importVolume, bool importBreath, bool importGender,
        bool importStrength, VsqBreathOption breath, Encoding lyricEncoding)
    {
        _importPitch = importPitch;
        _importVolume = importVolume;
        _importBreath = importBreath;
        _importGender = importGender;
        _importStrength = importStrength;
        _breath = breath;
        _lyricEncoding = lyricEncoding;
    }

    private double TickRate => (double)Constants.TicksInBeat / _ticksPerBeat;

    public Project Parse(MidiFile midi)
    {
        _ticksPerBeat = midi.TicksPerBeat;
        var tracksAsText = ExtractText(midi);
        int measurePrefix = GetMeasurePrefix(tracksAsText.Count > 0 ? tracksAsText[0] : "");
        var masterTrack = midi.Tracks.Count > 0 ? midi.Tracks[0] : new MidiTrackData();
        _timeSignatures = ParseTimeSignatures(masterTrack);
        _firstBarLength = (int)Math.Round(_timeSignatures[0].BarLength(_ticksPerBeat));
        int tickPrefix = _firstBarLength * measurePrefix;
        var songTempoList = ParseTempo(masterTrack, tickPrefix);
        _synchronizer = new TimeSynchronizer(songTempoList);

        var trackList = new List<Track>();
        foreach (var text in tracksAsText)
            trackList.Add(ParseTrack(text, tickPrefix));

        return new Project
        {
            SongTempoList = songTempoList,
            TimeSignatureList = _timeSignatures,
            TrackList = trackList,
        };
    }

    private List<string> ExtractText(MidiFile midi)
    {
        var result = new List<string>();
        foreach (var track in midi.Tracks)
        {
            var sb = new StringBuilder();
            foreach (var ev in track.TextEvents)
            {
                string decoded = DecodeText(ev.Data);
                if (decoded.StartsWith("DM:", StringComparison.Ordinal))
                    decoded = decoded.Substring(3);
                int idx = decoded.IndexOf(':');
                sb.Append(idx >= 0 ? decoded.Substring(idx + 1) : decoded);
            }
            result.Add(sb.ToString());
        }
        return result;
    }

    private string DecodeText(byte[] data)
    {
        try
        {
            return _lyricEncoding.GetString(data);
        }
        catch
        {
            return TextHelper.ShiftJis().GetString(data);
        }
    }

    private static int GetMeasurePrefix(string text)
    {
        var doc = VsqIniDocument.Parse(text);
        return doc.GetInt("Master", "PreMeasure", 1);
    }

    private List<TimeSignature> ParseTimeSignatures(MidiTrackData masterTrack)
    {
        var changes = new List<TimeSignature>();
        int prevTicks = 0;
        double measure = 0;
        foreach (var ts in masterTrack.TimeSigs.OrderBy(t => t.Tick))
        {
            double tickInFullNote = changes.Count > 0
                ? changes[^1].BarLength(_ticksPerBeat)
                : 4.0 * _ticksPerBeat;
            int tick = ts.Tick;
            measure += (tick - prevTicks) / tickInFullNote;
            changes.Add(new TimeSignature(Math.Max((int)Math.Floor(measure), 0), ts.Numerator, ts.Denominator));
            prevTicks = tick;
        }
        if (changes.Count == 0)
            changes.Add(new TimeSignature(0, 4, 4));
        return changes;
    }

    private List<SongTempo> ParseTempo(MidiTrackData masterTrack, int tickPrefix)
    {
        var tempos = new List<SongTempo>();
        foreach (var ev in masterTrack.Tempos.OrderBy(t => t.Tick))
        {
            double bpm = Math.Round(MidiFile.Tempo2Bpm(ev.Tempo), 3);
            int tick = (int)Math.Round(ev.Tick * TickRate);
            if (tick == 0)
            {
                tempos = new List<SongTempo> { new(0, bpm) };
            }
            else
            {
                double lastTempo = tempos.Count > 0 ? tempos[^1].Bpm : Constants.DefaultBpm;
                if (bpm != lastTempo)
                    tempos.Add(new SongTempo(tick - tickPrefix, bpm));
            }
        }
        if (tempos.Count == 0)
            tempos.Add(new SongTempo(0, Constants.DefaultBpm));
        return tempos;
    }

    private SingingTrack ParseTrack(string text, int tickPrefix)
    {
        var doc = VsqIniDocument.Parse(text);
        string singerName = "";
        string? singerIconKey = null;
        foreach (var section in doc.Sections)
        {
            if (section.Name.StartsWith("ID#", StringComparison.Ordinal) &&
                string.Equals(section.Get("type"), "Singer", StringComparison.Ordinal))
            {
                singerIconKey = section.Get("iconhandle");
                if (singerIconKey != null)
                    break;
            }
        }
        if (singerIconKey != null)
            singerName = doc.GetString(singerIconKey, "ids", "");

        var track = new SingingTrack
        {
            AiSingerName = singerName,
            Title = doc.GetString("Common", "Name", $"Track {doc.Sections.Count}"),
            NoteList = doc.HasSection("EventList") ? ParseNotes(doc, tickPrefix) : new List<Note>(),
        };

        if (_importPitch)
        {
            var pitch = ParsePitch(doc, track.NoteList, tickPrefix);
            if (pitch != null)
                track.EditedParams.Pitch = pitch;
        }
        ParseParams(doc, track.EditedParams, tickPrefix);
        return track;
    }

    private List<Note> ParseNotes(VsqIniDocument doc, int tickPrefix)
    {
        var notes = new List<Note>();
        var eventList = doc.GetSection("EventList");
        if (eventList == null)
            return notes;
        foreach (var item in eventList.Items)
        {
            if (!int.TryParse(item.Key.Trim(), out int rawTick))
                continue;
            int tick = rawTick - tickPrefix;
            string eventKey = item.Value;
            if (!eventKey.StartsWith("ID#", StringComparison.Ordinal))
                continue;
            var vsqNote = doc.GetSection(eventKey);
            if (vsqNote == null)
                continue;
            if (!string.Equals(vsqNote.Get("type"), "Anote", StringComparison.Ordinal))
                continue;
            int length = vsqNote.GetInt("length", 0);
            int key = vsqNote.GetInt("note#", 0);
            if (length == 0 || key == 0)
                continue;
            string lyricHandle = vsqNote.Get("lyrichandle") ?? "";
            string l0 = doc.GetString(lyricHandle, "L0", ",");
            var parts = l0.Split(',');
            string lyricValue = parts.Length > 0 ? parts[0] : "";
            string phonemeValue = parts.Length > 1 ? parts[1] : "";
            if (BreathPattern.IsMatch(phonemeValue.Trim('"')))
            {
                if (_breath == VsqBreathOption.Ignore)
                    continue;
                lyricValue = phonemeValue;
            }
            notes.Add(new Note
            {
                StartPos = tick,
                Length = length,
                KeyNumber = key,
                Lyric = lyricValue.Trim('"'),
                Pronunciation = null,
            });
        }
        return notes;
    }

    private ParamCurve? ParsePitch(VsqIniDocument doc, List<Note> noteList, int tickPrefix)
    {
        var adapter = new VsqControllerAdapter(tickPrefix);
        var pit = adapter.Extract(doc, "pitch_bend");
        if (pit == null)
            return null;
        var pbs = adapter.Extract(doc, "pitch_bend_sens")
            ?? new ControllerCurve("pitch_bend_sens", new List<ControllerEvent>(), 2, 1, 24);
        var handler = new VocaloidPitchHandler(_synchronizer, noteList, _timeSignatures, _firstBarLength);
        return handler.ToAbsolutePitch(new List<PitchBendData> { new(pit, pbs) });
    }

    private void ParseParams(VsqIniDocument doc, Params parameters, int tickPrefix)
    {
        var adapter = new VsqControllerAdapter(tickPrefix);
        if (_importVolume)
        {
            var curve = adapter.Extract(doc, "dynamics");
            if (curve != null)
                parameters.Volume.Points.AddRange(
                    VsqControllerHandler.ConvertVocaloidCurveToParamPoints(curve, _firstBarLength));
        }
        if (_importBreath)
        {
            var curve = adapter.Extract(doc, "breathiness");
            if (curve != null)
                parameters.Breath.Points.AddRange(
                    VsqControllerHandler.ConvertVocaloidCurveToParamPoints(curve, _firstBarLength));
        }
        if (_importGender)
        {
            var curve = adapter.Extract(doc, "gender");
            if (curve != null)
                parameters.Gender.Points.AddRange(
                    VsqControllerHandler.ConvertVocaloidCurveToParamPoints(curve, _firstBarLength, true));
        }
        if (_importStrength)
        {
            var curve = adapter.Extract(doc, "brightness");
            if (curve != null)
                parameters.Strength.Points.AddRange(
                    VsqControllerHandler.ConvertVocaloidCurveToParamPoints(curve, _firstBarLength));
        }
    }
}
