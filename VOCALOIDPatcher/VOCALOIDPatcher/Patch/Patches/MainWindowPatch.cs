using System;
using System.Collections.Generic;
using System.Windows;
using HarmonyLib;
using VOCALOIDPatcher.Utils;
using Yamaha.VOCALOID;
using Yamaha.VOCALOID.Media;
using Yamaha.VOCALOID.MusicalEditor;
using Yamaha.VOCALOID.WaveEditor;

namespace VOCALOIDPatcher.Patch.Patches;

public class MainWindowPatch
{

    public class UpdateRightZonePatch : PatchBase
    {
        public override string PatchName => "UpdateRightZonePatch";
        public override Type TargetClass => typeof(MainWindow);
        public override string TargetMethodName => "UpdateRightZoneViews";
        public override Type[]? ArgumentTypes => new[] { typeof(RightZoneTypeEnum) };

        [HarmonyPostfix]
        static void Postfix(RightZoneTypeEnum rightZoneType)
        {
            var xRightZone = ReflectionUtils.GetMainWindowField<RightZone>("xRightZone");
            WPFTranslationPatch.RefreshAll(xRightZone);

            List<DependencyObject> refreshList = new()
            {
                ReflectionUtils.GetField<NoteInspector>(xRightZone, "xNoteInspector"),
                ReflectionUtils.GetField<MidiPartInspector>(xRightZone, "xMidiPartInspector"),
                ReflectionUtils.GetField<AudioPartInspector>(xRightZone, "xAudioPartInspector"),
                ReflectionUtils.GetField<MediaBrowser>(xRightZone, "xMediaBrowser"),
            };
        
            refreshList.ForEach(WPFTranslationPatch.RefreshAll);
        }
        
    }
    
}