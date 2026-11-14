namespace VOCALOIDPatcher.Patch.Patches;

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using HarmonyLib;
using VOCALOIDPatcher.Translation;
using VOCALOIDPatcher.Utils;
using Yamaha.VOCALOID.Controls.IconSelector;
using Yamaha.VOCALOID.MusicalEditor;

public class AttackReleaseEffectTabTranslationPatch : PatchBase
{
    private static readonly List<WeakReference<DependencyObject>> HeaderRoots = new();

    private static bool _languageHooked;

    public override string PatchName => "AttackReleaseEffectTabTranslationPatch";

    public override Type TargetClass => typeof(AttackReleaseEffectControl);

    public override string TargetMethodName => "SetupIconSelector";

    [HarmonyPostfix]
    private static void Postfix(AttackReleaseEffectControl __instance)
    {
        try
        {
            if (AccessTools.Field(typeof(AttackReleaseEffectControl), "xTabbedIconSelector")
                    ?.GetValue(__instance) is not TabbedIconSelector selector)
                return;

            var tabControl = selector.InternalTabControl;
            if (tabControl == null)
                return;

            EnsureLanguageHook();

            foreach (var item in tabControl.Items)
            {
                if (item is TabItem { Header: DependencyObject header })
                {
                    Remember(header);
                    Translate(header);
                }
            }
        }
        catch (Exception e)
        {
            Debug.Print($"AttackReleaseEffectTabTranslationPatch failed: {e.Message}");
        }
    }

    private static void Translate(DependencyObject header)
    {
        WpfTranslationPatch.RefreshAll(header);

        if (header is not FrameworkElement element || element.Dispatcher.HasShutdownStarted)
            return;

        element.Dispatcher.BeginInvoke(
            new Action(() => WpfTranslationPatch.RefreshAll(element)),
            DispatcherPriority.Loaded);
    }

    private static void Remember(DependencyObject header)
    {
        lock (HeaderRoots)
        {
            HeaderRoots.RemoveAll(reference => !reference.TryGetTarget(out _));

            foreach (var reference in HeaderRoots)
            {
                if (reference.TryGetTarget(out var target) && ReferenceEquals(target, header))
                    return;
            }

            HeaderRoots.Add(new WeakReference<DependencyObject>(header));
        }
    }

    private static void EnsureLanguageHook()
    {
        if (_languageHooked)
            return;

        _languageHooked = true;
        TranslationManager.LanguageChanged += (_, _) =>
        {
            Application.Current?.Dispatcher.BeginInvoke(
                new Action(RetranslateHeaders),
                DispatcherPriority.Background);
        };
    }

    private static void RetranslateHeaders()
    {
        foreach (var header in SnapshotHeaders())
            WpfTranslationPatch.RefreshAll(header);
    }

    private static List<DependencyObject> SnapshotHeaders()
    {
        var headers = new List<DependencyObject>();

        lock (HeaderRoots)
        {
            HeaderRoots.RemoveAll(reference => !reference.TryGetTarget(out _));

            foreach (var reference in HeaderRoots)
            {
                if (reference.TryGetTarget(out var target))
                    headers.Add(target);
            }
        }

        return headers;
    }
}
