using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Companion.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Companion.Editor
{
    // Imports the Blender-rigged Meera character (CC-compatible skeleton, face/viseme blendshapes,
    // spring chains) and registers her beside Alita in TalkingCompanion. Re-runnable: updates in place.
    public static class MeeraCharacterSetup
    {
        public const string Root="Assets/Companion/Imported/Meera";
        public const string Fbx=Root+"/Meera.fbx";
        public const string RigJson=Root+"/Meera.rig.json";
        const string LegacyRuntimeMesh=Root+"/RuntimeMeshes/Meera_Body_Talking.asset";
        public const string Name="Meera";
        [Serializable] class Bone {public string name;public float[] head;}
        [Serializable] class Chain {public string name;public string[] bones;public float[] tip;public float stiffness,drag,gravity,radius;}
        [Serializable] class Collider {public string bone;public float[] center,tail;public bool capsule;public float radius;}
        [Serializable] class Rig {public Bone[] bones;public Chain[] chains;public Collider[] colliders;}

        [MenuItem("Companion/Characters/Import Meera")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=TalkingCharacterSetup.ScenePath)throw new InvalidOperationException("Open TalkingCompanion first.");
            ConfigureTextures();
            var materials=CreateMaterials();
            ConfigureModel(materials);   // reimport runs MeeraModelPostprocessor (frames folded in the Library)
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(Fbx)??throw new InvalidOperationException("Meera.fbx did not import.");
            var body=model.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r=>r.name=="Meera_Body").sharedMesh;
            if(body.GetBlendShapeIndex("Eye_Blink_L__f50")>=0)throw new InvalidOperationException("Blink frames were not folded on import");
            AddToScene(scene,model);
            // Earlier imports wrote a text runtime mesh into Assets; the imported mesh now carries the frames.
            if(AssetDatabase.LoadAssetAtPath<Mesh>(LegacyRuntimeMesh)!=null)AssetDatabase.DeleteAsset(LegacyRuntimeMesh);
            if(AssetDatabase.IsValidFolder(Root+"/RuntimeMeshes")&&AssetDatabase.FindAssets("",new[]{Root+"/RuntimeMeshes"}).Length==0)AssetDatabase.DeleteAsset(Root+"/RuntimeMeshes");
            AssetDatabase.SaveAssets();
        }

        static void ConfigureTextures()
        {
            void Set(string file,bool srgb,int max,bool normal=false,bool point=false,bool mips=true)
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(Root+"/Textures/"+file);
                importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
                importer.sRGBTexture=srgb&&!normal;importer.maxTextureSize=max;importer.mipmapEnabled=mips;
                importer.filterMode=point?FilterMode.Point:FilterMode.Bilinear;importer.anisoLevel=point?0:4;
                importer.textureCompression=point?TextureImporterCompression.Uncompressed:TextureImporterCompression.Compressed;
                importer.isReadable=false;importer.SaveAndReimport();
            }
            Set("Meera_BaseColor.jpg",true,2048);
            Set("Meera_Normal.png",false,2048,normal:true);
            Set("Meera_MetallicSmoothness.png",false,1024);
            Set("Meera_Eyes.png",true,1024);
            Set("Meera_Mouth.png",true,64,point:true,mips:false);
        }

        static Dictionary<string,Material> CreateMaterials()
        {
            Directory.CreateDirectory(Root+"/Materials");
            T Load<T>(string file) where T:UnityEngine.Object=>AssetDatabase.LoadAssetAtPath<T>(Root+"/Textures/"+file);
            var baseMap=Load<Texture2D>("Meera_BaseColor.jpg");var normal=Load<Texture2D>("Meera_Normal.png");var gloss=Load<Texture2D>("Meera_MetallicSmoothness.png");
            Material Make(string name,Texture2D albedo,Texture2D bump,Texture2D metallicGloss,float smoothness,float metallic=0,float bumpScale=.6f)
            {
                string path=Root+"/Materials/"+name+".mat";
                var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};AssetDatabase.CreateAsset(mat,path);}
                mat.SetFloat("_WorkflowMode",1);mat.SetTexture("_BaseMap",albedo);mat.SetColor("_BaseColor",Color.white);
                mat.SetTexture("_BumpMap",bump);mat.SetFloat("_BumpScale",bumpScale);
                if(bump!=null)mat.EnableKeyword("_NORMALMAP");else mat.DisableKeyword("_NORMALMAP");
                mat.SetTexture("_MetallicGlossMap",metallicGloss);
                if(metallicGloss!=null)mat.EnableKeyword("_METALLICSPECGLOSSMAP");else mat.DisableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetFloat("_Metallic",metallic);mat.SetFloat("_Smoothness",smoothness);
                EditorUtility.SetDirty(mat);return mat;
            }
            // One 2K atlas shared by every body slot; separate slots exist for wardrobe roles.
            var result=new Dictionary<string,Material> {
                {"Meera_Skin",Make("Meera_Skin",baseMap,normal,gloss,.62f,0,.45f)},
                {"Meera_Hair",Make("Meera_Hair",baseMap,normal,gloss,.95f,0,.7f)},
                {"Meera_Kurti",Make("Meera_Kurti",baseMap,normal,gloss,.55f)},
                {"Meera_Palazzo",Make("Meera_Palazzo",baseMap,normal,gloss,1f)},
                {"Meera_Shoes",Make("Meera_Shoes",baseMap,normal,gloss,.45f)},
                {"Meera_Earrings",Make("Meera_Earrings",baseMap,normal,null,.62f,.85f,.5f)},
                {"Meera_Eyes",Make("Meera_Eyes",Load<Texture2D>("Meera_Eyes.png"),null,null,.86f)},
                {"Meera_Mouth",Make("Meera_Mouth",Load<Texture2D>("Meera_Mouth.png"),null,null,.35f)},
            };
            AssetDatabase.SaveAssets();
            return result;
        }

        static void ConfigureModel(Dictionary<string,Material> materials)
        {
            var importer=(ModelImporter)AssetImporter.GetAtPath(Fbx);
            importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.NoAvatar;
            importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;importer.importVisibility=false;
            importer.importBlendShapes=true;importer.importNormals=ModelImporterNormals.Import;
            importer.importBlendShapeNormals=ModelImporterNormals.Calculate;importer.importTangents=ModelImporterTangents.CalculateMikk;
            importer.useFileScale=true;importer.globalScale=1;importer.meshCompression=ModelImporterMeshCompression.Off;
            importer.isReadable=false;importer.optimizeGameObjects=false;importer.skinWeights=ModelImporterSkinWeights.Standard;
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            foreach(var pair in materials)importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),pair.Key),pair.Value);
            importer.SaveAndReimport();
        }

        static void AddToScene(Scene scene,GameObject model)
        {
            var app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>(FindObjectsInactive.Include)??throw new InvalidOperationException("TalkingCharacter missing");
            var option=app.characters.FirstOrDefault(c=>c.name==Name);
            var go=option?.model!=null?option.model.gameObject:null;
            if(go==null) {
                go=(GameObject)PrefabUtility.InstantiatePrefab(model,scene);go.name=Name;
                Undo.RegisterCreatedObjectUndo(go,"Add Meera");
            }
            var reference=app.characters.Length>0&&app.characters[0].model!=null?app.characters[0].model:app.character;
            Undo.RecordObject(go.transform,"Place Meera");go.transform.SetPositionAndRotation(reference.position,reference.rotation);
            var body=go.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r=>r.name=="Meera_Body");
            // Use the imported mesh (frames folded on import); drop any earlier runtime-mesh override.
            var meshProperty=new SerializedObject(body).FindProperty("m_Mesh");
            if(meshProperty.prefabOverride)PrefabUtility.RevertPropertyOverride(meshProperty,InteractionMode.AutomatedAction);
            foreach(var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)){Undo.RecordObject(r,"Meera renderers");r.updateWhenOffscreen=false;r.skinnedMotionVectors=false;}
            var motion=go.GetComponent<CompanionSecondaryMotion>()??Undo.AddComponent<CompanionSecondaryMotion>(go);
            Undo.RecordObject(motion,"Meera springs");ConfigureSprings(motion,go.transform);
            if(option==null) {
                Undo.RecordObject(app,"Register Meera");
                app.characters=app.characters.Concat(new[]{new TalkingCharacter.CharacterOption{name=Name,model=go.transform,portraitDistance=1.15f}}).ToArray();
            }
            go.SetActive(app.character==go.transform);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }

        // The rig description is authored in Blender space. The axis mapping is chosen by matching the
        // exported bone heads against the imported bind poses, then verified to millimetre precision.
        public static void ConfigureSprings(CompanionSecondaryMotion motion,Transform model)
        {
            var rig=JsonUtility.FromJson<Rig>(AssetDatabase.LoadAssetAtPath<TextAsset>(RigJson).text);
            var rest=new Dictionary<string,Vector3>();
            foreach(var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                var toModel=model.worldToLocalMatrix*r.transform.localToWorldMatrix;var bind=r.sharedMesh.bindposes;
                for(int i=0;i<r.bones.Length&&i<bind.Length;i++)if(r.bones[i]!=null)rest[r.bones[i].name]=(toModel*bind[i].inverse).MultiplyPoint3x4(Vector3.zero);
            }
            var pairs=rig.bones.Where(b=>rest.ContainsKey(b.name)).Select(b=>(blender:new Vector3(b.head[0],b.head[1],b.head[2]),unity:rest[b.name])).ToArray();
            if(pairs.Length<8)throw new InvalidOperationException("Too few matching bones to place spring data");
            Func<Vector3,Vector3> best=null;float bestError=float.MaxValue;
            int[][] perms={new[]{0,1,2},new[]{0,2,1},new[]{1,0,2},new[]{1,2,0},new[]{2,0,1},new[]{2,1,0}};
            foreach(var p in perms)for(int s=0;s<8;s++) {
                var sign=new Vector3((s&1)!=0?-1:1,(s&2)!=0?-1:1,(s&4)!=0?-1:1);
                Func<Vector3,Vector3> map=b=>Vector3.Scale(new Vector3(b[p[0]],b[p[1]],b[p[2]]),sign);
                Vector3 offset=Vector3.zero;foreach(var q in pairs)offset+=q.unity-map(q.blender);offset/=pairs.Length;
                float error=pairs.Max(q=>(map(q.blender)+offset-q.unity).magnitude);
                if(error<bestError){bestError=error;var o=offset;best=b=>map(b)+o;}
            }
            if(bestError>.002f)throw new InvalidOperationException("Rig description does not match imported skeleton ("+bestError+" m)");
            Vector3 V(float[] a)=>best(new Vector3(a[0],a[1],a[2]));
            motion.chains=rig.chains.Select(c=>new CompanionSecondaryMotion.Chain{name=c.name,bones=c.bones,tip=V(c.tip),stiffness=c.stiffness,drag=c.drag,gravity=c.gravity,radius=c.radius}).ToArray();
            motion.colliders=rig.colliders.Select(c=>new CompanionSecondaryMotion.BodyCollider{bone=c.bone,center=V(c.center),tail=V(c.tail),capsule=c.capsule,radius=c.radius}).ToArray();
            motion.gravityDirection=Vector3.down;
            EditorUtility.SetDirty(motion);
        }
    }
}
