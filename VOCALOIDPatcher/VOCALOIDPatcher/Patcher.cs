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
        
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            Exception ex = (Exception) args.ExceptionObject;
            MessageUtils.ShowErrorMessage(ex.Message + Environment.NewLine + ex.StackTrace, "VOCALOIDPatcher 错误");
        };

        GetMainWindow().Closing += (sender, args) =>
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
                MessageUtils.Dbg($"正在应用 {p.PatchName}...");
                p.Apply(harmony);
            });
        } catch(Exception e)
        {
            MessageUtils.ShowErrorMessage(e.Message + e.StackTrace);
        }
        
        TranslationManager.Initialize();
        MessageUtils.Dbg("TranslationManager 已初始化");
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
                ConfigManager.Set("Language", lang);
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