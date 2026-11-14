namespace VOCALOIDPatcher.Patch.Patches;

using System;
using System.Windows.Controls;
using HarmonyLib;

public class ComboBoxTranslationPatch : PatchBase
{
    public override string PatchName => "ComboBoxTranslationPatch";

    public override Type TargetClass => typeof(ComboBox);

    public override string TargetMethodName => "UpdateSelectionBoxItem";

    [HarmonyPostfix]
    private static void Postfix(ComboBox __instance)
    {
        WpfTranslationPatch.RefreshComboBox(__instance);
    }
}
