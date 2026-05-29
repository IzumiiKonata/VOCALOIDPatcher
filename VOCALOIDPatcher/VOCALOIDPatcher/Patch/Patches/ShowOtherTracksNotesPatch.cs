using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using VOCALOIDPatcher.Utils;
using Yamaha.VOCALOID;
using Yamaha.VOCALOID.MusicalEditor;
using Yamaha.VOCALOID.VSM;

namespace VOCALOIDPatcher.Patch.Patches;

public class ShowOtherTracksNotesPatch : PatchBase
{
    public override string PatchName        => "ShowOtherTracksNotesPatch";
    public override Type   TargetClass      => typeof(Sequence);
    public override string TargetMethodName => "get_SelectedMidiPartsOutsideActiveTrack";

    [HarmonyPostfix]
    private static void Postfix(Sequence __instance, ref List<WIVSMMidiPart> __result)
    {
        if (!Settings.ShowOtherTracksNotes)
            return;

        try
        {
            var vsm = __instance.VSMSequence;
            if (vsm == null)
                return;

            var active = __instance.ActiveTrack;
            var skipMuted = Settings.ShowOtherTracksSkipMuted;

            var parts = new List<WIVSMMidiPart>();
            foreach (var track in vsm.MidiTracks)
            {
                if (active != null && track.Equals(active))
                    continue;

                if (skipMuted && track.IsMute)
                    continue;

                parts.AddRange(track.MidiParts);
            }

            __result = parts;
        }
        catch (Exception e)
        {
            // getter 在渲染期被调用，绝不能向编辑器抛异常；出错时保留原结果。
            Debug.Print($"收集其他轨道音符失败: {e.Message}");
        }
    }

    public static void RefreshPianoroll()
    {
        try
        {
            var updateView = AccessTools.Method(typeof(PianorollView), "UpdateView");
            if (updateView == null)
                return;

            var flags = new[]
            {
                Yamaha.VOCALOID.MusicalEditor.UpdateViewTypeFlag.NoteChanged,
                Yamaha.VOCALOID.MusicalEditor.UpdateViewTypeFlag.PartSelectionChanged
            };

            foreach (Window window in Application.Current.Windows)
            {
                foreach (var view in FindVisualChildren<PianorollView>(window))
                {
                    foreach (var flag in flags)
                        updateView.Invoke(view, new object?[] { view, flag, null, null });
                }
            }
        }
        catch (Exception e)
        {
            Debug.Print($"刷新钢琴窗失败: {e.Message}");
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject? root) where T : DependencyObject
    {
        if (root is not (Visual or Visual3D))
            yield break;

        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);

            if (child is T match)
                yield return match;

            foreach (var descendant in FindVisualChildren<T>(child))
                yield return descendant;
        }
    }
}
