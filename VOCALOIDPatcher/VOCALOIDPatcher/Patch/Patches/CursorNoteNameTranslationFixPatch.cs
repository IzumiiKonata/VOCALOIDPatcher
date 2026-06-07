using System;
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

    [HarmonyPostfix]
    private static void Postfix(PianorollView __instance)
    {
        if (AccessTools.Field(typeof(PianorollView), "xMouseCursorNoteNumber")?.GetValue(__instance)
                is not DependencyObject label)
            return;

        if (WpfTranslationPatch.Untranslatable.Add(label))
            WpfTranslationPatch.MarkUntranslatable(label);
    }
}
