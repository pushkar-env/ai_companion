using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Companion.Presentation;
using UnityEditor;
using UnityEngine;

namespace Companion.Editor
{
    public static class ExpressiveIdleReview
    {
        public static void Run()
        {
            if(EditorApplication.isPlaying)throw new Exception("Run isolated review while stopped");
            var app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>();
            var model=UnityEngine.Object.Instantiate(app.character.gameObject);model.hideFlags=HideFlags.HideAndDontSave;
            var wardrobe=new CompanionWardrobe(model.transform,false);var idle=new CompanionBodyIdle(model.transform);var gaze=new CompanionGaze(model.transform);
            foreach(var t in model.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
            var go=new GameObject("Gesture review camera"){hideFlags=HideFlags.HideAndDontSave};var camera=go.AddComponent<Camera>();camera.CopyFrom(app.portraitCamera);camera.enabled=false;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.055f,.065f,.07f,1);
            var target=new RenderTexture(600,900,24){antiAliasing=4};target.Create();camera.targetTexture=target;camera.aspect=2f/3;camera.fieldOfView=30;
            var image=new Texture2D(600,900,TextureFormat.RGB24,false);
            string folder=Path.GetFullPath(Application.dataPath+"/../../../docs/evidence/expressive-idle");Directory.CreateDirectory(folder);
            var bones=model.GetComponentsInChildren<Transform>().ToDictionary(t=>t.name);var jaw=bones["CC_Base_JawRoot"];var head=bones["CC_Base_Head"];var jawRest=jaw.localRotation;
            try {
                foreach(var gesture in new[]{CompanionBodyIdle.Gesture.FootAdjust,CompanionBodyIdle.Gesture.HipTurn,CompanionBodyIdle.Gesture.ShoulderRoll,CompanionBodyIdle.Gesture.SideStretch,CompanionBodyIdle.Gesture.Yawn})
                for(int outfit=0;outfit<5;outfit++) {
                    wardrobe.Apply(new CompanionWardrobe.Look{outfit=outfit==0?0:1,top=(outfit-1)/2,bottom=(outfit-1)%2,topColor=outfit==0?0:1});
                    foreach(var view in new[]{"front","side","back"}) {
                        idle.SampleGesture(3,gesture,gesture==CompanionBodyIdle.Gesture.FootAdjust?.27f:.5f);gaze.Sample(0,1,true,false);
                        float y=idle.YawnWeight;jaw.localRotation=jawRest*Quaternion.Euler(0,0,-16*y*.72f);head.rotation=Quaternion.AngleAxis(-6*y,model.transform.right)*head.rotation;
                        foreach(var r in model.GetComponentsInChildren<SkinnedMeshRenderer>())foreach(var name in new[]{"V_Open","Eye_Blink_L","Eye_Blink_R","C_BlinkL","C_BlinkR"}) {int index=r.sharedMesh.GetBlendShapeIndex(name);if(index>=0)r.SetBlendShapeWeight(index,y*(name=="V_Open"?48:85));}
                        var focus=model.transform.position+Vector3.up*1.06f;var dir=view=="front"?Vector3.forward:view=="back"?Vector3.back:Vector3.right;
                        camera.transform.position=focus+dir*4.4f;camera.transform.LookAt(focus);
                        Capture(camera,model,target,image,Path.Combine(folder,$"{gesture}-{outfit}-{view}.png"));
                    }
                }
            }finally{gaze.Dispose();idle.Dispose();wardrobe.Dispose();UnityEngine.Object.DestroyImmediate(model);UnityEngine.Object.DestroyImmediate(go);target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);}
        }
        public static void Capture(Camera camera,GameObject model,RenderTexture target,Texture2D image,string path)
        {
            var baked=new List<GameObject>();var renderers=model.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r=>r.enabled).ToArray();
            try {
                foreach(var r in renderers){var mesh=new Mesh();r.BakeMesh(mesh);var item=new GameObject("Baked gesture"){hideFlags=HideFlags.HideAndDontSave,layer=31};item.transform.SetPositionAndRotation(r.transform.position,r.transform.rotation);item.transform.localScale=r.transform.lossyScale;item.AddComponent<MeshFilter>().sharedMesh=mesh;item.AddComponent<MeshRenderer>().sharedMaterials=r.sharedMaterials;r.enabled=false;baked.Add(item);}
                camera.Render();var old=RenderTexture.active;RenderTexture.active=target;try{image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();}finally{RenderTexture.active=old;}File.WriteAllBytes(path,image.EncodeToPNG());
            }finally{foreach(var item in baked){UnityEngine.Object.DestroyImmediate(item.GetComponent<MeshFilter>().sharedMesh);UnityEngine.Object.DestroyImmediate(item);}foreach(var r in renderers)r.enabled=true;}
        }
    }
}
