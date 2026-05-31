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
            jobMenu.Items.Add(BuildItem("VOCALOIDPatcher_Job_DynEg_Header", "动态包络", ShowDynEgDialog));
            jobMenu.Items.Add(BuildItem("VOCALOIDPatcher_Job_Harmony_Header", "生成和声", ShowHarmonyDialog));

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

        var singerLabels = new[]
        {
            T("VOCALOIDPatcher_Job_Humanize_Singer_VocalGroup", "流行和声"),
            T("VOCALOIDPatcher_Job_Humanize_Singer_Choir", "合唱"),
            T("VOCALOIDPatcher_Job_Humanize_Singer_Gospel", "福音"),
            T("VOCALOIDPatcher_Job_Humanize_Singer_Chanting", "诵经"),
            T("VOCALOIDPatcher_Job_Humanize_Singer_Children", "儿童"),
            T("VOCALOIDPatcher_Job_Humanize_Singer_Overdub", "叠录"),
            T("VOCALOIDPatcher_Job_Humanize_Singer_Humanize", "纯人性化")
        };
        var singer = dialog.AddCombo("VOCALOIDPatcher_Job_Humanize_Singer", "歌唱类型", singerLabels, 0);

        var skillLabels = new[]
        {
            T("VOCALOIDPatcher_Job_Humanize_Skill_Novice", "初级"),
            T("VOCALOIDPatcher_Job_Humanize_Skill_Regular", "标准"),
            T("VOCALOIDPatcher_Job_Humanize_Skill_Advanced", "上级"),
            T("VOCALOIDPatcher_Job_Humanize_Skill_Skillful", "熟练")
        };
        var skill = dialog.AddCombo("VOCALOIDPatcher_Job_Humanize_Skill", "技能等级", skillLabels, 1);

        var noVibrato = dialog.AddCheckBox("VOCALOIDPatcher_Job_Humanize_NoVibrato", "关闭颤音", false);

        if (dialog.ShowForApply())
            JobTools.ApplyHumanize(singer.SelectedIndex, skill.SelectedIndex + 1, noVibrato.IsChecked == true);
    }

    private static string T(string key, string fallback) => TranslationManager.Get(key) ?? fallback;

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

    private static void ShowDynEgDialog()
    {
        var dialog = new JobDialog("VOCALOIDPatcher_Job_DynEg_Header", "动态包络");
        var initial = dialog.AddSlider("VOCALOIDPatcher_Job_DynEg_Initial", "起始音量", 0, 127, 64);
        var attackLevel = dialog.AddSlider("VOCALOIDPatcher_Job_DynEg_AttackLevel", "最大音量", 0, 127, 96);
        var attackTime = dialog.AddSlider("VOCALOIDPatcher_Job_DynEg_AttackTime", "起音 (tick)", 0, 240, 30);
        var holdTime = dialog.AddSlider("VOCALOIDPatcher_Job_DynEg_HoldTime", "保持 (tick)", 0, 240, 0);
        var decayTime = dialog.AddSlider("VOCALOIDPatcher_Job_DynEg_DecayTime", "衰减 (tick)", 0, 240, 30);
        var sustainLevel = dialog.AddSlider("VOCALOIDPatcher_Job_DynEg_SustainLevel", "持续音量", 0, 127, 64);
        var fadeLevel = dialog.AddSlider("VOCALOIDPatcher_Job_DynEg_FadeLevel", "渐弱音量", 0, 127, 56);
        var releaseLevel = dialog.AddSlider("VOCALOIDPatcher_Job_DynEg_ReleaseLevel", "最小音量", 0, 127, 32);
        var releaseTime = dialog.AddSlider("VOCALOIDPatcher_Job_DynEg_ReleaseTime", "释音 (tick)", 0, 240, 30);

        if (dialog.ShowForApply())
            JobTools.ApplyDynEnvelope((int)initial.Value, (int)attackLevel.Value, (int)attackTime.Value,
                (int)holdTime.Value, (int)decayTime.Value, (int)sustainLevel.Value, (int)fadeLevel.Value,
                (int)releaseLevel.Value, (int)releaseTime.Value);
    }

    private static readonly (JobTools.HarmonyInterval Interval, string Key, string Fallback, bool Default)[] HarmonyOptions =
    {
        (JobTools.HarmonyInterval.ThirdUp, "VOCALOIDPatcher_Job_Harmony_ThirdUp", "上三度", true),
        (JobTools.HarmonyInterval.FifthUp, "VOCALOIDPatcher_Job_Harmony_FifthUp", "上五度", false),
        (JobTools.HarmonyInterval.SixthUp, "VOCALOIDPatcher_Job_Harmony_SixthUp", "上六度", false),
        (JobTools.HarmonyInterval.FourthUp, "VOCALOIDPatcher_Job_Harmony_FourthUp", "上四度", false),
        (JobTools.HarmonyInterval.ThirdDown, "VOCALOIDPatcher_Job_Harmony_ThirdDown", "下三度", false),
        (JobTools.HarmonyInterval.OctaveUp, "VOCALOIDPatcher_Job_Harmony_OctaveUp", "上八度", false),
        (JobTools.HarmonyInterval.OctaveDown, "VOCALOIDPatcher_Job_Harmony_OctaveDown", "下八度", false)
    };

    private static void ShowHarmonyDialog()
    {
        var dialog = new JobDialog("VOCALOIDPatcher_Job_Harmony_Header", "生成和声");
        var roots = new[] { "C", "C#", "D", "Eb", "E", "F", "F#", "G", "G#", "A", "Bb", "B" };
        var root = dialog.AddCombo("VOCALOIDPatcher_Job_Harmony_Root", "调式根音", roots, 0);

        var trackLabels = new[]
        {
            TranslationManager.Get("VOCALOIDPatcher_Job_Harmony_TrackExisting") ?? "使用现有轨道",
            TranslationManager.Get("VOCALOIDPatcher_Job_Harmony_TrackNew") ?? "新建轨道"
        };
        var trackMode = dialog.AddCombo("VOCALOIDPatcher_Job_Harmony_Track", "目标轨道", trackLabels, 0);

        var boxes = HarmonyOptions
            .Select(o => (o.Interval, Box: dialog.AddCheckBox(o.Key, o.Fallback, o.Default)))
            .ToList();

        if (dialog.ShowForApply())
        {
            var selected = boxes.Where(b => b.Box.IsChecked == true).Select(b => b.Interval).ToList();
            JobTools.ApplyHarmony(Math.Clamp(root.SelectedIndex, 0, 11), selected, trackMode.SelectedIndex == 1);
        }
    }
}
