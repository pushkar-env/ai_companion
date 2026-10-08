using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Companion.Presentation;
using UnityEditor;
using UnityEngine;

namespace Companion.Editor
{
    public static class WardrobeReview
    {
        public static void Run()
        {
            if(EditorApplication.isPlaying)throw new Exception("Review requires stopped Editor");
            var app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>();
            var model=UnityEngine.Object.Instantiate(app.character.gameObject);model.hideFlags=HideFlags.HideAndDontSave;
            var wardrobe=new CompanionWardrobe(model.transform,false);var idle=new CompanionBodyIdle(model.transform);var gaze=new CompanionGaze(model.transform);
            foreach(var t in model.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
            var go=new GameObject("Wardrobe review camera"){hideFlags=HideFlags.HideAndDontSave};var camera=go.AddComponent<Camera>();camera.CopyFrom(app.portraitCamera);camera.enabled=false;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.055f,.065f,.07f,1);
            var target=new RenderTexture(600,900,24){antiAliasing=4};target.Create();camera.targetTexture=target;camera.aspect=2f/3;camera.fieldOfView=28;
            var image=new Texture2D(600,900,TextureFormat.RGB24,false);var results=new List<string>();
            string folder=Path.GetFullPath(Application.dataPath+"/../../../docs/evidence/wardrobe");Directory.CreateDirectory(folder);
            try {
                if(!wardrobe.HasSeparates)throw new Exception("Garment assets unavailable");
                for(int top=0;top<2;top++)for(int bottom=0;bottom<2;bottom++) {
                    wardrobe.Apply(new CompanionWardrobe.Look {outfit=1,top=top,bottom=bottom,topColor=1});
                    foreach(var time in new[]{0,7,20})foreach(var view in new[]{"front","side","back"}) {
                        idle.Sample(time);gaze.Sample(time,1,false,false);
                        var focus=model.transform.position+Vector3.up*.9f;var dir=view=="front"?Vector3.forward:view=="back"?Vector3.back:Vector3.right;
                        camera.transform.position=focus+dir*4.1f;camera.transform.LookAt(focus);
                        var baked=new List<GameObject>();var renderers=model.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r=>r.enabled).ToArray();
                        foreach(var r in renderers){var mesh=new Mesh();r.BakeMesh(mesh);var item=new GameObject("Baked review"){hideFlags=HideFlags.HideAndDontSave,layer=31};item.transform.SetPositionAndRotation(r.transform.position,r.transform.rotation);item.transform.localScale=r.transform.lossyScale;item.AddComponent<MeshFilter>().sharedMesh=mesh;item.AddComponent<MeshRenderer>().sharedMaterials=r.sharedMaterials;r.enabled=false;baked.Add(item);}
                        camera.Render();
                        foreach(var item in baked){UnityEngine.Object.DestroyImmediate(item.GetComponent<MeshFilter>().sharedMesh);UnityEngine.Object.DestroyImmediate(item);}foreach(var r in renderers)r.enabled=true;
                        var previous=RenderTexture.active;RenderTexture.active=target;image.ReadPixels(new Rect(0,0,600,900),0,0);image.Apply();RenderTexture.active=previous;
                        File.WriteAllBytes(Path.Combine(folder,$"outfit-{top}-{bottom}-{view}-{time}.png"),image.EncodeToPNG());
                    }
                }
                for(int i=0;i<50;i++)wardrobe.Apply(new CompanionWardrobe.Look{outfit=i%2,top=i%2,bottom=(i/2)%2});
                results.Add("PASS 50 outfit switches reuse four garment renderers");
                if(model.GetComponentsInChildren<SkinnedMeshRenderer>().Count(r=>r.name.EndsWith(" wardrobe"))!=4)throw new Exception("Garment leak");
                var h=model.GetComponentsInChildren<Transform>().First(t=>t.name=="CC_Base_Head");gaze.Sample(7,1,false,false);var glance=h.localRotation;gaze.Sample(7,1,true,false);if(Quaternion.Angle(glance,h.localRotation)<3)throw new Exception("Attention not restored");results.Add("PASS gaze returns toward viewer when engaged");
                gaze.Sample(7,1,false,true);var reduced=h.localRotation;gaze.Sample(20,1,false,true);if(Quaternion.Angle(reduced,h.localRotation)>.001f)throw new Exception("Reduced motion changed gaze");results.Add("PASS reduced motion fixes eye/head pose");
                File.WriteAllLines(Path.Combine(folder,"checks.txt"),results);
            }finally{gaze.Dispose();idle.Dispose();wardrobe.Dispose();UnityEngine.Object.DestroyImmediate(model);UnityEngine.Object.DestroyImmediate(go);target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);}
        }
    }
}
