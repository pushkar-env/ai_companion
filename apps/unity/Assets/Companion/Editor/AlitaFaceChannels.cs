using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Companion.Presentation;
using UnityEditor;
using UnityEngine;

namespace Companion.Editor
{
    // Alita's runtime meshes keep only the facial channels the app drives (AlitaPolishSetup). The face
    // layer's genuine (Duchenne) smile also needs the cheek raise and lower-lid squint, so those four
    // channels are appended here as exact copies of the source FBX frames. Re-runnable; asset GUIDs stay.
    public static class AlitaFaceChannels
    {
        public static readonly string[] Channels={"Cheek_Raise_L","Cheek_Raise_R","Eye_Squint_L","Eye_Squint_R"};
        const string Root="Assets/Companion/Imported/Alita";

        [MenuItem("Companion/Characters/Add Alita Expression Channels")]
        public static void Run()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/alita.Fbx");
            var app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>(FindObjectsInactive.Include);
            var alita=app.characters.First(c=>c.name=="Alita").model;
            var log=new List<string>();
            foreach(var runtime in alita.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                var mesh=runtime.sharedMesh;string path=AssetDatabase.GetAssetPath(mesh);
                if(!path.StartsWith(Root+"/RuntimeMeshes/",StringComparison.Ordinal))continue;
                var source=prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name==runtime.name).sharedMesh;
                var v=new Vector3[source.vertexCount];var n=new Vector3[source.vertexCount];var t=new Vector3[source.vertexCount];int added=0;
                foreach(var name in Channels) {
                    int index=source.GetBlendShapeIndex(name);
                    if(index<0||mesh.GetBlendShapeIndex(name)>=0)continue;
                    for(int f=0;f<source.GetBlendShapeFrameCount(index);f++) {
                        source.GetBlendShapeFrameVertices(index,f,v,n,t);
                        mesh.AddBlendShapeFrame(name,source.GetBlendShapeFrameWeight(index,f),v,n,t);
                    }
                    added++;
                }
                if(added==0)continue;
                EditorUtility.SetDirty(mesh);log.Add(runtime.name+": +"+added+" channels ("+mesh.blendShapeCount+" total)");
            }
            AssetDatabase.SaveAssets();
            Debug.Log(log.Count==0?"Alita expression channels already present":"Alita expression channels: "+string.Join("; ",log));
        }
    }
}
