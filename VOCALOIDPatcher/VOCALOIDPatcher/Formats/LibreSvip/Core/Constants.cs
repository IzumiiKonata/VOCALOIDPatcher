namespace VOCALOIDPatcher.Formats.LibreSvip.Core;

// 移植自 libresvip/core/constants.py
public static class Constants
{
    public const string PackageName = "libresvip";

    // 歌词相关
    public const string DefaultPhoneme = "la";
    public const string DefaultEnglishLyric = DefaultPhoneme;
    public const string DefaultSpanishLyric = DefaultEnglishLyric;
    public const string DefaultChineseLyric = "啦";
    public const string DefaultJapaneseLyric = "ラ";
    public const string DefaultKoreanLyric = "라";

    // MIDI 相关
    public const double DefaultBpm = 120.0;
    public const int KeyInOctave = 12;
    public const int TicksInBeat = 480;
    public const int MinBreakLengthBetweenPitchSections = TicksInBeat;
}
