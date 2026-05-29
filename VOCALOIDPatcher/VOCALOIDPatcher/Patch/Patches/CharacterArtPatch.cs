using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using VOCALOIDPatcher.Utils;
using Yamaha.VOCALOID.MusicalEditor;
using Yamaha.VOCALOID.VSM;
using UpdateViewTypeFlag = Yamaha.VOCALOID.MusicalEditor.UpdateViewTypeFlag;

namespace VOCALOIDPatcher.Patch.Patches;

public class CharacterArtPatch : PatchBase
{
    private const string OverlayTag = "VOCALOIDPatcher_CharacterArt";

    public override string PatchName        => "CharacterArtPatch";
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
            ApplyToView(view, typeFlags);
        }
        catch (Exception e)
        {
            Debug.Print($"立绘叠加失败: {e.Message}");
        }
    }

    private static void ApplyToView(PianorollView view, UpdateViewTypeFlag typeFlags)
    {
        var panel = GetRootPanel(view);
        if (panel == null)
            return;

        var image = FindOverlay(panel);

        if (!Settings.ShowCharacterArt)
        {
            if (image != null)
                image.Visibility = Visibility.Collapsed;
            return;
        }

        var justCreated = image == null;
        if (image == null)
        {
            image = CreateOverlay();
            panel.Children.Add(image);
        }

        image.Visibility = Visibility.Visible;

        if (justCreated || IsPartOrTrackChange(typeFlags))
            image.Source = LoadActiveArt(view);
    }

    private static bool IsPartOrTrackChange(UpdateViewTypeFlag f)
        => f == UpdateViewTypeFlag.ActivePartChanged
           || f == UpdateViewTypeFlag.ActiveTrackChanged
           || f == UpdateViewTypeFlag.ShowMusicalEditor
           || f == UpdateViewTypeFlag.SequenceChanged;

    private static Grid? GetRootPanel(PianorollView view)
    {
        if (AccessTools.Field(typeof(PianorollView), "xPanel")?.GetValue(view) is Grid grid)
            return grid;

        return (view as ContentControl)?.Content as Grid;
    }

    private static Image? FindOverlay(Grid panel)
        => panel.Children.OfType<Image>().FirstOrDefault(i => i.Tag as string == OverlayTag);

    private static Image CreateOverlay()
    {
        var image = new Image
        {
            Tag = OverlayTag,
            Width = 200,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 16, 16),
            Opacity = 0.9,
            IsHitTestVisible = false,
            SnapsToDevicePixels = true
        };
        Panel.SetZIndex(image, 60);
        return image;
    }

    private static BitmapImage? LoadActiveArt(PianorollView view)
    {
        try
        {
            if (view.DataContext is not MusicalEditorViewModel vm)
                return null;

            object? part = vm.ActivePart;
            if (part == null)
                return null;

            var voiceBank = GetVoiceBank(part);
            var path = voiceBank == null ? null : GetImagePath(voiceBank);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception e)
        {
            Debug.Print($"加载立绘失败: {e.Message}");
            return null;
        }
    }

    private static object? GetVoiceBank(object part)
    {
        var prop = part.GetType().GetProperty("VoiceBank");
        if (prop != null && prop.GetIndexParameters().Length == 0)
            return prop.GetValue(part);

        return AccessTools.Method("Yamaha.VOCALOID.WIVSMMidiPartExtension:VoiceBank")
            ?.Invoke(null, new[] { part });
    }

    private static string? GetImagePath(object voiceBank)
    {
        var extension = AccessTools.Method("Yamaha.VOCALOID.VoiceBankExtension:GetImagePath");
        if (extension != null)
            return extension.Invoke(null, new[] { voiceBank }) as string;

        return voiceBank.GetType().GetMethod("GetImagePath", Type.EmptyTypes)?.Invoke(voiceBank, null) as string;
    }
}
