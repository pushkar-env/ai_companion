using System;
using System.IO;
using System.Collections.Generic;
using Companion.Presentation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Companion.Editor
{
    public static class CCCharacterLabSetup
    {
        public const string ScenePath="Assets/Companion/Scenes/CCCharacterTest.unity";
        const string Imported="Assets/Companion/Imported/CC5Inspection";
        [MenuItem("Companion/Open CC Character Test")]
        public static void Open()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            if(!File.Exists(ScenePath))Create();EditorSceneManager.OpenScene(ScenePath);
        }
        public static void Create()
        {
            if(File.Exists(ScenePath))throw new InvalidOperationException("Existing scene preserved. Open it instead.");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Imported+"/femaleCC.Fbx");if(prefab==null)throw new InvalidOperationException("Inspection FBX not found");
            string materialsDir="Assets/Companion/Diagnostics/CCMaterials";Directory.CreateDirectory(materialsDir);AssetDatabase.Refresh();
            var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            try {
                var character=UnityEngine.Object.Instantiate(prefab);character.name="CC exported test character - not optimized";var animator=character.GetComponent<Animator>();if(animator!=null)animator.enabled=false;
                Transform head=null;foreach(var t in character.GetComponentsInChildren<Transform>())if(t.name=="CC_Base_Head")head=t;
                var cache=new Dictionary<string,Material>();
                foreach(var renderer in character.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                    renderer.updateWhenOffscreen=true;var materials=renderer.sharedMaterials;
                    for(int i=0;i<materials.Length;i++) {
                        string name=materials[i].name;
                        if(!cache.TryGetValue(name,out var mat)) {
                            string path=materialsDir+"/"+name+".mat";mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                            if(mat==null) {
                                mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_Metallic",0);mat.SetFloat("_Smoothness",.22f);
                                foreach(var file in Directory.GetFiles(Imported+"/EmbeddedTextures",name+"_Diffuse.*"))if(!file.EndsWith(".meta")){mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(file));break;}
                                foreach(var file in Directory.GetFiles(Imported+"/EmbeddedTextures",name+"_Normal.*"))if(!file.EndsWith(".meta")) {
                                    var ti=(TextureImporter)AssetImporter.GetAtPath(file);if(ti.textureType!=TextureImporterType.NormalMap){ti.textureType=TextureImporterType.NormalMap;ti.SaveAndReimport();}
                                    mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(file));mat.EnableKeyword("_NORMALMAP");break;
                                }
                                bool alpha=name.Contains("Transparency")||name.Contains("Tail")||name.Contains("Eyelash")||name.Contains("Tear")||name.Contains("Occlusion")||name.Contains("Cornea");
                                if(alpha){mat.SetFloat("_AlphaClip",1);mat.SetFloat("_Cutoff",.35f);mat.EnableKeyword("_ALPHATEST_ON");mat.SetOverrideTag("RenderType","TransparentCutout");mat.renderQueue=2450;mat.SetFloat("_Cull",0);}
                                AssetDatabase.CreateAsset(mat,path);
                            }cache[name]=mat;
                        }materials[i]=mat;
                    }renderer.sharedMaterials=materials;
                }
                RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.65f,.7f);
                var key=new GameObject("Soft key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.2f;key.transform.rotation=Quaternion.Euler(30,-25,0);key.shadows=LightShadows.None;
                var fill=new GameObject("Fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.5f;fill.transform.rotation=Quaternion.Euler(15,140,0);fill.shadows=LightShadows.None;
                var camera=new GameObject("Character portrait camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.075f,.10f,.15f);camera.fieldOfView=30;camera.nearClipPlane=.01f;camera.farClipPlane=20;
                var focus=head.position+new Vector3(0,.035f,0);camera.transform.position=focus+new Vector3(0,0,1.25f);camera.transform.LookAt(focus);camera.gameObject.AddComponent<AudioListener>();
                var ui=new GameObject("CC test controls - development only");ui.AddComponent<UIDocument>().panelSettings=AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Companion/Settings/MockPanel.asset");ui.AddComponent<AudioSource>().playOnAwake=false;
                var lab=ui.AddComponent<CCCharacterLab>();lab.character=character.transform;lab.portraitCamera=camera;
                EditorSceneManager.SaveScene(scene,ScenePath);
            } finally {if(previous.IsValid())SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true);}
        }
    }
    // Checks the actual processed scene, including explicit BuildPipeline scene lists.
    public sealed class CCCharacterLabBuildGuard : IProcessSceneWithReport
    {
        public int callbackOrder=>0;
        public void OnProcessScene(Scene scene,BuildReport report)
        {
            if(report!=null&&scene.path==CCCharacterLabSetup.ScenePath&&(report.summary.options&BuildOptions.Development)==0)
                throw new BuildFailedException("CC character test scene is development-only; production calibration and rights are not approved.");
        }
    }
}
