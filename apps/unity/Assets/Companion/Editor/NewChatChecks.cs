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
    public static class NewChatChecks
    {
        static readonly List<string> lines=new List<string>();
        static TalkingCharacter app;static int phase;static double deadline;
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/evidence/m1/new-chat"));
        static object Field(string name)=>typeof(TalkingCharacter).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(app);
        static void Check(bool ok,string label){if(!ok)throw new Exception(label);lines.Add("PASS "+label);}
        static void Empty()
        {
            Check(((List<TalkingCharacter.Message>)Field("history")).Count==0&&app.LastResponse==""&&app.Draft=="","new chat clears conversation context, reply and draft");
            Check(!app.IsSpeaking&&!app.IsRecording&&app.GetComponent<AudioSource>().clip==null&&app.CueCount==0,"new chat releases playback and resets speech cues");
            Check(((Queue<TalkingCharacter.Reply>)Field("speechQueue")).Count==0&&Field("request")==null,"new chat clears queued speech and active request");
            Check(app.GetComponent<UIDocument>().rootVisualElement.Q<ScrollView>("conversation").childCount==1,"old transcript is replaced by a fresh greeting");
        }
        [MenuItem("Companion/Run New Chat Checks")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying)throw new Exception("Enter Play first");
            app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>();if(app==null)throw new Exception("Open TalkingCompanion");
            EditorApplication.update-=Tick;lines.Clear();Directory.CreateDirectory(Folder);app.NewChat();
            app.Submit("Please say exactly these two sentences: Welcome to a bright new day. We can take a small happy step together.");
            phase=0;deadline=EditorApplication.timeSinceStartup+100;EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            try {
                if(app==null||!EditorApplication.isPlaying)throw new Exception("Play stopped");
                if((phase==0||phase==4)&&EditorApplication.timeSinceStartup>deadline)throw new Exception("Timeout: "+app.State);
                if(app.State.Contains("unavailable"))throw new Exception(app.State);
                if(phase==0&&app.IsSpeaking) {
                    var root=app.GetComponent<UIDocument>().rootVisualElement;
                    Check(root.Q("new-chat").enabledInHierarchy,"New chat remains available during speech");
                    root.Q<TextField>("message-input").value="discard this draft";
                    app.NewChat();Empty();app.Retry();Check(app.State.StartsWith("Ready")&&Field("request")==null,"Retry cannot resubmit a cleared prompt");
                    phase=1;deadline=EditorApplication.timeSinceStartup+2;
                } else if(phase==1&&EditorApplication.timeSinceStartup>deadline) {
                    Check(app.State.StartsWith("Ready")&&app.LastResponse==""&&!app.IsSpeaking,"late streamed audio cannot revive the old chat");
                    app.TranscribePcm(new byte[32000]);Check(app.State.StartsWith("Transcribing"),"generated silence starts real transcription without microphone capture");
                    app.NewChat();phase=2;deadline=EditorApplication.timeSinceStartup+2;
                } else if(phase==2&&EditorApplication.timeSinceStartup>deadline) {
                    Check(app.Draft==""&&app.State.StartsWith("Ready"),"cancelled transcription cannot fill the new chat draft");
                    app.Submit("Tell me a short cheerful story.");app.NewChat();phase=3;deadline=EditorApplication.timeSinceStartup+2;
                } else if(phase==3&&EditorApplication.timeSinceStartup>deadline) {
                    Check(app.State.StartsWith("Ready")&&app.LastResponse=="","reset during generation rejects late reply text");
                    app.Submit("Say welcome to this new conversation.");
                    var request=(UnityWebRequest)Field("request");var payload=System.Text.Encoding.UTF8.GetString(request.uploadHandler.data);
                    Check(payload.Contains("\"history\":[]")&&!payload.Contains("bright new day"),"next actual request carries empty history");
                    phase=4;deadline=EditorApplication.timeSinceStartup+100;
                } else if(phase==4&&app.State.Contains("finished")) {
                    Check(app.LastResponse.Length>0&&app.SpeechChunksPlayed>0,"new conversation completes real AI and speech after reset");
                    app.NewChat();var root=app.GetComponent<UIDocument>().rootVisualElement;
                    Check(Screen.height>Screen.width&&root.Q("chat-actions").worldBound.yMax<=root.worldBound.yMax,"portrait controls still fit");
                    Check(root.Q("new-chat").worldBound.height>=40&&root.Q("new-chat").worldBound.xMax<=root.worldBound.xMax,"New chat is reachable and touch-sized");
                    ScreenCapture.CaptureScreenshot(Path.Combine(Folder,"ready.png"));Finish();
                }
            }catch(Exception e){lines.Add("FAIL "+e.Message);Finish();Debug.LogError(e.Message);}
        }
        static void Finish(){EditorApplication.update-=Tick;File.WriteAllLines(Path.Combine(Folder,"editor-checks.txt"),lines);Debug.Log("New chat checks saved");}
    }
}
