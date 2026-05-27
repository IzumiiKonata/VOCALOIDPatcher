using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using VOCALOIDPatcher.Patch;
using VOCALOIDPatcher.Patch.Patches;
using VOCALOIDPatcher.Translation;
using VOCALOIDPatcher.Utils;
using Yamaha.VOCALOID;

namespace VOCALOIDPatcher;

public static class Patcher
{

    public static string Version => "1.1.0";

    public static readonly bool DebugMode = KeyState.IsKeyDown(0xA0);

    public static readonly string ConfigDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VOCALOIDPatcher");

    public static readonly string ConfigFile =
        Path.Combine(ConfigDir, "config.json");

    public static string DataDir =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VOCALOIDPatcher");

    public static ConfigManager ConfigManager = null!;

    private static Harmony _harmony = null!;

    public static bool VstPluginMode;

    #pragma warning disable CA2255
    [ModuleInitializer]
    public static void Initialize()
    {
        AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
        {
            if (args.Name.StartsWith("VOCALOID6"))
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name?.StartsWith("VOCALOID6") == true)
                    {
                        return assembly;
                    }
                }
            }

            return null;
        };

        try
        {
            PatcherInit();
        } catch (Exception e)
        {
            Debug.ShowErrorMessage("Patcher 初始化失败!", e);
        }
    }

    private static void PatcherInit()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var ex = (Exception) args.ExceptionObject;
            Debug.ShowErrorMessage(ex.Message + Environment.NewLine + ex.StackTrace, "VOCALOID Patcher 错误");
        };

        if (!Directory.Exists(ConfigDir))
        {
            Directory.CreateDirectory(ConfigDir);
        }

        VstPluginMode = DetectVstPluginMode();

        ConfigManager = new ConfigManager(ConfigFile);
        _harmony = new Harmony("VOCALOIDPatcher");

        ConsoleHelper.InitConsole();

        Debug.Print("已拉起 VOCALOID Patcher");
        Debug.Print($"版本: {Version}");
        Debug.Print("https://github.com/IzumiiKonata/VOCALOIDPatcher");

        var targetType = typeof(App);
        var asm = targetType.Assembly;
        var version = asm.GetName().Version;

        Debug.Print($"VOCALOID 编辑器版本: {version}");

        if (VstPluginMode)
        {
            DataDir = Path.Combine(new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "VOCALOID6", "Editor", "VOCALOIDPatcher" });
            Debug.Print("检测到正在以 VST 插件模式运行 VOCALOID6 编辑器");
            VstPluginPatch.ApplyPatches(_harmony);
        }

        ApplyPatches();
        TranslationManager.Initialize();
        Debug.Print("TranslationManager 已初始化");

        WpfTranslationPatch.InstallGlobalHandlers();

        if (!VstPluginMode)
        {
            PostInject();
        }
    }

    public static void PostInject()
    {
        AddPatcherMenuItem();
    }

    private static bool DetectVstPluginMode()
    {
        try
        {
            ReflectionUtils.GetMainWindow();
            return false;
        }
        catch (Exception)
        {
            return true;
        }
    }

    private static void ApplyPatches()
    {
        List<PatchBase> patches = new() {
            new AppLanguagePatch(),
            new WpfTranslationPatch(),
            new ResourceManagerPatch(),
            new DependencyObjectPatch(),
        };

        patches.ForEach(p =>
        {
            Debug.Print($"应用 {p.PatchName}...");
            p.Apply(_harmony);
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

            WpfTranslationPatch.MarkUntranslatable(PatcherMenuItem);

            LanguageMenuItem.Header = TranslationManager.Get("VOCALOIDPatcher_Language_Header");
            TranslationManager.LanguageChanged += (_, _) =>
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
                            Debug.ShowMessageBox($"{TranslationManager.Get("VOCALOIDPatcher_TranslateHardcodedStringsRestart")}");
                        }
                    }
            ));

            PatcherMenuItem.Items.Add(BuildMenuItem(
	            $"VOCALOID Patcher {Version}"
						+ (VstPluginMode ? " (VSTi)" : "")
#if NET6_0
	                    + " (.NET 6.0)"
#endif
	            ,
	            _ => BrowseUtils.Browse("https://github.com/IzumiiKonata/VOCALOIDPatcher")
	        ));
            PatcherMenuItem.Items.Add(BuildMenuItem("Made with ❤ by IzumiiKonata", _ => BrowseUtils.Browse("https://space.bilibili.com/357605683")));

            menu.Items.Insert(menu.Items.Count - 1, PatcherMenuItem);
        } catch(Exception e)
        {
            Debug.ShowErrorMessage(e.Message + e.StackTrace);
        }
    }

    private static MenuItem[] BuildLanguageItems()
    {
        var languageItems = new List<MenuItem>();

        for (var i = 0; i < TranslationManager.AvailableLanguages.Count; i++)
        {
            var lang = TranslationManager.AvailableLanguages[i];
            var item = new MenuItem
            {
                Header = (TranslationManager.CurrentLanguage == lang ? "✓ " : "   ") + lang,
                Name = $"VOCALOIDPatcherLanguageItem{i}"
            };
            WpfTranslationPatch.Untranslatable.Add(item);
            item.Click += (_, _) =>
            {
                ConfigManager.Set("Language", lang);
                TranslationManager.LoadLanguage(lang);
                WpfTranslationPatch.ReTranslate();

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

    private static int _distinctCounter;
    private static MenuItem BuildMenuItem(string header, Action<MenuItem>? action = null)
    {
        var it = new MenuItem
        {
            Header = header,
            Name = $"VOCALOIDPatcherLanguageItemLabel{_distinctCounter++}"
        };

        if (action != null)
        {
            it.Click += (_, _) => action(it);
        }

        WpfTranslationPatch.Untranslatable.Add(it);

        return it;
    }

    private static MenuItem BuildTogglableMenuItem(string header, string settingKey, bool defaultValue = false, Action<bool>? callback = null)
    {
        var item = BuildMenuItem(TranslationManager.Get(header) ?? header, it =>
        {
            var toggled = !ConfigManager.Get(settingKey, defaultValue);
            ConfigManager.Set(settingKey, toggled);
            it.Header = (toggled ? "✓ " : "   ") + TranslationManager.Get(header);
            Debug.Print($"{settingKey} = {toggled}");
            WpfTranslationPatch.ReTranslate();

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
