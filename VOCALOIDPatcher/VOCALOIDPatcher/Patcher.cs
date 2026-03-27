using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using VOCALOIDPatcher.Patch;
using VOCALOIDPatcher.Patch.Patches;
using VOCALOIDPatcher.Translation;
using VOCALOIDPatcher.Utils;
using Yamaha.VOCALOID;
using Yamaha.VOCALOID.MusicalEditor;
using Yamaha.VOCALOID.TrackEditor;
using Yamaha.VOCALOID.WaveEditor;
using RulerView = Yamaha.VOCALOID.TrackEditor.RulerView;
using ViewBase = System.Windows.Controls.ViewBase;

namespace VOCALOIDPatcher;

public static class Patcher
{
    
    public static readonly string Version = "1.0.2";

    public static readonly bool DebugMode = /*KeyState.IsKeyDown(0xA2) && */KeyState.IsKeyDown(0xA0);

    public static string AppDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VOCALOIDPatcher");

    public static string ConfigFile =>
        Path.Combine(AppDir, "config.json");
    
    public static ConfigManager ConfigManager;
    
    [ModuleInitializer]
    public static void Init()
    {
        if (DebugMode)
        {
            ConsoleHelper.InitConsole();
            MessageUtils.Dbg("已拉起 VOCALOIDPatcher");
            MessageUtils.Dbg($"版本: {Version}");
            MessageUtils.Dbg("https://github.com/IzumiiKonata/VOCALOIDPatcher");
            
            Type targetType = typeof(App);
            Assembly asm = targetType.Assembly;

            Version version = asm.GetName().Version;

            MessageUtils.Dbg($"VOCALOID 编辑器版本: {version}");
        }

        if (!Path.Exists(AppDir))
        {
            Directory.CreateDirectory(AppDir);
        }
        
        ConfigManager = new ConfigManager(ConfigFile);
        
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Exception ex = (Exception) args.ExceptionObject;
            MessageUtils.ShowErrorMessage(ex.Message + Environment.NewLine + ex.StackTrace, "VOCALOIDPatcher 错误");
        };

        GetMainWindow().Closing += (_, _) =>
        {
            ConfigManager.Save();
        };

        try
        {
            var harmony = new Harmony("VOCALOIDPatcher");

            List<PatchBase> patches =
            [
                new AppLanguagePatch(),
                new MenuItemsTranslationPatch(),
                new ResourceManagerPatch(),
                MenuItemsTranslationPatch.CreateContextMenuPatchFor<RoutedEventArgs>(typeof(PianorollView), "OnContextMenuOpened"),
                MenuItemsTranslationPatch.CreateContextMenuPatchFor<ContextMenuEventArgs>(typeof(TrackView), "OnContextMenuOpening"),
                MenuItemsTranslationPatch.CreateContextMenuPatchFor<ContextMenuEventArgs>(typeof(TrackViewBase), "OnContextMenuOpening"),
                MenuItemsTranslationPatch.CreateContextMenuPatchFor<ContextMenuEventArgs>(typeof(TrackToolbarView), "OnContextMenuOpening"),
                MenuItemsTranslationPatch.CreateContextMenuPatchFor<ContextMenuEventArgs>(typeof(TempoHeaderView), "OnContextMenuOpening"),
                MenuItemsTranslationPatch.CreateContextMenuPatchFor<ContextMenuEventArgs>(typeof(RulerView), "OnContextMenuOpening"),
                MenuItemsTranslationPatch.CreateContextMenuPatchFor<ContextMenuEventArgs>(typeof(WaveRulerView), "OnContextMenuOpening"),
                MenuItemsTranslationPatch.CreateContextMenuPatchFor<ContextMenuEventArgs>(typeof(ParameterHeaderControl), "OnContextMenuOpening"),
                MenuItemsTranslationPatch.CreateContextMenuPatchFor<ContextMenuEventArgs>(typeof(ParameterHeaderView), "OnContextMenuOpening"),
                MenuItemsTranslationPatch.CreateContextMenuPatchFor<ContextMenuEventArgs>(typeof(HeaderViewBase), "OnContextMenuOpening"),
                MenuItemsTranslationPatch.CreateContextMenuPatchFor<ContextMenuEventArgs>(typeof(ParameterView), "OnContextMenuOpening"),
                MenuItemsTranslationPatch.CreateContextMenuPatchFor<ContextMenuEventArgs>(typeof(ScrollViewerBase), "OnContextMenuOpening"),
            ];

            patches.ForEach(p =>
            {
                MessageUtils.Dbg($"应用 {p.PatchName}...");
                p.Apply(harmony);
            });
        } catch(Exception e)
        {
            MessageUtils.ShowErrorMessage(e.Message + e.StackTrace);
        }
        
        TranslationManager.Initialize();
        MessageUtils.Dbg("TranslationManager 已初始化");

        AddTranslationsItem();
    }

    public static MainWindow GetMainWindow()
    {
        if (Application.Current?.MainWindow is MainWindow mainWindow)
        {
            return mainWindow;
        }

        throw new InvalidOperationException("获取 MainWindow 失败。");
    }
    
    public static Menu GetMainMenu()
    {
        var mainWindow = GetMainWindow();

        var field = AccessTools.Field(mainWindow.GetType(), "xMainMenu")
            ?? throw new MissingFieldException(mainWindow.GetType().FullName, "xMainMenu");

        return field.GetValue(mainWindow) as Menu
               ?? throw new InvalidCastException("获取 xMainMenu 失败。");
    }

    public static T GetWindowField<T>(string fieldName) where T: class
    {
        MainWindow mainWindow = GetMainWindow();
        Type mainWindowType = mainWindow.GetType();
        FieldInfo? fieldInfo = mainWindowType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

        if (fieldInfo == null)
            throw new MissingFieldException(mainWindow.GetType().FullName + "." + fieldName, fieldName);
        
        return fieldInfo.GetValue(mainWindow) as T ?? throw new InvalidCastException(mainWindow.GetType().FullName + "." + fieldName);
    }

    public static TFieldType GetField<TFieldType, THolderType>(THolderType fieldHolder, string fieldName) 
        where TFieldType : class
        where THolderType : class
    {
        var type = typeof(THolderType);
        FieldInfo? fieldInfo  = AccessTools.Field(type, fieldName);

        if (fieldInfo == null)
            throw new MissingFieldException(type.FullName + "." + fieldName, fieldName);
        
        return fieldInfo.GetValue(fieldHolder) as TFieldType ?? throw new InvalidCastException(type.FullName + "." + fieldName);
    }

    private static MenuItem LanguageItem;

    public static void AddTranslationsItem()
    {
        try
        {
            Menu menu = GetMainMenu();
        
            LanguageItem = new MenuItem();
        
            LanguageItem.Header = TranslationManager.Get("VOCALOIDPatcher_Language_Header");
            LanguageItem.Name = "VOCALOIDPatcherLanguageItem";
            MenuItem[] items = BuildLanguageItems();
        
            foreach (MenuItem item in items)
            {
                LanguageItem.Items.Add(item);
            }
        
            menu.Items.Insert(menu.Items.Count - 1, LanguageItem);
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
            item.Header = (TranslationManager.CurrentLanguage == lang ? "✓ " : "   ") + lang;
            item.Click += (_, _) =>
            {
                ConfigManager.Set("Language", lang);
                TranslationManager.LoadLanguage(lang);
                MenuItemsTranslationPatch.DoTranslate();
                
                for (var j = 0; j < TranslationManager.AvailableLanguages.Count; j++)
                {
                    var l = TranslationManager.AvailableLanguages[j];
                    MenuItem it = (MenuItem) LanguageItem.Items[j];
                    it.Header = (TranslationManager.CurrentLanguage == l ? "✓ " : "   ") + l;
                }
                
            };
            languageItems.Add(item);
        }
        
        languageItems.Add(BuildItemLabel($"VOCALOIDPatcher {Version}", () => BrowseUtils.Browse("https://github.com/IzumiiKonata/VOCALOIDPatcher")));
        languageItems.Add(BuildItemLabel("Made with ❤ by IzumiiKonata", () => BrowseUtils.Browse("https://space.bilibili.com/357605683")));

        return languageItems.ToArray();
    }

    private static int distinctCounter = 0;
    private static MenuItem BuildItemLabel(string label, Action? action = null)
    {
        MenuItem it = new MenuItem();
        it.Name = $"VOCALOIDPatcherLanguageItemLabel{distinctCounter++}";
        it.Header = label;

        if (action != null)
        {
            it.Click += (_, _) => action();
        }
        
        return it;
    }
    
}