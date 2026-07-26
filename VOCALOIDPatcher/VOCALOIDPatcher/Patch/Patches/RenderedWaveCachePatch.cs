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
using Yamaha.VOCALOID.MusicalEditor;
using Yamaha.VOCALOID.VSM;

namespace VOCALOIDPatcher.Patch.Patches;

#if !NET6_0
public class RenderedWaveCachePatch : PatchBase
{
    public override string PatchName        => "RenderedWaveCachePatch";
    public override Type   TargetClass      => typeof(RenderedWaveCacheManager);
    public override string TargetMethodName => "GetSampleEnumerator";

    public override Type[] ArgumentTypes => new[] { typeof(WIVSMMidiPart) };

    private static int Capacity => Settings.FreeAudioPcmCache ? 4 : 1;

    private static readonly AccessTools.FieldRef<RenderedWaveCacheManager, Dictionary<nint, Tuple<string, AugmentedAudioBuffer>>>? DictRef =
        CreateDictRef();

    private static AccessTools.FieldRef<RenderedWaveCacheManager, Dictionary<nint, Tuple<string, AugmentedAudioBuffer>>>? CreateDictRef()
    {
        try
        {
            return AccessTools.FieldRefAccess<RenderedWaveCacheManager, Dictionary<nint, Tuple<string, AugmentedAudioBuffer>>>("waveDictionary");
        }
        catch
        {
            return null;
        }
    }

    private static readonly MethodInfo? DrawWaveMethod =
        AccessTools.Method(typeof(PianorollView), "DrawRenderedWaveCanvas", new[] { typeof(MusicalEditorViewModel) });

    private sealed class LruState
    {
        public readonly LinkedList<nint> Order = new();
        public readonly HashSet<nint> Pending = new();
        public int Generation;
    }

    private static readonly ConditionalWeakTable<RenderedWaveCacheManager, LruState> States = new();
    private static readonly SemaphoreSlim LoadSlots = new(2);

    [HarmonyPrefix]
    private static bool Prefix(RenderedWaveCacheManager __instance, WIVSMMidiPart part, ref IVSMSampleEnumerator? __result)
    {
        if (!Settings.CacheRenderedWaves || DictRef == null)
            return true;

        try
        {
            __result = null;
            if (part == null)
                return false;

            var dict = DictRef(__instance);
            var state = States.GetOrCreateValue(__instance);
            var key = (nint)part;

            if (!part.HasValidRenderedWave)
            {
                dict.Remove(key);
                state.Order.Remove(key);
                return false;
            }

            var path = part.WaveFilePath;
            if (string.IsNullOrEmpty(path))
                return false;

            if (dict.TryGetValue(key, out var tuple) && tuple != null && tuple.Item1 == path)
            {
                state.Order.Remove(key);
                state.Order.AddFirst(key);
                __result = tuple.Item2;
                return false;
            }

            if (!state.Pending.Add(key))
                return false;

            int generation = state.Generation;
            Task.Run(() =>
            {
                AugmentedAudioBuffer? buffer = null;
                try
                {
                    LoadSlots.Wait();
                    if (Volatile.Read(ref state.Generation) != generation)
                        return;

                    var loaded = new AugmentedAudioBuffer();
                    if (loaded.Load(path))
                        buffer = loaded;
                }
                catch
                {
                }
                finally
                {
                    LoadSlots.Release();
                }

                Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    state.Pending.Remove(key);
                    if (buffer == null || state.Generation != generation)
                        return;

                    dict[key] = Tuple.Create(path, buffer);
                    state.Order.Remove(key);
                    state.Order.AddFirst(key);
                    while (state.Order.Count > Capacity)
                    {
                        var oldest = state.Order.Last!.Value;
                        state.Order.RemoveLast();
                        dict.Remove(oldest);
                    }

                    RefreshWaveCanvases();
                }), DispatcherPriority.Background);
            });

            return false;
        }
        catch (Exception e)
        {
            Debug.Print(TranslationManager.Tr("VOCALOIDPatcher_Debug_RenderedWaveCache_Failed", e.Message));
            return true;
        }
    }

    private static void RefreshWaveCanvases()
    {
        if (DrawWaveMethod == null || Application.Current == null)
            return;

        try
        {
            foreach (Window window in Application.Current.Windows)
            {
                foreach (var view in ShowOtherTracksNotesPatch.FindVisualChildren<PianorollView>(window))
                {
                    if (view.DataContext is MusicalEditorViewModel vm)
                        DrawWaveMethod.Invoke(view, new object[] { vm });
                }
            }
        }
        catch (Exception e)
        {
            Debug.Print(TranslationManager.Tr("VOCALOIDPatcher_Debug_RenderedWaveCache_RefreshCanvasFailed", e.Message));
        }
    }

    internal static void Invalidate(RenderedWaveCacheManager manager)
    {
        var state = States.GetOrCreateValue(manager);
        state.Generation++;
        state.Pending.Clear();
        state.Order.Clear();
    }
}

public class RenderedWaveCacheClearPatch : PatchBase
{
    public override string PatchName        => "RenderedWaveCacheClearPatch";
    public override Type   TargetClass      => typeof(RenderedWaveCacheManager);
    public override string TargetMethodName => "Clear";

    public override Type[] ArgumentTypes => Type.EmptyTypes;

    [HarmonyPostfix]
    private static void Postfix(RenderedWaveCacheManager __instance)
    {
        RenderedWaveCachePatch.Invalidate(__instance);
    }
}
#endif
