using UnityEditor;

namespace Companion.Editor
{
    // Arjun: fourth Blender-rigged Tripo character and the first male companion (ADR-074), imported through
    // the shared CharacterSetup. Shirt buttons sit on their own Trim slot so wardrobe tints keep them white.
    public static class ArjunCharacterSetup
    {
        public static readonly CharacterSpec Spec=new CharacterSpec {
            Name="Arjun",PortraitDistance=1.25f,
            Slots=new[] {
                new CharacterSpec.Slot{Name="Arjun_Skin",Smoothness=.6f,BumpScale=.45f},
                new CharacterSpec.Slot{Name="Arjun_Hair",Smoothness=.9f,BumpScale=.7f},
                new CharacterSpec.Slot{Name="Arjun_Top",Smoothness=.7f},
                new CharacterSpec.Slot{Name="Arjun_Bottom",Smoothness=.9f},
                new CharacterSpec.Slot{Name="Arjun_Shoes",Smoothness=.8f},
                new CharacterSpec.Slot{Name="Arjun_Trim",Smoothness=.6f},
                new CharacterSpec.Slot{Name="Arjun_Eyes",Texture="Eyes",Shared=false,Smoothness=.86f},
                new CharacterSpec.Slot{Name="Arjun_Mouth",Texture="Mouth",Shared=false,Smoothness=.35f},
            },
            TopMaterial="Arjun_Top",BottomMaterial="Arjun_Bottom",TrimMaterial="Arjun_Trim",HasShoes=true,
            RoleSummary="skin, shirt, trousers, hair and shoe roles found; shirt buttons stay untinted",
            Face=FaceTunings.Tripo(),Voice="male",
        };

        [MenuItem("Companion/Characters/Import Arjun")]
        public static void Run()=>CharacterSetup.Import(Spec);
        [MenuItem("Companion/Characters/Check Arjun Rig")]
        public static void Rig()=>CharacterRigChecks.Rig(Spec);
        [MenuItem("Companion/Characters/Check Arjun In App")]
        public static void InApp()=>CharacterRigChecks.InApp(Spec);
    }
}
