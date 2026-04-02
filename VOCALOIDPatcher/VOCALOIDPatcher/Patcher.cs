using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
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
    
    public static string Version => "1.0.6";

    public static readonly bool DebugMode = KeyState.IsKeyDown(0xA0);

    public static string ConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VOCALOIDPatcher");

    public static string ConfigFile =>
        Path.Combine(ConfigDir, "config.json");

    public static string DataDir =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VOCALOIDPatcher");
        
    
    public static ConfigManager ConfigManager;

    private static Harmony Harmony;

    #pragma warning disable CA2255
    [ModuleInitializer]
    public static void Initializer()
    {
        try
        {
            PatcherInit();
        } catch (Exception e)
        {
            MessageUtils.ShowErrorMessage("Patcher 初始化失败!", e);
        }
    }

    static void PatcherInit()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var ex = (Exception) args.ExceptionObject;
            MessageUtils.ShowErrorMessage(ex.Message + Environment.NewLine + ex.StackTrace, "VOCALOID Patcher 错误");
        };
        
        if (!Path.Exists(ConfigDir))
        {
            Directory.CreateDirectory(ConfigDir);
        }
        
        ConfigManager = new ConfigManager(ConfigFile);
        Harmony = new Harmony("VOCALOIDPatcher");
        
        ConsoleHelper.InitConsole();
            
        MessageUtils.Dbg("已拉起 VOCALOID Patcher");
        MessageUtils.Dbg($"版本: {Version}");
        MessageUtils.Dbg("https://github.com/IzumiiKonata/VOCALOIDPatcher");
        
        var targetType = typeof(App);
        var asm = targetType.Assembly;
        var version = asm.GetName().Version;

        MessageUtils.Dbg($"VOCALOID 编辑器版本: {version}");

        ReflectionUtils.GetMainWindow().Closing += (_, _) =>
        {
            ConfigManager.Save();
        };
        
        ApplyPatches();
        
        TranslationManager.Initialize();
        MessageUtils.Dbg("TranslationManager 已初始化");

        AddPatcherMenuItem();
    }

    private static void ApplyPatches()
    {
        List<PatchBase> patches =
        [
            new AppLanguagePatch(),
            new WPFTranslationPatch(),
            new ResourceManagerPatch(),
            new DependencyObjectPatch(),
            new MainWindowPatch.UpdateRightZonePatch(),
            new MainViewModelPatch.ShowAudioEffectWindowPatch(),
            WPFTranslationPatch.CreateContextMenuPatchFor<ContextMenuEventArgs>(typeof(ParameterHeaderControl), "OnContextMenuOpening"),
            WPFTranslationPatch.CreateContextMenuPatchFor<ContextMenuEventArgs>(typeof(ParameterHeaderView), "OnContextMenuOpening"),
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
            p.Apply(Harmony);
        });
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

    private static void AddPatcherMenuItem()
    {
        try
        {
            var menu = ReflectionUtils.GetMainMenu();

            LanguageMenuItem.Header = TranslationManager.Get("VOCALOIDPatcher_Language_Header");
            var items = BuildLanguageItems();
        
            foreach (var item in items)
            {
                LanguageMenuItem.Items.Add(item);
            }
            
            PatcherMenuItem.Items.Add(LanguageMenuItem);

            PatcherMenuItem.Items.Add(BuildTogglableMenuItem(
                    $"VOCALOIDPatcher_TranslateHardcodedStrings_Header", 
                    Settings.TranslateHardcodedStringsKey, 
                    true,
                    enabled =>
                    {
                        if (!enabled)
                        {
                            MessageUtils.ShowMessageBox($"{TranslationManager.Get("VOCALOIDPatcher_TranslateHardcodedStringsRestart")}");
                        }
                    }
            ));
            PatcherMenuItem.Items.Add(BuildMenuItem($"VOCALOID Patcher {Version}", _ => BrowseUtils.Browse("https://github.com/IzumiiKonata/VOCALOIDPatcher")));
            PatcherMenuItem.Items.Add(BuildMenuItem("Made with ❤ by IzumiiKonata", _ => BrowseUtils.Browse("https://space.bilibili.com/357605683")));
        
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
    private static MenuItem BuildMenuItem(string header, Action<MenuItem>? action = null)
    {
        var it = new MenuItem
        {
            Header = header,
            Name = $"VOCALOIDPatcherLanguageItemLabel{distinctCounter++}"
        };

        if (action != null)
        {
            it.Click += (_, _) => action(it);
        }
        
        WPFTranslationPatch.Untranslatable.Add(it);
        
        return it;
    }

    private static MenuItem BuildTogglableMenuItem(string header, string settingKey, bool defaultValue = false, Action<bool>? callback = null)
    {
        var item = BuildMenuItem(TranslationManager.Get(header) ?? header, it =>
        {
            var toggled = !ConfigManager.Get(settingKey, defaultValue);
            ConfigManager.Set(settingKey, toggled);
            it.Header = (toggled ? "✓ " : "   ") + TranslationManager.Get(header);
            MessageUtils.Dbg($"{settingKey} = {toggled}");
            WPFTranslationPatch.ReTranslate();

            callback?.Invoke(toggled);
        });
        
        var toggled = ConfigManager.Get(settingKey, defaultValue);
        item.Header = (toggled ? "✓ " : "   ") + TranslationManager.Get(header);

        TranslationManager.LanguageChanged += (_, _) =>
        {
            item.Header = (ConfigManager.Get(settingKey, defaultValue) ? "✓ " : "   ") + TranslationManager.Get(header);
        };
        
        return item;
    }
    
}