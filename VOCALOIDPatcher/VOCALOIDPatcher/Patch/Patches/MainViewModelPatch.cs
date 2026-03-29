using System.Windows.Media;
using HarmonyLib;
using VOCALOIDPatcher.Utils;
using Yamaha.VOCALOID;
using Yamaha.VOCALOID.AudioEffect;

namespace VOCALOIDPatcher.Patch.Patches;

public class MainViewModelPatch
{

    public class ShowAudioEffectWindowPatch : PatchBase
    {
        public override string PatchName => "ShowAudioEffectWindowPatch";
        public override Type TargetClass => typeof(MainViewModel);
        public override string TargetMethodName => nameof(MainViewModel.ShowAudioEffectWindow);
        public override Type[]? ArgumentTypes => [ typeof(object) ];

        [HarmonyPostfix]
        static void Postfix(object? selector)
        {
            var mainWindow = ReflectionUtils.GetMainWindow();
            var audioEffectWindow = mainWindow.AudioEffectWindow;
        
            if (audioEffectWindow != null)
            {
                WPFTranslationPatch.TranslateTextBox = true;
                WPFTranslationPatch.RefreshAll(audioEffectWindow);
                WPFTranslationPatch.TranslateTextBox = false;
            }
        }
        
    }
    
}