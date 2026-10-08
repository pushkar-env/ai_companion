using System;
using System.IO;
using Companion.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Companion.Editor
{
    // Captures the actual running UI at 12 Hz without changing the Editor's Game view.
    public static class WardrobeMotionPreview
    {
        static TalkingCharacter app;static UIDocument doc;static PanelSettings original,panel;
        static RenderTexture target;static Texture2D image;static int frame;static double next;
        static Vector3 startHip,startHand;static float hipMotion,handMotion;
        static Transform hip,hand;static string folder;
        [MenuItem("Companion/Capture Wardrobe Motion Preview")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying||app!=null)throw new Exception("Run once in Play mode");
            app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>();doc=app.GetComponent<UIDocument>();original=doc.panelSettings;
            panel=UnityEngine.Object.Instantiate(original);panel.scaleMode=PanelScaleMode.ConstantPixelSize;panel.scale=1;
            target=new RenderTexture(390,844,24);target.Create();panel.targetTexture=target;doc.panelSettings=panel;
            doc.rootVisualElement.style.width=390;doc.rootVisualElement.style.height=844;app.PreviewSafeInsets=new Vector2(24,24);
            image=new Texture2D(390,844,TextureFormat.RGB24,false);
            foreach(var t in app.character.GetComponentsInChildren<Transform>()){if(t.name=="CC_Base_Hip")hip=t;if(t.name=="CC_Base_L_Hand")hand=t;}
            startHip=hip.position;startHand=hand.position;hipMotion=handMotion=0;
            folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../artifacts/wardrobe-motion"));Directory.CreateDirectory(folder);
            frame=0;next=EditorApplication.timeSinceStartup+2;EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            EditorApplication.QueuePlayerLoopUpdate();if(EditorApplication.timeSinceStartup<next)return;
            try {
                if(!EditorApplication.isPlaying||app==null)throw new Exception("Preview interrupted");
                hipMotion=Mathf.Max(hipMotion,Vector3.Distance(startHip,hip.position));handMotion=Mathf.Max(handMotion,Vector3.Distance(startHand,hand.position));
                var old=RenderTexture.active;RenderTexture.active=target;
                try{image.ReadPixels(new Rect(0,0,390,844),0,0);image.Apply();File.WriteAllBytes(Path.Combine(folder,"frame-"+frame.ToString("000")+".png"),image.EncodeToPNG());}
                finally{RenderTexture.active=old;}
                if(++frame>=528){Finish(null);return;}next=EditorApplication.timeSinceStartup+1.0/12;
            }catch(Exception e){Finish(e.Message);}
        }
        static void Finish(string error)
        {
            EditorApplication.update-=Tick;
            if(doc!=null){doc.rootVisualElement.style.width=StyleKeyword.Null;doc.rootVisualElement.style.height=Length.Percent(100);doc.panelSettings=original;}
            if(app!=null)app.PreviewSafeInsets=Vector2.zero;
            if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}if(image!=null)UnityEngine.Object.DestroyImmediate(image);if(panel!=null)UnityEngine.Object.DestroyImmediate(panel);
            var evidence=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/evidence/wardrobe"));Directory.CreateDirectory(evidence);
            File.WriteAllText(Path.Combine(evidence,"runtime-motion.txt"),(error==null&&hipMotion>.005f&&handMotion>.005f?"PASS":"FAIL")+" live Update body movement; frames="+frame+" hip range="+hipMotion.ToString("F5")+" m, hand range="+handMotion.ToString("F5")+" m. "+error);
            app=null;
        }
    }
}
