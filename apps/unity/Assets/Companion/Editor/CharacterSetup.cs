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
    // One Blender-rigged character (CC_Base skeleton, face/viseme blendshapes, spring chains in
    // <Name>.rig.json) exported by the tripo-character-rig pipeline into Imported/<Name>/.
    public sealed class CharacterSpec
    {
        public sealed class Slot
        {
            public string Name;                 // material slot name in the FBX, e.g. Tara_Hair
            public string Texture="BaseColor";  // <Name>_<Texture> under Textures/
            public bool Shared=true;            // shared atlas slots also get the normal and metallic/smoothness maps
            public string Normal;               // own normal map <Name>_<Normal>.png (garment atlases)
            public float Smoothness=.6f,Metallic,BumpScale=.6f;
        }
        // A swappable garment exported as Wardrobe/<File>.fbx (skinned to the same skeleton), with optional
        // spring chains in Wardrobe/<File>.item.json. Several garments may share one mesh with other materials.
        public sealed class Garment
        {
            public string Id,DisplayName,File,Object;
            public GarmentSlot Slot;
            public string[] Materials=Array.Empty<string>();   // material slot names, in submesh order
            public string[] Hides=Array.Empty<string>();       // body renderers hidden while worn
        }
        public WardrobeCategory Category=WardrobeCategory.Female;
        public Garment[] Garments=Array.Empty<Garment>();
        public CompanionWardrobeProfile.Outfit[] Outfits=Array.Empty<CompanionWardrobeProfile.Outfit>();   // first = signature look
        public bool Modular=>Garments.Length>0;
        public string BodyFamily=>Name.ToLowerInvariant();
        public string WardrobeRoot=>Root+"/Wardrobe";
        public string Name;
        public float PortraitDistance=1.15f;
        public Slot[] Slots=Array.Empty<Slot>();
        // Expectations for the rig and in-app checks.
        public string TopMaterial,BottomMaterial,TrimMaterial;
        public bool HasShoes;
        public string RoleSummary;
        // Facial calibration for this rig (jaw range, viseme and expression gains), copied to the roster.
        public FaceTuning Face=new FaceTuning();
        // Local speech voice key for the roster entry ("female" or "male").
        public string Voice="female";
        public string Root=>"Assets/Companion/Imported/"+Name;
        public string Fbx=>Root+"/"+Name+".fbx";
        public string RigJson=>Root+"/"+Name+".rig.json";
        public string Body=>Name+"_Body";
        public string EvidenceFolder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/evidence/m1/"+Name.ToLowerInvariant()));
    }

    public static class FaceTunings
    {
        // Alita's CC5 morphs are subtle lip postures driven with the jaw bone (no mouthClose channel).
        public static FaceTuning Alita()=>new FaceTuning();
        // Pipeline-built Tripo rigs: lip-only viseme shapes at full posture, corner-aware jaw skinning
        // and ARKit mouthClose for sealing p/b/m and rounding 'oo' against the jaw gap.
        public static FaceTuning Tripo(float jawDegrees=11f)=>new FaceTuning {
            jawDegrees=jawDegrees,openGain=.9f,roundGain=.95f,wideGain=.85f,tightGain=.7f,affricateGain=.85f,dentalGain=.9f,explosiveGain=.9f,tongueGain=.9f,
            sealGain=1f,smileGain=.85f};
    }

    // Imports a character from its spec and registers it beside Alita in TalkingCompanion.
    // Re-runnable: textures, materials, model settings, springs and the roster entry update in place.
    public static class CharacterSetup
    {
        [Serializable] class Bone {public string name;public float[] head;}
        [Serializable] class Chain {public string name;public string[] bones;public float[] tip;public float stiffness,drag,gravity,radius;}
        [Serializable] class Collider {public string bone;public float[] center,tail;public bool capsule;public float radius;}
        [Serializable] class Share {public string bone,source;public float amount;}
        [Serializable] class Rig {public Bone[] bones;public Chain[] chains;public Collider[] colliders;public Share[] shares;}

        public static void Import(CharacterSpec spec)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=TalkingCharacterSetup.ScenePath)throw new InvalidOperationException("Open TalkingCompanion first.");
            ConfigureTextures(spec);
            var materials=CreateMaterials(spec);
            ConfigureModel(spec.Fbx,materials);   // reimport runs MeeraModelPostprocessor (frames folded in the Library)
            foreach(var file in spec.Garments.Select(g=>g.File).Distinct())ConfigureModel(spec.WardrobeRoot+"/"+file+".fbx",materials);
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(spec.Fbx)??throw new InvalidOperationException(spec.Name+".fbx did not import.");
            var body=model.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r=>r.name==spec.Body).sharedMesh;
            if(body.GetBlendShapeIndex("Eye_Blink_L__f50")>=0)throw new InvalidOperationException("Blink frames were not folded on import");
            AddToScene(scene,model,spec,materials);
            AssetDatabase.SaveAssets();
        }

        static void ConfigureTextures(CharacterSpec spec)
        {
            void Set(string file,bool srgb,int max,bool normal=false,bool point=false,bool mips=true,bool compress=true)
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(spec.Root+"/Textures/"+file)??throw new InvalidOperationException("Missing texture "+file);
                importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
                importer.sRGBTexture=srgb&&!normal;importer.maxTextureSize=max;importer.mipmapEnabled=mips;
                importer.filterMode=point?FilterMode.Point:FilterMode.Bilinear;importer.anisoLevel=point?0:4;
                importer.textureCompression=point||!compress?TextureImporterCompression.Uncompressed:TextureImporterCompression.Compressed;
                importer.isReadable=false;importer.SaveAndReimport();
            }
            Set(spec.Name+"_BaseColor.jpg",true,2048);
            Set(spec.Name+"_Normal.png",false,2048,normal:true);
            Set(spec.Name+"_MetallicSmoothness.png",false,1024);
            Set(spec.Name+"_Eyes.png",true,1024);
            // 256 px atlas: 64 px solid colour blocks (UVs at block centres) plus painted teeth strips.
            Set(spec.Name+"_Mouth.png",true,256,compress:false);
            // garment atlases (own colour and normal maps)
            var done=new HashSet<string>{"BaseColor","Normal","MetallicSmoothness","Eyes","Mouth"};
            foreach(var slot in spec.Slots) {
                if(done.Add(slot.Texture))Set(spec.Name+"_"+slot.Texture+".png",true,2048);
                if(!string.IsNullOrEmpty(slot.Normal)&&done.Add(slot.Normal))Set(spec.Name+"_"+slot.Normal+".png",false,2048,normal:true);
            }
        }

        static Dictionary<string,Material> CreateMaterials(CharacterSpec spec)
        {
            Directory.CreateDirectory(spec.Root+"/Materials");
            Texture2D Load(string suffix)
            {
                foreach(var ext in new[]{".jpg",".png"}) {
                    var t=AssetDatabase.LoadAssetAtPath<Texture2D>(spec.Root+"/Textures/"+spec.Name+"_"+suffix+ext);if(t!=null)return t;
                }
                throw new InvalidOperationException("Missing texture "+spec.Name+"_"+suffix);
            }
            var normal=Load("Normal");var gloss=Load("MetallicSmoothness");
            var result=new Dictionary<string,Material>();
            foreach(var slot in spec.Slots) {
                string path=spec.Root+"/Materials/"+slot.Name+".mat";
                var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=slot.Name};AssetDatabase.CreateAsset(mat,path);}
                var bump=!string.IsNullOrEmpty(slot.Normal)?Load(slot.Normal):slot.Shared?normal:null;var metallicGloss=slot.Shared&&slot.Metallic<.5f?gloss:null;
                mat.SetFloat("_WorkflowMode",1);mat.SetTexture("_BaseMap",Load(slot.Texture));mat.SetColor("_BaseColor",Color.white);
                mat.SetTexture("_BumpMap",bump);mat.SetFloat("_BumpScale",slot.BumpScale);
                if(bump!=null)mat.EnableKeyword("_NORMALMAP");else mat.DisableKeyword("_NORMALMAP");
                mat.SetTexture("_MetallicGlossMap",metallicGloss);
                if(metallicGloss!=null)mat.EnableKeyword("_METALLICSPECGLOSSMAP");else mat.DisableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetFloat("_Metallic",slot.Metallic);mat.SetFloat("_Smoothness",slot.Smoothness);
                EditorUtility.SetDirty(mat);result[slot.Name]=mat;
            }
            AssetDatabase.SaveAssets();
            return result;
        }

        static void ConfigureModel(string fbx,Dictionary<string,Material> materials)
        {
            var importer=(ModelImporter)AssetImporter.GetAtPath(fbx)??throw new InvalidOperationException("Missing "+fbx);
            importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.NoAvatar;
            importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;importer.importVisibility=false;
            importer.importBlendShapes=true;importer.importNormals=ModelImporterNormals.Import;
            // Calculated shape normals flip 180 degrees along the thin inner-lip walls (orange lip
            // "flakes"); the lip shapes are small, so the imported base normals shade them correctly.
            importer.importBlendShapeNormals=ModelImporterNormals.None;importer.importTangents=ModelImporterTangents.CalculateMikk;
            importer.useFileScale=true;importer.globalScale=1;importer.meshCompression=ModelImporterMeshCompression.Off;
            importer.isReadable=false;importer.optimizeGameObjects=false;importer.skinWeights=ModelImporterSkinWeights.Standard;
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            foreach(var pair in materials)importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),pair.Key),pair.Value);
            importer.SaveAndReimport();
        }

        static void AddToScene(Scene scene,GameObject model,CharacterSpec spec,Dictionary<string,Material> materials)
        {
            var app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>(FindObjectsInactive.Include)??throw new InvalidOperationException("TalkingCharacter missing");
            var option=app.characters.FirstOrDefault(c=>c.name==spec.Name);
            var go=option?.model!=null?option.model.gameObject:null;
            if(go==null) {
                go=(GameObject)PrefabUtility.InstantiatePrefab(model,scene);go.name=spec.Name;
                Undo.RegisterCreatedObjectUndo(go,"Add "+spec.Name);
            }
            var reference=app.characters.Length>0&&app.characters[0].model!=null?app.characters[0].model:app.character;
            Undo.RecordObject(go.transform,"Place "+spec.Name);go.transform.SetPositionAndRotation(reference.position,reference.rotation);
            var body=go.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r=>r.name==spec.Body);
            // Use the imported mesh (frames folded on import); drop any earlier runtime-mesh override.
            var meshProperty=new SerializedObject(body).FindProperty("m_Mesh");
            if(meshProperty.prefabOverride)PrefabUtility.RevertPropertyOverride(meshProperty,InteractionMode.AutomatedAction);
            foreach(var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)){Undo.RecordObject(r,spec.Name+" renderers");r.updateWhenOffscreen=false;r.skinnedMotionVectors=false;}
            if(spec.Modular)AssembleGarments(go,model,spec,materials);
            var motion=go.GetComponent<CompanionSecondaryMotion>()??Undo.AddComponent<CompanionSecondaryMotion>(go);
            Undo.RecordObject(motion,spec.Name+" springs");
            ConfigureSprings(motion,go.transform,spec.RigJson,spec.Garments.Select(g=>GarmentChainsPath(spec,g)).Where(p=>p!=null).Distinct().ToArray());
            Undo.RecordObject(app,"Register "+spec.Name);
            if(option==null) {
                option=new TalkingCharacter.CharacterOption{name=spec.Name,model=go.transform,portraitDistance=spec.PortraitDistance};
                app.characters=app.characters.Concat(new[]{option}).ToArray();
            }
            option.face=spec.Face.Clone();option.voice=spec.Voice;
            go.SetActive(app.character==go.transform);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }

        static string GarmentChainsPath(CharacterSpec spec,CharacterSpec.Garment g)
        {
            string path=spec.WardrobeRoot+"/"+g.File+".item.json";
            return AssetDatabase.LoadAssetAtPath<TextAsset>(path)!=null?path:null;
        }

        // Binds every garment FBX of the spec to the character's own skeleton (by bone name), grafting the
        // garment-only spring bones under their parents, and records catalog data on CompanionGarment.
        // Garments of the signature outfit start active. Re-runnable: objects are found by name and updated.
        // TryGetComponent: in the Editor a missing built-in component comes back as a fake null that '??' keeps.
        static T Ensure<T>(GameObject go) where T:Component=>go.TryGetComponent<T>(out var c)?c:Undo.AddComponent<T>(go);
        static void AssembleGarments(GameObject go,GameObject model,CharacterSpec spec,Dictionary<string,Material> materials)
        {
            var profile=Ensure<CompanionWardrobeProfile>(go);
            Undo.RecordObject(profile,spec.Name+" wardrobe profile");
            profile.category=spec.Category;profile.bodyFamily=spec.BodyFamily;
            profile.outfits=spec.Outfits.Select(o=>new CompanionWardrobeProfile.Outfit{id=o.id,displayName=o.displayName,garments=o.garments.ToArray()}).ToArray();
            EditorUtility.SetDirty(profile);
            var signature=new HashSet<string>(spec.Outfits.Length>0?spec.Outfits[0].garments:Array.Empty<string>());
            var baseBones=new HashSet<string>(model.GetComponentsInChildren<Transform>(true).Select(t=>t.name));
            int Depth(Transform t){int d=0;while(t.parent!=null){t=t.parent;d++;}return d;}
            foreach(var g in spec.Garments) {
                string path=spec.WardrobeRoot+"/"+g.File+".fbx";
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path)??throw new InvalidOperationException("Missing "+path);
                var src=prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault()??throw new InvalidOperationException(path+" has no skinned mesh");
                var map=go.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(x=>x.Key,x=>x.First());
                // garment-only bones (spring chains): created once, rest transform refreshed on every import
                foreach(var bone in src.bones.Where(b=>b!=null&&!baseBones.Contains(b.name)).OrderBy(Depth)) {
                    if(bone.parent==null||!map.TryGetValue(bone.parent.name,out var parent))throw new InvalidOperationException("No parent for garment bone "+bone.name);
                    if(!map.TryGetValue(bone.name,out var b)) {
                        b=new GameObject(bone.name).transform;Undo.RegisterCreatedObjectUndo(b.gameObject,"Graft "+bone.name);
                        b.SetParent(parent,false);map[bone.name]=b;
                    }
                    Undo.RecordObject(b,"Rest "+bone.name);b.localPosition=bone.localPosition;b.localRotation=bone.localRotation;b.localScale=bone.localScale;
                }
                var missing=src.bones.Where(b=>b==null||!map.ContainsKey(b.name)).Select(b=>b==null?"<null>":b.name).ToArray();
                if(missing.Length>0)throw new InvalidOperationException(g.File+" uses bones the character lacks: "+string.Join(", ",missing));
                var child=go.transform.Find(g.Object)?.gameObject;
                if(child==null) {
                    child=new GameObject(g.Object);Undo.RegisterCreatedObjectUndo(child,"Add "+g.Object);
                    child.transform.SetParent(go.transform,false);
                }
                Undo.RecordObject(child.transform,"Place "+g.Object);
                child.transform.localPosition=src.transform.localPosition;child.transform.localRotation=src.transform.localRotation;child.transform.localScale=src.transform.localScale;
                var smr=Ensure<SkinnedMeshRenderer>(child);
                Undo.RecordObject(smr,"Bind "+g.Object);
                smr.sharedMesh=src.sharedMesh;
                smr.bones=src.bones.Select(b=>map[b.name]).ToArray();
                smr.rootBone=src.rootBone!=null&&map.TryGetValue(src.rootBone.name,out var root)?root:map["CC_Base_Hip"];
                smr.sharedMaterials=g.Materials.Select(n=>materials.TryGetValue(n,out var m)?m:throw new InvalidOperationException("No material slot "+n)).ToArray();
                smr.localBounds=src.localBounds;smr.updateWhenOffscreen=false;smr.skinnedMotionVectors=false;
                var garment=Ensure<CompanionGarment>(child);
                Undo.RecordObject(garment,"Garment "+g.Id);
                garment.id=g.Id;garment.displayName=g.DisplayName;garment.category=spec.Category;garment.bodyFamily=spec.BodyFamily;garment.slot=g.Slot;garment.hides=g.Hides.ToArray();
                var chainsPath=GarmentChainsPath(spec,g);
                garment.chains=chainsPath==null?Array.Empty<string>():JsonUtility.FromJson<Rig>(AssetDatabase.LoadAssetAtPath<TextAsset>(chainsPath).text).chains.Select(c=>c.name).ToArray();
                EditorUtility.SetDirty(garment);
                child.SetActive(signature.Contains(g.Id));
            }
        }

        // The rig description is authored in Blender space. The axis mapping is chosen by matching the
        // exported bone heads against the imported bind poses, then verified to millimetre precision.
        // Garment chain files (<File>.item.json) add their chains with the same mapping, plus share joints
        // (helper bones that follow part of a body bone's rotation), listed once even when garments repeat them.
        public static void ConfigureSprings(CompanionSecondaryMotion motion,Transform model,string rigJson,params string[] garmentChains)
        {
            var rig=JsonUtility.FromJson<Rig>(AssetDatabase.LoadAssetAtPath<TextAsset>(rigJson).text);
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
            var chains=rig.chains.ToList();var shares=(rig.shares??Array.Empty<Share>()).ToList();
            foreach(var path in garmentChains) {
                var item=JsonUtility.FromJson<Rig>(AssetDatabase.LoadAssetAtPath<TextAsset>(path).text);
                chains.AddRange(item.chains);if(item.shares!=null)shares.AddRange(item.shares);
            }
            motion.chains=chains.Select(c=>new CompanionSecondaryMotion.Chain{name=c.name,bones=c.bones,tip=V(c.tip),stiffness=c.stiffness,drag=c.drag,gravity=c.gravity,radius=c.radius}).ToArray();
            motion.colliders=rig.colliders.Select(c=>new CompanionSecondaryMotion.BodyCollider{bone=c.bone,center=V(c.center),tail=V(c.tail),capsule=c.capsule,radius=c.radius}).ToArray();
            motion.shares=shares.GroupBy(s=>s.bone).Select(g=>g.First()).Select(s=>new CompanionSecondaryMotion.Share{bone=s.bone,source=s.source,amount=s.amount}).ToArray();
            motion.gravityDirection=Vector3.down;
            EditorUtility.SetDirty(motion);
        }
    }
}
