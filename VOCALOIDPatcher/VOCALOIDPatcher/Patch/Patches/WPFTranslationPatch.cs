using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using VOCALOIDPatcher.Translation;
using VOCALOIDPatcher.Utils;
using Yamaha.VOCALOID;
using Yamaha.VOCALOID.Properties;

namespace VOCALOIDPatcher.Patch.Patches;

public class WPFTranslationPatch : PatchBase
{
    public override string PatchName        => "WPFTranslationPatch";
    public override Type   TargetClass      => typeof(MainWindow);
    public override string TargetMethodName => "InitializeCommandBindings";

    [HarmonyPrefix]
    static void Prefix()
    {
        ReTranslate();
        FixFilepathSeparator();
    }

    private static readonly Dictionary<object, string> OriginalMapping = new();

    public static readonly HashSet<object> Untranslatable = new();

    private static string GetOriginal(object obj, string? translated)
    {
        if (Untranslatable.Contains(obj))
            return translated;
        
        if (!OriginalMapping.ContainsKey(obj))
        {
            OriginalMapping[obj] = translated;
        }
        
        if (translated is not null && TranslationManager.TranslatedToOriginalMap.TryGetValue(translated, out var res))
            OriginalMapping[obj] = res;

        return OriginalMapping[obj];
    }

    private static readonly List<string> MissingKeyList = [];

    private static string GetTranslatedText(string value)
    {
        var resourceKey = GetResourceKey(value);

        var isNullOrEmpty = string.IsNullOrEmpty(resourceKey);

        if (isNullOrEmpty)
        {
            if (Settings.TranslateHardcodedStrings && TranslationManager.HardcodedPropertyMapping.TryGetValue(value, out var res))
            {
                return TranslationManager.Get(res) ?? value;
            }
            
            if (TranslationManager.TranslatedToTranslationKeyMap.TryGetValue(value, out var r))
            {
                return TranslationManager.Get(r) ?? value;
            }
                
            if (!MissingKeyList.Contains(value))
            {
                MessageUtils.Dbg($"Key not found: {value}");
                MissingKeyList.Add(value);
            }
            
            return value;
        }
        
        return TranslationManager.Get(resourceKey) ?? value;
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

    public static bool TranslateTextBox = false;

    public static void TranslateElement(object element)
    {
        if (Untranslatable.Contains(element))
            return;
        
        switch (element)
        {
            case HeaderedItemsControl hic:
                if (hic.Header is string hicHeader)
                    hic.Header = GetTranslatedText(GetOriginal(hic, hicHeader));
                break;

            case HeaderedContentControl hcc:
                if (hcc.Header is string hccHeader)
                    hcc.Header = GetTranslatedText(GetOriginal(hcc, hccHeader));
                break;

            case ContentControl cc:
                if (cc.Content is string text)
                {
                    cc.Content = GetTranslatedText(GetOriginal(cc, text));
                }
                break;

            case TextBlock tb:
                if (TranslateTextBox)
                    tb.Text = GetTranslatedText(tb.Text);
                break;
        }

        if (element is FrameworkElement fe)
        {
            if (fe.ToolTip is string tip)
                fe.ToolTip = GetTranslatedText(GetOriginal(fe, tip));
        }
    }

    public static void RefreshAll(DependencyObject obj)
    {
        var visited = new HashSet<DependencyObject>();
        // visited.Clear();
        _RefreshAll(obj, visited);
        
        if (obj is FrameworkElement fe && !fe.IsLoaded)
        {
            fe.Loaded += (_, _) =>
            {
                RefreshAll(fe);
            };
        }
    }
    
    private static void _RefreshAll(DependencyObject? root, HashSet<DependencyObject> visited)
    {
        if (root == null || !visited.Add(root)) return;

        visited.Add(root);

        TranslateElement(root);

        if (root is Visual || root is Visual3D)
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                _RefreshAll(child, visited);
            }
        }

        foreach (var child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is DependencyObject dep)
                _RefreshAll(dep, visited);
        }

        if (root is ItemsControl ic)
        {
            foreach (var item in ic.Items)
            {
                var container = ic.ItemContainerGenerator.ContainerFromItem(item);
                if (container is DependencyObject dep)
                    _RefreshAll(dep, visited);
            }
        }

        if (root is FrameworkElement fe)
        {
            if (fe.ContextMenu != null)
                _RefreshAll(fe.ContextMenu, visited);

            if (fe.ToolTip is DependencyObject tip)
                _RefreshAll(tip, visited);
        }
    }

    /**
     * 翻译所有控件。
     * 因为劫持的 dll 拉起要比窗口创建晚，所以还需要手动刷新一下。
     */
    public static void ReTranslate()
    {
        var mainMenu = ReflectionUtils.GetMainMenu();
        RefreshAll(mainMenu);
        
        foreach (Window window in Application.Current.Windows)
        {
            RefreshAll(window);
        }

        var mainWindow = ReflectionUtils.GetMainWindow();
        var audioEffectWindow = mainWindow.AudioEffectWindow;
        
        if (audioEffectWindow != null)
        {
            TranslateTextBox = true;
            RefreshAll(audioEffectWindow);
            TranslateTextBox = false;
        }

    }

    /** 
     * 修复 "文件 -> 最近打开..." 中文件路径分隔符全都是 ￥ 的问题
     */
    private static void FixFilepathSeparator()
    {
        var xRecentFiles = ReflectionUtils.GetMainWindowField<MenuItem>("xRecentFiles");
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
            RefreshAll(xContextMenu);
        }
    }
}