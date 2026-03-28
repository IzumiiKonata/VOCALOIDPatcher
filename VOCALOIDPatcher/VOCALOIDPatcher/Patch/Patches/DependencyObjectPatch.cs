using System.Windows;
using System.Windows.Controls;
using HarmonyLib;
using VOCALOIDPatcher.Utils;
using Yamaha.VOCALOID;

namespace VOCALOIDPatcher.Patch.Patches;

public class DependencyObjectPatch : PatchBase
{
    public override string PatchName => "DependencyObjectPatch";
    public override Type TargetClass => typeof(DependencyObject);
    public override string TargetMethodName => nameof(DependencyObject.SetValue);
    public override Type[] ArgumentTypes => [ typeof(DependencyProperty), typeof(object) ];
    
    [HarmonyPrefix]
    static void Prefix(object __instance, DependencyProperty dp, ref object value)
    {
        if ((__instance is PushButton || __instance is PushToggleButton) && value is Viewbox vb)
        {
            WPFTranslationPatch.TranslateTextBox = true;
            WPFTranslationPatch.TranslateElement(vb);
            WPFTranslationPatch.TranslateTextBox = false;
        }
    }
}