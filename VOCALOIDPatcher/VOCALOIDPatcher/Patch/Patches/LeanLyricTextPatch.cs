using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Windows;
using System.Windows.Media;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using VOCALOIDPatcher.Translation;
using Yamaha.VOCALOID;
using Yamaha.VOCALOID.MusicalEditor;
using Yamaha.VOCALOID.VSM;

namespace VOCALOIDPatcher.Patch.Patches;

internal static class LyricTextState
{
    public static bool Lazy => Settings.FastProjectLoad;

    public static Typeface? Typeface;
    public static Brush? Brush;
    public static double Dpi = 1.0;

    private static readonly FieldInfo? BrushField =
        AccessTools.Field(typeof(PianorollView), "brushLyricFontWhenLetterMode");

    public static void Capture(PianorollView view)
    {
        Typeface = new Typeface(view.FontFamily, view.FontStyle, view.FontWeight, view.FontStretch);
        Brush = BrushField?.GetValue(view) as Brush;
        Dpi = VisualTreeHelper.GetDpi(view).PixelsPerDip;
    }

    public static void EnsureBuilt(UILyricAndPhoneme lyric)
    {
        if (!Lazy || lyric.FirstFormattedTexts != null)
            return;

        var lyrics = lyric.Lyrics;
        var phoneme = lyric.Phoneme;
        if (string.IsNullOrEmpty(lyrics) || string.IsNullOrEmpty(phoneme))
            return;

        var vm = lyric.VM;
        var typeface = Typeface;
        if (vm == null || typeface == null)
            return;

        try
        {
            string first;
            string second;
            if (vm.LyricInputMode == LyricInputModeEnum.Letter)
            {
                first  = lyrics;
                second = " [" + phoneme + "]";
            }
            else
            {
                first  = "[" + phoneme + "]";
                second = " " + lyrics;
            }

            var size = lyric.FontSize;

            lyric.FirstFormattedTexts = new FormattedText(first, CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, typeface, size, Brush, Dpi);
            lyric.SecondFormattedTexts = new FormattedText(second, CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, typeface, size, Brush, Dpi);
        }
        catch (Exception e)
        {
            VOCALOIDPatcher.Utils.Debug.Print(TranslationManager.Tr("VOCALOIDPatcher_Debug_LeanLyric_DeferConstructFailed", e.Message));
        }
    }
}

public class LeanLyricTextPatch : PatchBase
{
    public override string PatchName        => "LeanLyricTextPatch";
    public override Type   TargetClass      => typeof(PianorollView);
    public override string TargetMethodName => "InsertLyricsAndPhoneme";

    public override Type[] ArgumentTypes => new[] { typeof(MusicalEditorViewModel), typeof(WIVSMNote) };

    [HarmonyPrefix]
    private static void Prefix(PianorollView __instance)
    {
        if (LyricTextState.Lazy)
            LyricTextState.Capture(__instance);
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Newobj
                && instruction.operand is ConstructorInfo ctor
                && IsNullableConstruction(ctor))
            {
                var argCount = ctor.GetParameters().Length;

                for (var i = 0; i < argCount; i++)
                {
                    var pop = new CodeInstruction(OpCodes.Pop);

                    if (i == 0)
                    {
                        pop.labels.AddRange(instruction.labels);
                        pop.blocks.AddRange(instruction.blocks);
                    }

                    yield return pop;
                }

                yield return new CodeInstruction(OpCodes.Ldnull);
                continue;
            }

            yield return instruction;
        }
    }

    private static bool IsNullableConstruction(ConstructorInfo ctor)
    {
        if (ctor.DeclaringType == typeof(NoteFormattedText))
            return true;

        return LyricTextState.Lazy && ctor.DeclaringType == typeof(FormattedText);
    }
}

public class LazyLyricRenderPatch : PatchBase
{
    public override string PatchName        => "LazyLyricRenderPatch";
    public override Type   TargetClass      => typeof(UILyricAndPhoneme);
    public override string TargetMethodName => "OnRender";

    public override Type[] ArgumentTypes => new[] { typeof(DrawingContext) };

    [HarmonyPriority(Priority.First)]
    [HarmonyPrefix]
    private static void Prefix(UILyricAndPhoneme __instance)
    {
        LyricTextState.EnsureBuilt(__instance);
    }
}
