using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.Model;
using VOCALOIDPatcher.Formats.Util;

namespace VOCALOIDPatcher.Formats.Process;

public static class LengthLimit
{
    public static Project LengthLimited(this Project project, long maxLength)
    {
        var tracks = project.Tracks.Select(t => t.LengthLimited(maxLength)).ToList();
        var tickCounter = new TickCounter();
        var timeSignatures = new List<TimeSignature>();
        foreach (var ts in project.TimeSignatures)
        {
            tickCounter.GoToMeasure(ts);
            if (tickCounter.Tick <= maxLength)
                timeSignatures.Add(ts);
        }

        var tempos = project.Tempos.Where(t => t.TickPosition <= maxLength).ToList();
        return project with { Tracks = tracks, TimeSignatures = timeSignatures, Tempos = tempos };
    }

    private static Track LengthLimited(this Track track, long maxLength)
    {
        var notes = track.Notes
            .Where(n => n.TickOff <= maxLength)
            .Select((note, index) => note with { Id = index })
            .ToList();
        var pitch = track.Pitch != null
            ? track.Pitch with { Data = track.Pitch.Data.Where(p => p.Tick <= maxLength).ToList() }
            : null;
        return track with { Notes = notes, Pitch = pitch };
    }
}
