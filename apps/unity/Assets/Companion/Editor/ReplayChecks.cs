using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using Companion.Presentation;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
namespace Companion.Editor
{
    public static class ReplayChecks
    {
        static TalkingCharacter app;static int phase;static double deadline;static string replyText,bubble;static int chunks;
        static readonly List<string> lines=new List<string>();
        static object Field(string name)=>typeof(TalkingCharacter).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(app);
        static int HistoryCount=>((List<TalkingCharacter.Message>)Field("history")).Count;
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/evidence/m1/replay"));
        static void Check(bool ok,string name){if(!ok)throw new Exception(name);lines.Add("PASS "+name);}
        [MenuItem("Companion/Run Reply Replay Checks")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying)throw new Exception("Enter Play first");
            app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>();if(app==null)throw new Exception("Open TalkingCompanion");
            EditorApplication.update-=Tick;lines.Clear();Directory.CreateDirectory(Folder);app.NewChat();
            Check(!app.CanReplay,"empty chat cannot replay");
            app.Submit("Please say exactly these two sentences: Welcome to a bright new day. We can take a small happy step together.");
            phase=0;deadline=EditorApplication.timeSinceStartup+100;EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            try {
                if(app==null||!EditorApplication.isPlaying)throw new Exception("Play stopped");
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Timeout: "+app.State);
                if(app.State.Contains("unavailable"))throw new Exception(app.State);
                if(phase==0&&app.State.Contains("reply finished")) {
                    replyText=app.LastResponse;chunks=app.SpeechChunksReceived;bubble=((Label)Field("replyLabel")).text;
                    Check(chunks>=2&&app.CanReplay,"completed multi-sentence reply enables Replay");
                    app.Replay();Check(app.IsSpeaking&&Field("request")==null,"Replay starts from cached audio without a turn request");phase=1;
                } else if(phase==1&&app.State.Contains("replay finished")) {
                    Check(app.SpeechChunksPlayed==chunks&&app.LastResponse==replyText,"Replay plays every original sentence and retains exact reply");
                    Check(HistoryCount==2,"Replay does not duplicate conversation context");
                    Check(((Label)Field("replyLabel")).text==bubble,"Replay does not duplicate or rewrite transcript");
                    app.Replay();phase=2;
                } else if(phase==2&&app.PlayedSamples>1000&&app.JawAngle>0) {
                    Check(app.GetComponent<AudioSource>().isPlaying&&app.JawAngle>0,"replayed audio advances with facial motion");
                    app.Interrupt();Check(!app.IsSpeaking&&!app.GetComponent<AudioSource>().isPlaying&&app.CanReplay,"Stop silences Replay and allows listening again");
                    Check(HistoryCount==2&&((Label)Field("replyLabel")).text==bubble,"stopping Replay preserves completed exchange and label");
                    var root=app.GetComponent<UIDocument>().rootVisualElement;
                    Check(root.Q("replay-reply").worldBound.height>=40&&root.Q("replay-reply").worldBound.xMax<=root.worldBound.xMax&&root.Q("chat-actions").worldBound.yMax<=root.worldBound.yMax,"Replay remains touch-sized and fits portrait actions");
                    app.NewChat();Check(!app.CanReplay&&((List<TalkingCharacter.Reply>)Field("replayAudio")).Count==0,"New chat releases replay audio");
                    ScreenCapture.CaptureScreenshot(Path.Combine(Folder,"controls.png"));Finish();
                }
            }catch(Exception e){lines.Add("FAIL "+e.Message);Finish();Debug.LogError(e.Message);}
        }
        static void Finish(){EditorApplication.update-=Tick;File.WriteAllLines(Path.Combine(Folder,"editor-checks.txt"),lines);Debug.Log("Reply replay checks saved");}
    }
}
