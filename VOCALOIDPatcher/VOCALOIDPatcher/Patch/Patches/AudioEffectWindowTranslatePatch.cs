using System;
using System.Windows;
using HarmonyLib;
using VOCALOIDPatcher.Utils;
using Yamaha.VOCALOID.AudioEffect;

namespace VOCALOIDPatcher.Patch.Patches;

public class AudioEffectWindowTranslatePatch : PatchBase
{
    public override string PatchName => "AudioEffectWindowTranslatePatch";
    public override Type TargetClass => typeof(AudioEffectWindowViewModel);
    public override string TargetMethodName => "OnAudioEffectSelectorPropertyChanged";
    public override Type[] ArgumentTypes => new[] { typeof(DependencyPropertyChangedEventArgs) };

    [HarmonyPostfix]
    private static void Postfix(DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue == null)
            return;

        var window = ReflectionUtils.GetMainWindow()?.AudioEffectWindow;
        if (window != null)
            WpfTranslationPatch.RefreshAll(window);
    }
}
