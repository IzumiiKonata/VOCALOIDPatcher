using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using HarmonyLib;
using Yamaha.VOCALOID.MusicalEditor;
using Yamaha.VOCALOID.TrackEditor;

namespace VOCALOIDPatcher.Patch.Patches;

#if !NET6_0
internal static class FrozenPenCache
{
    private static readonly ConditionalWeakTable<Brush, Dictionary<long, Pen>> Cache = new();

    public static Pen Get(Brush? brush, double thickness)
    {
        if (brush == null)
            return new Pen(null, thickness);

        var pens = Cache.GetOrCreateValue(brush);
        long key = BitConverter.DoubleToInt64Bits(thickness);
        if (pens.TryGetValue(key, out var pen))
            return pen;

        pen = new Pen(brush, thickness);
        if (pen.CanFreeze)
            pen.Freeze();
        pens[key] = pen;
        return pen;
    }

    public static IEnumerable<CodeInstruction> Replace(IEnumerable<CodeInstruction> instructions)
    {
        var constructor = AccessTools.Constructor(typeof(Pen), new[] { typeof(Brush), typeof(double) });
        var factory = AccessTools.Method(typeof(FrozenPenCache), nameof(Get));

        foreach (var instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Newobj && Equals(instruction.operand, constructor))
            {
                var replacement = new CodeInstruction(OpCodes.Call, factory);
                replacement.labels.AddRange(instruction.labels);
                replacement.blocks.AddRange(instruction.blocks);
                yield return replacement;
            }
            else
            {
                yield return instruction;
            }
        }
    }
}

public class PianorollSelectionPenCachePatch : PatchBase
{
    public override string PatchName        => "PianorollSelectionPenCachePatch";
    public override Type   TargetClass      => typeof(PianorollView);
    public override string TargetMethodName => "RedrawSelectChangedNotes";

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        FrozenPenCache.Replace(instructions);
}

public class MidiPartSelectionPenCachePatch : PatchBase
{
    public override string PatchName        => "MidiPartSelectionPenCachePatch";
    public override Type   TargetClass      => typeof(MidiTrackControl);
    public override string TargetMethodName => "RedrawSelectChangedColorChangedPart";
    public override Type[] ArgumentTypes    => new[] { typeof(Yamaha.VOCALOID.VSM.WIVSMMidiPart) };

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        FrozenPenCache.Replace(instructions);
}

public class AudioPartSelectionPenCachePatch : PatchBase
{
    public override string PatchName        => "AudioPartSelectionPenCachePatch";
    public override Type   TargetClass      => typeof(AudioTrackControl);
    public override string TargetMethodName => "RedrawSelectChangedColorChangedPart";
    public override Type[] ArgumentTypes    => new[] { typeof(Yamaha.VOCALOID.VSM.WIVSMAudioPart) };

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        FrozenPenCache.Replace(instructions);
}
#endif
