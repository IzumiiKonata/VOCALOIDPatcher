using System;
using System.Reflection;
using System.Windows;
using HarmonyLib;
using Yamaha.VOCALOID.MusicalEditor;

namespace VOCALOIDPatcher.Patch.Patches;

public class CursorNoteNameTranslationFixPatch : PatchBase
{
    public override string PatchName        => "CursorNoteNameTranslationFixPatch";
    public override Type   TargetClass      => typeof(PianorollView);
    public override string TargetMethodName => "UpdateMouseCursorNoteNumber";

    public override Type[] ArgumentTypes => new[]
    {
        typeof(double), typeof(System.Windows.Point), typeof(bool), typeof(bool)
    };

    private static readonly FieldInfo? LabelField =
        AccessTools.Field(typeof(PianorollView), "xMouseCursorNoteNumber");

    [HarmonyPostfix]
    private static void Postfix(PianorollView __instance)
    {
        if (LabelField?.GetValue(__instance) is not DependencyObject label)
            return;

        if (WpfTranslationPatch.Untranslatable.TryGetValue(label, out _))
            return;

        WpfTranslationPatch.Untranslatable.Add(label, label);
        WpfTranslationPatch.MarkUntranslatable(label);
    }
}
