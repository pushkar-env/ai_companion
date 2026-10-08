using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Companion.Presentation;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Profiling;
using UnityEditor;

namespace Companion.Editor
{
    // Editor measurements include Simulator/Editor overhead; never a mobile certification.
    public static class AlitaPerformanceChecks
    {
        static TalkingCharacter app;static string label;static int phase,lastFrame;
        static double until,timeout;static ProfilerRecorder updateRecorder;
        static readonly List<double> frames=new List<double>(),updates=new List<double>();
        static readonly List<string> report=new List<string>();
        static readonly List<string> samples=new List<string>();
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/evidence/m1/alita-polish"));
        public static void Run(string name)
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play first");
            app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>();app.SelectCharacter(0);
            if(app==null)throw new InvalidOperationException("TalkingCompanion required");
            var index=Array.FindIndex(app.characters,c=>c.name=="Alita");if(index>=0)app.SelectCharacter(index);
            EditorApplication.update-=Tick;if(updateRecorder.Valid)updateRecorder.Dispose();
            label=name;Directory.CreateDirectory(Folder);report.Clear();samples.Clear();frames.Clear();updates.Clear();
            samples.Add("phase,frame_ms,character_update_ms");
            report.Add("Unity "+Application.unityVersion+"; "+SystemInfo.graphicsDeviceName+"; Editor Simulator "+Screen.width+"x"+Screen.height);
            report.Add("Configured targetFrameRate="+Application.targetFrameRate+" vSyncCount="+QualitySettings.vSyncCount);
            var renderers=app.characters.SelectMany(c=>c.model.GetComponentsInChildren<SkinnedMeshRenderer>(true)).ToArray();
            var meshes=renderers.Select(r=>r.sharedMesh).Distinct().ToArray();
            var textures=renderers.SelectMany(r=>r.sharedMaterials).Distinct().SelectMany(m=>m.GetTexturePropertyNames().Select(n=>m.GetTexture(n))).Where(t=>t!=null).Distinct().ToArray();
            report.Add("Referenced characters="+app.characters.Length+" meshes="+meshes.Length+" meshBytes="+meshes.Sum(m=>Profiler.GetRuntimeMemorySizeLong(m))+" textures="+textures.Length+" textureBytes="+textures.Sum(t=>Profiler.GetRuntimeMemorySizeLong(t)));
            var active=app.character.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r=>r.enabled).ToArray();
            report.Add("Active rig triangles="+active.Sum(r=>r.sharedMesh.triangles.Length/3)+" materialSlots="+active.Sum(r=>r.sharedMaterials.Length));
            app.NewChat();phase=0;until=EditorApplication.timeSinceStartup+5;timeout=until+150;
            updateRecorder=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"Companion.CharacterUpdate",1);
            lastFrame=Time.frameCount;EditorApplication.update+=Tick;
        }
        static void Sample(string mode)
        {
            if(Time.frameCount==lastFrame)return;lastFrame=Time.frameCount;
            double frame=Time.unscaledDeltaTime*1000,update=updateRecorder.LastValue/1000000.0;
            frames.Add(frame);updates.Add(update);samples.Add(FormattableString.Invariant($"{mode},{frame:F4},{update:F6}"));
        }
        static double Percentile(List<double> data,double p){var values=data.OrderBy(v=>v).ToArray();return values[Math.Max(0,(int)Math.Ceiling(values.Length*p)-1)];}
        static void Summarize(string mode)
        {
            if(frames.Count==0)throw new Exception("No rendered samples");
            report.Add(FormattableString.Invariant($"{mode}: n={frames.Count}; frame p50={Percentile(frames,.5):F3}ms p95={Percentile(frames,.95):F3}ms p99={Percentile(frames,.99):F3}ms max={frames.Max():F3}ms; >100ms={frames.Count(f=>f>100)}; character update p50={Percentile(updates,.5):F4}ms p95={Percentile(updates,.95):F4}ms"));
            frames.Clear();updates.Clear();
        }
        static void Tick()
        {
            try {
                double now=EditorApplication.timeSinceStartup;
                if(!EditorApplication.isPlaying||app==null)throw new Exception("Play stopped");
                if(now>timeout)throw new Exception("Timed out: "+app.State);
                if(phase==0&&now>=until){phase=1;until=now+30;}
                else if(phase==1){Sample("idle");if(now>=until){Summarize("idle");app.Submit("Please say exactly: Today is a lovely day to pause and enjoy a quiet moment. We can take a small happy step together.");phase=2;timeout=now+120;}}
                else if(phase==2&&app.CanReplay){app.Replay();phase=3;until=now+30;timeout=until+10;lastFrame=Time.frameCount;}
                else if(phase==3){Sample("speech-replay");if(app.CanReplay)app.Replay();if(now>=until){Summarize("speech-replay");app.Interrupt();report.Add("PASS timed idle and real cached speech workloads completed");Finish();}}
            }catch(Exception e){report.Add("FAIL "+e.Message);Finish();Debug.LogError(e.Message);}
        }
        static void Finish(){EditorApplication.update-=Tick;updateRecorder.Dispose();File.WriteAllLines(Path.Combine(Folder,label+".txt"),report);File.WriteAllLines(Path.Combine(Folder,label+".csv"),samples);Debug.Log("Alita performance report saved: "+label);}
    }
}
