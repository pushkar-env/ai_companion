using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Companion.Presentation;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace Companion.Editor
{
    public static class AlitaRuntimeChecks
    {
        static readonly List<string> lines=new List<string>();
        static void Check(bool ok,string name){if(!ok)throw new Exception(name);lines.Add("PASS "+name);}
        [MenuItem("Companion/Check Alita Runtime Meshes")]
        public static void Meshes()
        {
            lines.Clear();try {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Companion/Imported/Alita/alita.Fbx");
                var app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>();
                Check(app.characters.Length==1&&app.characters[0].name=="Alita"&&app.character==app.characters[0].model,"Alita is the only scene character and default");
                var dependencies=AssetDatabase.GetDependencies(TalkingCharacterSetup.ScenePath,true).Concat(AssetDatabase.GetDependencies(CCCharacterLabSetup.ScenePath,true));
                Check(!dependencies.Any(p=>p.Contains("CC5Inspection")||p.Contains("Cosmos")||p.Contains("Diagnostics/CCMaterials")),"talking and diagnostic scenes have no removed-character dependency");
                foreach(var runtime in app.character.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                    var source=prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name==runtime.name).sharedMesh;var mesh=runtime.sharedMesh;
                    Check(source.vertices.SequenceEqual(mesh.vertices)&&source.triangles.SequenceEqual(mesh.triangles)&&source.normals.SequenceEqual(mesh.normals)&&source.tangents.SequenceEqual(mesh.tangents)&&source.uv.SequenceEqual(mesh.uv)&&source.boneWeights.SequenceEqual(mesh.boneWeights)&&source.bindposes.SequenceEqual(mesh.bindposes),runtime.name+" retains exact base geometry, UVs, normals, tangents and skinning");
                    int frames=0;var a=new Vector3[source.vertexCount];var b=new Vector3[source.vertexCount];var an=new Vector3[source.vertexCount];var bn=new Vector3[source.vertexCount];var at=new Vector3[source.vertexCount];var bt=new Vector3[source.vertexCount];
                    for(int i=0;i<mesh.blendShapeCount;i++) {
                        int original=source.GetBlendShapeIndex(mesh.GetBlendShapeName(i));Check(original>=0&&source.GetBlendShapeFrameCount(original)==mesh.GetBlendShapeFrameCount(i),runtime.name+" frame count: "+mesh.GetBlendShapeName(i));
                        for(int f=0;f<mesh.GetBlendShapeFrameCount(i);f++) {
                            source.GetBlendShapeFrameVertices(original,f,a,an,at);mesh.GetBlendShapeFrameVertices(i,f,b,bn,bt);
                            if(!a.SequenceEqual(b)||!an.SequenceEqual(bn)||!at.SequenceEqual(bt)||source.GetBlendShapeFrameWeight(original,f)!=mesh.GetBlendShapeFrameWeight(i,f))throw new Exception("Retained morph changed");frames++;
                        }
                    }
                    Check(true,runtime.name+" "+frames+" retained delta frames exactly match source");
                    Check(runtime.sharedMaterials.All(m=>m!=null&&m.shader!=null),runtime.name+" material references valid");
                }
                Check(TalkingCharacter.CanAnimate(app.character),"all ten speech channels and required bones remain");
            }catch(Exception e){lines.Add("FAIL "+e.Message);throw;}finally{Save("mesh-checks.txt");}
        }
        [MenuItem("Companion/Check Alita Portrait and Face")]
        public static void Face()
        {
            lines.Clear();try {
                if(!EditorApplication.isPlaying)throw new Exception("Enter Play first");
                var app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>();app.NewChat();
                var root=app.GetComponent<UIDocument>().rootVisualElement;
                Check(root.Q("character-picker")==null&&root.Q<Label>("character-title").text=="Alita","single-character header has no stale selector");
                Check(Screen.height>Screen.width&&root.Q("chat-actions").worldBound.yMax<=root.worldBound.yMax,"all portrait chat controls fit");
                var body=app.character.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name=="CC_Base_Body");
                var shape=typeof(TalkingCharacter).GetMethod("Shape",BindingFlags.Instance|BindingFlags.NonPublic);var reset=typeof(TalkingCharacter).GetMethod("ResetFace",BindingFlags.Instance|BindingFlags.NonPublic);
                foreach(string channel in new[]{"V_Open","Eye_Blink_L","Mouth_Corner_Pull_L","Brow_Raise_In_L","Mouth_Corner_Depress_L","Eye_Widen_L"}) {
                    var before=new Mesh();var after=new Mesh();body.BakeMesh(before);shape.Invoke(app,new object[]{channel,.5f});body.BakeMesh(after);
                    var a=before.vertices;var b=after.vertices;Check(a.Where((v,i)=>(v-b[i]).sqrMagnitude>1e-12f).Any(),channel+" actually deforms retained mesh");
                    reset.Invoke(app,null);Check(app.ShapeWeight(channel)==0,channel+" returns to neutral immediately");UnityEngine.Object.DestroyImmediate(before);UnityEngine.Object.DestroyImmediate(after);
                }
                root.Q<TextField>("message-input").value="Keep this draft";Check(!app.SelectCharacter(1)&&app.Draft=="Keep this draft"&&app.character.name=="Alita","removed selection rejected without changing draft");app.NewChat();
                ScreenCapture.CaptureScreenshot(Path.Combine(Folder,"polished.png"));
            }catch(Exception e){lines.Add("FAIL "+e.Message);throw;}finally{Save("face-checks.txt");}
        }
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/evidence/m1/alita-polish"));
        static void Save(string name){Directory.CreateDirectory(Folder);File.WriteAllLines(Path.Combine(Folder,name),lines);}
    }
}
