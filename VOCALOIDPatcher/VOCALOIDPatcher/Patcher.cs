using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using VOCALOIDPatcher.Jobs;
using VOCALOIDPatcher.Patch;
using VOCALOIDPatcher.Patch.Patches;
using VOCALOIDPatcher.Translation;
using VOCALOIDPatcher.UI;
using VOCALOIDPatcher.Utils;
using Yamaha.VOCALOID;

namespace VOCALOIDPatcher;

public static class Patcher
{
    public static readonly bool DebugMode = KeyState.IsKeyDown(0xA0);

    public static readonly string ConfigDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VOCALOIDPatcher");

    public static readonly string ConfigFile =
        Path.Combine(ConfigDir, "config.json");

    public static string DataDir =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VOCALOIDPatcher");

    public static ConfigManager ConfigManager = null!;

    private static Harmony _harmony = null!;

    public static bool VstPluginMode;

    public static string Version => "2.3.1";

#pragma warning disable CA2255
    [ModuleInitializer]
    public static void Initialize()
    {
        AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
        {
            if (args.Name.StartsWith("VOCALOID6"))
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    if (assembly.GetName().Name?.StartsWith("VOCALOID6") == true)
                        return assembly;

            return null;
        };

        try
        {
            PatcherInit();
        }
        catch (Exception e)
        {
            Debug.ShowErrorMessage(TranslationManager.Tr("VOCALOIDPatcher_Debug_Patcher_InitFailed"), e);
        }
    }

    private static void PatcherInit()
    {
        if (!Directory.Exists(ConfigDir)) Directory.CreateDirectory(ConfigDir);

        VstPluginMode = DetectVstPluginMode();

        if (VstPluginMode)
            DataDir = Path.Combine(new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "VOCALOID6", "Editor",
                "VOCALOIDPatcher"
            });

        ConfigManager = new ConfigManager(ConfigFile);
        _harmony = new Harmony("VOCALOIDPatcher");

        ConsoleHelper.InitConsole();

        TranslationManager.Initialize();

        if (!CheckRuntimeCompatibility()) return;

        Debug.Print(TranslationManager.Tr("VOCALOIDPatcher_Debug_Patcher_Started"));
        Debug.Print(TranslationManager.Tr("VOCALOIDPatcher_Debug_Patcher_Version", Version));
        Debug.Print("https://github.com/IzumiiKonata/VOCALOIDPatcher");

        var version = typeof(App).Assembly.GetName().Version;
        Debug.Print(TranslationManager.Tr("VOCALOIDPatcher_Debug_Patcher_EditorVersion", version));
        Debug.Print(TranslationManager.Tr("VOCALOIDPatcher_Debug_Patcher_TranslationInitialized"));

        if (VstPluginMode)
        {
            Debug.Print(TranslationManager.Tr("VOCALOIDPatcher_Debug_Patcher_VstMode"));
            VstPluginPatch.ApplyPatches(_harmony);
        }

        ApplyPatches();

        WpfTranslationPatch.InstallGlobalHandlers();

        if (!VstPluginMode) PostInject();

        UpdateChecker.CheckAsync();
    }

    public static void PostInject()
    {
        AddPatcherMenuItem();
        JobMenu.Install();
        Formats.LibreSvip.SvipFormatMenu.Install();
        Formats.LibreSvip.SvipFormatDragDrop.Install();
        AutoSaveService.UpdateFromSettings();
        WorkingSetTrimmer.Install();
        SpectrumWidget.Install();
    }

    private static bool CheckRuntimeCompatibility()
    {
#if NET6_0
        var runtimeMajor = Environment.Version.Major;
        if (runtimeMajor != 6)
        {
            Debug.ShowErrorMessage(
                TranslationManager.Tr("VOCALOIDPatcher_Debug_Patcher_RuntimeMismatch", runtimeMajor),
                TranslationManager.Tr("VOCALOIDPatcher_Debug_Patcher_RuntimeMismatchTitle"));
            Environment.Exit(0);
            return false;
        }
#endif
        return true;
    }

    private static bool DetectVstPluginMode()
    {
        try
        {
            ReflectionUtils.GetMainWindow();
            return false;
        }
        catch (Exception)
        {
            return true;
        }
    }

    private sealed class FeatureGroup
    {
        public readonly string? FeatureKey;
        public readonly PatchBase[] Patches;

        public FeatureGroup(string? featureKey, params PatchBase[] patches)
        {
            FeatureKey = featureKey;
            Patches = patches;
        }
    }

    private static void ApplyPatches()
    {
        var groups = new List<FeatureGroup>
        {
            new(null,
                new UnhandledExceptionDialogPatch(),
                new AppLanguagePatch(),
                new WpfTranslationPatch(),
                new ResourceManagerPatch(),
                new AudioEffectWindowTranslatePatch(),
                new SwingMenuPatch(),
                new TouchpadHorizontalScrollPatch(),
                new TrackTouchpadHorizontalScrollPatch(),
                new CursorNoteNameTranslationFixPatch(),
                new FastCanvasViewportRectPatch(),
                new FastCanvasViewportRangePatch(),
#if !NET6_0
                new AudioBufferReleaseSafetyPatch(),
                new CommonUserSettingsJsonOptionsPatch(),
                new UniqueUserSettingsJsonOptionsPatch(),
                new NewsImageCachePatch(),
                new ShortcutModifierCachePatch(),
                new ShortcutKeyMapCachePatch(),
                new ShortcutJsonModifierCachePatch(),
#endif
                new WaveformBaselineInvalidatePatch(),
                new TrackSelectedFillBrushPatch(),
                new TrackSelectedStrokeBrushPatch(),
                new TrackUnselectedFillBrushPatch(),
                new TrackUnselectedStrokeBrushPatch(),
                new TrackOverlayFillBrushPatch()),

            new(Settings.ShowOtherTracksNotesKey,
                new ShowOtherTracksNotesPatch(),
                new TrackMuteRefreshPatch(),
                new TrackSoloRefreshPatch()),

            new(Settings.ShowNotePitchKey, new ShowNotePitchPatch()),
            new(Settings.RoundedNotesKey, new RoundedNotePatch()),

            new(Settings.CenteredLyricsKey,
                new CenteredLyricPatch(),
                new CenteredLyricPlainPatch()),

            new(Settings.AlwaysShowWaveformKey,
                new AlwaysShowWaveformPatch(),
                new WaveformRenderPatch()),

            new(Settings.SvEditorStyleKey,
                new NoteRowRemapPatch(),
                new ScoreFrameCaptureListPatch(),
                new ScoreFrameCaptureFilePatch()
#if !NET6_0
                , new ScoreFrameCaptureCombinedPatch()
#endif
            ),

            new(Settings.ShowCharacterArtKey, new CharacterArtPatch()),

            new(Settings.FastProjectLoadKey,
                new LazyLyricRenderPatch(),
#if !NET6_0
                new DeferAudioBufferLoadPatch(),
                new CancelDeferredAudioBufferLoadPatch(),
                new ExcludeRemovedDeferredAudioBufferPatch(),
                new ExcludeReplacedDeferredAudioBufferPatch(),
                new VisiblePianorollNotesPatch(),
                new VisiblePianorollLyricsPatch(),
                new PianorollVirtualizationViewportPatch(),
#endif
                new DeferLoadAnalyticsPatch(),
                new LeanLyricTextPatch()),

            new(Settings.SkipUnchangedPartRedrawKey,
                new SkipUnchangedPartRedrawPatch()
#if !NET6_0
                , new PianorollSelectionPenCachePatch()
#endif
            ),

            new(Settings.ThrottleRendererPreviewKey, new RendererPreviewThrottlePatch()),

            new(Settings.FastSelectionSweepKey,
                new TimeSigSelectionTrackPatch(),
                new TempoSelectionTrackPatch(),
                new BreakPointSelectionTrackPatch(),
                new FastSelectionSweepPatch()),

            new(Settings.DeferParameterViewUpdateKey,
                new ParameterViewDeferPatch()
#if !NET6_0
                , new ParameterVisibleRangePatch()
#endif
            ),

#if !NET6_0
            new(Settings.SmoothPlayheadKey,
                new SmoothPlayheadBeginPatch(),
                new SmoothPlayheadEndPatch()),

            new(Settings.FreeAudioPcmCacheKey,
                new AudioPcmReleasePatch(),
                new AudioThumbFromCachePatch()),
#endif

            new(Settings.TrimWorkingSetKey,
                new WorkingSetTrimPatch(),
                new RenderCompleteTrimPatch()),

#if !NET6_0
            new(Settings.OptimizeTrackRenderingKey,
                new TrackNoteInsertPatch(),
                new TrackNoteRemovePatch(),
                new TrackNoteDurationChangedPatch(),
                new TrackNoteMovedPatch(),
                new TrackNoteNumberChangedPatch(),
                new TrackPartDurationChangedPatch(),
                new UIMidiPartNotesRenderPatch(),
                new AudioTrackHeightResizePatch(),
                new MidiTrackSelectionRefreshPatch(),
                new AudioTrackSelectionRefreshPatch(),
                new MidiPartSelectionPenCachePatch(),
                new AudioPartSelectionPenCachePatch(),
                new TrackRulerGridLinePatch(),
                new MusicalRulerGridLinePatch()),

            new(Settings.CacheRenderedWavesKey,
                new RenderedWaveCachePatch(),
                new RenderedWaveCacheClearPatch()),
#endif
        };

        var disabled = new List<string>();

        foreach (var group in groups)
        {
            foreach (var patch in group.Patches)
            {
                Debug.Print(TranslationManager.Tr("VOCALOIDPatcher_Debug_Patcher_ApplyingPatch", patch.PatchName));

                if (patch.Apply(_harmony, group.FeatureKey) || group.FeatureKey == null)
                    continue;

                if (!Settings.IsFeatureDisabled(group.FeatureKey))
                {
                    Settings.DisableFeature(group.FeatureKey);
                    disabled.Add(group.FeatureKey);
                    Debug.Print(TranslationManager.Tr("VOCALOIDPatcher_Debug_Patcher_FeatureDisabled", group.FeatureKey));
                }
            }
        }

        if (disabled.Count > 0)
            ReportDisabledFeatures(disabled);
    }

    private const string NotifiedDisabledFeaturesKey = "NotifiedDisabledFeatures";

    private static void ReportDisabledFeatures(List<string> features)
    {
        var notified = ConfigManager.Get(NotifiedDisabledFeaturesKey, new List<string>());

        var newly = features.FindAll(f => !notified.Contains(f));
        if (newly.Count == 0)
            return;

        var list = string.Join(Environment.NewLine,
            newly.Select(key => "• " + TranslationManager.Tr($"VOCALOIDPatcher_{key}_Header")));

        Debug.ShowMessageBox(TranslationManager.Tr("VOCALOIDPatcher_Patch_FeaturesDisabled", list));

        notified.AddRange(newly);
        ConfigManager.Set(NotifiedDisabledFeaturesKey, notified);
    }

    private static readonly MenuItem PatcherMenuItem = new()
    {
        Header = "VOCALOID Patcher",
        Name = "VOCALOIDPatcherMenuItem"
    };

    private static void AddPatcherMenuItem()
    {
        try
        {
            var menu = ReflectionUtils.GetMainMenu();

            WpfTranslationPatch.MarkUntranslatable(PatcherMenuItem);
            PatcherMenuItem.Click += (_, _) => SettingsWindow.ShowSingleton();

            menu.Items.Insert(menu.Items.Count - 1, PatcherMenuItem);

            TranslationManager.LanguageChanged += (_, _) => RefreshPatcherMenuHeader();
            UpdateChecker.UpdateAvailable += RefreshPatcherMenuHeader;
            RefreshPatcherMenuHeader();
        }
        catch (Exception e)
        {
            Debug.ShowErrorMessage(e.Message + e.StackTrace);
        }
    }

    private static void RefreshPatcherMenuHeader()
    {
        void Apply()
        {
            var header = "VOCALOID Patcher";
            if (UpdateChecker.HasUpdate)
                header += TranslationManager.Tr("VOCALOIDPatcher_Update_MenuSuffix");

            PatcherMenuItem.Header = header;
        }

        if (PatcherMenuItem.Dispatcher.CheckAccess())
            Apply();
        else
            PatcherMenuItem.Dispatcher.Invoke(Apply);
    }
}
