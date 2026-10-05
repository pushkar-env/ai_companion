using System;
using System.IO;
using System.Linq;
using Companion.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Companion.Editor
{
    // Converts the supplied CC5 material manifests without modifying their source FBXs.
    public static class CharacterRosterSetup
    {
        [Serializable] class Map { public Entry[] materials; }
        [Serializable] class Entry { public string mesh,name,baseMap,normal,opacity; public float[] color; }
        static string Resolve(string root,string path)
        {
            if(string.IsNullOrEmpty(path))return null;
            string result=path.Contains("/")?root+"/"+path.TrimStart('.','/'):root+"/EmbeddedTextures/"+path;
            if(!File.Exists(result))throw new FileNotFoundException("Missing character texture",result);
            return result;
        }
        static Material MaterialFor(string root,Entry entry)
        {
            string path=root+"/Materials/"+entry.name+".mat";
            var existing=AssetDatabase.LoadAssetAtPath<Material>(path);if(existing!=null)return existing;
            var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=entry.name};
            var c=entry.color;mat.SetColor("_BaseColor",c!=null&&c.Length==3?new Color(c[0]/255,c[1]/255,c[2]/255):Color.white);
            mat.SetFloat("_Metallic",0);mat.SetFloat("_Smoothness",.22f);
            string diffuse=Resolve(root,entry.baseMap),opacity=Resolve(root,entry.opacity);
            // Most CC exports pack alpha in the diffuse PNG. Separate masks need packing.
            if(opacity!=null&&opacity!=diffuse) {
                var mask=new Texture2D(2,2);mask.LoadImage(File.ReadAllBytes(opacity));
                var pixels=new Texture2D(2,2);
                if(diffuse!=null)pixels.LoadImage(File.ReadAllBytes(diffuse));
                else {UnityEngine.Object.DestroyImmediate(pixels);pixels=new Texture2D(mask.width,mask.height);var white=Enumerable.Repeat(Color.white,mask.width*mask.height).ToArray();pixels.SetPixels(white);}
                var colors=pixels.GetPixels();for(int y=0;y<pixels.height;y++)for(int x=0;x<pixels.width;x++)colors[y*pixels.width+x].a*=mask.GetPixelBilinear((x+.5f)/pixels.width,(y+.5f)/pixels.height).r;
                pixels.SetPixels(colors);pixels.Apply();diffuse=root+"/Materials/"+entry.name+"_RGBA.png";File.WriteAllBytes(diffuse,pixels.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(mask);UnityEngine.Object.DestroyImmediate(pixels);AssetDatabase.ImportAsset(diffuse);
            }
            if(diffuse!=null)mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(diffuse));
            string normal=Resolve(root,entry.normal);
            if(normal!=null){var importer=(TextureImporter)AssetImporter.GetAtPath(normal);if(importer.textureType!=TextureImporterType.NormalMap){importer.textureType=TextureImporterType.NormalMap;importer.SaveAndReimport();}mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normal));mat.EnableKeyword("_NORMALMAP");}
            if(opacity!=null){mat.SetFloat("_AlphaClip",1);mat.SetFloat("_Cutoff",.35f);mat.SetFloat("_Cull",0);mat.EnableKeyword("_ALPHATEST_ON");mat.SetOverrideTag("RenderType","TransparentCutout");mat.renderQueue=2450;}
            // Procedural CC eye occlusion has no baked shadow map; hide that overlay until a dedicated shader is supplied.
            if(entry.mesh=="CC_Base_EyeOcclusion"&&string.IsNullOrEmpty(entry.baseMap))mat.SetColor("_BaseColor",new Color(0,0,0,0));
            AssetDatabase.CreateAsset(mat,path);return mat;
        }
        [MenuItem("Companion/Add Supplied Characters to Talking Scene")]
        public static void Run()
        {
            var scene=SceneManager.GetActiveScene();
            if(EditorApplication.isPlaying||scene.path!=TalkingCharacterSetup.ScenePath||scene.isDirty)throw new InvalidOperationException("Open the saved TalkingCompanion scene outside Play mode first.");
            var app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>();
            if(app.characters!=null&&app.characters.Length>1)throw new InvalidOperationException("Roster already exists; preserving it.");
            var roster=new System.Collections.Generic.List<TalkingCharacter.CharacterOption>{new TalkingCharacter.CharacterOption{name="Original",model=app.character}};
            foreach(string name in new[]{"Alita","Cosmos"}) {
                string root="Assets/Companion/Imported/"+name;Directory.CreateDirectory(root+"/Materials");AssetDatabase.Refresh();
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(root+"/"+name.ToLowerInvariant()+".Fbx");
                if(!TalkingCharacter.CanAnimate(prefab.transform))throw new InvalidOperationException(name+" lacks required speech controls");
                var map=JsonUtility.FromJson<Map>(File.ReadAllText(root+"/material-map.json"));
                // Prepare materials before changing the scene.
                var materials=map.materials.ToDictionary(e=>e.mesh+"/"+e.name,e=>MaterialFor(root,e));
                var model=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);Undo.RegisterCreatedObjectUndo(model,"Add "+name);model.name=name;
                var animator=model.GetComponent<Animator>();if(animator!=null)animator.enabled=false;
                model.transform.SetPositionAndRotation(app.character.position,app.character.rotation);model.transform.localScale=app.character.localScale;
                foreach(var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                    renderer.updateWhenOffscreen=true;
                    renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>materials[renderer.name+"/"+m.name]).ToArray();
                }
                model.SetActive(false);roster.Add(new TalkingCharacter.CharacterOption{name=name,model=model.transform,portraitDistance=name=="Cosmos"?.95f:1.25f});
            }
            Undo.RecordObject(app,"Set character roster");app.characters=roster.ToArray();EditorUtility.SetDirty(app);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            Debug.Log("Original, Alita and Cosmos ready in TalkingCompanion.");
        }
    }
}
