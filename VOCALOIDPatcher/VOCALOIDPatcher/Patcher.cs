using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using HarmonyLib;
using VOCALOIDPatcher.Patches;
using VOCALOIDPatcher.Translation;
using Yamaha.VOCALOID;


namespace VOCALOIDPatcher;

public static class Patcher
{
    
    public static readonly string Version = "1.0.1";
    
    [ModuleInitializer]
    public static void Init()
    {
        PatcherDebug.ShowDbgMessage("Nuck Figgers 有感觉吗");

#if PATCHER_DEBUG
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            Exception ex = (Exception) args.ExceptionObject;
            PatcherDebug.ShowErrorMessage(ex.Message + Environment.NewLine + ex.StackTrace, "Patcher Unhandled Exception");
        };
#endif
        
        TranslationManager.Initialize();
        
        try
        {
            var harmony = new Harmony("VOCALOIDPatcher");
        
            harmony.PatchAll();
        } catch(Exception e)
        {
            PatcherDebug.ShowErrorMessage(e.Message + e.StackTrace);
        }
    }

    public static MainWindow GetMainWindow()
    {
        return (MainWindow) Application.Current.MainWindow;
    }
    
    public static Menu GetMainMenu()
    {
        MainWindow mainWindow = GetMainWindow();
        Type mainWindowType = mainWindow.GetType();
        FieldInfo? fieldInfo = mainWindowType.GetField("xMainMenu", BindingFlags.Instance | BindingFlags.NonPublic);
        return (fieldInfo.GetValue(mainWindow)) as Menu;
    }

    public static T? GetWindowField<T>(string fieldName) where T: class
    {
        MainWindow mainWindow = GetMainWindow();
        Type mainWindowType = mainWindow.GetType();
        FieldInfo? fieldInfo = mainWindowType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

        if (fieldInfo == null)
            return null;
        
        return (fieldInfo.GetValue(mainWindow)) as T;
    }

    public static void AddTranslationsItem()
    {
        try
        {
            Menu menu = GetMainMenu();
        
            MenuItem languageItem = new MenuItem();
        
            languageItem.Header = TranslationManager.Get("VOCALOIDPatcher_Language_Header");
            languageItem.Name = "VOCALOIDPatcherLanguageItem";
            MenuItem[] items = BuildLanguageItems();
        
            foreach (MenuItem item in items)
            {
                languageItem.Items.Add(item);
            }
        
            menu.Items.Insert(menu.Items.Count - 1, languageItem);
        } catch(Exception e)
        {
            PatcherDebug.ShowErrorMessage(e.Message + e.StackTrace);
        }
    }

    private static MenuItem[] BuildLanguageItems()
    {
        List<MenuItem> languageItems = new List<MenuItem>();

        for (var i = 0; i < TranslationManager.AvailableLanguages.Count; i++)
        {
            var lang = TranslationManager.AvailableLanguages[i];
            MenuItem item = new MenuItem();
            item.Name = $"VOCALOIDPatcherLanguageItem{i}";
            item.Header = lang;
            item.Click += (object sender, RoutedEventArgs args) =>
            {
                TranslationManager.LoadLanguage(lang);
                MenuItemsTranslationPatch.DoTranslate();
            };
            languageItems.Add(item);
        }
        
        languageItems.Add(BuildItemLabel($"VOCALOIDPatcher {Version}"));
        languageItems.Add(BuildItemLabel("Made with <3 by IzumiiKonata"));

        return languageItems.ToArray();
    }

    private static int distinctCounter = 0;
    private static MenuItem BuildItemLabel(string label)
    {
        MenuItem it = new MenuItem();
        it.Name = $"VOCALOIDPatcherLanguageItemLabel{distinctCounter++}";
        it.Header = label;
        return it;
    }
    
}