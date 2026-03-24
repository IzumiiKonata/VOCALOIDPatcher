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
    }

    public static void DoTranslate()
    {
        IterateAndTranslate(Patcher.GetXMenu().Items);
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
                VPResourceManagerPatch.Skip = true;
                string v = (string) prop.GetValue(null);
                VPResourceManagerPatch.Skip = false;

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