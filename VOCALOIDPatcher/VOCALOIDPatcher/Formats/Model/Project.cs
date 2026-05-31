using System;
using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.Exceptions;
using VOCALOIDPatcher.Formats.Process;

namespace VOCALOIDPatcher.Formats.Model;

public enum Feature
{
    ConvertPitch,
    SplitProject,
    ConvertPhonemes,
}

public static class FeatureExtensions
{
    public static bool IsAvailable(this Feature feature, Project project) => feature switch
    {
        Feature.ConvertPitch => project.Tracks.Any(t => t.Pitch != null),
        Feature.SplitProject => true,
        Feature.ConvertPhonemes => project.Tracks.Any(t => t.Notes.Any(n => n.Phoneme != null)),
        _ => false,
    };
}

public abstract class FeatureConfig
{
    public abstract Feature Type { get; }

    public sealed class ConvertPitch : FeatureConfig
    {
        public override Feature Type => Feature.ConvertPitch;
    }

    public sealed class SplitProject : FeatureConfig
    {
        public SplitProject(int maxTrackCount) => MaxTrackCount = maxTrackCount;

        public int MaxTrackCount { get; }
        public override Feature Type => Feature.SplitProject;

        public static SplitProject GetDefault(Format format) =>
            format == Format.Svp ? new SplitProject(3) : new SplitProject(1);
    }
}

public static class FeatureConfigListExtensions
{
    public static bool Contains(this IReadOnlyList<FeatureConfig> configs, Feature feature) =>
        configs.Any(c => c.Type == feature);
}

public sealed record Project(
    Format Format,
    IReadOnlyList<ImportFile> InputFiles,
    string Name,
    IReadOnlyList<Track> Tracks,
    IReadOnlyList<TimeSignature> TimeSignatures,
    IReadOnlyList<Tempo> Tempos,
    int MeasurePrefix,
    IReadOnlyList<ImportWarning> ImportWarnings,
    JapaneseLyricsType JapaneseLyricsType = JapaneseLyricsType.Unknown)
{
    public Project LyricsTypeAnalysed()
    {
        var analysed = JapaneseLyrics.AnalyseTypeForProject(this);
        var possible = FormatRegistry.Get(Format).PossibleLyricsTypes;
        return this with
        {
            JapaneseLyricsType = possible.Contains(analysed) ? analysed : JapaneseLyricsType.Unknown,
        };
    }

    public Project? WithoutEmptyTracks()
    {
        var tracks = Tracks
            .Where(t => t.Notes.Count > 0)
            .Select((t, index) => t with { Id = index })
            .ToList();
        return tracks.Count > 0 ? this with { Tracks = tracks } : null;
    }

    public bool HasXSampaData => Tracks.Any(t => t.Notes.Any(n => n.Phoneme != null));

    public Project RequireValid()
    {
        for (int index = 0; index < Tracks.Count; index++)
        {
            var firstNote = Tracks[index].Notes.FirstOrDefault();
            if (firstNote != null && firstNote.TickOn < 0L)
                throw new IllegalNotePositionException(firstNote, index);
        }

        return this;
    }
}
