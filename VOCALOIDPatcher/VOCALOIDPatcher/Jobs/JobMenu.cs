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

/**
 * 把带参数的音符工具(人性化 / 歌词替换 / 量化时值)注入编辑器菜单栏自带的 "Job(任务)" 菜单，
 * 和 VOCALOID4 的 Job Plugin 出现位置一致。点击菜单项弹参数对话框，确认后应用。
 */
public static class JobMenu
{
    private const string MarkerTag = "VOCALOIDPatcher_Job";

    private static readonly List<(MenuItem Item, string Key, string Fallback)> Localizers = new();
    private static bool _languageHooked;

    public static void Install()
    {
        try
        {
            var menu = ReflectionUtils.GetMainMenu();
            var jobMenu = FindJobMenu(menu);
            if (jobMenu == null)
            {
                Debug.Print("[JobMenu] 未找到菜单栏 Job 菜单");
                return;
            }

            if (jobMenu.Items.OfType<MenuItem>().Any(m => m.Tag as string == MarkerTag))
                return;

            jobMenu.Items.Add(new Separator());
            jobMenu.Items.Add(BuildItem("VOCALOIDPatcher_Job_Humanize_Header", "人性化", ShowHumanizeDialog));
            jobMenu.Items.Add(BuildItem("VOCALOIDPatcher_Job_Lyric_Header", "歌词替换", ShowLyricDialog));
            jobMenu.Items.Add(BuildItem("VOCALOIDPatcher_Job_QuantizeLength_Header", "量化时值", ShowQuantizeDialog));

            HookLanguage();
            RefreshHeaders();
        }
        catch (Exception e)
        {
            Debug.Print($"[JobMenu] 安装失败: {e.Message}");
        }
    }

    private static MenuItem BuildItem(string key, string fallback, Action onClick)
    {
        var item = new MenuItem { Tag = MarkerTag };
        item.Click += (_, _) => onClick();
        WpfTranslationPatch.MarkUntranslatable(item);
        Localizers.Add((item, key, fallback));
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
        foreach (var (item, key, fallback) in Localizers)
            item.Header = (TranslationManager.Get(key) ?? fallback) + "...";
    }

    private static void ShowHumanizeDialog()
    {
        var dialog = new JobDialog("VOCALOIDPatcher_Job_Humanize_Header", "人性化");
        var timing = dialog.AddSlider("VOCALOIDPatcher_Job_Humanize_Timing", "起始 (tick)", 0, 60, 15);
        var duration = dialog.AddSlider("VOCALOIDPatcher_Job_Humanize_Duration", "时值 (%)", 0, 30, 8);
        var velocity = dialog.AddSlider("VOCALOIDPatcher_Job_Humanize_Velocity", "力度", 0, 30, 8);

        if (dialog.ShowForApply())
            JobTools.ApplyHumanize((int)timing.Value, duration.Value, (int)velocity.Value);
    }

    private static void ShowLyricDialog()
    {
        var dialog = new JobDialog("VOCALOIDPatcher_Job_Lyric_Header", "歌词替换");
        var box = dialog.AddTextBox("VOCALOIDPatcher_Job_Lyric_Syllable", "音节", "la");

        if (dialog.ShowForApply())
            JobTools.ApplyLyric(box.Text);
    }

    private static void ShowQuantizeDialog()
    {
        var dialog = new JobDialog("VOCALOIDPatcher_Job_QuantizeLength_Header", "量化时值");
        var labels = new[] { "1/1", "1/2", "1/4", "1/8", "1/16", "1/32" };
        var denoms = new[] { 1, 2, 4, 8, 16, 32 };
        var grid = dialog.AddCombo("VOCALOIDPatcher_Job_QuantizeLength_Grid", "网格", labels, 4);
        var strength = dialog.AddSlider("VOCALOIDPatcher_Job_QuantizeLength_Strength", "强度 (%)", 0, 100, 100);

        if (dialog.ShowForApply())
        {
            int denom = denoms[Math.Clamp(grid.SelectedIndex, 0, denoms.Length - 1)];
            int gridTicks = Yamaha.VOCALOID.Design.Sequence.resolution * 4 / denom;
            JobTools.ApplyQuantizeLength(gridTicks, strength.Value / 100.0);
        }
    }
}
