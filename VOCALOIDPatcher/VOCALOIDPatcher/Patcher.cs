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
    
    public static string Version => "1.0.3";

    public static readonly bool DebugMode = KeyState.IsKeyDown(0xA0);

    public static string ConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VOCALOIDPatcher");

    public static string ConfigFile =>
        Path.Combine(ConfigDir, "config.json");

    public static string DataDir =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VOCALOIDPatcher");
        
    
    public static readonly ConfigManager ConfigManager;

    static Patcher()
    {
        if (!Path.Exists(ConfigDir))
        {
            Directory.CreateDirectory(ConfigDir);
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
            MessageUtils.ShowErrorMessage(ex.Message + Environment.NewLine + ex.StackTrace, "VOCALOID Patcher 错误");
        };
        
        AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
        {
            if (args.Name.StartsWith("0Harmony") ||  args.Name.StartsWith("VOCALOID6"))
            {
                return Assembly.GetExecutingAssembly();
            }
            return null;
        };
        
        try
        {
            if (DebugMode)
            {
                ConsoleHelper.InitConsole();
                
                MessageUtils.Dbg("已拉起 VOCALOID Patcher");
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
            
            var harmony = new Harmony("VOCALOIDPatcher");

            List<PatchBase> patches =
            [
                new AppLanguagePatch(),
                new WPFTranslationPatch(),
                new ResourceManagerPatch(),
                new DependencyObjectPatch(),
                new MainWindowPatch.UpdateRightZonePatch(),
                new MainViewModelPatch.ShowAudioEffectWindowPatch(),
                WPFTranslationPatch.CreateContextMenuPatchFor<ContextMenuEventArgs>(typeof(ParameterHeaderControl), "OnContextMenuOpening"),
                WPFTranslationPatch.CreateContextMenuPatchFor<ContextMenuEventArgs>(typeof(ParameterHeaderView),      "OnContextMenuOpening"),
                WPFTranslationPatch.CreateContextMenuPatchFor<ContextMenuEventArgs>(typeof(TrackToolbarView), "OnContextMenuOpening"),
                WPFTranslationPatch.CreateContextMenuPatchFor<ContextMenuEventArgs>(typeof(ScrollViewerBase), "OnContextMenuOpening"),
                WPFTranslationPatch.CreateContextMenuPatchFor<ContextMenuEventArgs>(typeof(TempoHeaderView), "OnContextMenuOpening"),
                WPFTranslationPatch.CreateContextMenuPatchFor<ContextMenuEventArgs>(typeof(HeaderViewBase), "OnContextMenuOpening"),
                WPFTranslationPatch.CreateContextMenuPatchFor<RoutedEventArgs>     (typeof(PianorollView), "OnContextMenuOpened"),
                WPFTranslationPatch.CreateContextMenuPatchFor<ContextMenuEventArgs>(typeof(TrackViewBase), "OnContextMenuOpening"),
                WPFTranslationPatch.CreateContextMenuPatchFor<ContextMenuEventArgs>(typeof(WaveRulerView), "OnContextMenuOpening"),
                WPFTranslationPatch.CreateContextMenuPatchFor<ContextMenuEventArgs>(typeof(ParameterView), "OnContextMenuOpening"),
                WPFTranslationPatch.CreateContextMenuPatchFor<ContextMenuEventArgs>(typeof(TrackView), "OnContextMenuOpening"),
                WPFTranslationPatch.CreateContextMenuPatchFor<ContextMenuEventArgs>(typeof(RulerView), "OnContextMenuOpening"),
            ];

            patches.ForEach(p =>
            {
                MessageUtils.Dbg($"应用 {p.PatchName}...");
                p.Apply(harmony);
            });
            
            TranslationManager.Initialize();
            MessageUtils.Dbg("TranslationManager 已初始化");

            AddPatcherMenuItem();
        } catch(Exception e)
        {
            MessageUtils.ShowErrorMessage(e.Message + e.StackTrace);
        }
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

    public static T GetMainWindowField<T>(string fieldName) where T: class
    {
        var mainWindow = GetMainWindow();
        var mainWindowType = mainWindow.GetType();
        var fieldInfo = mainWindowType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

        if (fieldInfo == null)
            throw new MissingFieldException(mainWindow.GetType().FullName + "." + fieldName, fieldName);
        
        return fieldInfo.GetValue(mainWindow) as T ?? throw new InvalidCastException(mainWindow.GetType().FullName + "." + fieldName);
    }

    public static TFieldType GetField<TFieldType>(object holderInstance, string fieldName) 
        where TFieldType : class
    {
        var type = holderInstance.GetType();
        FieldInfo? fieldInfo  = AccessTools.Field(type, fieldName);

        if (fieldInfo == null)
            throw new MissingFieldException(type.FullName + "." + fieldName, fieldName);
        
        return fieldInfo.GetValue(holderInstance) as TFieldType ?? throw new InvalidCastException(type.FullName + "." + fieldName);
    }
    
    public static TFieldType GetFirstFieldWithType<TFieldType>(object holderInstance) 
        where TFieldType : class
    {
        var type = holderInstance.GetType();
        var declaredFields = AccessTools.GetDeclaredFields(type);

        var fieldInfo = declaredFields.Find(field => field.FieldType == typeof(TFieldType));
        if (fieldInfo == null)
            throw new MissingFieldException(type.FullName + ", typeof " + typeof(TFieldType).FullName, typeof(TFieldType).FullName);

        return (TFieldType) (fieldInfo.GetValue(holderInstance) ?? throw new MissingFieldException(type.FullName + ", typeof " + typeof(TFieldType).FullName,typeof(TFieldType).FullName));
    }

    private static readonly MenuItem PatcherMenuItem = new()
    {
        Header = "VOCALOID Patcher",
        Name = "VOCALOIDPatcherMenuItem"
    };

    private static readonly MenuItem LanguageMenuItem = new()
    {
        Name = "VOCALOIDPatcherMenuItem_LanguageMenuItem"
    };

    public static void AddPatcherMenuItem()
    {
        try
        {
            var menu = GetMainMenu();

            LanguageMenuItem.Header = TranslationManager.Get("VOCALOIDPatcher_Language_Header");
            var items = BuildLanguageItems();
        
            foreach (var item in items)
            {
                LanguageMenuItem.Items.Add(item);
            }
            
            PatcherMenuItem.Items.Add(LanguageMenuItem);
            
            PatcherMenuItem.Items.Add(BuildItemLabel($"VOCALOID Patcher {Version}", () => BrowseUtils.Browse("https://github.com/IzumiiKonata/VOCALOIDPatcher")));
            PatcherMenuItem.Items.Add(BuildItemLabel("Made with ❤ by IzumiiKonata", () => BrowseUtils.Browse("https://space.bilibili.com/357605683")));
        
            menu.Items.Insert(menu.Items.Count - 1, PatcherMenuItem);
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
                WPFTranslationPatch.ReTranslate();
                
                for (var j = 0; j < TranslationManager.AvailableLanguages.Count; j++)
                {
                    var l = TranslationManager.AvailableLanguages[j];
                    if (LanguageMenuItem.Items[j] is MenuItem it)
                    {
                        it.Header = (TranslationManager.CurrentLanguage == l ? "✓ " : "   ") + l;
                    }
                }
                
            };
            languageItems.Add(item);
        }

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