using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using HarmonyLib;
using VOCALOIDPatcher.Translation;
using VOCALOIDPatcher.Utils;
using Yamaha.VOCALOID;
using Yamaha.VOCALOID.MusicalEditor;
using Yamaha.VOCALOID.Properties;
using Yamaha.VOCALOID.TrackEditor;

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
        Patcher.AddTranslationsItem();
        
        FixFilepathSeparator();
    }

    private static readonly Dictionary<object, string> OriginalMapping = new();

    static string GetOriginal(object obj, string? original)
    {
        if (!OriginalMapping.ContainsKey(obj))
        {
            OriginalMapping[obj] = original;
        }
        
        if (original != null && ResourceManagerPatch.ReversedMap.TryGetValue(original, out var res))
            OriginalMapping[obj] = res;

        return OriginalMapping[obj];
    }
    
    static void Refresh(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);

        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);

            if (child is HeaderedContentControl content)
            {
                content.Header = GetTranslatedText(GetOriginal(content, (string)content.Header));
            }

            if (child is HeaderedItemsControl item)
            {
                item.Header = GetTranslatedText(GetOriginal(item, (string)item.Header));
            }
            
            if (child is ItemsControl ic)
            {
                foreach (var it in ic.Items)
                {
                    if (it is ComboBoxItem cbi && cbi.Content is string txt)
                    {
                        cbi.Content = GetTranslatedText(GetOriginal(it, txt));
                    }
                }
            }
            
            if (child is FrameworkElement fe && fe.ToolTip is string tip)
            {
                fe.ToolTip = GetTranslatedText(GetOriginal(child, tip));
            }
            
            if (child is ContentControl cc && cc.Content is string text)
            {
                cc.Content = GetTranslatedText(GetOriginal(cc, text));
            }
            
            if (child is MenuItem subItem)
            {
                RefreshMenuItem(subItem);
            }
            
            if (child is ItemsControl items)
            {
                RefreshCollection(items.Items);
            }

            Refresh(child);
        }
    }
    
    public static void RefreshContextMenu(FrameworkElement element)
    {
        if (element.ContextMenu != null)
        {
            foreach (var item in element.ContextMenu.Items)
            {
                if (item is MenuItem menuItem)
                {
                    RefreshMenuItem(menuItem);
                }
            }
        }
    }

    private static void RefreshMenuItem(MenuItem item)
    {
        item.Header = GetTranslatedText(GetOriginal(item, (string)item.Header));

        foreach (var sub in item.Items)
        {
            if (sub is MenuItem subItem)
            {
                RefreshMenuItem(subItem);
            }
        }
    }
    
    private static void RefreshCollection(ItemCollection collection)
    {
        foreach (var child in collection)
        {
            if (child is HeaderedContentControl content)
            {
                content.Header = GetTranslatedText(GetOriginal(content, (string)content.Header));
            }

            if (child is HeaderedItemsControl item)
            {
                if (!OriginalMapping.ContainsKey(item))
                    OriginalMapping[item] = (string)item.Header;
                
                item.Header = GetTranslatedText(GetOriginal(item, (string)item.Header));
            }
            
            if (child is ItemsControl ic)
            {
                foreach (var it in ic.Items)
                {
                    if (it is ComboBoxItem cbi && cbi.Content is string txt)
                    {
                        cbi.Content = GetTranslatedText(GetOriginal(it, txt));
                    }
                }
            }
            
            if (child is FrameworkElement fe && fe.ToolTip is string tip)
            {
                fe.ToolTip = GetTranslatedText(GetOriginal(child, tip));
            }
            
            if (child is ContentControl cc && cc.Content is string text)
            {
                cc.Content = GetTranslatedText(GetOriginal(cc, text));
            }
            
            if (child is MenuItem subItem)
            {
                RefreshMenuItem(subItem);
            }

            if (child is ItemsControl items)
            {
                RefreshCollection(items.Items);
            }
        }
    }

    /**
     * 修复 文件 -> 最近打开... 中的文件分隔符全都是￥的问题
     */
    private static void FixFilepathSeparator()
    {
        MenuItem? xRecentFiles = Patcher.GetWindowField<MenuItem>("xRecentFiles");

        if (xRecentFiles != null)
        {
            xRecentFiles.FontFamily = new FontFamily("Consolas");
        }
        else
        {
            MessageUtils.ShowErrorMessage("Cannot get xRecentFiles!");
        }
    }

    /**
     * 翻译所有控件
     * 因为劫持的 dll 拉起要比窗口创建晚, 所以还需要手动刷新一下
     */
    public static void DoTranslate()
    {
        var mainMenu = Patcher.GetMainMenu();
        Refresh(mainMenu);
        RefreshContextMenu(mainMenu);
        RefreshCollection(mainMenu.Items);
        // var musicalEditorDivision = Patcher.GetWindowField<MusicalEditorDivision>("xMusicalEditorDiv");
        // Refresh(musicalEditorDivision);
        // RefreshContextMenu(Patcher.GetWindowField<MusicalEditorDivision>("xMusicalEditorDiv"));
        //
        // var pianorollView = Patcher.GetField<PianorollView, MusicalEditorDivision>(musicalEditorDivision, "xPianorollView");
        // Refresh(pianorollView);
        // RefreshContextMenu(pianorollView);
        //
        
        foreach (Window window in Application.Current.Windows)
        {
            Refresh(window);
        
            if (window is FrameworkElement fe)
            {
                RefreshContextMenu(fe);
            }
        }
    }

    public static PatchBase CreateContextMenuPatchFor<T>(Type type, string methodName)
    {
        return new XContextMenuPatch<T>(type, methodName);
    }

    public class XContextMenuPatch<T> : PatchBase
    {
        private readonly Type targetClass;
        private readonly string methodName;

        public XContextMenuPatch(Type targetClass, string methodName)
        {
            this.targetClass = targetClass;
            this.methodName = methodName;
        }
        
        public override string PatchName => $"XContextMenuPatch{targetClass.Name}";
        public override Type TargetClass => targetClass;
        public override string TargetMethodName => methodName;
        public override Type[] ArgumentTypes => [typeof(object), typeof(T)];

        [HarmonyPostfix]
        static void Postfix(object sender, T e)
        {
            // MessageUtils.ShowDbgMessage("Hit!");
            var xContextMenu = (Control) sender;
            Refresh(xContextMenu);
            RefreshContextMenu(xContextMenu);
            
            if (sender is ItemCollection ic)
                RefreshCollection(ic);
        }
    }
    
    
    /**
     * 从已翻译的文本获取本地化键值
     * "Open Recent..." -> "MainMenu_File_OpenRecent_Header"
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
            if (prop.PropertyType == typeof(string) && prop.CanRead)
            {
                string name = prop.Name;
                ResourceManagerPatch.Skip = true;
                string v = (string) prop.GetValue(null);
                ResourceManagerPatch.Skip = false;

                if (value == v)
                {
                    return name;
                }
            }
        }

        return "";
    }
    
    private static string GetTranslatedText(string value)
    {
        var resourceKey = GetResourceKey(value);
        if (String.IsNullOrEmpty(resourceKey))
        {
            return value;
        }

        string? translated = TranslationManager.Get(resourceKey);
        return translated ?? value;
    }
}