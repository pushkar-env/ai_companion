using System;
using System.IO;
using System.Collections.Generic;
using Companion.Core;
using Companion.Presentation;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
namespace Companion.Editor
{
    public static class VoiceInputChecks
    {
        [Serializable] class Fixture {public string pcm;public int sampleRate;}
        static readonly List<string> lines=new List<string>();static TalkingCharacter app;static double deadline;static int phase;static byte[] fixture;
        static string Repo=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
        static void Check(bool ok,string label){if(!ok)throw new Exception(label);lines.Add("PASS "+label);}
        [MenuItem("Companion/Run Local Voice Input Checks")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first");
            app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>();if(app==null)throw new Exception("Open TalkingCompanion");
            lines.Clear();EditorApplication.update-=Tick;app.Interrupt();Directory.CreateDirectory(Path.Combine(Repo,"docs/evidence/m1/voice-input"));
            var stereo=new float[32000];for(int i=0;i<stereo.Length;i+=2){stereo[i]=1;stereo[i+1]=-1;}
            var pcm=MicrophonePcm.Encode(stereo,2,16000,16000);Check(pcm.Length==32000&&Array.TrueForAll(pcm,b=>b==0),"stereo downmix produces correct mono PCM");
            Check(MicrophonePcm.Encode(new float[48000],1,48000,48000).Length==32000,"48 kHz capture converts to bounded 16 kHz PCM");
            bool rejected=false;try{MicrophonePcm.Encode(new float[640002],1,16000,320001);}catch(ArgumentException){rejected=true;}Check(rejected,"recordings above duration cap rejected");
            var capture=new LocalMicrophoneCapture();Check(!capture.Begin("missing-device-for-test")&&!capture.Recording,"missing microphone fails without recording");capture.Cancel();
            var root=app.GetComponent<UIDocument>().rootVisualElement;Check(root.Q("record-voice").worldBound.height>=40&&root.Q("record-voice").worldBound.height<=50,"record control stays compact and touch-sized");
            Check(root.Q("chat-actions").worldBound.yMax<=root.worldBound.yMax,"voice and send controls fit portrait");
            Check(!app.IsRecording,"scene never records automatically");
            var json=JsonUtility.FromJson<Fixture>(File.ReadAllText(Path.Combine(Repo,"artifacts/voice-input/synthetic-speech.json")));fixture=Convert.FromBase64String(json.pcm);
            app.TranscribePcm((byte[])fixture.Clone());Check(app.State.StartsWith("Transcribing"),"generated PCM enters real transcription request");
            phase=0;deadline=EditorApplication.timeSinceStartup+60;EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            try {
                if(!EditorApplication.isPlaying||app==null)throw new Exception("Play stopped during checks");
                if(phase!=1&&EditorApplication.timeSinceStartup>deadline)throw new Exception("Timeout: "+app.State);
                if(app.State.Contains("unavailable")||app.State.StartsWith("Invalid"))throw new Exception(app.State);
                if(phase==0&&app.State.StartsWith("Review")) {
                    Check(app.Draft.Length>0,"real recognized text reaches editable draft");Check(!app.IsSpeaking&&app.LastResponse=="","transcription does not automatically send to AI");
                    app.TranscribePcm((byte[])fixture.Clone());app.Interrupt();phase=1;deadline=EditorApplication.timeSinceStartup+2;
                } else if(phase==1&&EditorApplication.timeSinceStartup>deadline-.1) {
                    Check(app.State.StartsWith("Stopped")&&!app.IsSpeaking,"cancelled transcription cannot overwrite state or start speech");
                    ScreenCapture.CaptureScreenshot(Path.Combine(Repo,"docs/evidence/m1/voice-input/review.png"));Finish();
                }
            }catch(Exception e){lines.Add("FAIL "+e.Message);Finish();Debug.LogError(e.Message);}
        }
        static void Finish(){EditorApplication.update-=Tick;Directory.CreateDirectory(Path.Combine(Repo,"docs/evidence/m1/voice-input"));File.WriteAllLines(Path.Combine(Repo,"docs/evidence/m1/voice-input/editor-checks.txt"),lines);if(fixture!=null)Array.Clear(fixture,0,fixture.Length);Debug.Log("Voice input checks saved");}
    }
}
