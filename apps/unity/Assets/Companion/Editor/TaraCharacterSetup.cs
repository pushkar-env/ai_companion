using UnityEditor;

namespace Companion.Editor
{
    // Tara: third Blender-rigged Tripo character (ADR-072), imported through the shared CharacterSetup.
    // Barefoot, so she has no shoe role; bead trims, lacing and the tassel keep their colour under tints.
    public static class TaraCharacterSetup
    {
        public static readonly CharacterSpec Spec=new CharacterSpec {
            Name="Tara",PortraitDistance=1.15f,
            Slots=new[] {
                new CharacterSpec.Slot{Name="Tara_Skin",Smoothness=.62f,BumpScale=.45f},
                new CharacterSpec.Slot{Name="Tara_Hair",Smoothness=.95f,BumpScale=.7f},
                new CharacterSpec.Slot{Name="Tara_Top",Smoothness=.65f},
                new CharacterSpec.Slot{Name="Tara_Bottom",Smoothness=1f},
                new CharacterSpec.Slot{Name="Tara_Earrings",Smoothness=.7f,Metallic=.1f,BumpScale=.5f},
                new CharacterSpec.Slot{Name="Tara_Trim",Smoothness=.5f},
                new CharacterSpec.Slot{Name="Tara_Eyes",Texture="Eyes",Shared=false,Smoothness=.86f},
                new CharacterSpec.Slot{Name="Tara_Mouth",Texture="Mouth",Shared=false,Smoothness=.35f},
            },
            TopMaterial="Tara_Top",BottomMaterial="Tara_Bottom",TrimMaterial="Tara_Trim",HasShoes=false,
            RoleSummary="skin, tunic, jeans and hair roles found; no shoe role for the barefoot character",
            Face=FaceTunings.Tripo(),
        };

        [MenuItem("Companion/Characters/Import Tara")]
        public static void Run()=>CharacterSetup.Import(Spec);
        [MenuItem("Companion/Characters/Check Tara Rig")]
        public static void Rig()=>CharacterRigChecks.Rig(Spec);
        [MenuItem("Companion/Characters/Check Tara In App")]
        public static void InApp()=>CharacterRigChecks.InApp(Spec);
    }
}
