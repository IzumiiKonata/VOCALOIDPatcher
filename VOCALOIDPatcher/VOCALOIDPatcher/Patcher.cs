using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using HarmonyLib;
using VOCALOIDPatcher.Patch;
using VOCALOIDPatcher.Patch.Patches;
using VOCALOIDPatcher.Translation;
using VOCALOIDPatcher.Utils;
using Yamaha.VOCALOID;

namespace VOCALOIDPatcher;

public static class Patcher
{
    
    public static readonly string Version = "1.0.2";

    public static readonly bool DebugMode = /*KeyState.IsKeyDown(0xA2) && */KeyState.IsKeyDown(0xA0);
    
    [ModuleInitializer]
    public static void Init()
    {
        if (DebugMode)
        {
            MessageUtils.ShowDbgMessage("VOCALOIDPatcher 已被加载");

            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                Exception ex = (Exception) args.ExceptionObject;
                MessageUtils.ShowErrorMessage(ex.Message + Environment.NewLine + ex.StackTrace, "Patcher Unhandled Exception");
            };
        }
        
        TranslationManager.Initialize();
        
        try
        {
            var harmony = new Harmony("VOCALOIDPatcher");

            List<PatchBase> patches = [
                new AppLanguagePatch(),
                new MenuItemsTranslationPatch(),
                new ResourceManagerPatch()
            ];
            
            patches.ForEach(p => p.Apply(harmony));
        } catch(Exception e)
        {
            MessageUtils.ShowErrorMessage(e.Message + e.StackTrace);
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
            MessageUtils.ShowErrorMessage(e.Message + e.StackTrace);
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