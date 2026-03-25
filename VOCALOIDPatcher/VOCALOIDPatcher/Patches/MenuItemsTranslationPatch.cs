using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HarmonyLib;
using VOCALOIDPatcher.Patches;
using VOCALOIDPatcher.Translation;
using Yamaha.VOCALOID;
using Yamaha.VOCALOID.Properties;

namespace VOCALOIDPatcher;

[HarmonyPatch(typeof(MainWindow), "InitializeCommandBindings")]
public static class MenuItemsTranslationPatch
{
    static void Prefix()
    {
        DoTranslate();
        Patcher.AddTranslationsItem();
        
        FixFilepathSeparator();
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
            PatcherDebug.ShowErrorMessage("Cannot get xRecentFiles!");
        }
    }

    /**
     * 翻译所有控件
     * 因为劫持的 dll 拉起要比窗口创建晚, 所以还需要手动刷新一下
     */
    public static void DoTranslate()
    {
        IterateAndTranslate(Patcher.GetMainMenu().Items);
    }

    private static void IterateAndTranslate(ItemCollection collection)
    {
        foreach (var item in collection)
        {
            if (item is HeaderedItemsControl headered)
            {
                string header = (string)headered.Header;
                headered.Header = GetTranslatedText(header);
            }

            if (item is ItemsControl items)
            {
                IterateAndTranslate(items.Items);
            }
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