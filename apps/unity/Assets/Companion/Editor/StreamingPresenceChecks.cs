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
    public static class StreamingPresenceChecks
    {
        static TalkingCharacter app;static UIDocument doc;static PanelSettings original,panel;static RenderTexture target;
        static StyleLength width,height;static int phase;static double deadline,started;
        static bool partial,speaking,typing;static readonly HashSet<string> versions=new HashSet<string>();
        static readonly List<string> checks=new List<string>();
        static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static string Folder=>Path.GetFullPath(Application.dataPath+"/../../../docs/evidence/streaming-presence");
        static object Get(string field)=>typeof(TalkingCharacter).GetField(field,Flags).GetValue(app);
        static void Check(bool value,string label){checks.Add((value?"PASS ":"FAIL ")+label);if(!value)throw new Exception(label);}
        public static void Run()
        {
            if(!EditorApplication.isPlaying||app!=null)throw new Exception("Run once in Play mode");
            app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>();doc=app.GetComponent<UIDocument>();original=doc.panelSettings;
            panel=UnityEngine.Object.Instantiate(original);panel.scaleMode=PanelScaleMode.ConstantPixelSize;panel.scale=1;
            target=new RenderTexture(390,844,24);target.Create();panel.targetTexture=target;doc.panelSettings=panel;
            width=doc.rootVisualElement.style.width;height=doc.rootVisualElement.style.height;doc.rootVisualElement.style.width=390;doc.rootVisualElement.style.height=844;
            Directory.CreateDirectory(Folder);checks.Clear();versions.Clear();partial=speaking=typing=false;phase=0;app.NewChat();app.CheckSetup();deadline=EditorApplication.timeSinceStartup+100;started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;
        }
        static void Capture(string name)
        {
            var prior=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(390,844,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,390,844),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Folder,name+".png"),image.EncodeToPNG());RenderTexture.active=prior;UnityEngine.Object.DestroyImmediate(image);
        }
        static void Tick()
        {
            EditorApplication.QueuePlayerLoopUpdate();
            try {
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Timeout: "+app.State);
                if(phase==0&&app.PresenceText.Contains("Online")&&EditorApplication.timeSinceStartup-started>1) {
                    Check(true,"Online follows successful local readiness check");Capture("online");
                    app.Submit("Please say exactly these two sentences: Welcome to a bright new day. We can take a small happy step together.");phase=1;started=EditorApplication.timeSinceStartup;
                } else if(phase==1) {
                    var root=doc.rootVisualElement;
                    if(!typing&&app.PresenceText.Contains("Typing")&&root.Q("typing-indicator").resolvedStyle.display==DisplayStyle.Flex&&EditorApplication.timeSinceStartup-started>.1){typing=true;Capture("typing");}
                    if(!string.IsNullOrEmpty(app.LastResponse))versions.Add(app.LastResponse);
                    if(!partial&&(bool)Get("streamText")&&!(bool)Get("textFinal")){partial=true;Capture("partial-text");}
                    if(!speaking&&app.IsSpeaking){speaking=true;Check(app.PresenceText.Contains("Speaking"),"Header reflects actual voice playback");Capture("speaking");}
                    if(app.State.Contains("unavailable"))throw new Exception(app.State);
                    if(app.State.Contains("reply finished")) {
                        Check(typing,"Typing appears while awaiting/generating reply");
                        Check(partial&&versions.Count>1,"Actual model response grows through multiple partial text states");
                        Check(((Label)Get("replyLabel")).text==app.LastResponse,"Single reply bubble matches canonical final text");
                        Check(app.FirstTextSeconds>=0&&app.FirstTextSeconds<app.StreamFinishedSeconds,"First text arrives before stream completion");
                        Check(app.FirstAudioSeconds<app.StreamFinishedSeconds&&app.SpeechChunksReceived>=2,"Speech starts before all clips have arrived");
                        Check(root.Q("typing-indicator").resolvedStyle.display==DisplayStyle.None,"Typing clears on completed reply");
                        Check(app.CanReplay&&app.PresenceText.Contains("Online"),"Completed reply returns Online and supports Replay");
                        Check(root.Q("new-messages").resolvedStyle.display==DisplayStyle.None,"Following live reply does not show spurious new-message button");
                        Check(root.Q("conversation").worldBound.height>80,"Active conversation retains readable transcript space");
                        checks.Add($"METRIC first_text_s={app.FirstTextSeconds:F3} first_audio_s={app.FirstAudioSeconds:F3} stream_done_s={app.StreamFinishedSeconds:F3} observed_text_versions={versions.Count}");
                        typeof(TalkingCharacter).GetMethod("MarkAvailability",Flags).Invoke(app,new object[]{false});phase=2;started=EditorApplication.timeSinceStartup;
                    }
                } else if(phase==2&&EditorApplication.timeSinceStartup-started>.5) {
                    Check(app.PresenceText.Contains("Last seen"),"Unavailable service shows last confirmed availability");Capture("last-seen");
                    app.Submit("Say a short greeting in two sentences.");phase=3;
                } else if(phase==3&&(bool)Get("streamText")) {
                    app.Interrupt();phase=4;started=EditorApplication.timeSinceStartup;
                } else if(phase==4&&EditorApplication.timeSinceStartup-started>.5) {
                    Check(!app.IsSpeaking&&Get("request")==null&&!(bool)Get("responding"),"Stop cancels progressive response and audio ownership");
                    Check(doc.rootVisualElement.Q("typing-indicator").resolvedStyle.display==DisplayStyle.None,"Stop removes typing indicator");Finish(null);
                }
            }catch(Exception e){Finish(e.ToString());}
        }
        static void Finish(string error)
        {
            EditorApplication.update-=Tick;if(error!=null)checks.Add("FAIL "+error);app.Interrupt();
            doc.rootVisualElement.style.width=width;doc.rootVisualElement.style.height=height;doc.panelSettings=original;
            target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(panel);File.WriteAllLines(Path.Combine(Folder,"ui-checks.txt"),checks);app=null;
        }
    }
}
