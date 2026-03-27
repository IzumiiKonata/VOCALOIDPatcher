using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HarmonyLib;
using VOCALOIDPatcher.Translation;
using Yamaha.VOCALOID;
using Yamaha.VOCALOID.Properties;

namespace VOCALOIDPatcher.Patch.Patches;

public class MenuItemsTranslationPatch : PatchBase
{
    public override string PatchName        => "MenuItemsTranslationPatch";
    public override Type   TargetClass      => typeof(MainWindow);
    public override string TargetMethodName => "InitializeCommandBindings";

    [HarmonyPrefix]
    static void Prefix()
    {
        DoTranslate();
        FixFilepathSeparator();
    }

    private static readonly Dictionary<object, string> OriginalMapping = new();

    private static string GetOriginal(object obj, string? original)
    {
        if (!OriginalMapping.ContainsKey(obj))
        {
            OriginalMapping[obj] = original;
        }
        
        if (original != null && ResourceManagerPatch.ReversedMap.TryGetValue(original, out var res))
            OriginalMapping[obj] = res;

        return OriginalMapping[obj];
    }

    private static string GetTranslatedText(string value)
    {
        var resourceKey = GetResourceKey(value);
        return string.IsNullOrEmpty(resourceKey)
            ? value
            : TranslationManager.Get(resourceKey) ?? value;
    }

    /**
     * 从已翻译的文本获取本地化键值，例如 "Open Recent..." -> "MainMenu_File_OpenRecent_Header"
     */
    private static string GetResourceKey(string value)
    {
        var type = typeof(Resources);
        
        var properties = type.GetProperties(
            BindingFlags.Public | 
            BindingFlags.NonPublic | 
            BindingFlags.Static | 
            BindingFlags.Instance);

        foreach (var prop in properties)
        {
            if (prop.PropertyType != typeof(string) || !prop.CanRead)
                continue;
            
            var name = prop.Name;
            ResourceManagerPatch.Skip = true;
            var v = prop.GetValue(null);
            ResourceManagerPatch.Skip = false;

            if (v is string str && value == str)
                return name;
        }

        return "";
    }

    private static void TranslateElement(object element)
    {
        switch (element)
        {
            case HeaderedItemsControl hic when hic.Header is string hicHeader:
                hic.Header = GetTranslatedText(GetOriginal(hic, hicHeader));
                TranslateCollection(hic.Items);
                break;
            case HeaderedContentControl hcc when hcc.Header is string hccHeader:
                hcc.Header = GetTranslatedText(GetOriginal(hcc, hccHeader));
                break;
            case ItemsControl ic:
                TranslateCollection(ic.Items);
                break;
            case ContentControl cc when cc.Content is string text:
                cc.Content = GetTranslatedText(GetOriginal(cc, text));
                break;
        }

        if (element is FrameworkElement fe)
        {
            RefreshContextMenu(fe);
            if (fe.ToolTip is string tip)
                fe.ToolTip = GetTranslatedText(GetOriginal(fe, tip));
        }
    }

    private static void TranslateCollection(ItemCollection collection)
    {
        foreach (var child in collection)
            TranslateElement(child);
    }

    private static void Refresh(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);

        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            TranslateElement(child);
            Refresh(child);
        }
    }

    private static void RefreshContextMenu(FrameworkElement element)
    {
        if (element.ContextMenu == null)
            return;

        foreach (var item in element.ContextMenu.Items)
            TranslateElement(item);
    }

    /**
     * 翻译所有控件。
     * 因为劫持的 dll 拉起要比窗口创建晚，所以还需要手动刷新一下。
     */
    public static void DoTranslate()
    {
        var mainMenu = Patcher.GetMainMenu();
        Refresh(mainMenu);
        RefreshContextMenu(mainMenu);
        TranslateCollection(mainMenu.Items);

        foreach (Window window in Application.Current.Windows)
        {
            Refresh(window);

            if (window is FrameworkElement fe)
                RefreshContextMenu(fe);
        }
    }

    /** 
     * 修复 "文件 -> 最近打开..." 中文件路径分隔符全都是 ￥ 的问题
     */
    private static void FixFilepathSeparator()
    {
        var xRecentFiles = Patcher.GetWindowField<MenuItem>("xRecentFiles");
        xRecentFiles.FontFamily = new FontFamily("Consolas");
    }

    public static PatchBase CreateContextMenuPatchFor<T>(Type type, string methodName)
        => new XContextMenuPatch<T>(type, methodName);

    private class XContextMenuPatch<T>(Type targetClass, string methodName) : PatchBase
    {
        public override string  PatchName        => $"XContextMenuPatch{targetClass.Name}Patch";
        public override Type    TargetClass      => targetClass;
        public override string  TargetMethodName => methodName;
        public override Type[]  ArgumentTypes    => [typeof(object), typeof(T)];

        [HarmonyPostfix]
        static void Postfix(object sender, T e)
        {
            var xContextMenu = (Control)sender;
            Refresh(xContextMenu);
            RefreshContextMenu(xContextMenu);

            if (sender is ItemCollection ic)
                TranslateCollection(ic);
        }
    }
}