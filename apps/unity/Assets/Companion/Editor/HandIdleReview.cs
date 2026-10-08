using System;
using System.IO;
using System.Linq;
using Companion.Presentation;
using UnityEditor;
using UnityEngine;

namespace Companion.Editor
{
    public static class HandIdleReview
    {
        [MenuItem("Companion/Capture Idle Hand Review")]
        public static void Run()
        {
            if(EditorApplication.isPlaying)throw new Exception("Run isolated review while stopped");
            var app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>();
            var model=UnityEngine.Object.Instantiate(app.character.gameObject);model.hideFlags=HideFlags.HideAndDontSave;
            foreach(var t in model.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
            var go=new GameObject("Idle review camera"){hideFlags=HideFlags.HideAndDontSave};var camera=go.AddComponent<Camera>();camera.CopyFrom(app.portraitCamera);camera.enabled=false;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.055f,.065f,.07f,1);
            var target=new RenderTexture(800,1000,24){antiAliasing=4};target.Create();camera.targetTexture=target;camera.aspect=.8f;camera.fieldOfView=25;
            var image=new Texture2D(800,1000,TextureFormat.RGB24,false);var idle=new CompanionBodyIdle(model.transform);
            var bones=model.GetComponentsInChildren<Transform>().ToDictionary(t=>t.name);
            var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/evidence/hand-idle"));Directory.CreateDirectory(folder);
            try {
                foreach(var time in new[]{0,6,12,18}) {
                    idle.Sample(time);
                    foreach(var side in new[]{"L","R"})foreach(var view in new[]{"front","side"}) {
                        Vector3 focus=bones["CC_Base_"+side+"_Hand"].position+Vector3.down*.045f;
                        var direction=view=="front"?new Vector3(side=="L"?-.2f:.2f,0,1):new Vector3(side=="L"?-1:1,.12f,.2f);
                        camera.transform.position=focus+direction.normalized*.85f;camera.transform.LookAt(focus);
                        camera.Render();var old=RenderTexture.active;RenderTexture.active=target;image.ReadPixels(new Rect(0,0,800,1000),0,0);image.Apply();RenderTexture.active=old;
                        File.WriteAllBytes(Path.Combine(folder,side+"-"+view+"-"+time+".png"),image.EncodeToPNG());
                    }
                }
                // Diagnostic motion sequence, sampled from exactly the same runtime pose function.
                string frames=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../artifacts/hand-idle"));Directory.CreateDirectory(frames);
                target.Release();target.width=400;target.height=500;target.Create();UnityEngine.Object.DestroyImmediate(image);image=new Texture2D(400,500,TextureFormat.RGB24,false);
                for(int frame=0;frame<288;frame++) {
                    idle.Sample(frame/12f);
                    foreach(var view in new[]{"front","side"}) {
                        var side=view=="front"?"L":"R";
                        Vector3 focus=bones["CC_Base_"+side+"_Hand"].position+Vector3.down*.045f;
                        var direction=view=="front"?new Vector3(-.2f,0,1):new Vector3(1,.12f,.2f);
                        camera.transform.position=focus+direction.normalized*.85f;camera.transform.LookAt(focus);camera.Render();
                        var old=RenderTexture.active;RenderTexture.active=target;image.ReadPixels(new Rect(0,0,400,500),0,0);image.Apply();RenderTexture.active=old;
                        File.WriteAllBytes(Path.Combine(frames,view+"-"+frame.ToString("000")+".png"),image.EncodeToPNG());
                    }
                }
            }finally{idle.Dispose();UnityEngine.Object.DestroyImmediate(model);UnityEngine.Object.DestroyImmediate(go);target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);}
        }
    }
}
