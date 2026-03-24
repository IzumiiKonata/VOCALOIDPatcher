using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HarmonyLib;
using VOCALOIDPatcher.Translation;
using Yamaha.VOCALOID;
using Yamaha.VOCALOID.Properties;
using MessageBox = System.Windows.MessageBox;


namespace VOCALOIDPatcher;

public static class Patcher
{
    [ModuleInitializer]
    public static void Init()
    {
        PatcherDebug.ShowDbgMessage("Nuck Figgers 有感觉吗");
        
        TranslationManager.Initialize();
        
        try
        {
            var harmony = new Harmony("VOCALOIDPatcher");

            // var original = typeof(App).GetMethod("ValidateAuthorization", BindingFlags.NonPublic | BindingFlags.Static);
            // var prefix = typeof(PatchValidation).GetMethod("Prefix");
        
            harmony.PatchAll();
        } catch(Exception e)
        {
            PatcherDebug.ShowErrorMessage(e.Message + e.StackTrace);
        }
    }

    public static Menu GetXMenu()
    {
        Window mainWindow = (MainWindow) Application.Current.MainWindow;
        Type mainWindowType = mainWindow.GetType();
        FieldInfo fieldInfo = mainWindowType.GetField("xMainMenu", BindingFlags.Instance | BindingFlags.NonPublic);
        return (fieldInfo.GetValue(mainWindow)) as Menu;
    }

    public static void AddTranslationsItem()
    {
        try
        {
            Menu menu = GetXMenu();
        
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
            item.Name = "VOCALOIDPatcherLanguageItem" + i;
            item.Header = lang;
            item.Click += (object sender, RoutedEventArgs args) =>
            {
                TranslationManager.LoadLanguage(lang);
                MenuItemsTranslationPatch.DoTranslate();
            };
            languageItems.Add(item);
        }
        
        MenuItem it = new MenuItem();
        it.Name = "VOCALOIDPatcherLanguageItemFooter";
        it.Header = "Made with <3 by IzumiiKonata";
        languageItems.Add(it);

        return languageItems.ToArray();
    }
    
}