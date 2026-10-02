using System;
using System.Collections.Generic;
using System.IO;
using Companion.Core;
using Companion.Presentation;
using UnityEditor;
using UnityEngine;

namespace Companion.Editor
{
    public static class FacialLabChecks
    {
        static FacialLab lab;
        static int step;
        static double deadline;
        static long epoch;
        static readonly List<string> evidence=new List<string>();
        [MenuItem("Companion/Run M1 Synthetic Play Mode Checks")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play Mode in FacialDiagnostics first.");
            lab=UnityEngine.Object.FindAnyObjectByType<FacialLab>();
            if(lab==null||!lab.Ready)throw new InvalidOperationException("Synthetic lab not ready.");
            evidence.Clear();step=0;
            try {
                Check(FacialLabSetup.Validate(lab.rig).Count==0,"52 named bindings and weight ranges valid");
                Check(FacialLabSetup.Validate(null).Count>0,"missing rig rejected");
                var baseline=new Mesh();var posed=new Mesh();
                try {
                    lab.StopPlayback("Checking geometry");lab.rig.BakeMesh(baseline);
                    for(int c=0;c<52;c++) {
                        lab.SetChannel(c,1);lab.rig.BakeMesh(posed);int changed=0;
                        var a=baseline.vertices;var b=posed.vertices;
                        for(int v=0;v<a.Length;v++)if(Vector3.Distance(a[v],b[v])>.001f)changed++;
                        bool isolated=true;for(int other=0;other<52;other++)if(other!=c&&lab.rig.GetBlendShapeWeight(other)!=0)isolated=false;
                        Check(changed==2&&isolated&&lab.rig.GetBlendShapeWeight(c)==100,"channel "+c+" deforms exactly its two pad vertices");
                    }
                } finally {UnityEngine.Object.DestroyImmediate(baseline);UnityEngine.Object.DestroyImmediate(posed);}
                lab.PlayTone();epoch=lab.Playback.Epoch;deadline=EditorApplication.timeSinceStartup+10;
                EditorApplication.update-=Advance;EditorApplication.update+=Advance;
            } catch(Exception e){Fail(e);}
        }
        static void Check(bool ok,string name){if(!ok)throw new Exception(name);evidence.Add("PASS "+name);}
        static bool Rest(){for(int i=0;i<52;i++)if(lab.rig.GetBlendShapeWeight(i)!=0)return false;return true;}
        static void Advance()
        {
            try {
                if(!EditorApplication.isPlaying||lab==null)throw new Exception("Play Mode ended");
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Audio sample clock timed out at step "+step);
                var source=lab.GetComponent<AudioSource>();
                if(step==0&&source.timeSamples>7000&&!Rest()) {
                    Check(lab.Playing&&lab.Playback.Active,"audio sample clock drives nonzero mesh weights");
                    lab.StopPlayback("Test interrupt");Check(!lab.Playing&&!lab.Playback.Active&&Rest(),"interrupt flushes audio and mesh");
                    Check(!lab.Playback.Enqueue(epoch,"synthetic-tone-v1",new FacialFrame(0,100,new float[52])),"late frame rejected after interruption");
                    lab.PlayTone();step=1;deadline=EditorApplication.timeSinceStartup+10;
                }
                if(step==1&&!lab.Playing&&!lab.Playback.Active) {
                    Check(Rest(),"natural completion resets mesh");
                    lab.PlayTone();lab.SendMessage("OnApplicationPause",true);Check(!lab.Playing&&Rest(),"background callback flushes playback");
                    lab.PlayTone();lab.SendMessage("OnAudioConfigurationChanged",true);Check(!lab.Playing&&Rest(),"route callback flushes playback (simulated)");
                    for(int i=0;i<20;i++){lab.PlayTone();lab.StopPlayback("Repeat session");}
                    Check(Rest()&&!lab.Playing,"20 repeated playback/stop sessions return to rest");
                    lab.StopPlayback("Checks passed • synthetic only");Finish();
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath("../../docs/evidence/m1/synthetic-lab.png"));
                }
            }catch(Exception e){Fail(e);}
        }
        static void Fail(Exception e){evidence.Add("FAIL "+e.Message);if(lab!=null)lab.StopPlayback("Check failed");Finish();Debug.LogException(e);}
        static void Finish()
        {
            EditorApplication.update-=Advance;
            string dir=Path.GetFullPath("../../docs/evidence/m1");Directory.CreateDirectory(dir);
            File.WriteAllLines(Path.Combine(dir,"synthetic-checks.txt"),new[]{DateTime.UtcNow.ToString("O"),"Unity "+Application.unityVersion+" / Windows Editor / synthetic only; no device or measured lip-sync evidence"});
            File.AppendAllLines(Path.Combine(dir,"synthetic-checks.txt"),evidence);Debug.Log(string.Join("\n",evidence));
        }
    }
}
