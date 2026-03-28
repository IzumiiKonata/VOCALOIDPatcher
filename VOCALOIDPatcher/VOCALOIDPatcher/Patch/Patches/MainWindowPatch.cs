using System.Windows;
using HarmonyLib;
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
        public override Type[]? ArgumentTypes => [ typeof(RightZoneTypeEnum) ];

        [HarmonyPostfix]
        static void Postfix(RightZoneTypeEnum rightZoneType)
        {
            var xRightZone = Patcher.GetMainWindowField<RightZone>("xRightZone");
            WPFTranslationPatch.Refresh(xRightZone);

            List<DependencyObject> refreshList = [
                Patcher.GetField<NoteInspector>(xRightZone, "xNoteInspector"),
                Patcher.GetField<MidiPartInspector>(xRightZone, "xMidiPartInspector"),
                Patcher.GetField<AudioPartInspector>(xRightZone, "xAudioPartInspector"),
                Patcher.GetField<MediaBrowser>(xRightZone, "xMediaBrowser"),
            ];
        
            refreshList.ForEach(WPFTranslationPatch.Refresh);
        }
        
    }
    
}