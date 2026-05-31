using System.Collections.Generic;
using System.Linq;

namespace VOCALOIDPatcher.Formats.Model;

public static class Constants
{
    public const int TicksInBeat = 480;
    public const int TicksInFullNote = TicksInBeat * 4;
    public const int KeyInOctave = 12;
    public const string DefaultLyric = "あ";
    public const double DefaultBpm = 120.0;
    public const int DefaultMeterHigh = 4;
    public const int DefaultMeterLow = 4;
    public const int DefaultKey = 60;
    public const double KeyCenterC = 60.0;
    public const double LogFrqCenterC = 5.566914341;
    public const double LogFrqDiffOneKey = 0.05776226505;
}

public sealed record Note(
    int Id,
    int Key,
    string Lyric,
    long TickOn,
    long TickOff,
    string? Phoneme = null)
{
    public long Length => TickOff - TickOn;
}

public sealed record Pitch(
    IReadOnlyList<(long Tick, double? Value)> Data,
    bool IsAbsolute);

public sealed record Tempo(long TickPosition, double Bpm)
{
    public static Tempo Default => new(0, Constants.DefaultBpm);
}

public sealed record TimeSignature(int MeasurePosition, int Numerator, int Denominator)
{
    public string DisplayValue => $"{Numerator}/{Denominator}";
    public int TicksInMeasure => Constants.TicksInFullNote * Numerator / Denominator;

    public static TimeSignature Default => new(0, Constants.DefaultMeterHigh, Constants.DefaultMeterLow);
}

public sealed record Track(
    int Id,
    string Name,
    IReadOnlyList<Note> Notes,
    Pitch? Pitch = null);

public sealed class ImportFile
{
    public ImportFile(string name, byte[] content)
    {
        Name = name;
        Content = content;
    }

    public string Name { get; }
    public byte[] Content { get; }

    public string ExtensionName
    {
        get
        {
            int dot = Name.LastIndexOf('.');
            return dot < 0 ? "" : Name[(dot + 1)..].ToLowerInvariant();
        }
    }

    public string NameWithoutExtension
    {
        get
        {
            int dot = Name.LastIndexOf('.');
            return dot < 0 ? Name : Name[..dot];
        }
    }
}

public enum ExportNotification
{
    PhonemeResetRequiredVsq,
    PhonemeResetRequiredV4,
    PhonemeResetRequiredV5,
    TimeSignatureIgnored,
    PitchDataExported,
    DataOverLengthLimitIgnored,
}

public sealed class ExportResult
{
    public ExportResult(byte[] content, string fileName, IReadOnlyList<ExportNotification> notifications)
    {
        Content = content;
        FileName = fileName;
        Notifications = notifications;
    }

    public byte[] Content { get; }
    public string FileName { get; }
    public IReadOnlyList<ExportNotification> Notifications { get; }
}

public abstract class ImportWarning
{
    public sealed class TempoNotFound : ImportWarning
    {
    }

    public sealed class TempoIgnoredInFile : ImportWarning
    {
        public TempoIgnoredInFile(ImportFile file, Tempo tempo)
        {
            File = file;
            Tempo = tempo;
        }

        public ImportFile File { get; }
        public Tempo Tempo { get; }
    }

    public sealed class TempoIgnoredInTrack : ImportWarning
    {
        public TempoIgnoredInTrack(Track track, Tempo tempo)
        {
            Track = track;
            Tempo = tempo;
        }

        public Track Track { get; }
        public Tempo Tempo { get; }
    }

    public sealed class TempoIgnoredInPreMeasure : ImportWarning
    {
        public TempoIgnoredInPreMeasure(Tempo tempo) => Tempo = tempo;

        public Tempo Tempo { get; }
    }

    public sealed class DefaultTempoFixed : ImportWarning
    {
        public DefaultTempoFixed(double originalBpm) => OriginalBpm = originalBpm;

        public double OriginalBpm { get; }
    }

    public sealed class TimeSignatureNotFound : ImportWarning
    {
    }

    public sealed class TimeSignatureIgnoredInTrack : ImportWarning
    {
        public TimeSignatureIgnoredInTrack(Track track, TimeSignature timeSignature)
        {
            Track = track;
            TimeSignature = timeSignature;
        }

        public Track Track { get; }
        public TimeSignature TimeSignature { get; }
    }

    public sealed class TimeSignatureIgnoredInPreMeasure : ImportWarning
    {
        public TimeSignatureIgnoredInPreMeasure(TimeSignature timeSignature) => TimeSignature = timeSignature;

        public TimeSignature TimeSignature { get; }
    }

    public sealed class IncompatibleFormatSerializationVersion : ImportWarning
    {
        public IncompatibleFormatSerializationVersion(string currentVersion, string dataVersion)
        {
            CurrentVersion = currentVersion;
            DataVersion = dataVersion;
        }

        public string CurrentVersion { get; }
        public string DataVersion { get; }
    }
}

public sealed class ImportParams
{
    public bool SimpleImport { get; init; }
    public bool MultipleMode { get; init; }
    public string DefaultLyric { get; init; } = Constants.DefaultLyric;
}

public sealed class ConversionParams
{
    public bool ConvertPitch { get; init; }
}

public enum JapaneseLyricsType
{
    Unknown,
    RomajiCv,
    RomajiVcv,
    KanaCv,
    KanaVcv,
}

public static class JapaneseLyricsTypeExtensions
{
    public static bool IsRomaji(this JapaneseLyricsType type) =>
        type is JapaneseLyricsType.RomajiCv or JapaneseLyricsType.RomajiVcv;

    public static bool IsCv(this JapaneseLyricsType type) =>
        type is JapaneseLyricsType.RomajiCv or JapaneseLyricsType.KanaCv;

    public static JapaneseLyricsType? FindBestConversionTargetIn(this JapaneseLyricsType type, FormatInfo outputFormat)
    {
        if (outputFormat.SuggestedLyricType is { } suggested)
            return suggested;

        var options = outputFormat.PossibleLyricsTypes;
        if (options.Contains(type))
            return type;

        foreach (var option in options)
            if (option.IsRomaji() == type.IsRomaji())
                return option;

        foreach (var option in options)
            if (option.IsCv() == type.IsCv())
                return option;

        return options.Count > 0 ? options[0] : null;
    }
}
