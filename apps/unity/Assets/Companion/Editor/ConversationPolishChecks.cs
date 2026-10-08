using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Companion.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Companion.Editor
{
    public static class ConversationPolishChecks
    {
        static TalkingCharacter app;static UIDocument doc;static PanelSettings original,panel;static RenderTexture target;
        static int phase;static double ready;static readonly List<string> lines=new List<string>();
        static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/evidence/ui-polish"));
        static void Check(bool ok,string name){if(!ok)throw new Exception(name);lines.Add("PASS "+name);}
        static void Set(string field,object value)=>typeof(TalkingCharacter).GetField(field,Flags).SetValue(app,value);
        static object Get(string field)=>typeof(TalkingCharacter).GetField(field,Flags).GetValue(app);
        [MenuItem("Companion/Run Conversation Polish Checks")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying||app!=null)throw new Exception("Run once in Play mode");
            app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>();doc=app.GetComponent<UIDocument>();original=doc.panelSettings;
            panel=UnityEngine.Object.Instantiate(original);panel.scaleMode=PanelScaleMode.ConstantPixelSize;panel.scale=1;
            target=new RenderTexture(390,844,24);target.Create();panel.targetTexture=target;doc.panelSettings=panel;
            Directory.CreateDirectory(Folder);lines.Clear();phase=0;app.NewChat();Configure(390,844);
            EditorApplication.update+=Tick;
        }
        static void Configure(int w,int h)
        {
            target.Release();target.width=w;target.height=h;target.Create();doc.rootVisualElement.style.width=w;doc.rootVisualElement.style.height=h;
            app.PreviewSafeInsets=new Vector2(24,24);ready=EditorApplication.timeSinceStartup+2;EditorApplication.QueuePlayerLoopUpdate();
        }
        static void Tick()
        {
            EditorApplication.QueuePlayerLoopUpdate();if(EditorApplication.timeSinceStartup<ready)return;
            try {
                var root=doc.rootVisualElement;
                foreach(var name in new[]{"message-input","record-voice","send-message"}) {
                    var b=root.Q(name).worldBound;var frame=root.worldBound;
                    Check(b.width>0&&b.height>=47.5f&&b.xMin>=0&&b.xMax<=frame.xMax+.5f&&b.yMax<=frame.yMax-Mathf.Max(24,app.PreviewKeyboardInset)+.5f,"phase "+phase+" reachable "+name);
                }
                var bounds=app.FullBodyBounds;
                foreach(var y in new[]{bounds.min.y,bounds.max.y}) {
                    var v=app.portraitCamera.WorldToViewportPoint(new Vector3(bounds.center.x,y,bounds.center.z));
                    Check(v.y>.01f&&v.y<.99f&&v.z>0,"phase "+phase+" full-body vertical bound "+y);
                }
                var stage=root.Q("companion-stage").worldBound;
                Check(stage.height>=40&&stage.Overlaps(root.Q("chat-drawer").worldBound),"phase "+phase+" scene behind chat overlay");
                Capture("layout-"+phase);
                if(phase==0){Configure(360,640);phase++;return;}
                if(phase==1){Configure(390,844);phase++;return;}
                if(phase==2){app.PreviewKeyboardInset=280;Configure(390,844);phase++;return;}
                if(phase==3){app.PreviewKeyboardInset=240;Configure(360,640);phase++;return;}
                if(phase==4){app.PreviewKeyboardInset=0;Set("largeText",true);root.AddToClassList("large-text");Configure(360,640);phase++;return;}
                if(phase==5){Set("largeText",false);root.RemoveFromClassList("large-text");Set("speaking",true);typeof(TalkingCharacter).GetMethod("SetState",Flags).Invoke(app,new object[]{"Speaking • verification fixture"});Configure(360,640);phase++;return;}
                if(phase==6){
                    var stop=root.Q("stop-response");Check(stop.resolvedStyle.display==DisplayStyle.Flex&&stop.worldBound.height>=44,"busy shows reachable Stop");
                    Check(!root.Q<Button>("send-message").enabledSelf,"busy prevents duplicate send");app.Interrupt();
                    var input=root.Q<TextField>("message-input");input.value="Draft survives settings and history";
                    app.OpenSettings();Configure(390,844);phase++;return;
                }
                if(phase==7){
                    Check(app.Draft=="Draft survives settings and history","settings preserves draft");
                    Check(!root.Q("conversation-shell").enabledInHierarchy,"settings blocks underlying chat interaction");app.CloseSettings();
                    var nav=(SyntheticHistoryNavigation)Get("historyNavigation");nav.Open();nav.Close();
                    Check(app.Draft=="Draft survives settings and history"&&root.Q("conversation-shell").resolvedStyle.display!=DisplayStyle.None,"history Back restores shell and draft");
                    Check(root.Q("companion-settings").style.display.value==DisplayStyle.None,"history Back keeps settings closed");
                    // Start and immediately cancel a local retry; no microphone is accessed.
                    typeof(TalkingCharacter).GetMethod("AddMessage",Flags).Invoke(app,new object[]{"You","Retry fixture"});Set("lastPrompt","Retry fixture");
                    var transcript=(ScrollView)Get("transcript");int count=transcript.childCount;
                    app.Retry();app.Interrupt();Check(transcript.childCount==count,"Retry reuses user message instead of duplicating bubble");
                    app.NewChat();Check(app.Draft==""&&!app.CanReplay,"New chat clears draft and replay");
                    Finish(null);return;
                }
            }catch(Exception e){Finish(e.Message);}
        }
        static void Capture(string name)
        {
            var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Folder,name+".png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=old;
        }
        static void Finish(string error)
        {
            EditorApplication.update-=Tick;if(error!=null)lines.Add("FAIL "+error);
            if(app!=null){app.Interrupt();app.PreviewKeyboardInset=0;app.PreviewSafeInsets=Vector2.zero;Set("largeText",PlayerPrefs.GetInt("Companion.UI.LargeText",0)==1);}
            if(doc!=null){doc.rootVisualElement.style.width=StyleKeyword.Null;doc.rootVisualElement.style.height=Length.Percent(100);doc.panelSettings=original;}
            if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}if(panel!=null)UnityEngine.Object.DestroyImmediate(panel);
            File.WriteAllLines(Path.Combine(Folder,"checks.txt"),lines);app=null;Debug.Log("Conversation polish checks: "+(error??"passed"));
        }
    }
}
