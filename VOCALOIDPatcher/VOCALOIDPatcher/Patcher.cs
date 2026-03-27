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

namespace VOCALOIDPatcher;

public static class Patcher
{
    
    public static string Version => "1.0.2";

    public static bool DebugMode => KeyState.IsKeyDown(0xA0); // left shift

    public static string AppDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VOCALOIDPatcher");

    public static string ConfigFile =>
        Path.Combine(AppDir, "config.json");
    
    public static readonly ConfigManager ConfigManager;

    static Patcher()
    {
        if (!Path.Exists(AppDir))
        {
            Directory.CreateDirectory(AppDir);
        }
        
        try
        {
            ConfigManager = new ConfigManager(ConfigFile);
        }
        catch (Exception e)
        {
            throw new ApplicationException($"初始化 ConfigManager 失败!\n{e.Message}\n{e.StackTrace}");
        }
    }
    
    #pragma warning disable CA2255
    [ModuleInitializer]
    public static void Init()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var ex = (Exception) args.ExceptionObject;
            MessageUtils.ShowErrorMessage(ex.Message + Environment.NewLine + ex.StackTrace, "VOCALOIDPatcher 错误");
        };
        
        if (DebugMode)
        {
            ConsoleHelper.InitConsole();
            MessageUtils.Dbg("已拉起 VOCALOIDPatcher");
            MessageUtils.Dbg($"版本: {Version}");
            MessageUtils.Dbg("https://github.com/IzumiiKonata/VOCALOIDPatcher");
            
            var targetType = typeof(App);
            var asm = targetType.Assembly;
            var version = asm.GetName().Version;

            MessageUtils.Dbg($"VOCALOID 编辑器版本: {version}");
        }

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
                MenuItemsTranslationPatch.CreateContextMenuPatchFor<ContextMenuEventArgs>(typeof(ParameterHeaderControl), "OnContextMenuOpening"),
                MenuItemsTranslationPatch.CreateContextMenuPatchFor<ContextMenuEventArgs>(typeof(ParameterHeaderView),      "OnContextMenuOpening"),
                MenuItemsTranslationPatch.CreateContextMenuPatchFor<ContextMenuEventArgs>(typeof(TrackToolbarView), "OnContextMenuOpening"),
                MenuItemsTranslationPatch.CreateContextMenuPatchFor<ContextMenuEventArgs>(typeof(ScrollViewerBase), "OnContextMenuOpening"),
                MenuItemsTranslationPatch.CreateContextMenuPatchFor<ContextMenuEventArgs>(typeof(TempoHeaderView), "OnContextMenuOpening"),
                MenuItemsTranslationPatch.CreateContextMenuPatchFor<ContextMenuEventArgs>(typeof(HeaderViewBase), "OnContextMenuOpening"),
                MenuItemsTranslationPatch.CreateContextMenuPatchFor<RoutedEventArgs>     (typeof(PianorollView), "OnContextMenuOpened"),
                MenuItemsTranslationPatch.CreateContextMenuPatchFor<ContextMenuEventArgs>(typeof(TrackViewBase), "OnContextMenuOpening"),
                MenuItemsTranslationPatch.CreateContextMenuPatchFor<ContextMenuEventArgs>(typeof(WaveRulerView), "OnContextMenuOpening"),
                MenuItemsTranslationPatch.CreateContextMenuPatchFor<ContextMenuEventArgs>(typeof(ParameterView), "OnContextMenuOpening"),
                MenuItemsTranslationPatch.CreateContextMenuPatchFor<ContextMenuEventArgs>(typeof(TrackView), "OnContextMenuOpening"),
                MenuItemsTranslationPatch.CreateContextMenuPatchFor<ContextMenuEventArgs>(typeof(RulerView), "OnContextMenuOpening"),
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
        var mainWindow = GetMainWindow();
        var mainWindowType = mainWindow.GetType();
        var fieldInfo = mainWindowType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

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

    private static readonly MenuItem LanguageItem = new();

    public static void AddTranslationsItem()
    {
        try
        {
            var menu = GetMainMenu();
        
            LanguageItem.Header = TranslationManager.Get("VOCALOIDPatcher_Language_Header");
            LanguageItem.Name = "VOCALOIDPatcherLanguageItem";
            var items = BuildLanguageItems();
        
            foreach (var item in items)
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
            var item = new MenuItem
            {
                Header = (TranslationManager.CurrentLanguage == lang ? "✓ " : "   ") + lang,
                Name = $"VOCALOIDPatcherLanguageItem{i}"
            };
            item.Click += (_, _) =>
            {
                ConfigManager.Set("Language", lang);
                TranslationManager.LoadLanguage(lang);
                MenuItemsTranslationPatch.DoTranslate();
                
                for (var j = 0; j < TranslationManager.AvailableLanguages.Count; j++)
                {
                    var l = TranslationManager.AvailableLanguages[j];
                    if (LanguageItem.Items[j] is MenuItem it)
                    {
                        it.Header = (TranslationManager.CurrentLanguage == l ? "✓ " : "   ") + l;
                    }
                }
                
            };
            languageItems.Add(item);
        }
        
        languageItems.Add(BuildItemLabel($"VOCALOIDPatcher {Version}", () => BrowseUtils.Browse("https://github.com/IzumiiKonata/VOCALOIDPatcher")));
        languageItems.Add(BuildItemLabel("Made with ❤ by IzumiiKonata", () => BrowseUtils.Browse("https://space.bilibili.com/357605683")));

        return languageItems.ToArray();
    }

    private static int distinctCounter;
    private static MenuItem BuildItemLabel(string label, Action? action = null)
    {
        var it = new MenuItem
        {
            Header = label,
            Name = $"VOCALOIDPatcherLanguageItemLabel{distinctCounter++}"
        };

        if (action != null)
        {
            it.Click += (_, _) => action();
        }
        
        return it;
    }
    
}