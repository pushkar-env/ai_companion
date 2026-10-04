using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using Companion.Presentation;
using UnityEditor;
using UnityEngine;
namespace Companion.Editor
{
    public static class SpeechStreamChecks
    {
        static readonly List<string> lines=new List<string>();static TalkingCharacter app;static double deadline;static int phase;static bool captured;
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/evidence/m1/sentence-stream"));
        static void Check(bool ok,string name){if(!ok)throw new Exception(name);lines.Add("PASS "+name);}
        static bool Feed(LocalSpeechStream s,byte[] bytes)=>(bool)typeof(LocalSpeechStream).GetMethod("ReceiveData",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(s,new object[]{bytes,bytes.Length});
        [MenuItem("Companion/Run Sentence Stream Checks")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying)throw new Exception("Enter Play first");app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>();if(app==null)throw new Exception("Open TalkingCompanion");
            EditorApplication.update-=Tick;lines.Clear();Directory.CreateDirectory(Folder);app.Interrupt();
            using(var stream=new LocalSpeechStream()) {
                var data=Encoding.UTF8.GetBytes("{\"text\":\"Hello 🌞\"}\n");foreach(var b in data)CheckFeed(Feed(stream,new[]{b}));
                Check(stream.TryRead(out var line)&&line=="{\"text\":\"Hello 🌞\"}","UTF-8 codepoints and lines survive fragmented network packets");
                Check(!stream.TryRead(out _),"each stream frame is consumed once");
            }
            using(var stream=new LocalSpeechStream()){Feed(stream,Encoding.UTF8.GetBytes("unfinished"));typeof(LocalSpeechStream).GetMethod("CompleteContent",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(stream,null);Check(stream.Invalid,"truncated stream frame fails closed");}
            using(var stream=new LocalSpeechStream()){Check(!Feed(stream,Encoding.UTF8.GetBytes("1\n2\n3\n4\n5\n6\n7\n8\n9\n")),"undrained frame queue is bounded");}
            phase=0;captured=false;deadline=EditorApplication.timeSinceStartup+100;
            app.Submit("Please say exactly these two sentences: Welcome to a bright new day. We can take a small happy step together.");EditorApplication.update+=Tick;
        }
        static void CheckFeed(bool ok){if(!ok)throw new Exception("Valid UTF-8 packet rejected");}
        static void Tick()
        {
            try {
                if(app==null||!EditorApplication.isPlaying)throw new Exception("Play stopped");if(phase!=2&&EditorApplication.timeSinceStartup>deadline)throw new Exception("Timeout: "+app.State);
                if(app.State.Contains("unavailable"))throw new Exception(app.State);
                if(phase==0&&app.IsSpeaking&&!captured){captured=true;ScreenCapture.CaptureScreenshot(Path.Combine(Folder,"speaking.png"));}
                if(phase==0&&app.State.Contains("finished")) {
                    Check(app.SpeechChunksReceived>=2&&app.SpeechChunksPlayed==app.SpeechChunksReceived,"all real streamed speech clips play in sequence");
                    Check(app.FirstAudioSeconds>=0&&app.FirstAudioSeconds<app.StreamFinishedSeconds,"Unity begins audio before the full stream has arrived");
                    lines.Add("First audio seconds="+app.FirstAudioSeconds+"; stream complete seconds="+app.StreamFinishedSeconds);
                    app.Submit("Please say exactly these two sentences: Welcome to a bright new day. We can take a small happy step together.");phase=1;
                }else if(phase==1&&app.IsSpeaking){app.Interrupt();Check(!app.IsSpeaking&&!app.GetComponent<AudioSource>().isPlaying,"Stop silences current streamed clip immediately");phase=2;deadline=EditorApplication.timeSinceStartup+2;}
                else if(phase==2&&EditorApplication.timeSinceStartup>deadline-.15){Check(!app.IsSpeaking&&app.State.StartsWith("Stopped"),"queued or late clips cannot restart after Stop");Finish();}
            }catch(Exception e){lines.Add("FAIL "+e.Message);Finish();Debug.LogError(e.Message);}
        }
        static void Finish(){EditorApplication.update-=Tick;File.WriteAllLines(Path.Combine(Folder,"editor-checks.txt"),lines);Debug.Log("Sentence stream checks saved");}
    }
}
