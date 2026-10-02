using System;
using System.IO;
using System.Collections.Generic;
using Companion.Presentation;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace Companion.Editor
{
    // Exercises actual local inference, Windows synthesis and Play Mode sample-clock animation.
    public static class TalkingCharacterChecks
    {
        static TalkingCharacter app;static int phase,count;static double deadline,phaseStart;
        static float maxJaw,maxSmile,maxConcern;static int maxSamples;static bool captured;
        static readonly List<string> lines=new List<string>();
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/evidence/m1/talking-companion"));
        public static void Run()
        {
            EditorApplication.update-=Tick;
            app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>();
            if(!EditorApplication.isPlaying||app==null)throw new InvalidOperationException("Open TalkingCompanion and enter Play first");
            lines.Clear();count=0;phase=0;maxJaw=maxSmile=maxConcern=0;maxSamples=0;captured=false;
            Directory.CreateDirectory(Folder);deadline=EditorApplication.timeSinceStartup+180;
            Check(Screen.height>Screen.width,"portrait Simulator retained");
            var root=app.GetComponent<UIDocument>().rootVisualElement;
            Check(root.Q<TextField>("message-input").worldBound.height>0,"message input visible");
            app.Submit(new string('x',501));Check(app.State.Contains("500"),"oversized prompt rejected locally");
            app.Submit("I got a new job today! Please celebrate with me in two short sentences.");
            Check(app.State.StartsWith("Thinking"),"real request enters thinking state");
            EditorApplication.update+=Tick;
        }
        static void Check(bool ok,string label){if(!ok)throw new Exception(label);count++;lines.Add("PASS "+label);}
        static void Tick()
        {
            try {
                if(!EditorApplication.isPlaying||app==null)throw new Exception("Play stopped during verification");
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Timed out: "+app.State);
                if(app.State.Contains("unavailable")||app.State.StartsWith("Invalid"))throw new Exception(app.State);
                if(phase==0&&app.IsSpeaking){phase=1;phaseStart=EditorApplication.timeSinceStartup;Check(app.CueCount>5,"Windows speech returned timed visemes");Check(app.LastResponse.Length>0,"local model returned a reply");lines.Add("Reply 1: "+app.LastResponse+" ["+app.Expression+"]");}
                if(phase==1){
                    maxJaw=Mathf.Max(maxJaw,app.JawAngle);maxSmile=Mathf.Max(maxSmile,app.ShapeWeight("Mouth_Corner_Pull_L"));maxSamples=Math.Max(maxSamples,app.PlayedSamples);
                    if(!captured&&maxJaw>1&&app.PlayedSamples>8000){ScreenCapture.CaptureScreenshot(Path.Combine(Folder,"speaking.png"));captured=true;}
                    if(EditorApplication.timeSinceStartup-phaseStart>1.7){
                        Check(maxSamples>8000,"AudioSource sample clock advances during real speech");Check(maxJaw>1,"jaw moves with speech cues");Check(maxSmile>10,"happy response drives smile blendshape");
                        app.Interrupt();Check(!app.IsSpeaking&&!app.GetComponent<AudioSource>().isPlaying,"Stop immediately silences playback");Check(app.JawAngle<.01f&&app.ShapeWeight("Mouth_Corner_Pull_L")==0,"Stop resets speech and expression");
                        app.Submit("Tell me a short story.");app.Interrupt();Check(app.State.StartsWith("Stopped"),"pending generation can be cancelled");phase=2;phaseStart=EditorApplication.timeSinceStartup;
                    }
                }
                if(phase==2&&EditorApplication.timeSinceStartup-phaseStart>1.5){Check(!app.IsSpeaking,"cancelled turn cannot start late audio");app.Submit("I feel disappointed after a difficult day. Please respond with a concerned expression and a short kind sentence.");phase=3;}
                if(phase==3&&app.IsSpeaking){phase=4;Check(app.Expression=="concerned","concerned reply selects a distinct expression");lines.Add("Reply 2: "+app.LastResponse+" ["+app.Expression+"]");}
                if(phase==4){maxConcern=Mathf.Max(maxConcern,app.ShapeWeight("Brow_Raise_In_L"));if(!app.IsSpeaking){Check(maxConcern>10,"concerned brow animation applied");Check(app.State.Contains("finished"),"natural playback returns to ready");
                    var root=app.GetComponent<UIDocument>().rootVisualElement;
                    Check(root.Q("chat-actions").worldBound.yMax<=root.worldBound.yMax,"controls remain inside portrait after multiple turns");
                    Check(root.Q("talking-character").worldBound.height>=root.worldBound.height*.35f,"character stays large as history grows");
                    ScreenCapture.CaptureScreenshot(Path.Combine(Folder,"conversation.png"));app.Retry();Check(app.State.StartsWith("Thinking"),"Retry submits the previous prompt");app.Interrupt();lines.Add("PASS "+count+" real Editor conversation checks");Finish();}}
            }catch(Exception e){lines.Add("FAIL "+e.Message);Finish();UnityEngine.Debug.LogError("Talking character checks: "+e.Message);}
        }
        static void Finish(){EditorApplication.update-=Tick;File.WriteAllLines(Path.Combine(Folder,"editor-checks.txt"),lines);UnityEngine.Debug.Log("Talking character checks saved: "+count);}
    }
}
