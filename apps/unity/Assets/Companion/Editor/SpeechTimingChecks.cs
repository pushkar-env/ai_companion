using System;
using System.IO;
using System.Globalization;
using System.Collections.Generic;
using Companion.Presentation;
using UnityEngine;
using UnityEditor;

namespace Companion.Editor
{
    // Explicit, synthetic Editor run. Never records a microphone or writes user text/audio.
    public static class SpeechTimingChecks
    {
        static TalkingCharacter app;
        static int phase;
        static double deadline;
        static bool motion;
        static float completed, gap, firstText, firstAudio, streamFinished;
        static readonly List<string> checks=new List<string>();
        static readonly List<string> rows=new List<string>();
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/evidence/m1/speech-timing"));
        const string Prompt="Please say exactly these two sentences: Welcome to a bright new day. We can take a small happy step together.";
        static string Number(float value)=>value.ToString("F3",CultureInfo.InvariantCulture);
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks.Add("PASS "+message);}

        [MenuItem("Companion/Run Speech Timing Checks")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying)throw new Exception("Enter TalkingCompanion Play mode first.");
            app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>();
            if(app==null)throw new Exception("Open TalkingCompanion.");
            EditorApplication.update-=Tick;
            checks.Clear();rows.Clear();Directory.CreateDirectory(Folder);
            rows.Add("run,text_seconds,first_audio_seconds,stream_done_seconds,reply_done_seconds,max_observed_gap_seconds,chunks");
            app.NewChat();phase=0;StartTurn();EditorApplication.update+=Tick;
        }
        static void StartTurn(){motion=false;app.Submit(Prompt);deadline=EditorApplication.timeSinceStartup+120;}
        static void Tick()
        {
            try {
                if(app==null||!EditorApplication.isPlaying)throw new Exception("Play stopped before completion");
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Timed out before successful playback");
                motion|=app.IsSpeaking&&app.PlayedSamples>1000&&app.JawAngle>0;
                if(phase<2&&app.State.Contains("reply finished")) {
                    Check(app.FirstTextSeconds>=0&&app.FirstAudioSeconds>=app.FirstTextSeconds,"run "+(phase+1)+": text precedes first audio");
                    Check(app.StreamFinishedSeconds>=app.FirstTextSeconds&&app.ReplyCompletedSeconds>=app.StreamFinishedSeconds&&app.ReplyCompletedSeconds>=app.FirstAudioSeconds,"run "+(phase+1)+": completion follows delivery and playback");
                    Check(motion&&app.SpeechChunksPlayed>=2&&app.SpeechChunksPlayed==app.SpeechChunksReceived,"run "+(phase+1)+": multi-sentence audio clock and mouth motion observed");
                    Check(app.MaxSpeechGapSeconds>=0&&app.MaxSpeechGapSeconds<=app.ReplyCompletedSeconds,"run "+(phase+1)+": observed gap is bounded by turn duration");
                    rows.Add((phase+1)+","+Number(app.FirstTextSeconds)+","+Number(app.FirstAudioSeconds)+","+Number(app.StreamFinishedSeconds)+","+Number(app.ReplyCompletedSeconds)+","+Number(app.MaxSpeechGapSeconds)+","+app.SpeechChunksPlayed);
                    if(phase++==0){StartTurn();return;}
                    completed=app.ReplyCompletedSeconds;gap=app.MaxSpeechGapSeconds;firstText=app.FirstTextSeconds;firstAudio=app.FirstAudioSeconds;streamFinished=app.StreamFinishedSeconds;
                    app.Replay();deadline=EditorApplication.timeSinceStartup+120;
                } else if(phase==2&&app.State.Contains("replay finished")) {
                    Check(app.ReplyCompletedSeconds==completed&&app.MaxSpeechGapSeconds==gap&&app.FirstTextSeconds==firstText&&app.FirstAudioSeconds==firstAudio&&app.StreamFinishedSeconds==streamFinished,"Replay preserves original generation timings");
                    app.NewChat();
                    Check(app.FirstTextSeconds<0&&app.FirstAudioSeconds<0&&app.StreamFinishedSeconds<0&&app.ReplyCompletedSeconds<0&&app.MaxSpeechGapSeconds==0,"New chat resets timing state");
                    app.Submit(Prompt);app.Interrupt();
                    Check(app.ReplyCompletedSeconds<0,"cancelled generation is not measured as completed");
                    app.NewChat();Finish();
                }
            }catch(Exception e){checks.Add("FAIL "+e.Message);Finish();Debug.LogError("Speech timing check failed: "+e.Message);}
        }
        static void Finish()
        {
            EditorApplication.update-=Tick;
            File.WriteAllLines(Path.Combine(Folder,"editor-checks.txt"),checks);
            File.WriteAllLines(Path.Combine(Folder,"timings.csv"),rows);
            Debug.Log("Speech timing evidence saved (synthetic turns; no transcript/audio).");
        }
    }
}
