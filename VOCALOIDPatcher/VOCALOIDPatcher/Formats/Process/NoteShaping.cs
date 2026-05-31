using System;
using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.Model;

namespace VOCALOIDPatcher.Formats.Process;

public static class NoteShaping
{
    public static IReadOnlyList<Note> ValidateNotes(this IReadOnlyList<Note> notes)
    {
        if (notes.Count == 0)
            return notes;

        var sorted = notes.OrderBy(n => n.TickOn).ToList();
        var result = new List<Note>();
        for (int i = 0; i < sorted.Count - 1; i++)
        {
            var current = sorted[i];
            var next = sorted[i + 1];
            var truncated = current with { TickOff = Math.Min(current.TickOff, next.TickOn) };
            if (truncated.Length > 0)
                result.Add(truncated);
        }

        result.Add(sorted[^1]);

        for (int i = 0; i < result.Count; i++)
            result[i] = result[i] with { Id = i };

        return result;
    }

    public static Track ValidateNotes(this Track track) => track with { Notes = track.Notes.ValidateNotes() };

    public static Project FillRests(this Project project, long excludedMaxLength) =>
        project with { Tracks = project.Tracks.Select(t => FillRests(t, excludedMaxLength)).ToList() };

    private static Track FillRests(Track track, long excludedMaxLength)
    {
        if (track.Notes.Count == 0)
            return track;

        var notes = track.Notes;
        var result = new List<Note>();
        for (int i = 0; i < notes.Count - 1; i++)
        {
            var note = notes[i];
            var nextNote = notes[i + 1];
            result.Add(nextNote.TickOn - note.TickOff < excludedMaxLength ? note with { TickOff = nextNote.TickOn } : note);
        }

        result.Add(notes[^1]);
        return track with { Notes = result };
    }
}
