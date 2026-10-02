// Execute through Unity MCP RunCommand in the existing Editor. No scene is saved.
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        string dir="Assets/Companion/Imported/CC5Inspection";
        string output=Path.GetFullPath("../../docs/evidence/m1/cc5");
        var scene=EditorSceneManager.NewPreviewScene();
        var materials=new List<Material>();var log=new StringBuilder();
        var snapshots=new List<UnityEngine.Mesh>();
        var rt=new RenderTexture(512,640,24);var previous=RenderTexture.active;
        var baked=new UnityEngine.Mesh();var baseline=new UnityEngine.Mesh();
        try {
            var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(dir+"/femaleCC.Fbx"));
            SceneManager.MoveGameObjectToScene(go,scene);
            var animator=go.GetComponent<Animator>();if(animator!=null)animator.enabled=false;
            var renderers=go.GetComponentsInChildren<SkinnedMeshRenderer>();
            SkinnedMeshRenderer body=null;Transform head=null;
            foreach(var t in go.GetComponentsInChildren<Transform>())if(t.name=="CC_Base_Head")head=t;
            foreach(var r in renderers) {
                r.updateWhenOffscreen=true;if(r.name=="CC_Base_Body")body=r;
                var ms=r.sharedMaterials;
                for(int i=0;i<ms.Length;i++) {
                    string name=ms[i].name;var mat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));materials.Add(mat);
                    foreach(var file in Directory.GetFiles(dir+"/EmbeddedTextures",name+"_Diffuse.*")) {
                        if(file.EndsWith(".meta"))continue;
                        mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(file));break;
                    }
                    mat.SetFloat("_AlphaClip",1);mat.SetFloat("_Cutoff",.35f);mat.EnableKeyword("_ALPHATEST_ON");ms[i]=mat;
                }r.sharedMaterials=ms;
            }
            if(body==null||head==null)throw new Exception("Expected body/head absent");
            body.BakeMesh(baseline);var start=baseline.vertices;
            string[] probes={"Eye_Blink_L","Eye_Blink_R","Mouth_Corner_Pull_L","Mouth_Corner_Pull_R","V_Tight_O","V_Explosive","Jaw_Open","V_Open"};
            foreach(var name in probes) {
                int index=body.sharedMesh.GetBlendShapeIndex(name);if(index<0){log.AppendLine("MISSING "+name);continue;}
                body.SetBlendShapeWeight(index,100);body.BakeMesh(baked);var end=baked.vertices;int changed=0;float max=0;
                for(int i=0;i<start.Length;i++){float distance=Vector3.Distance(start[i],end[i]);if(distance>.000001f)changed++;max=Mathf.Max(max,distance);}
                log.AppendLine(name+" changedVertices="+changed+" maxLocalDisplacement="+max.ToString("G6"));body.SetBlendShapeWeight(index,0);
            }
            var cam=new GameObject("Temporary inspection camera").AddComponent<Camera>();SceneManager.MoveGameObjectToScene(cam.gameObject,scene);cam.scene=scene;
            foreach(var r in renderers) {
                var holder=new GameObject("Baked preview "+r.name);holder.transform.SetParent(r.transform,false);
                var mesh=new UnityEngine.Mesh();snapshots.Add(mesh);holder.AddComponent<MeshFilter>().sharedMesh=mesh;
                holder.AddComponent<MeshRenderer>().sharedMaterials=r.sharedMaterials;r.enabled=false;
            }
            cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.1f,.13f,.18f);cam.fieldOfView=30;
            var focus=head.position+new Vector3(0,.06f,0);cam.transform.position=focus+new Vector3(0,0,1.1f);cam.transform.LookAt(focus);cam.nearClipPlane=.01f;cam.farClipPlane=20;cam.targetTexture=rt;
            // First render warms the new preview scene's renderer registration.
            foreach(var pose in new[]{"warmup","neutral","blink","pucker"}) {
                foreach(var r in renderers)for(int i=0;i<r.sharedMesh.blendShapeCount;i++) {
                    string n=r.sharedMesh.GetBlendShapeName(i);
                    r.SetBlendShapeWeight(i,pose=="blink"&&(n=="Eye_Blink_L"||n=="Eye_Blink_R")?100:pose=="pucker"&&n=="V_Tight_O"?100:0);
                }
                for(int i=0;i<renderers.Length;i++)renderers[i].BakeMesh(snapshots[i]);
                cam.Render();if(pose=="warmup")continue;
                RenderTexture.active=rt;var image=new Texture2D(512,640,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,512,640),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,pose+".png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
            }
            File.WriteAllText(Path.Combine(output,"deformation-checks.txt"),log.ToString());result.Log(log.ToString());
        } finally {
            RenderTexture.active=previous;EditorSceneManager.ClosePreviewScene(scene);
            foreach(var m in materials)UnityEngine.Object.DestroyImmediate(m);
            foreach(var m in snapshots)UnityEngine.Object.DestroyImmediate(m);
            rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(baked);UnityEngine.Object.DestroyImmediate(baseline);
        }
    }
}
