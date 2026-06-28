# VOCALOID Patcher

[<img src="./res/lang_zh.svg" alt="阅读中文版" height="28">](./README.MD)
[<img src="./res/lang_ja.svg" alt="日本語で読む" height="28">](./README_JA.MD)

A patch that adds extra features to the `VOCALOID 6` editor. In theory it supports every version of the V6 editor.

### Translation version: `6.13.0.1`
### Tested editor versions: `6.1.0.2 (.net 6.0)`, `6.6.0`, `6.11.0.1`, `6.12.0.1`, `6.13.0.1`

I don't have much experience with the V6 editor, so some translations may be inaccurate. Open an issue or report it in the group, and I'll fix the translation in the next release.

## Installation
Download the latest release from [Releases](https://github.com/IzumiiKonata/VOCALOIDPatcher/releases).  
If your editor is an older version, download the `.net 6.0` build.  
Copy all files from the archive _(except README.txt)_ into `C:\Program Files\VOCALOID6\Editor`, overwriting the existing ones (this may not work if the editor is not installed in the default directory).  
Launch the editor and open a project; if a `VOCALOID Patcher` entry appears in the menu bar, the installation was successful.  
![img](./res/screenshot.png)

## Features

### Localization / Multi-language
- Translates both the editor's resource entries and its hardcoded strings. Translation files are external XML, so you can edit them yourself.

### Piano Roll Enhancements
- **Show Notes from Other Tracks**: Overlays the notes of other tracks onto the current track's piano roll, with an option to skip muted tracks.  
![Show Other Notes](./res/show_other_notes.png)
- **Show Note Pitch**: Overlays the pitch name (e.g. `C4`, `F#3`) on each note.  
![Show Note Pitch](./res/show_note_pitch.png)
- **Rounded Notes**: Changes the note style to rounded rectangles.  
![Rounded Notes](./res/rounded_notes.png)
- **Center Lyrics**: Vertically centers the lyrics.  
![Centered Notes](./res/centered_notes_b.png)
- **Always Show Waveform**  
![Always Show Wave](./res/always_show_wave.png)
- **Show Voicebank Cover / Custom Art**: Displays the cover art of the voicebank used by the current part in the bottom-right of the piano roll, with adjustable size and opacity.
  - You can upload a custom image **per voicebank** to replace the default cover; it updates automatically when a part's voicebank changes.
  - The art supports static images as well as animated `GIF` and video files such as `MP4` (looped and muted), with no extra dependencies.
  - The art is rendered beneath the notes, so it never covers them.

### Widgets
- **Spectrum Visualizer**: Shows a real-time audio spectrum in the editor (does not work properly when using ASIO).

### Jobs
- **Replace Lyrics**: Batch-replaces the lyrics of the selected notes with a specified syllable.
- **Quantize Length**: Quantizes note lengths to the grid.
- **Generate Harmony**: Generates upper/lower 3rd, 4th, 5th, 6th, octave (and more) harmony voices based on the scale root; can be written to an existing track or a new one.
- **Swing Groove**: Built-in 8th / 16th Swing and Shuffle presets in the piano roll's right-click menu.

### Multi-format Import / Export
- In addition to the native `Vpr/Vsqx/MIDI`, it adds round-trip (import + export) conversion with the following project formats:
  `USTX` (OpenUTAU), `UST` (UTAU), `SVP` (Synthesizer V Studio), `S5P` (Synthesizer V Editor), `VSQ` (VOCALOID2), `CCS` (CeVIO), `Tssln` (VoiSona), `ACEP` (ACE Studio), `VVProj` (VOICEVOX), `PPSF` (Piapro Studio NT), `DV` (DeepVocal), `TLPX` / `TLP` (TuneLab), `SVIP` (X Studio), `DS` (DiffSinger), `AISP` (AISingers), `Y77` (Yuan77), `VOG` (Vogen), `MusicXML`, `OpenSVIP` (json), `ufdata` (UtaFormatix).
- It can also export lyrics / subtitles: `LRC`, `SRT`, `ASS`.
- During import/export, data such as pitch curves and phonemes is automatically converted to match the target format.

### Performance & Smoothness
- **Faster Project Loading**: Moves deferrable work (audio waveform pre-reads, load-time analytics, etc.) off the critical path so large projects open faster.
- **Memory Optimization**: Frees audio caches that are no longer needed and trims the working set after loading and rendering.
- **Optimize Track Area Rendering**: Caches reusable objects such as track / note brushes to cut the track editor's redraw cost.
- **Faster Part Switching**: Skips redrawing unchanged content when switching between parts within the same track, making switching smoother.
- **Rendered Waveform Cache**: Reuses already-loaded synthesized waveforms when switching parts, avoiding a fresh disk read on the UI thread every time.
- **Faster Selection Clearing**: Only deselects events that were actually selected, instead of scanning the entire project on every click.
- **Deferred Parameter Panel Refresh**: Refreshes the bottom parameter panel slightly later when switching tracks / parts, so switching feels instant.
- **Smooth Playhead**: Drives the piano roll's white playback-position line from the display's refresh cycle (instead of a background timer), removing the irregular stutter — especially noticeable while the spectrum widget is on.

### Misc
- **Auto Save**: Automatically saves the current project at a configurable interval.
- **Touchpad Horizontal Scrolling**: Adds horizontal (left/right) scrolling of the piano roll for touchpads.

## Debugging
After installing the patch, press and hold `Left Shift` while opening the `VOCALOID 6` editor to bring up a console, where `VOCALOIDPatcher` prints some debug logs.

## Updating Translations
Run `ResourceTranslationComparer`; the console will print the translation comparison results:

- **[1] Untranslated entries**: Newly added resx translation entries.
- **[2] Untranslated and unused entries**: resx entries that are not referenced by any UI element.
- **[3] Hardcoded strings**: Add these to `HardcodedPropertyMap.xml` (literal → custom key).
- **[4] Redundant keys / [5] Unused hardcoded strings**

Pass the `--write-map` argument to additionally export a cleaned-up `HardcodedPropertyMap.generated.xml`.

## Development

### Notes
You need a recent VOCALOID6 editor installed in the default directory `(C:\Program Files\VOCALOID6\Editor)`.  
After building, a dependency-bundled `Microsoft.Xaml.Behaviors.dll` is produced in `VOCALOIDPatcher\bin\Release\net8.0-windows\out`.  
You can use the `mklink` command to symlink the built dll into the editor directory, avoiding the need to manually overwrite it after every build.
