using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Yamaha.VOCALOID;
using MessageBox = System.Windows.MessageBox;

namespace Microsoft.Xaml.Behaviors;

public static class PatcherEntry
{
    [ModuleInitializer]
    public static void Init()
    {
        MessageBox.Show("Nuck Figgers 有感觉吗");
        try
        {
            var harmony = new Harmony("VOCALOIDPatcher");

            var original = typeof(App).GetMethod("ValidateAuthorization", BindingFlags.NonPublic | BindingFlags.Static);
            var prefix = typeof(PatchValidation).GetMethod("Prefix");
        
            harmony.Patch(original, new HarmonyMethod(prefix));
        } catch(Exception e)
        {
            MessageBox.Show(e.Message);
        }
    }
}