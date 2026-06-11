using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using VOCALOIDPatcher.Utils;
using Yamaha.VOCALOID;
using Yamaha.VOCALOID.MusicalEditor;
using Yamaha.VOCALOID.VSM;
using UpdateViewTypeFlag = Yamaha.VOCALOID.MusicalEditor.UpdateViewTypeFlag;

namespace VOCALOIDPatcher.Patch.Patches;

public class ParameterViewDeferPatch : PatchBase
{
    public override string PatchName        => "ParameterViewDeferPatch";
    public override Type   TargetClass      => typeof(ParameterView);
    public override string TargetMethodName => "UpdateView";

    public override Type[] ArgumentTypes => new[]
    {
        typeof(object),
        typeof(UpdateViewTypeFlag),
        typeof(UpdateObserverNotifyEventArgs),
        typeof(object)
    };

    private static readonly MethodInfo? MUpdateActiveTrack =
        AccessTools.Method(typeof(ParameterView), "UpdateActiveTrack", Type.EmptyTypes);

    private static readonly MethodInfo? MUpdateActivePart =
        AccessTools.Method(typeof(ParameterView), "UpdateActivePart", Type.EmptyTypes);

    private sealed class PendingBox
    {
        public bool Scheduled;
        public bool TrackKind;
    }

    private static readonly ConditionalWeakTable<ParameterView, PendingBox> Pending = new();

    [HarmonyPrefix]
    private static bool Prefix(ParameterView __instance, UpdateViewTypeFlag typeFlags)
    {
        if (!Settings.DeferParameterViewUpdate || MUpdateActiveTrack == null || MUpdateActivePart == null)
            return true;

        if (typeFlags != UpdateViewTypeFlag.ActiveTrackChanged && typeFlags != UpdateViewTypeFlag.ActivePartChanged)
            return true;

        var box = Pending.GetOrCreateValue(__instance);
        if (typeFlags == UpdateViewTypeFlag.ActiveTrackChanged)
            box.TrackKind = true;

        if (box.Scheduled)
            return false;

        box.Scheduled = true;
        __instance.Dispatcher.BeginInvoke(new Action(() =>
        {
            box.Scheduled = false;
            bool trackKind = box.TrackKind;
            box.TrackKind = false;

            try
            {
                (trackKind ? MUpdateActiveTrack : MUpdateActivePart).Invoke(__instance, null);
            }
            catch (Exception e)
            {
                Debug.Print($"延后参数面板更新失败: {e.Message}");
            }
        }), DispatcherPriority.Background);

        return false;
    }
}
