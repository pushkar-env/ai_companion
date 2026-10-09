using Companion.Presentation;
using UnityEditor;
using UnityEngine;

namespace Companion.Editor
{
    // Meera: Blender-rigged Tripo character (ADR-071), imported through the shared CharacterSetup.
    public static class MeeraCharacterSetup
    {
        public const string Name="Meera";
        public const string Root="Assets/Companion/Imported/Meera";
        public const string Fbx=Root+"/Meera.fbx";
        public const string RigJson=Root+"/Meera.rig.json";
        const string LegacyRuntimeMesh=Root+"/RuntimeMeshes/Meera_Body_Talking.asset";
        public static readonly CharacterSpec Spec=new CharacterSpec {
            Name=Name,PortraitDistance=1.15f,
            // One 2K atlas shared by every body slot; separate slots exist for wardrobe roles.
            Slots=new[] {
                new CharacterSpec.Slot{Name="Meera_Skin",Smoothness=.62f,BumpScale=.45f},
                new CharacterSpec.Slot{Name="Meera_Hair",Smoothness=.95f,BumpScale=.7f},
                new CharacterSpec.Slot{Name="Meera_Kurti",Smoothness=.55f},
                new CharacterSpec.Slot{Name="Meera_Palazzo",Smoothness=1f},
                new CharacterSpec.Slot{Name="Meera_Shoes",Smoothness=.45f},
                new CharacterSpec.Slot{Name="Meera_Earrings",Smoothness=.62f,Metallic=.85f,BumpScale=.5f},
                new CharacterSpec.Slot{Name="Meera_Eyes",Texture="Eyes",Shared=false,Smoothness=.86f},
                new CharacterSpec.Slot{Name="Meera_Mouth",Texture="Mouth",Shared=false,Smoothness=.35f},
            },
            TopMaterial="Meera_Kurti",BottomMaterial="Meera_Palazzo",HasShoes=true,
            RoleSummary="skin, kurti, palazzo, hair and sneaker roles found",
            Face=FaceTunings.Tripo(),
        };

        [MenuItem("Companion/Characters/Import Meera")]
        public static void Run()
        {
            CharacterSetup.Import(Spec);
            // Earlier imports wrote a text runtime mesh into Assets; the imported mesh now carries the frames.
            if(AssetDatabase.LoadAssetAtPath<Mesh>(LegacyRuntimeMesh)!=null)AssetDatabase.DeleteAsset(LegacyRuntimeMesh);
            if(AssetDatabase.IsValidFolder(Root+"/RuntimeMeshes")&&AssetDatabase.FindAssets("",new[]{Root+"/RuntimeMeshes"}).Length==0)AssetDatabase.DeleteAsset(Root+"/RuntimeMeshes");
            AssetDatabase.SaveAssets();
        }

        public static void ConfigureSprings(CompanionSecondaryMotion motion,Transform model)=>CharacterSetup.ConfigureSprings(motion,model,RigJson);
    }
}
