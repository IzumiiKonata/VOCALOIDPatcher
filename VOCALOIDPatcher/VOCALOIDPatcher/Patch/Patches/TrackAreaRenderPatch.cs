using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using Yamaha.VOCALOID;
using Yamaha.VOCALOID.TrackEditor;
using Yamaha.VOCALOID.VSM;
using UpdateViewTypeFlag = Yamaha.VOCALOID.TrackEditor.UpdateViewTypeFlag;

namespace VOCALOIDPatcher.Patch.Patches;

#if !NET6_0
internal static class TrackAreaRenderState
{
    public static readonly bool Enabled = Settings.OptimizeTrackRendering;

    internal struct NoteData
    {
        public long Rel;
        public long Dur;
        public int Number;
    }

    internal sealed class PartNotesCache
    {
        public readonly List<NoteData> Notes = new();
        public bool Dirty = true;
    }

    private static readonly ConditionalWeakTable<UIMidiPart, PartNotesCache> Caches = new();

    public static readonly AccessTools.FieldRef<MidiTrackControl, Canvas>? MidiCanvasRef =
        CreateFieldRef<MidiTrackControl, Canvas>("xMidiCanvas");

    public static readonly AccessTools.FieldRef<AudioTrackControl, Canvas>? AudioCanvasRef =
        CreateFieldRef<AudioTrackControl, Canvas>("xAudioCanvas");

    public static readonly AccessTools.FieldRef<AudioTrackControl, FastCanvas>? WaveCanvasRef =
        CreateFieldRef<AudioTrackControl, FastCanvas>("xWaveCanvas");

    public static readonly double PartYMargin = ReadPartYMargin();

    private static AccessTools.FieldRef<TOwner, TField>? CreateFieldRef<TOwner, TField>(string name)
        where TOwner : class
    {
        try
        {
            return AccessTools.FieldRefAccess<TOwner, TField>(name);
        }
        catch
        {
            return null;
        }
    }

    private static double ReadPartYMargin()
    {
        try
        {
            var field = AccessTools.Field(typeof(TrackControlBase), "partYMargin");
            if (field?.GetValue(null) is double margin)
                return margin;
        }
        catch
        {
        }

        return 1.0;
    }

    public static PartNotesCache GetCache(UIMidiPart part) => Caches.GetOrCreateValue(part);

    public static void MarkDirty(UIMidiPart uiPart)
    {
        GetCache(uiPart).Dirty = true;
        uiPart.InvalidateVisual();
    }

    public static void MarkDirty(MidiTrackControl control, WIVSMMidiPart? part)
    {
        if (MidiCanvasRef == null)
            return;

        foreach (var child in MidiCanvasRef(control).Children)
        {
            if (child is not UIMidiPart uiPart)
                continue;

            if (part == null || uiPart.Part?.Equals(part) == true)
                MarkDirty(uiPart);
        }
    }

    public static void Rebuild(PartNotesCache cache, WIVSMMidiPart part)
    {
        cache.Dirty = false;
        cache.Notes.Clear();

        foreach (var note in part.NotesEnumerator)
            cache.Notes.Add(new NoteData
            {
                Rel = note.RelPosTick.Value,
                Dur = note.DurationTick.Value,
                Number = note.NoteNumber
            });
    }
}

public class TrackNoteInsertPatch : PatchBase
{
    public override string PatchName        => "TrackNoteInsertPatch";
    public override Type   TargetClass      => typeof(MidiTrackControl);
    public override string TargetMethodName => "InsertNote";

    public override Type[] ArgumentTypes => new[]
    {
        typeof(TrackEditorViewModel), typeof(UIMidiPart), typeof(WIVSMNote)
    };

    [HarmonyPrefix]
    private static bool Prefix(UIMidiPart uiPart)
    {
        if (!TrackAreaRenderState.Enabled)
            return true;

        TrackAreaRenderState.MarkDirty(uiPart);
        return false;
    }
}

public class TrackNoteRemovePatch : PatchBase
{
    public override string PatchName        => "TrackNoteRemovePatch";
    public override Type   TargetClass      => typeof(MidiTrackControl);
    public override string TargetMethodName => "RemoveNote";

    public override Type[] ArgumentTypes => new[] { typeof(WIVSMNote) };

    [HarmonyPrefix]
    private static bool Prefix(MidiTrackControl __instance, WIVSMNote note)
    {
        if (!TrackAreaRenderState.Enabled)
            return true;

        TrackAreaRenderState.MarkDirty(__instance, note?.Parent);
        return false;
    }
}

public class TrackNoteDurationChangedPatch : PatchBase
{
    public override string PatchName        => "TrackNoteDurationChangedPatch";
    public override Type   TargetClass      => typeof(MidiTrackControl);
    public override string TargetMethodName => "RedrawDurationChangedNote";

    public override Type[] ArgumentTypes => new[] { typeof(WIVSMNote) };

    [HarmonyPrefix]
    private static bool Prefix(MidiTrackControl __instance, WIVSMNote note)
    {
        if (!TrackAreaRenderState.Enabled)
            return true;

        TrackAreaRenderState.MarkDirty(__instance, note?.Parent);
        return false;
    }
}

public class TrackNoteMovedPatch : PatchBase
{
    public override string PatchName        => "TrackNoteMovedPatch";
    public override Type   TargetClass      => typeof(MidiTrackControl);
    public override string TargetMethodName => "RedrawMovedNote";

    public override Type[] ArgumentTypes => new[] { typeof(WIVSMNote) };

    [HarmonyPrefix]
    private static bool Prefix(MidiTrackControl __instance, WIVSMNote note)
    {
        if (!TrackAreaRenderState.Enabled)
            return true;

        TrackAreaRenderState.MarkDirty(__instance, note?.Parent);
        return false;
    }
}

public class TrackNoteNumberChangedPatch : PatchBase
{
    public override string PatchName        => "TrackNoteNumberChangedPatch";
    public override Type   TargetClass      => typeof(MidiTrackControl);
    public override string TargetMethodName => "RedrawNumberChangedNote";

    public override Type[] ArgumentTypes => new[] { typeof(WIVSMNote) };

    [HarmonyPrefix]
    private static bool Prefix(MidiTrackControl __instance, WIVSMNote note)
    {
        if (!TrackAreaRenderState.Enabled)
            return true;

        TrackAreaRenderState.MarkDirty(__instance, note?.Parent);
        return false;
    }
}

public class TrackPartDurationChangedPatch : PatchBase
{
    public override string PatchName        => "TrackPartDurationChangedPatch";
    public override Type   TargetClass      => typeof(MidiTrackControl);
    public override string TargetMethodName => "RedrawDurationChangedPart";

    public override Type[] ArgumentTypes => new[]
    {
        typeof(TrackEditorViewModel), typeof(WIVSMMidiPart)
    };

    [HarmonyPostfix]
    private static void Postfix(MidiTrackControl __instance, WIVSMMidiPart midiPart)
    {
        if (!TrackAreaRenderState.Enabled)
            return;

        TrackAreaRenderState.MarkDirty(__instance, midiPart);
    }
}

public class UIMidiPartNotesRenderPatch : PatchBase
{
    public override string PatchName        => "UIMidiPartNotesRenderPatch";
    public override Type   TargetClass      => typeof(UIMidiPart);
    public override string TargetMethodName => "OnRender";

    public override Type[] ArgumentTypes => new[] { typeof(DrawingContext) };

    [HarmonyPostfix]
    private static void Postfix(UIMidiPart __instance, DrawingContext drawingContext)
    {
        if (!TrackAreaRenderState.Enabled || drawingContext == null)
            return;

        var part = __instance.Part;
        if (part == null)
            return;

        var cache = TrackAreaRenderState.GetCache(__instance);
        if (cache.Dirty)
            TrackAreaRenderState.Rebuild(cache, part);

        long partDuration = __instance.DurationTick.Value;
        if (cache.Notes.Count == 0 || partDuration <= 0)
            return;

        double width = __instance.ActualWidth;
        double height = __instance.ActualHeight;
        if (width <= 0.0 || height <= 0.0)
            return;

        var brush = part.Parent?.GetSelectedStrokeBrush();
        if (brush == null)
            return;

        double widthPerTick = width >= 9.0
            ? (width + 1.0) / partDuration
            : width / (0.9 * partDuration);
        double keyHeight = height / MainViewModel.MaxKeyCount;
        double noteHeight = keyHeight < 1.0 ? 1.0 : keyHeight;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            foreach (var note in cache.Notes)
            {
                if (note.Rel < 0 || note.Rel >= partDuration)
                    continue;

                double x = note.Rel * widthPerTick;
                double noteWidth = note.Dur * widthPerTick;
                double y = height - (note.Number - MainViewModel.MinKeyNumber) * keyHeight;

                ctx.BeginFigure(new Point(x, y), true, true);
                ctx.LineTo(new Point(x + noteWidth, y), false, false);
                ctx.LineTo(new Point(x + noteWidth, y + noteHeight), false, false);
                ctx.LineTo(new Point(x, y + noteHeight), false, false);
            }
        }

        geometry.Freeze();
        drawingContext.DrawGeometry(brush, null, geometry);
    }
}

public class AudioTrackHeightResizePatch : PatchBase
{
    public override string PatchName        => "AudioTrackHeightResizePatch";
    public override Type   TargetClass      => typeof(AudioTrackControl);
    public override string TargetMethodName => "UpdateView";

    public override Type[] ArgumentTypes => new[]
    {
        typeof(object),
        typeof(UpdateViewTypeFlag),
        typeof(UpdateObserverNotifyEventArgs),
        typeof(object)
    };

    [HarmonyPrefix]
    private static bool Prefix(AudioTrackControl __instance, UpdateViewTypeFlag typeFlags, object? addition)
    {
        if (!TrackAreaRenderState.Enabled)
            return true;

        if (typeFlags != UpdateViewTypeFlag.TrackLaneHeightChanged
            && typeFlags != UpdateViewTypeFlag.VerticalZoomed)
            return true;

        var track = __instance.Track;
        if (track == null || __instance.DataContext is not TrackEditorViewModel vm)
            return true;

        if (typeFlags == UpdateViewTypeFlag.TrackLaneHeightChanged
            && (addition is not WIVSMTrack changed || !track.Equals(changed)))
            return true;

        if (TrackAreaRenderState.AudioCanvasRef == null || TrackAreaRenderState.WaveCanvasRef == null)
            return true;

        double drawHeight = TrackEditorViewModel.NormalizeTrackHeight(track.HeightTrackLane * vm.SliderVerticalZoom);
        __instance.DrawHeight = drawHeight;

        double partHeight = drawHeight - 1.0 - TrackAreaRenderState.PartYMargin * 2.0;
        foreach (var child in TrackAreaRenderState.AudioCanvasRef(__instance).Children)
            if (child is UIAudioPart audioPart)
                audioPart.Height = partHeight;

        foreach (var virtualChild in TrackAreaRenderState.WaveCanvasRef(__instance).VirtualChildren)
            if (virtualChild is UIWave wave)
                wave.SetViewPositionHeight(drawHeight);

        return false;
    }
}
#endif
