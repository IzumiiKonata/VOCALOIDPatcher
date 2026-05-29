using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using VOCALOIDPatcher.Jobs;
using VOCALOIDPatcher.Patch;
using VOCALOIDPatcher.Patch.Patches;
using VOCALOIDPatcher.Translation;
using VOCALOIDPatcher.UI;
using VOCALOIDPatcher.Utils;
using Yamaha.VOCALOID;

namespace VOCALOIDPatcher;

public static class Patcher
{
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

    public static string Version => "1.1.0";

#pragma warning disable CA2255
    [ModuleInitializer]
    public static void Initialize()
    {
        AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
        {
            if (args.Name.StartsWith("VOCALOID6"))
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    if (assembly.GetName().Name?.StartsWith("VOCALOID6") == true)
                        return assembly;

            return null;
        };

        try
        {
            PatcherInit();
        }
        catch (Exception e)
        {
            Debug.ShowErrorMessage("Patcher 初始化失败!", e);
        }
    }

    private static void PatcherInit()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var ex = (Exception)args.ExceptionObject;
            Debug.ShowErrorMessage(ex.Message + Environment.NewLine + ex.StackTrace, "VOCALOID Patcher 错误");
        };

        if (!Directory.Exists(ConfigDir)) Directory.CreateDirectory(ConfigDir);

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
            DataDir = Path.Combine(new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "VOCALOID6", "Editor",
                "VOCALOIDPatcher"
            });
            Debug.Print("检测到正在以 VST 插件模式运行 VOCALOID6 编辑器");
            VstPluginPatch.ApplyPatches(_harmony);
        }

        ApplyPatches();
        TranslationManager.Initialize();
        Debug.Print("TranslationManager 已初始化");

        WpfTranslationPatch.InstallGlobalHandlers();

        if (!VstPluginMode) PostInject();
    }

    public static void PostInject()
    {
        AddPatcherMenuItem();
        JobMenu.Install();
        AutoSaveService.UpdateFromSettings();
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
        List<PatchBase> patches = new()
        {
            new AppLanguagePatch(),
            new WpfTranslationPatch(),
            new ResourceManagerPatch(),
            new DependencyObjectPatch(),
            new ShowOtherTracksNotesPatch(),
            new ShowNotePitchPatch(),
            new CharacterArtPatch(),
            new SwingMenuPatch()
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

    private static void AddPatcherMenuItem()
    {
        try
        {
            var menu = ReflectionUtils.GetMainMenu();

            WpfTranslationPatch.MarkUntranslatable(PatcherMenuItem);
            PatcherMenuItem.Click += (_, _) => SettingsWindow.ShowSingleton();

            menu.Items.Insert(menu.Items.Count - 1, PatcherMenuItem);
        }
        catch (Exception e)
        {
            Debug.ShowErrorMessage(e.Message + e.StackTrace);
        }
    }
}
