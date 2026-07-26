using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using VOCALOIDPatcher.Translation;
using VOCALOIDPatcher.Utils;
using Yamaha.VOCALOID;
using Yamaha.VOCALOID.TrackEditor;
using Yamaha.VOCALOID.VSM;

namespace VOCALOIDPatcher.Patch.Patches;

#if !NET6_0
public class DeferAudioBufferLoadPatch : PatchBase
{
    public override string PatchName        => "DeferAudioBufferLoadPatch";
    public override Type   TargetClass      => typeof(AudioPartCacheManager);
    public override string TargetMethodName => "AddAllAudioBuffer";

    public override Type[] ArgumentTypes => new[] { typeof(WIVSMSequence) };

    internal sealed class LoadState
    {
        public int Generation;
        public CancellationTokenSource? Cancellation;
        public readonly HashSet<nint> Excluded = new();
    }

    private static readonly ConditionalWeakTable<AudioPartCacheManager, LoadState> States = new();

    private static readonly AccessTools.FieldRef<AudioPartCacheManager, Dictionary<nint, AugmentedAudioBuffer>>?
        AudioBuffers = CreateAudioBuffersRef();

    private static readonly MethodInfo? DrawAudioTrackCanvas =
        AccessTools.Method(typeof(AudioTrackControl), "DrawTrackCanvas", new[] { typeof(TrackEditorViewModel) });

    [HarmonyPrefix]
    private static bool Prefix(AudioPartCacheManager __instance, WIVSMSequence vsmSequence)
    {
        if (!Settings.FastProjectLoad || AudioBuffers == null)
            return true;

        var app = Application.Current;
        if (app == null)
            return true;

        var state = States.GetOrCreateValue(__instance);
        state.Cancellation?.Cancel();
        state.Cancellation?.Dispose();
        state.Cancellation = new CancellationTokenSource();
        state.Excluded.Clear();
        int generation = ++state.Generation;
        var token = state.Cancellation.Token;
        var entries = new List<(nint Handle, string Path)>();

        AudioBuffers(__instance).Clear();
        if (vsmSequence != null)
        {
            foreach (var track in vsmSequence.AudioTracks)
            {
                foreach (var part in track.Parts)
                {
                    if (part is not WIVSMAudioPart audioPart)
                        continue;

                    string path = audioPart.GetWaveFilePath();
                    if (!string.IsNullOrEmpty(path))
                        entries.Add(((nint)audioPart, path));
                }
            }
        }

        _ = LoadAsync(__instance, state, generation, entries, token);

        return false;
    }

    internal static void Cancel(AudioPartCacheManager manager)
    {
        var state = States.GetOrCreateValue(manager);
        state.Cancellation?.Cancel();
        state.Cancellation?.Dispose();
        state.Cancellation = null;
        state.Excluded.Clear();
        state.Generation++;
    }

    internal static void Exclude(AudioPartCacheManager manager, WIVSMAudioPart part)
    {
        if (part == null)
            return;

        States.GetOrCreateValue(manager).Excluded.Add((nint)part);
    }

    private static async Task LoadAsync(
        AudioPartCacheManager manager,
        LoadState state,
        int generation,
        List<(nint Handle, string Path)> entries,
        CancellationToken token)
    {
        try
        {
            var loaded = await Task.Run(() =>
            {
                var result = new List<(nint Handle, AugmentedAudioBuffer Buffer)>();
                foreach (var entry in entries)
                {
                    token.ThrowIfCancellationRequested();
                    var buffer = new AugmentedAudioBuffer();
                    if (buffer.Load(entry.Path))
                        result.Add((entry.Handle, buffer));
                }
                return result;
            }, token).ConfigureAwait(false);

            var app = Application.Current;
            if (app == null || token.IsCancellationRequested)
                return;

            await app.Dispatcher.InvokeAsync(() =>
            {
                if (state.Generation != generation || token.IsCancellationRequested || AudioBuffers == null)
                    return;

                var buffers = AudioBuffers(manager);
                foreach (var item in loaded)
                    if (!state.Excluded.Contains(item.Handle))
                        buffers[item.Handle] = item.Buffer;

                RefreshTrackWaveforms();
                ShowOtherTracksNotesPatch.RequestRefreshPianoroll();
            }, DispatcherPriority.Background);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Debug.Print(TranslationManager.Tr("VOCALOIDPatcher_Debug_FastProjectLoad_DeferWaveformFailed", e.Message));
        }
    }

    private static AccessTools.FieldRef<AudioPartCacheManager, Dictionary<nint, AugmentedAudioBuffer>>?
        CreateAudioBuffersRef()
    {
        try
        {
            return AccessTools.FieldRefAccess<AudioPartCacheManager, Dictionary<nint, AugmentedAudioBuffer>>("audioBuffers");
        }
        catch
        {
            return null;
        }
    }

    private static void RefreshTrackWaveforms()
    {
        foreach (Window window in Application.Current.Windows)
        {
            foreach (var division in ShowOtherTracksNotesPatch.FindVisualChildren<TrackEditorDivision>(window))
            {
                if (DrawAudioTrackCanvas != null)
                {
                    foreach (var track in ShowOtherTracksNotesPatch.FindVisualChildren<AudioTrackControl>(division))
                        if (track.DataContext is TrackEditorViewModel vm)
                            DrawAudioTrackCanvas.Invoke(track, new object?[] { vm });
                }

                division.UpdateAudioWaveViewport();
            }
        }
    }
}

public class CancelDeferredAudioBufferLoadPatch : PatchBase
{
    public override string PatchName        => "CancelDeferredAudioBufferLoadPatch";
    public override Type   TargetClass      => typeof(AudioPartCacheManager);
    public override string TargetMethodName => "RemoveAllAudioBuffer";

    [HarmonyPrefix]
    private static void Prefix(AudioPartCacheManager __instance)
    {
        if (Settings.FastProjectLoad)
            DeferAudioBufferLoadPatch.Cancel(__instance);
    }
}

public class ExcludeRemovedDeferredAudioBufferPatch : PatchBase
{
    public override string PatchName        => "ExcludeRemovedDeferredAudioBufferPatch";
    public override Type   TargetClass      => typeof(AudioPartCacheManager);
    public override string TargetMethodName => "RemoveAudioBuffer";
    public override Type[] ArgumentTypes    => new[] { typeof(WIVSMAudioPart) };

    [HarmonyPrefix]
    private static void Prefix(AudioPartCacheManager __instance, WIVSMAudioPart audioPart)
    {
        if (Settings.FastProjectLoad)
            DeferAudioBufferLoadPatch.Exclude(__instance, audioPart);
    }
}

public class ExcludeReplacedDeferredAudioBufferPatch : PatchBase
{
    public override string PatchName        => "ExcludeReplacedDeferredAudioBufferPatch";
    public override Type   TargetClass      => typeof(AudioPartCacheManager);
    public override string TargetMethodName => "ReplaceAudioBuffer";
    public override Type[] ArgumentTypes    => new[] { typeof(WIVSMAudioPart) };

    [HarmonyPrefix]
    private static void Prefix(AudioPartCacheManager __instance, WIVSMAudioPart audioPart)
    {
        if (Settings.FastProjectLoad)
            DeferAudioBufferLoadPatch.Exclude(__instance, audioPart);
    }
}
#endif

public class DeferLoadAnalyticsPatch : PatchBase
{
    public override string PatchName        => "DeferLoadAnalyticsPatch";
    public override Type   TargetClass      => typeof(Sequence);
    public override string TargetMethodName => "SendSequenceLogOnLoad";

    private static readonly MethodInfo? Original =
        AccessTools.Method(typeof(Sequence), "SendSequenceLogOnLoad");

    private static bool _passthrough;

    [HarmonyPrefix]
    private static bool Prefix(Sequence __instance)
    {
        if (_passthrough || !Settings.FastProjectLoad || Original == null)
            return true;

        var app = Application.Current;
        if (app == null)
            return true;

        app.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                _passthrough = true;
                Original.Invoke(__instance, null);
            }
            catch (Exception e)
            {
                Debug.Print(TranslationManager.Tr("VOCALOIDPatcher_Debug_FastProjectLoad_DeferLogFailed", e.Message));
            }
            finally
            {
                _passthrough = false;
            }
        }), DispatcherPriority.Background);

        return false;
    }
}
