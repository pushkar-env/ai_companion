using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Companion.Core;
using Companion.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Companion.Editor
{
    public static class AlitaPolishSetup
    {
        const string Root="Assets/Companion/Imported/Alita";
        static readonly HashSet<string> FaceNames=new HashSet<string>(SpeechMouthMotion.Channels.Concat(new[]{
            "Eye_Blink_L","Eye_Blink_R","C_BlinkL","C_BlinkR","Mouth_Smile_L","Mouth_Smile_R",
            "Mouth_Frown_L","Mouth_Frown_R","Brow_Raise_Inner_L","Brow_Raise_Inner_R",
            "Brow_Raise_Outer_L","Brow_Raise_Outer_R","Eye_Wide_L","Eye_Wide_R"}));
        public static void Run()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first");
            if(Directory.Exists(Root+"/RuntimeMeshes"))throw new InvalidOperationException("Alita polish already applied; preserving generated meshes and scene.");
            var scene=SceneManager.GetActiveScene();if(scene.path!=TalkingCharacterSetup.ScenePath)throw new InvalidOperationException("Open TalkingCompanion");
            var app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>();
            var alita=app.characters.First(c=>c.name=="Alita");
            string archive=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../models/archived-unity/Scenes"));Directory.CreateDirectory(archive);
            foreach(var path in new[]{TalkingCharacterSetup.ScenePath,CCCharacterLabSetup.ScenePath}) {
                var backup=Path.Combine(archive,Path.GetFileName(path));if(!File.Exists(backup)){File.Copy(path,backup);File.Copy(path+".meta",backup+".meta");}
            }
            // Convert the existing diagnostic scene before removing its source model dependency.
            var labScene=EditorSceneManager.OpenScene(CCCharacterLabSetup.ScenePath,OpenSceneMode.Additive);
            try {
                var lab=labScene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CCCharacterLab>(true)).Single();
                var old=lab.character.gameObject;var copy=UnityEngine.Object.Instantiate(alita.model.gameObject);copy.name="Alita diagnostic character";
                SceneManager.MoveGameObjectToScene(copy,labScene);copy.SetActive(true);lab.character=copy.transform;UnityEngine.Object.DestroyImmediate(old);
                var head=copy.GetComponentsInChildren<Transform>().First(t=>t.name=="CC_Base_Head");var focus=head.position+new Vector3(0,.035f,0);lab.portraitCamera.transform.position=focus+new Vector3(0,0,1.15f);lab.portraitCamera.transform.LookAt(focus);
                EditorSceneManager.SaveScene(labScene);
            }finally{EditorSceneManager.CloseScene(labScene,true);}
            foreach(var option in app.characters)if(option.model!=alita.model)Undo.DestroyObjectImmediate(option.model.gameObject);
            Undo.RecordObject(app,"Keep Alita only");app.character=alita.model;app.characters=new[]{alita};alita.portraitDistance=1.15f;alita.model.gameObject.SetActive(true);
            Directory.CreateDirectory(Root+"/RuntimeMeshes");AssetDatabase.Refresh();
            var evidence=new List<string>();long sourceBytes=0,reducedBytes=0;
            foreach(var renderer in alita.model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                renderer.updateWhenOffscreen=false;var source=renderer.sharedMesh;
                sourceBytes+=UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(source);
                if(source.blendShapeCount==0){reducedBytes+=UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(source);continue;}
                string path=Root+"/RuntimeMeshes/"+renderer.name+".asset";
                if(File.Exists(path))throw new InvalidOperationException("Runtime subset already exists; preserve it");
                var reduced=UnityEngine.Object.Instantiate(source);reduced.name=source.name+"_TalkingSubset";reduced.ClearBlendShapes();
                var v=new Vector3[source.vertexCount];var n=new Vector3[source.vertexCount];var t=new Vector3[source.vertexCount];
                for(int i=0;i<source.blendShapeCount;i++)if(FaceNames.Contains(source.GetBlendShapeName(i)))
                    for(int f=0;f<source.GetBlendShapeFrameCount(i);f++){source.GetBlendShapeFrameVertices(i,f,v,n,t);reduced.AddBlendShapeFrame(source.GetBlendShapeName(i),source.GetBlendShapeFrameWeight(i,f),v,n,t);}
                // Exact clone geometry and exact retained delta arrays: no decimation or retargeting.
                AssetDatabase.CreateAsset(reduced,path);renderer.sharedMesh=reduced;
                reducedBytes+=UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(reduced);
                evidence.Add(renderer.name+": "+source.blendShapeCount+" -> "+reduced.blendShapeCount+" channels");
            }
            evidence.Add("Alita source mesh bytes="+sourceBytes+" reduced mesh bytes="+reducedBytes);
            foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{Root+"/Materials"})) {
                var mat=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                bool hair=mat.name.Contains("Hair")||mat.name.Contains("Scalp");bool skin=mat.name.Contains("Skin");
                if(hair){mat.SetFloat("_Cutoff",.20f);mat.SetFloat("_Smoothness",.4f);mat.SetFloat("_BumpScale",.55f);mat.SetFloat("_AlphaToMask",1);}
                if(skin){mat.SetFloat("_Smoothness",.32f);mat.SetFloat("_BumpScale",.5f);}
                if(mat.name.Contains("Cornea"))mat.SetFloat("_Smoothness",.7f);
                EditorUtility.SetDirty(mat);
            }
            var paths=alita.model.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r=>r.sharedMaterials).Distinct().SelectMany(m=>m.GetTexturePropertyNames().Select(p=>m.GetTexture(p))).Where(t=>t!=null).Select(AssetDatabase.GetAssetPath).Distinct();
            foreach(var path in paths) {
                var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer==null)continue;
                bool hero=path.Contains("Skin_Head")||path.Contains("Hair_Transparency");
                importer.maxTextureSize=hero?(importer.textureType==TextureImporterType.NormalMap?1024:2048):512;
                importer.mipmapEnabled=true;importer.anisoLevel=4;importer.isReadable=false;importer.textureCompression=TextureImporterCompression.Compressed;importer.SaveAndReimport();
            }
            RenderSettings.ambientLight=new Color(.45f,.48f,.54f);
            foreach(var light in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>())) {
                if(light.name=="Soft key"){light.intensity=1.1f;light.color=new Color(1,.91f,.82f);}
                else if(light.name=="Fill"){light.intensity=.65f;light.color=new Color(.75f,.86f,1);}
            }
            app.portraitCamera.backgroundColor=new Color(.045f,.075f,.095f);app.portraitCamera.allowMSAA=true;
            var h=alita.model.GetComponentsInChildren<Transform>().First(t=>t.name=="CC_Base_Head");var center=h.position+new Vector3(0,.035f,0);app.portraitCamera.transform.position=center+new Vector3(0,0,1.15f);app.portraitCamera.transform.LookAt(center);
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            var dir=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/evidence/m1/alita-polish"));Directory.CreateDirectory(dir);File.WriteAllLines(Path.Combine(dir,"mesh-reduction.txt"),evidence);
        }
    }
}
