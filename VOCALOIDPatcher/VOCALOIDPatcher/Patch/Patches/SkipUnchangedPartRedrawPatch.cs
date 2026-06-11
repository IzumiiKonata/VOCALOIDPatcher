using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using VOCALOIDPatcher.Translation;
using VOCALOIDPatcher.Utils;
using Yamaha.VOCALOID;
using Yamaha.VOCALOID.MusicalEditor;
using Yamaha.VOCALOID.VSM;
using UpdateViewTypeFlag = Yamaha.VOCALOID.MusicalEditor.UpdateViewTypeFlag;

namespace VOCALOIDPatcher.Patch.Patches;

public class SkipUnchangedPartRedrawPatch : PatchBase
{
    public override string PatchName        => "SkipUnchangedPartRedrawPatch";
    public override Type   TargetClass      => typeof(PianorollView);
    public override string TargetMethodName => "UpdateView";

    public override Type[] ArgumentTypes => new[]
    {
        typeof(object),
        typeof(UpdateViewTypeFlag),
        typeof(UpdateObserverNotifyEventArgs),
        typeof(object)
    };

    private static readonly MethodInfo? MDrawNoteInside =
        AccessTools.Method(typeof(PianorollView), "DrawNoteInsideActiveTrack", new[] { typeof(MusicalEditorViewModel) });

    private static readonly MethodInfo? MDrawRenderedWave =
        AccessTools.Method(typeof(PianorollView), "DrawRenderedWaveCanvas", new[] { typeof(MusicalEditorViewModel) });

    private static readonly MethodInfo? MDrawVibrato =
        AccessTools.Method(typeof(PianorollView), "DrawVibratoPitchCurveCanvas", Type.EmptyTypes);

    private static readonly MethodInfo? MDrawPitchBend =
        AccessTools.Method(typeof(PianorollView), "DrawPitchBendPitchCurveCanvas", Type.EmptyTypes);

    private static readonly MethodInfo? MDrawAmplitude =
        AccessTools.Method(typeof(PianorollView), "DrawAmplitudeCurveCanvas", new[] { typeof(MusicalEditorViewModel) });

    private static readonly MethodInfo? MDrawPartName =
        AccessTools.Method(typeof(PianorollView), "DrawPartName", new[] { typeof(MusicalEditorViewModel) });

    private static readonly MethodInfo? MUpdateOutsideLayer =
        AccessTools.Method(typeof(PianorollView), "UpdateOutsideActivePartLayer", new[] { typeof(MusicalEditorViewModel) });

    private static readonly bool MethodsResolved =
        MDrawNoteInside != null && MDrawRenderedWave != null && MDrawVibrato != null
        && MDrawPitchBend != null && MDrawAmplitude != null && MDrawPartName != null
        && MUpdateOutsideLayer != null;

    private sealed class TrackBox
    {
        public WIVSMMidiTrack? Track;
    }

    private static readonly ConditionalWeakTable<PianorollView, TrackBox> LastTrack = new();

    [HarmonyPrefix]
    private static bool Prefix(PianorollView __instance, UpdateViewTypeFlag typeFlags)
    {
        if (!Settings.SkipUnchangedPartRedraw || !MethodsResolved)
            return true;

        if (typeFlags != UpdateViewTypeFlag.ActivePartChanged)
            return true;

        try
        {
            if (__instance.DataContext is not MusicalEditorViewModel vm)
                return true;

            var box = LastTrack.GetOrCreateValue(__instance);
            var current = vm.ActiveTrack;
            bool sameTrack = current != null && box.Track != null && current.Equals(box.Track);
            box.Track = current;

            if (!sameTrack)
                return true;

            var args = new object?[] { vm };

            if (__instance.EditMode == EditModeME.Emotion)
                MDrawNoteInside!.Invoke(__instance, args);

            MDrawRenderedWave!.Invoke(__instance, args);
            MDrawVibrato!.Invoke(__instance, null);
            MDrawPitchBend!.Invoke(__instance, null);
            MDrawAmplitude!.Invoke(__instance, args);
            MDrawPartName!.Invoke(__instance, args);
            MUpdateOutsideLayer!.Invoke(__instance, args);
            vm.UpdateViewport();

            return false;
        }
        catch (Exception e)
        {
            Debug.Print(TranslationManager.Tr("VOCALOIDPatcher_Debug_SkipPartRedraw_Failed", e.Message));
            return true;
        }
    }
}
