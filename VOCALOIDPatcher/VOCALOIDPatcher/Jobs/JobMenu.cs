using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VOCALOIDPatcher.Patch.Patches;
using VOCALOIDPatcher.Translation;
using VOCALOIDPatcher.UI;
using VOCALOIDPatcher.Utils;

namespace VOCALOIDPatcher.Jobs;

public static class JobMenu
{
    private const string MarkerTag = "VOCALOIDPatcher_Job";

    private static readonly List<(MenuItem Item, string Key)> Localizers = new();
    private static bool _languageHooked;

    public static void Install()
    {
        try
        {
            var menu = ReflectionUtils.GetMainMenu();
            var jobMenu = FindJobMenu(menu);
            if (jobMenu == null)
            {
                Debug.Print(TranslationManager.Tr("VOCALOIDPatcher_Debug_JobMenu_MenuNotFound"));
                return;
            }

            if (jobMenu.Items.OfType<MenuItem>().Any(m => m.Tag as string == MarkerTag))
                return;

            jobMenu.Items.Add(new Separator());
            jobMenu.Items.Add(BuildItem("VOCALOIDPatcher_Job_Lyric_Header", ShowLyricDialog));
            jobMenu.Items.Add(BuildItem("VOCALOIDPatcher_Job_QuantizeLength_Header", ShowQuantizeDialog));
            jobMenu.Items.Add(BuildItem("VOCALOIDPatcher_Job_Harmony_Header", ShowHarmonyDialog));

            HookLanguage();
            RefreshHeaders();
        }
        catch (Exception e)
        {
            Debug.Print(TranslationManager.Tr("VOCALOIDPatcher_Debug_JobMenu_InstallFailed", e.Message));
        }
    }

    private static MenuItem BuildItem(string key, Action onClick)
    {
        var item = new MenuItem { Tag = MarkerTag };
        item.Click += (_, _) => onClick();
        WpfTranslationPatch.MarkUntranslatable(item);
        Localizers.Add((item, key));
        return item;
    }

    private static MenuItem? FindJobMenu(Menu menu)
    {
        foreach (var obj in menu.Items)
        {
            if (obj is not MenuItem candidate || !candidate.HasItems)
                continue;

            foreach (var child in candidate.Items)
                if (child is MenuItem childItem
                    && childItem.Command is RoutedUICommand command
                    && command.Name is "InsertLyricsCommand" or "NormalizeWaveCommand")
                    return candidate;
        }

        return null;
    }

    private static void HookLanguage()
    {
        if (_languageHooked)
            return;
        _languageHooked = true;
        TranslationManager.LanguageChanged += (_, _) => Application.Current?.Dispatcher.Invoke(RefreshHeaders);
    }

    private static void RefreshHeaders()
    {
        foreach (var (item, key) in Localizers)
            item.Header = TranslationManager.Tr(key) + "...";
    }


    private static void ShowLyricDialog()
    {
        var dialog = new JobDialog("VOCALOIDPatcher_Job_Lyric_Header");
        var box = dialog.AddTextBox("VOCALOIDPatcher_Job_Lyric_Syllable", "la");

        if (dialog.ShowForApply())
            JobTools.ApplyLyric(box.Text);
    }

    private static void ShowQuantizeDialog()
    {
        var dialog = new JobDialog("VOCALOIDPatcher_Job_QuantizeLength_Header");
        var labels = new[] { "1/1", "1/2", "1/4", "1/8", "1/16", "1/32" };
        var denoms = new[] { 1, 2, 4, 8, 16, 32 };
        var grid = dialog.AddCombo("VOCALOIDPatcher_Job_QuantizeLength_Grid", labels, 4);
        var strength = dialog.AddSlider("VOCALOIDPatcher_Job_QuantizeLength_Strength", 0, 100, 100);

        if (dialog.ShowForApply())
        {
            int denom = denoms[Math.Clamp(grid.SelectedIndex, 0, denoms.Length - 1)];
            int gridTicks = Yamaha.VOCALOID.Design.Sequence.resolution * 4 / denom;
            JobTools.ApplyQuantizeLength(gridTicks, strength.Value / 100.0);
        }
    }

    private static readonly (JobTools.HarmonyInterval Interval, string Key, bool Default)[] HarmonyOptions =
    {
        (JobTools.HarmonyInterval.ThirdUp, "VOCALOIDPatcher_Job_Harmony_ThirdUp", true),
        (JobTools.HarmonyInterval.FifthUp, "VOCALOIDPatcher_Job_Harmony_FifthUp", false),
        (JobTools.HarmonyInterval.SixthUp, "VOCALOIDPatcher_Job_Harmony_SixthUp", false),
        (JobTools.HarmonyInterval.FourthUp, "VOCALOIDPatcher_Job_Harmony_FourthUp", false),
        (JobTools.HarmonyInterval.ThirdDown, "VOCALOIDPatcher_Job_Harmony_ThirdDown", false),
        (JobTools.HarmonyInterval.OctaveUp, "VOCALOIDPatcher_Job_Harmony_OctaveUp", false),
        (JobTools.HarmonyInterval.OctaveDown, "VOCALOIDPatcher_Job_Harmony_OctaveDown", false)
    };

    private static void ShowHarmonyDialog()
    {
        var dialog = new JobDialog("VOCALOIDPatcher_Job_Harmony_Header");
        var roots = new[] { "C", "C#", "D", "Eb", "E", "F", "F#", "G", "G#", "A", "Bb", "B" };
        var root = dialog.AddCombo("VOCALOIDPatcher_Job_Harmony_Root", roots, 0);

        var trackLabels = new[]
        {
            TranslationManager.Tr("VOCALOIDPatcher_Job_Harmony_TrackExisting"),
            TranslationManager.Tr("VOCALOIDPatcher_Job_Harmony_TrackNew")
        };
        var trackMode = dialog.AddCombo("VOCALOIDPatcher_Job_Harmony_Track", trackLabels, 0);

        var boxes = HarmonyOptions
            .Select(o => (o.Interval, Box: dialog.AddCheckBox(o.Key, o.Default)))
            .ToList();

        if (dialog.ShowForApply())
        {
            var selected = boxes.Where(b => b.Box.IsChecked == true).Select(b => b.Interval).ToList();
            JobTools.ApplyHarmony(Math.Clamp(root.SelectedIndex, 0, 11), selected, trackMode.SelectedIndex == 1);
        }
    }
}
