using Companion.Presentation;
using UnityEditor;

namespace Companion.Editor
{
    // Arjun: fourth Blender-rigged Tripo character and the first male companion (ADR-074), imported through
    // the shared CharacterSetup. Modular since ADR-075: the base body (skin, hair, face rig) plus swappable
    // male garments fitted to his body. The signature look is his original brown shirt, grey trousers and
    // white sneakers; "Chambray casual" is the owner's reference outfit (open chambray shirt over a white
    // tee, olive chinos, white sneakers and a steel watch). Shirt buttons, the tee and the watch stay untinted.
    public static class ArjunCharacterSetup
    {
        public static readonly CharacterSpec Spec=new CharacterSpec {
            Name="Arjun",PortraitDistance=1.25f,
            Slots=new[] {
                new CharacterSpec.Slot{Name="Arjun_Skin",Smoothness=.6f,BumpScale=.45f},
                new CharacterSpec.Slot{Name="Arjun_Hair",Smoothness=.9f,BumpScale=.7f},
                new CharacterSpec.Slot{Name="Arjun_Top",Smoothness=.7f},
                new CharacterSpec.Slot{Name="Arjun_Trim",Smoothness=.6f},
                new CharacterSpec.Slot{Name="Arjun_Shoes",Smoothness=.8f},
                // both trousers share one fitted mesh; grey keeps the signature look on the garment layout
                new CharacterSpec.Slot{Name="Arjun_Bottom",Texture="Trousers_Grey_BaseColor",Normal="Trousers_Normal",Shared=false,Smoothness=.3f,BumpScale=.8f},
                new CharacterSpec.Slot{Name="Arjun_Chinos_Bottom",Texture="Chinos_Olive_BaseColor",Normal="Trousers_Normal",Shared=false,Smoothness=.28f,BumpScale=.8f},
                new CharacterSpec.Slot{Name="Arjun_Chambray_Top",Texture="Chambray_BaseColor",Normal="Chambray_Normal",Shared=false,Smoothness=.26f,BumpScale=.9f},
                new CharacterSpec.Slot{Name="Arjun_Tee",Texture="Chambray_BaseColor",Normal="Chambray_Normal",Shared=false,Smoothness=.2f,BumpScale=.7f},
                new CharacterSpec.Slot{Name="Arjun_Chambray_Buttons",Texture="Chambray_BaseColor",Normal="Chambray_Normal",Shared=false,Smoothness=.62f,BumpScale=.6f},
                new CharacterSpec.Slot{Name="Arjun_Watch_Steel",Texture="Watch_BaseColor",Normal="Watch_Normal",Shared=false,Metallic=.85f,Smoothness=.78f,BumpScale=.6f},
                new CharacterSpec.Slot{Name="Arjun_Watch_Dial",Texture="Watch_BaseColor",Normal="Watch_Normal",Shared=false,Smoothness=.9f,BumpScale=.5f},
                new CharacterSpec.Slot{Name="Arjun_Eyes",Texture="Eyes",Shared=false,Smoothness=.86f},
                new CharacterSpec.Slot{Name="Arjun_Mouth",Texture="Mouth",Shared=false,Smoothness=.35f},
            },
            TopMaterial="Arjun_Top",BottomMaterial="Arjun_Bottom",TrimMaterial="Arjun_Trim",HasShoes=true,
            RoleSummary="skin, hair, shirt, trousers and shoe roles found across the modular garments",
            Face=FaceTunings.Tripo(),Voice="male",
            Category=WardrobeCategory.Male,
            Garments=new[] {
                new CharacterSpec.Garment{Id="arjun.top.brown-shirt",DisplayName="Brown shirt",File="Arjun_Shirt_Classic",Object="Arjun_Shirt_Classic",Slot=GarmentSlot.Top,Materials=new[]{"Arjun_Top","Arjun_Trim"}},
                new CharacterSpec.Garment{Id="arjun.top.chambray-shirt",DisplayName="Chambray shirt & white tee",File="Arjun_Shirt_Chambray",Object="Arjun_Shirt_Chambray",Slot=GarmentSlot.Top,Materials=new[]{"Arjun_Chambray_Top","Arjun_Tee","Arjun_Chambray_Buttons"}},
                new CharacterSpec.Garment{Id="arjun.bottom.grey-trousers",DisplayName="Grey trousers",File="Arjun_Trousers",Object="Arjun_Trousers_Grey",Slot=GarmentSlot.Bottom,Materials=new[]{"Arjun_Bottom"}},
                new CharacterSpec.Garment{Id="arjun.bottom.olive-chinos",DisplayName="Olive chinos",File="Arjun_Trousers",Object="Arjun_Chinos_Olive",Slot=GarmentSlot.Bottom,Materials=new[]{"Arjun_Chinos_Bottom"}},
                new CharacterSpec.Garment{Id="arjun.shoes.white-sneakers",DisplayName="White sneakers",File="Arjun_Sneakers",Object="Arjun_Sneakers",Slot=GarmentSlot.Shoes,Materials=new[]{"Arjun_Shoes"}},
                new CharacterSpec.Garment{Id="arjun.accessory.steel-watch",DisplayName="Steel watch",File="Arjun_Watch",Object="Arjun_Watch",Slot=GarmentSlot.Accessory,Materials=new[]{"Arjun_Watch_Steel","Arjun_Watch_Dial"}},
            },
            Outfits=new[] {
                new CompanionWardrobeProfile.Outfit{id="signature",displayName="Signature look",garments=new[]{"arjun.top.brown-shirt","arjun.bottom.grey-trousers","arjun.shoes.white-sneakers"}},
                new CompanionWardrobeProfile.Outfit{id="chambray-casual",displayName="Chambray casual",garments=new[]{"arjun.top.chambray-shirt","arjun.bottom.olive-chinos","arjun.shoes.white-sneakers","arjun.accessory.steel-watch"}},
            },
        };

        [MenuItem("Companion/Characters/Import Arjun")]
        public static void Run()=>CharacterSetup.Import(Spec);
        [MenuItem("Companion/Characters/Check Arjun Rig")]
        public static void Rig()=>CharacterRigChecks.Rig(Spec);
        [MenuItem("Companion/Characters/Check Arjun In App")]
        public static void InApp()=>CharacterRigChecks.InApp(Spec);
    }
}
