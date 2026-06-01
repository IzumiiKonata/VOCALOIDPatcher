using System.Collections.Generic;
using System.Linq;
using System.Text;
using VOCALOIDPatcher.Formats.Model;
using VOCALOIDPatcher.Formats.Process;
using VOCALOIDPatcher.Formats.Util;

namespace VOCALOIDPatcher.Formats.Io;

public static class StandardMid
{
    public static Model.Project Parse(IReadOnlyList<ImportFile> files, ImportParams parms)
    {
        var file = files[0];
        var midi = MidiFile.Parse(file.Content);
        int timeDivision = midi.TicksPerBeat;

        var warnings = new List<ImportWarning>();
        var (tempos, timeSignatures, tickPrefix) = Mid.ParseMasterTrack(timeDivision, midi.Tracks[0], 0, warnings);

        var tracks = midi.Tracks
            .Select((midiTrack, index) => ParseTrack(index, timeDivision, tickPrefix, midiTrack, parms.DefaultLyric))
            .ToList();

        if (tracks.Count > 0 && tracks[0].Notes.Count == 0)
            tracks.RemoveAt(0);

        return new Model.Project(Format.StandardMid, files, file.Name, tracks, timeSignatures, tempos, 0, warnings);
    }

    private static Track ParseTrack(int id, int timeDivision, long tickPrefix, IReadOnlyList<MidiEvent> events, string defaultLyric)
    {
        string trackName = $"Track {id + 1}";
        var notes = new List<Note>();
        long tickPosition = tickPrefix;
        string? pendingLyric = null;
        (Note Note, int Channel)? pendingNoteHead = null;
        var pendingNotesHeadsWithLyric = new Dictionary<int, Note>();

        foreach (var ev in events)
        {
            int delta = MidiUtil.ConvertInputTimeToStandardTime(ev.DeltaTime, timeDivision);
            if (delta > 0)
            {
                if (pendingNoteHead is { } head)
                {
                    if (pendingNotesHeadsWithLyric.TryGetValue(head.Channel, out var existing))
                        notes.Add(existing with { TickOff = tickPosition + delta });
                    pendingNotesHeadsWithLyric[head.Channel] = head.Note with { Lyric = pendingLyric ?? defaultLyric };
                }

                pendingLyric = null;
                pendingNoteHead = null;
            }

            tickPosition += delta;
            switch (ev.Type)
            {
                case "lyrics":
                    pendingLyric = Texts.DetectAndDecode(ev.TextBytes ?? new byte[0]);
                    break;
                case "text":
                    if (pendingLyric == null)
                        pendingLyric = Texts.DetectAndDecode(ev.TextBytes ?? new byte[0]);
                    break;
                case "trackName":
                    trackName = Texts.DetectAndDecode(ev.TextBytes ?? new byte[0]);
                    break;
                case "noteOn":
                    pendingNoteHead = (new Note(0, ev.NoteNumber, Constants.DefaultLyric, tickPosition, tickPosition), ev.Channel);
                    break;
                case "noteOff":
                    if (pendingNotesHeadsWithLyric.TryGetValue(ev.Channel, out var note))
                    {
                        notes.Add(note with { TickOff = tickPosition });
                        pendingNotesHeadsWithLyric.Remove(ev.Channel);
                    }

                    break;
            }
        }

        return new Track(id, trackName, notes).ValidateNotes();
    }

    public static ExportResult Generate(Model.Project project, IReadOnlyList<FeatureConfig> features)
    {
        var content = Mid.GenerateContent(project, (track, tickPrefix, _) => GenerateTrack(track, tickPrefix));
        return new ExportResult(content, FormatRegistry.Get(Format.StandardMid).GetFileName(project.Name), new List<ExportNotification>());
    }

    private static List<byte> GenerateTrack(Track track, int tickPrefix)
    {
        var bytes = new List<byte>();
        bytes.Add(0x00);
        bytes.AddRange(MidiUtil.MetaType.TrackName.EventHeaderBytes());
        bytes.AddString(track.Name, Mid.IsLittleEndian, lengthInVariableLength: true);

        long tickPosition = -(long)tickPrefix;
        foreach (var note in track.ValidateNotes().Notes)
        {
            long delta = note.TickOn - tickPosition;
            tickPosition = note.TickOn;

            string lyricText = string.IsNullOrWhiteSpace(note.Lyric) ? Constants.DefaultLyric : note.Lyric;
            var lyric = Encoding.UTF8.GetBytes(lyricText).ToList();
            bytes.AddIntVariableLengthBigEndian((int)delta);
            bytes.AddRange(MidiUtil.MetaType.Lyric.EventHeaderBytes());
            bytes.AddBlock(lyric, Mid.IsLittleEndian, lengthInVariableLength: true);

            bytes.AddIntVariableLengthBigEndian(0);
            bytes.Add(MidiUtil.EventType.NoteOn.GetStatusByte(0));
            bytes.Add((byte)System.Math.Clamp(note.Key, 0, 127));
            bytes.Add(127);

            delta = note.TickOff - tickPosition;
            tickPosition = note.TickOff;

            bytes.AddIntVariableLengthBigEndian((int)delta);
            bytes.Add(MidiUtil.EventType.NoteOff.GetStatusByte(0));
            bytes.Add((byte)System.Math.Clamp(note.Key, 0, 127));
            bytes.Add(0);
        }

        bytes.Add(0x00);
        bytes.AddRange(MidiUtil.MetaType.EndOfTrack.EventHeaderBytes());
        bytes.Add(0x00);
        return bytes;
    }
}
