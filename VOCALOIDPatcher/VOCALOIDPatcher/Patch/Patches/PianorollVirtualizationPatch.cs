using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using Yamaha.VOCALOID;
using Yamaha.VOCALOID.MusicalEditor;
using Yamaha.VOCALOID.VSM;

namespace VOCALOIDPatcher.Patch.Patches;

#if !NET6_0
internal static class PianorollVirtualization
{
    private const int MinimumNotes = 1000;

    internal sealed class State
    {
        public bool Enabled;
        public bool Rebuilding;
        public bool Bypass;
        public double Left;
        public double Right;
    }

    private static readonly ConditionalWeakTable<PianorollView, State> States = new();

    private static readonly MethodInfo? InsertNote =
        AccessTools.Method(typeof(PianorollView), "InsertNoteInsideActiveTrack",
            new[] { typeof(MusicalEditorViewModel), typeof(WIVSMNote) });

    private static readonly MethodInfo? InsertEmotionNote =
        AccessTools.Method(typeof(PianorollView), "InsertEmotionNoteInsideActiveTrack",
            new[] { typeof(MusicalEditorViewModel), typeof(WIVSMNote) });

    private static readonly MethodInfo? InsertLyricAndPhoneme =
        AccessTools.Method(typeof(PianorollView), "InsertLyricsAndPhoneme",
            new[] { typeof(MusicalEditorViewModel), typeof(WIVSMNote) });

    private static readonly MethodInfo? InsertLyric =
        AccessTools.Method(typeof(PianorollView), "InsertLyrics",
            new[] { typeof(MusicalEditorViewModel), typeof(WIVSMNote) });

    private static readonly MethodInfo? DrawNotes =
        AccessTools.Method(typeof(PianorollView), "DrawNoteInsideActiveTrack",
            new[] { typeof(MusicalEditorViewModel) });

    private static readonly MethodInfo? DrawLyrics =
        AccessTools.Method(typeof(PianorollView), "DrawLyricCanvas",
            new[] { typeof(MusicalEditorViewModel) });

    private static readonly AccessTools.FieldRef<PianorollView, FastCanvas>? NoteCanvas =
        CreateCanvasRef("xNoteInsideActiveTrackCanvas");

    private static readonly AccessTools.FieldRef<PianorollView, FastCanvas>? LyricCanvas =
        CreateCanvasRef("xLyricCanvas");

    internal static bool CanRun =>
        InsertNote != null && InsertEmotionNote != null && InsertLyricAndPhoneme != null
        && InsertLyric != null && DrawNotes != null && DrawLyrics != null
        && NoteCanvas != null && LyricCanvas != null;

    internal static bool DrawVisibleNotes(PianorollView view, MusicalEditorViewModel vm)
    {
        if (!TryPrepare(view, vm, out var state, out long leftTick, out long rightTick))
            return false;

        NoteCanvas!(view).ClearElement();
        var insert = vm.EditorMode.Mode == EditModeME.Emotion ? InsertEmotionNote : InsertNote;
        var args = new object?[2];
        args[0] = vm;

        foreach (var part in vm.Sequence!.MidiPartsInsideActiveTrack)
        {
            if (part.AbsEndTick.Value < leftTick || part.AbsPosTick.Value > rightTick)
                continue;

            foreach (var note in part.NotesInPart)
            {
                if (note.AbsEndTick.Value < leftTick || note.AbsPosTick.Value > rightTick)
                    continue;

                args[1] = note;
                insert!.Invoke(view, args);
            }
        }

        state.Enabled = true;
        return true;
    }

    internal static bool DrawVisibleLyrics(PianorollView view, MusicalEditorViewModel vm)
    {
        if (!TryPrepare(view, vm, out var state, out long leftTick, out long rightTick))
            return false;

        var track = vm.ActiveTrack;
        if (track == null)
            return false;

        LyricCanvas!(view).ClearElement();
        var mode = vm.EditorMode.Mode;
        var insert = mode == EditModeME.Emotion || mode == EditModeME.PhonemeTiming
            ? InsertLyric
            : InsertLyricAndPhoneme;
        var args = new object?[2];
        args[0] = vm;

        foreach (var part in track.MidiParts)
        {
            if (part.AbsEndTick.Value < leftTick || part.AbsPosTick.Value > rightTick)
                continue;

            foreach (var note in part.NotesInPart)
            {
                if (note.AbsEndTick.Value < leftTick || note.AbsPosTick.Value > rightTick)
                    continue;

                args[1] = note;
                insert!.Invoke(view, args);
            }
        }

        state.Enabled = true;
        return true;
    }

    internal static void EnsureWindow(MusicalEditorViewModel vm)
    {
        var view = vm.PianorollView;
        var viewer = vm.PianorollViewer;
        if (view == null || viewer == null || !States.TryGetValue(view, out var state)
            || !state.Enabled || state.Rebuilding)
            return;

        double visibleLeft = viewer.HorizontalOffset;
        double visibleRight = visibleLeft + viewer.ViewportWidth;
        double margin = viewer.ViewportWidth * 0.25;
        bool leftInside = state.Left <= 0.0 || visibleLeft >= state.Left + margin;
        if (leftInside && visibleRight <= state.Right - margin)
            return;

        state.Rebuilding = true;
        try
        {
            var args = new object?[] { vm };
            DrawNotes!.Invoke(view, args);
            DrawLyrics!.Invoke(view, args);
        }
        catch
        {
            state.Enabled = false;
            state.Bypass = true;
            var args = new object?[] { vm };
            DrawNotes!.Invoke(view, args);
            DrawLyrics!.Invoke(view, args);
        }
        finally
        {
            state.Bypass = false;
            state.Rebuilding = false;
        }
    }

    private static bool TryPrepare(
        PianorollView view,
        MusicalEditorViewModel vm,
        out State state,
        out long leftTick,
        out long rightTick)
    {
        state = States.GetOrCreateValue(view);
        leftTick = 0;
        rightTick = 0;

        if (!Settings.FastProjectLoad || !CanRun || state.Bypass
            || vm.Sequence == null || vm.PianorollViewer == null || vm.WidthPerTick <= 0.0)
            return false;

        ulong noteCount = 0;
        foreach (var part in vm.Sequence.MidiPartsInsideActiveTrack)
        {
            noteCount += part.NumNotes;
            if (noteCount >= MinimumNotes)
                break;
        }

        if (noteCount < MinimumNotes)
        {
            state.Enabled = false;
            return false;
        }

        var viewer = vm.PianorollViewer;
        double width = Math.Max(1.0, viewer.ViewportWidth);
        state.Left = Math.Max(0.0, viewer.HorizontalOffset - width);
        state.Right = viewer.HorizontalOffset + width * 2.0;
        leftTick = Math.Max(0L, (long)Math.Floor(state.Left / vm.WidthPerTick));
        rightTick = Math.Max(leftTick, (long)Math.Ceiling(state.Right / vm.WidthPerTick));
        return true;
    }

    private static AccessTools.FieldRef<PianorollView, FastCanvas>? CreateCanvasRef(string name)
    {
        try
        {
            return AccessTools.FieldRefAccess<PianorollView, FastCanvas>(name);
        }
        catch
        {
            return null;
        }
    }
}

public class VisiblePianorollNotesPatch : PatchBase
{
    public override string PatchName        => "VisiblePianorollNotesPatch";
    public override Type   TargetClass      => typeof(PianorollView);
    public override string TargetMethodName => "DrawNoteInsideActiveTrack";
    public override Type[] ArgumentTypes    => new[] { typeof(MusicalEditorViewModel) };

    [HarmonyPrefix]
    private static bool Prefix(PianorollView __instance, MusicalEditorViewModel vm) =>
        !PianorollVirtualization.DrawVisibleNotes(__instance, vm);
}

public class VisiblePianorollLyricsPatch : PatchBase
{
    public override string PatchName        => "VisiblePianorollLyricsPatch";
    public override Type   TargetClass      => typeof(PianorollView);
    public override string TargetMethodName => "DrawLyricCanvas";
    public override Type[] ArgumentTypes    => new[] { typeof(MusicalEditorViewModel) };

    [HarmonyPrefix]
    private static bool Prefix(PianorollView __instance, MusicalEditorViewModel vm) =>
        !PianorollVirtualization.DrawVisibleLyrics(__instance, vm);
}

public class PianorollVirtualizationViewportPatch : PatchBase
{
    public override string PatchName        => "PianorollVirtualizationViewportPatch";
    public override Type   TargetClass      => typeof(MusicalEditorDivision);
    public override string TargetMethodName => "UpdateViewport";

    private static readonly AccessTools.FieldRef<MusicalEditorDivision, MusicalEditorViewModel>? ViewModel =
        CreateViewModelRef();

    [HarmonyPrefix]
    private static void Prefix(MusicalEditorDivision __instance)
    {
        if (Settings.FastProjectLoad && ViewModel != null)
            PianorollVirtualization.EnsureWindow(ViewModel(__instance));
    }

    private static AccessTools.FieldRef<MusicalEditorDivision, MusicalEditorViewModel>? CreateViewModelRef()
    {
        try
        {
            return AccessTools.FieldRefAccess<MusicalEditorDivision, MusicalEditorViewModel>("_vm");
        }
        catch
        {
            return null;
        }
    }
}
#endif
