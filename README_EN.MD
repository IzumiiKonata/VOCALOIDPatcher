# VOCALOID Patcher

[<img src="./res/lang_zh.svg" alt="阅读中文版" height="28">](./README.MD)
[<img src="./res/lang_ja.svg" alt="日本語で読む" height="28">](./README_JA.MD)

VOCALOID Patcher is loaded directly by `VOCALOID 6 Editor` and adds interface translations, piano-roll conveniences, batch tools, and project format conversion.

- Translation data: `6.13.1.1`
- Tested with: `6.6.0`, `6.11.0.1`, `6.12.0.1`, `6.13.0.1` and `6.13.1.1 (Latest)`

I do not use VOCALOID 6 heavily enough to encounter every possible project, so translation and conversion edge cases are bound to turn up. When reporting one, please include the editor version, source format, reproduction steps, and a small sample project if possible.

## Installation

1. Download the latest build from [Releases](https://github.com/IzumiiKonata/VOCALOIDPatcher/releases).
2. Older versions of VOCALOID 6 (versions `6.3.0` and earlier) are no longer supported starting with version `2.3.1`; if you are using an older version of the editor and it fails to launch after installing the patch, please download the `2.3.0` build with the `_NET6` suffix.
3. Copy everything except `README.txt` into `C:\Program Files\VOCALOID6\Editor`, replacing the existing files.
4. Start the editor and open a project. Installation succeeded if `VOCALOID Patcher` appears in the menu bar.

If VOCALOID 6 is installed elsewhere, use its actual Editor directory instead.

![VOCALOID Patcher menu](./res/screenshot.png)

## Features

### Interface translation

Both editor resources and a number of hardcoded labels can be translated. Translations live in external XML files, so languages can be switched or edited without rebuilding the patch.

### Piano roll

- Show notes from other tracks in the current piano roll, with an option to skip muted tracks.
- Display note names such as `C4` and `F#3`.
- Rounded notes, vertically centered lyrics, and an always-visible waveform.
- Show the current voicebank cover in the lower-right corner. Each voicebank can have its own art, size, and opacity.
- Static images, animated `GIF` files, and videos such as `MP4` are supported.
- Horizontal touchpad scrolling for the piano roll.

![Show notes from other tracks](./res/show_other_notes.png)
![Show note pitch](./res/show_note_pitch.png)
![Rounded notes](./res/rounded_notes.png)
![Centered lyrics](./res/centered_notes_b.png)
![Always show waveform](./res/always_show_wave.png)

### Batch tools

- Replace the lyrics of selected notes.
- Quantize note lengths to the current grid.
- Generate upper or lower harmonies from thirds through octaves, into an existing or a new track.
- Apply eighth-note or sixteenth-note Swing and Shuffle presets from the piano-roll context menu.

### Project format conversion

In addition to the formats supported by the editor itself, the patch can import and export:

`USTX`, `UST`, `SVP`, `S5P`, `VSQ`, `VSQX`, `VPR`, `CCS`, `TSSln`, `ACEP`, `VVProj`, `PPSF`, `DV`, `TLPX`, `TLP`, `SVIP`, `DS`, `AISP`, `Y77`, `VOG`, `MusicXML`, `OpenSVIP JSON`, and `UFData`.

Lyrics can also be exported as `LRC`, `SRT`, or `ASS`.

This converter is ported from [LibreSVIP](https://github.com/SoulMelody/LibreSVIP) and adapted to VOCALOID 6's project model:

- A format-specific options window appears after selecting a file. Drag-and-drop import uses the same settings.
- Pitch, volume, breathiness, gender, and strength curves are mapped both ways between the neutral model and VOCALOID controllers.
- SVP import handles default vibrato, Instant Pitch, breath notes, and note groups. Vibrato amplitude follows LibreSVIP's original calculation.
- Instrumental tracks are carried over when the format provides them. Embedded VPR audio can be extracted on request.
- Format-specific settings include lyric encoding, singer and language defaults, sampling intervals, subtitle splitting, and track selection.

The source and target editors do not share an identical data model. Check pitch curves, phonemes, breath marks, and instrumental timing after conversion. Modern ZIP/JSON PPSF projects are supported; legacy binary PPSF is not.

### Other

- Save the current project automatically at a configurable interval.
- Display a real-time spectrum in the editor. Spectrum capture may not work with ASIO.

## Debugging

Hold left `Shift` while starting VOCALOID 6 to open an additional console with patch logs. Please include the relevant output when reporting startup, conversion, or compatibility problems.

## Development

A recent VOCALOID 6 installation is required at `C:\Program Files\VOCALOID6\Editor`. The project builds both `.NET 6` and `.NET 8` targets:

```powershell
dotnet build VOCALOIDPatcher\VOCALOIDPatcher.csproj -c Release
```

The Release build merges dependencies into:

```text
VOCALOIDPatcher\bin\Release\net8.0-windows\out\Microsoft.Xaml.Behaviors.dll
```

During development, `mklink` can be used to link this DLL into the editor directory instead of copying it after every build.

### Updating translations

Run `ResourceTranslationComparer` to find:

- new resource entries that have not been translated;
- resources that the editor no longer uses;
- hardcoded labels that belong in `HardcodedPropertyMap.xml`;
- stale or redundant translation keys.

Pass `--write-map` to generate a cleaned `HardcodedPropertyMap.generated.xml`.
