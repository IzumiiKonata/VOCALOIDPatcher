using System;
using System.Linq;
using System.Windows.Controls;
using HarmonyLib;
using VOCALOIDPatcher.Jobs;
using VOCALOIDPatcher.Translation;
using VOCALOIDPatcher.Utils;
using Yamaha.VOCALOID.MusicalEditor;
using Yamaha.VOCALOID.VSM;
using UpdateViewTypeFlag = Yamaha.VOCALOID.MusicalEditor.UpdateViewTypeFlag;

namespace VOCALOIDPatcher.Patch.Patches;

public class SwingMenuPatch : PatchBase
{
    private const string MarkerTag = "VOCALOIDPatcher_SwingMenu";

    private static readonly (string Key, string Fallback, int Subdivision, double Ratio)[] Presets =
    {
        ("VOCALOIDPatcher_Job_Swing_8", "8 分摇摆", 8, 60.0),
        ("VOCALOIDPatcher_Job_Swing_8_Shuffle", "8 分 Shuffle", 8, 66.667),
        ("VOCALOIDPatcher_Job_Swing_16", "16 分摇摆", 16, 60.0),
        ("VOCALOIDPatcher_Job_Swing_16_Shuffle", "16 分 Shuffle", 16, 66.667),
    };

    public override string PatchName        => "SwingMenuPatch";
    public override Type   TargetClass      => typeof(PianorollView);
    public override string TargetMethodName => "UpdateView";

    public override Type[] ArgumentTypes => new[]
    {
        typeof(object),
        typeof(UpdateViewTypeFlag),
        typeof(UpdateObserverNotifyEventArgs),
        typeof(object)
    };

    [HarmonyPostfix]
    private static void Postfix(object __instance, UpdateViewTypeFlag typeFlags)
    {
        if (__instance is not PianorollView view)
            return;

        try
        {
            EnsureSwingMenu(view);
        }
        catch (Exception e)
        {
            Debug.Print($"[SwingMenu] 异常: {e.Message}");
        }
    }

    private static void EnsureSwingMenu(PianorollView view)
    {
        if (AccessTools.Field(typeof(PianorollView), "xContextMenu")?.GetValue(view) is not ContextMenu contextMenu)
            return;

        var parent = FindJobMenu(contextMenu.Items)?.Items ?? contextMenu.Items;

        var swing = parent.OfType<MenuItem>().FirstOrDefault(m => (m.Tag as string) == MarkerTag);
        if (swing == null)
        {
            swing = new MenuItem { Tag = MarkerTag };
            foreach (var preset in Presets)
            {
                var captured = preset;
                var item = new MenuItem();
                item.Click += (_, _) => JobTools.ApplySwing(captured.Subdivision, captured.Ratio);
                swing.Items.Add(item);
            }
            WpfTranslationPatch.MarkUntranslatable(swing);
            parent.Add(swing);
        }

        swing.Header = T("VOCALOIDPatcher_Job_Swing_Header", "摇摆");
        for (int i = 0; i < Presets.Length && i < swing.Items.Count; i++)
            if (swing.Items[i] is MenuItem item)
                item.Header = T(Presets[i].Key, Presets[i].Fallback);
    }

    private static MenuItem? FindJobMenu(ItemCollection items)
    {
        foreach (var obj in items)
        {
            if (obj is not MenuItem menu || !menu.HasItems)
                continue;

            foreach (var child in menu.Items)
                if (child is MenuItem childItem
                    && childItem.Command?.GetType().Name is "StaccatoNoteCommand" or "DivideNoteCommand")
                    return menu;
        }

        return null;
    }

    private static string T(string key, string fallback) => TranslationManager.Get(key) ?? fallback;
}
