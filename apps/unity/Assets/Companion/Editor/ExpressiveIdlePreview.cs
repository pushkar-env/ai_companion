using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Companion.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Companion.Editor
{
    public static class ExpressiveIdlePreview
    {
        static TalkingCharacter app;static CompanionBodyIdle idle;static UIDocument doc;static PanelSettings original,panel;
        static RenderTexture target;static Texture2D image;static int frame,gesture;static double next,begin;static bool started;
        static Transform[] extremities;static string frames,folder;static readonly List<string> checks=new List<string>();static readonly List<double> timestamps=new List<double>();
        static readonly CompanionBodyIdle.Gesture[] Sequence={CompanionBodyIdle.Gesture.FootAdjust,CompanionBodyIdle.Gesture.HipTurn,CompanionBodyIdle.Gesture.ShoulderRoll,CompanionBodyIdle.Gesture.SideStretch,CompanionBodyIdle.Gesture.Yawn};
        public static void Run()
        {
            if(!EditorApplication.isPlaying||app!=null)throw new Exception("Run once in Play mode");
            app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>();idle=(CompanionBodyIdle)typeof(TalkingCharacter).GetField("bodyIdle",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(app);
            doc=app.GetComponent<UIDocument>();original=doc.panelSettings;panel=UnityEngine.Object.Instantiate(original);panel.scaleMode=PanelScaleMode.ConstantPixelSize;panel.scale=1;
            target=new RenderTexture(390,844,24);target.Create();panel.targetTexture=target;doc.panelSettings=panel;doc.rootVisualElement.style.width=390;doc.rootVisualElement.style.height=844;app.PreviewSafeInsets=new Vector2(24,24);
            image=new Texture2D(390,844,TextureFormat.RGB24,false);
            extremities=app.character.GetComponentsInChildren<Transform>().Where(t=>new[]{"CC_Base_L_Mid3","CC_Base_R_Mid3","CC_Base_L_Foot","CC_Base_R_Foot","CC_Base_Head"}.Contains(t.name)).ToArray();
            frames=Path.GetFullPath(Application.dataPath+"/../../../artifacts/expressive-idle");folder=Path.GetFullPath(Application.dataPath+"/../../../docs/evidence/expressive-idle");Directory.CreateDirectory(frames);Directory.CreateDirectory(folder);
            frame=gesture=0;started=false;checks.Clear();timestamps.Clear();next=begin=EditorApplication.timeSinceStartup+2;EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            EditorApplication.QueuePlayerLoopUpdate();if(EditorApplication.timeSinceStartup<next)return;
            try {
                if(app==null||!EditorApplication.isPlaying)throw new Exception("Preview interrupted");
                if(!started&&EditorApplication.timeSinceStartup>=begin){idle.PreviewGesture(Sequence[gesture],gesture%2==0?1:-1);started=true;checks.Add("START "+Sequence[gesture]+" at frame "+frame);}
                foreach(var t in extremities){var p=app.portraitCamera.WorldToViewportPoint(t.position);if(p.x<0||p.x>1||p.y<0||p.y>1||p.z<0)throw new Exception("Gesture clipped: "+t.name+" "+idle.CurrentGesture);}
                var old=RenderTexture.active;RenderTexture.active=target;try{image.ReadPixels(new Rect(0,0,390,844),0,0);image.Apply();File.WriteAllBytes(Path.Combine(frames,"frame-"+frame.ToString("0000")+".png"),image.EncodeToPNG());}finally{RenderTexture.active=old;}
                timestamps.Add(EditorApplication.timeSinceStartup);frame++;
                if(started&&idle.CurrentGesture==CompanionBodyIdle.Gesture.None){checks.Add("PASS "+Sequence[gesture]+" completes live, hands and feet in frame");if(++gesture==Sequence.Length){Finish(null);return;}started=false;begin=EditorApplication.timeSinceStartup+1.5;}
                if(frame>1500)throw new Exception("Preview timeout; disable Reduce idle motion for review");next=EditorApplication.timeSinceStartup+1.0/12;
            }catch(Exception e){Finish(e.Message);}
        }
        static void Finish(string error)
        {
            EditorApplication.update-=Tick;if(error!=null)checks.Add("FAIL "+error);checks.Add("Frames="+frame);File.WriteAllLines(Path.Combine(folder,"live-checks.txt"),checks);
            File.WriteAllLines(Path.Combine(frames,"timestamps.txt"),timestamps.Select(t=>t.ToString("R",System.Globalization.CultureInfo.InvariantCulture)));
            if(doc!=null){doc.rootVisualElement.style.width=StyleKeyword.Null;doc.rootVisualElement.style.height=Length.Percent(100);doc.panelSettings=original;}if(app!=null)app.PreviewSafeInsets=Vector2.zero;
            if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}if(image!=null)UnityEngine.Object.DestroyImmediate(image);if(panel!=null)UnityEngine.Object.DestroyImmediate(panel);app=null;
        }
    }
}
