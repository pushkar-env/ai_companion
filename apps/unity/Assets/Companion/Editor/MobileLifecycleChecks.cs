using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using Companion.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace Companion.Editor
{
    public static class MobileLifecycleChecks
    {
        static TalkingCharacter app;
        static readonly BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
        static readonly List<string> checks=new List<string>();
        static string draft,look;static int targetTone;static bool cameraEnabled;static double next;static int step;
        static Transform hip;static Quaternion pausedHip;static AudioClip fixture;
        static object Get(string name)=>typeof(TalkingCharacter).GetField(name,Flags).GetValue(app);
        static void Set(string name,object value)=>typeof(TalkingCharacter).GetField(name,Flags).SetValue(app,value);
        static void Check(bool value,string label){checks.Add((value?"PASS ":"FAIL ")+label);if(!value)throw new Exception(label);}
        public static void Run()
        {
            if(!EditorApplication.isPlaying||app!=null)throw new Exception("Run once in Play mode");
            app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>();draft=app.Draft;cameraEnabled=app.portraitCamera.enabled;
            checks.Clear();step=0;next=EditorApplication.timeSinceStartup+1;EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            EditorApplication.QueuePlayerLoopUpdate();if(EditorApplication.timeSinceStartup<next)return;
            try {
                var root=app.GetComponent<UIDocument>().rootVisualElement;
                switch(step++) {
                    case 0:
                        root.Q<TextField>("message-input").value="Unsent mobile draft • नमस्ते";
                        Set("request",UnityWebRequest.Get("http://127.0.0.1:1/unused")); // Never sent.
                        fixture=AudioClip.Create("Silent lifecycle fixture",16000,1,16000,false);
                        var source=app.GetComponent<AudioSource>();source.clip=fixture;source.loop=true;source.Play();Set("speaking",true);
                        app.SetApplicationSuspended(true);
                        Check(app.IsSuspended&&!app.IsSpeaking&&!source.isPlaying,"Background stops speech and playback");
                        Check(Get("request")==null&&Get("routine")==null,"Background releases pending request ownership");
                        Check(!app.IsRecording,"Background has no active capture");
                        Check(!app.portraitCamera.enabled,"Portrait rendering stops in background");
                        Check(Get("setupRequest")==null&&Get("setupRoutine")==null,"Setup requests stop in background");
                        Check(!root.Q<Button>("send-message").enabledSelf&&!root.Q<Button>("record-voice").enabledSelf,"Background disables Send and microphone");
                        foreach(var t in app.character.GetComponentsInChildren<Transform>())if(t.name=="CC_Base_Hip")hip=t;
                        pausedHip=hip.localRotation;
                        app.Submit("Must not send");app.Retry();app.ToggleRecording();app.CheckSetup();
                        Check(Get("request")==null&&Get("setupRequest")==null&&!app.IsRecording,"Actions cannot start while suspended");
                        app.SetApplicationSuspended(true);break;
                    case 1:
                        Check(Quaternion.Angle(pausedHip,hip.localRotation)<.001f,"Body stays frozen across background frames");
                        Check(app.Draft=="Unsent mobile draft • नमस्ते","Unicode draft survives background");
                        app.SetApplicationSuspended(false);
                        Check(!app.IsSuspended&&app.portraitCamera.enabled==cameraEnabled,"Resume restores original camera state");
                        Check(!app.IsSpeaking&&!app.IsRecording&&Get("request")==null,"Resume starts no audio capture or request");
                        Check(root.Q<Button>("send-message").enabledSelf,"Resume restores usable composer");
                        for(int i=0;i<20;i++){app.SetApplicationSuspended(true);app.SetApplicationSuspended(true);app.SetApplicationSuspended(false);}
                        Check(app.portraitCamera.enabled==cameraEnabled&&app.Draft=="Unsent mobile draft • नमस्ते","20 repeated cycles preserve camera and draft");
                        app.portraitCamera.enabled=false;app.SetApplicationSuspended(true);app.SetApplicationSuspended(false);
                        Check(!app.portraitCamera.enabled,"Already disabled camera stays disabled after resume");app.portraitCamera.enabled=cameraEnabled;
                        app.OpenSettings();using(var evt=NavigationCancelEvent.GetPooled()){root.SendEvent(evt);}
                        Check(root.Q("companion-settings").style.display.value==DisplayStyle.None&&root.Q("conversation-shell").enabledSelf,"Navigation cancel closes Settings and re-enables chat");
                        look=JsonUtility.ToJson(app.CurrentLook);targetTone=(app.CurrentLook.skinTone+1)%6;app.OpenWardrobe();break;
                    case 2:
                        var swatch=root.Q<Button>("skin-tone-"+targetTone);swatch.Focus();using(var evt=NavigationSubmitEvent.GetPooled()){swatch.SendEvent(evt);}break;
                    case 3:
                        Check(app.CurrentLook.skinTone==targetTone,"Wardrobe preview visibly changes before Back");
                        Check(app.HandleBack()&&!app.WardrobeOpen&&JsonUtility.ToJson(app.CurrentLook)==look,"Back rolls back unsaved wardrobe preview");
                        var nav=(SyntheticHistoryNavigation)Get("historyNavigation");nav.Open();Check(app.HandleBack()&&!nav.IsOpen,"Back exits history route");
                        Check(!app.HandleBack()&&app.Draft=="Unsent mobile draft • नमस्ते","Root Back leaves conversation and draft intact");
                        Finish(null);return;
                }
                next=EditorApplication.timeSinceStartup+1;
            }catch(Exception e){Finish(e.ToString());}
        }
        static void Finish(string error)
        {
            EditorApplication.update-=Tick;if(error!=null)checks.Add("FAIL "+error);
            app.SetApplicationSuspended(false);app.Interrupt();
            if(app.WardrobeOpen)app.CloseWardrobe(false);app.CloseSettings();
            app.GetComponent<UIDocument>().rootVisualElement.Q<TextField>("message-input").value=draft;
            var source=app.GetComponent<AudioSource>();source.clip=null;source.loop=false;
            app.portraitCamera.enabled=cameraEnabled;if(fixture!=null)UnityEngine.Object.Destroy(fixture);
            var folder=Path.GetFullPath(Application.dataPath+"/../../../docs/evidence/mobile-lifecycle");Directory.CreateDirectory(folder);File.WriteAllLines(Path.Combine(folder,"checks.txt"),checks);app=null;
        }
    }
}
