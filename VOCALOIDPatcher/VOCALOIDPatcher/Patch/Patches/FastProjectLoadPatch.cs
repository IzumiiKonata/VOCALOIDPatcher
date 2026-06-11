using System;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using VOCALOIDPatcher.Translation;
using VOCALOIDPatcher.Utils;
using Yamaha.VOCALOID;
using Yamaha.VOCALOID.VSM;

namespace VOCALOIDPatcher.Patch.Patches;

#if !NET6_0
public class DeferAudioBufferLoadPatch : PatchBase
{
    public override string PatchName        => "DeferAudioBufferLoadPatch";
    public override Type   TargetClass      => typeof(AudioPartCacheManager);
    public override string TargetMethodName => "AddAllAudioBuffer";

    public override Type[] ArgumentTypes => new[] { typeof(WIVSMSequence) };

    private static bool _passthrough;

    [HarmonyPrefix]
    private static bool Prefix(AudioPartCacheManager __instance, WIVSMSequence vsmSequence)
    {
        if (_passthrough || !Settings.FastProjectLoad)
            return true;

        var app = Application.Current;
        if (app == null)
            return true;

        app.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                _passthrough = true;
                __instance.AddAllAudioBuffer(vsmSequence);
            }
            catch (Exception e)
            {
                Debug.Print(TranslationManager.Tr("VOCALOIDPatcher_Debug_FastProjectLoad_DeferWaveformFailed", e.Message));
            }
            finally
            {
                _passthrough = false;
            }

            ShowOtherTracksNotesPatch.RequestRefreshPianoroll();
        }), DispatcherPriority.Background);

        return false;
    }
}
#endif

public class DeferLoadAnalyticsPatch : PatchBase
{
    public override string PatchName        => "DeferLoadAnalyticsPatch";
    public override Type   TargetClass      => typeof(Sequence);
    public override string TargetMethodName => "SendSequenceLogOnLoad";

    private static readonly MethodInfo? Original =
        AccessTools.Method(typeof(Sequence), "SendSequenceLogOnLoad");

    private static bool _passthrough;

    [HarmonyPrefix]
    private static bool Prefix(Sequence __instance)
    {
        if (_passthrough || !Settings.FastProjectLoad || Original == null)
            return true;

        var app = Application.Current;
        if (app == null)
            return true;

        app.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                _passthrough = true;
                Original.Invoke(__instance, null);
            }
            catch (Exception e)
            {
                Debug.Print(TranslationManager.Tr("VOCALOIDPatcher_Debug_FastProjectLoad_DeferLogFailed", e.Message));
            }
            finally
            {
                _passthrough = false;
            }
        }), DispatcherPriority.Background);

        return false;
    }
}
