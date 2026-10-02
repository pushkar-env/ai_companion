using System;
using System.IO;
using System.Collections.Generic;
using Companion.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Companion.Editor
{
    public static class CCCharacterLabChecks
    {
        static CCCharacterLab lab;static int step;static double next,deadline;static long epoch;
        static readonly List<string> evidence=new List<string>();
        static string Dir=>Path.GetFullPath("../../docs/evidence/m1/cc-character-test");
        [MenuItem("Companion/Run CC Character Play Mode Checks")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play Mode in CCCharacterTest first.");
            lab=UnityEngine.Object.FindAnyObjectByType<CCCharacterLab>();if(lab==null||!lab.Ready)throw new InvalidOperationException("CC lab not ready");
            evidence.Clear();Directory.CreateDirectory(Dir);
            try {
                foreach(string channel in CCCharacterLab.Channels) {
                    Check(lab.BindingCount(channel)>0,"binding present: "+channel);lab.SetChannel(channel,.7f);Check(!lab.AtRest,"applies: "+channel);lab.Stop();Check(lab.AtRest,"resets: "+channel);
                }
                Check(lab.BindingCount("Eye_Blink_L")>1,"blink drives coordinated meshes");
                CheckBlinkCorrective("Eye_Blink_L","C_BlinkL");
                CheckBlinkCorrective("Eye_Blink_R","C_BlinkR");
                CheckJawCalibration();
                lab.Pose("Open");Check(lab.JawAngle>1,"prototype jaw bone assist moves jaw");lab.Stop();Check(lab.AtRest,"jaw returns to bind pose");
                bool rejected=false;try{lab.SetChannel("missing",.5f);}catch(ArgumentException){rejected=true;}Check(rejected,"unknown binding rejected");
                rejected=false;try{lab.SetChannel("V_Open",float.NaN);}catch(ArgumentOutOfRangeException){rejected=true;}Check(rejected,"NaN rejected");
                lab.Stop();step=0;next=EditorApplication.timeSinceStartup+.7;deadline=next+20;EditorApplication.update-=Advance;EditorApplication.update+=Advance;
            } catch(Exception e){Fail(e);}
        }
        static void Check(bool ok,string name){if(!ok)throw new Exception(name);evidence.Add("PASS "+name);}
        static void CheckJawCalibration()
        {
            float original=lab.JawCalibrationDegrees;var baked=new Mesh();
            try {
                lab.Stop();var body=Array.Find(lab.character.GetComponentsInChildren<SkinnedMeshRenderer>(),r=>r.name=="CC_Base_Body");
                body.BakeMesh(baked);var neutral=baked.vertices;
                lab.SetJawCalibration(20);lab.PreviewJaw(.7f);
                Check(Mathf.Abs(lab.JawAngle-14)<.01f,"bone-only calibrated angle applied");
                bool morphsNeutral=true;foreach(var r in lab.character.GetComponentsInChildren<SkinnedMeshRenderer>())for(int i=0;i<r.sharedMesh.blendShapeCount;i++)morphsNeutral&=r.GetBlendShapeWeight(i)==0;
                Check(morphsNeutral,"bone-only preview leaves every morph neutral");
                body.BakeMesh(baked);var posed=baked.vertices;float moved=0;for(int i=0;i<neutral.Length;i++)moved=Mathf.Max(moved,(posed[i]-neutral[i]).magnitude);
                Check(moved>1e-5f,"bone-only preview deforms skinned body geometry");
                lab.Stop();body.BakeMesh(baked);var reset=baked.vertices;float residual=0;for(int i=0;i<neutral.Length;i++)residual=Mathf.Max(residual,(reset[i]-neutral[i]).magnitude);
                Check(lab.AtRest&&residual<1e-6f,"bone-only reset restores actual geometry");
                lab.PlayTone();long before=lab.Epoch;lab.SetJawCalibration(12);
                Check(!lab.Playing&&lab.AtRest&&lab.Epoch>before,"angle edit cancels active audio and restores neutral");
                foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,-1f,31f}) {
                    bool rejected=false;try{lab.SetJawCalibration(invalid);}catch(ArgumentOutOfRangeException){rejected=true;}
                    Check(rejected&&lab.JawCalibrationDegrees==12,"invalid calibration rejected without changing angle: "+invalid);
                }
                lab.SetJawCalibration(0);lab.PreviewJaw(1);Check(lab.AtRest,"zero angle bone-only preview stays neutral");
                lab.SetJawCalibration(30);lab.PreviewJaw(1);Check(Mathf.Abs(lab.JawAngle-30)<.01f,"maximum provisional angle bounded at 30 degrees");
                lab.SendMessage("OnApplicationPause",true);Check(lab.AtRest,"background resets bone-only preview");
            } finally {lab.SetJawCalibration(original);UnityEngine.Object.DestroyImmediate(baked);}
        }
        static void CheckBlinkCorrective(string blink,string corrective)
        {
            var body=Array.Find(lab.character.GetComponentsInChildren<SkinnedMeshRenderer>(),r=>r.name=="CC_Base_Body");
            int index=body.sharedMesh.GetBlendShapeIndex(corrective);
            Check(index>=0,corrective+" binding present");
            var baked=new Mesh();
            try {
                lab.SetChannel(blink,.5f);Check(body.GetBlendShapeWeight(index)==100,corrective+" peaks at half blink");
                body.BakeMesh(baked);var corrected=baked.vertices;body.SetBlendShapeWeight(index,0);body.BakeMesh(baked);var raw=baked.vertices;
                int moved=0;for(int i=0;i<raw.Length;i++)if((corrected[i]-raw[i]).sqrMagnitude>1e-12f)moved++;
                Check(moved>0,corrective+" changes actual eyelid geometry");
                lab.SetChannel(blink,1);Check(body.GetBlendShapeWeight(index)==0,corrective+" zero at full closure");
                lab.Stop();Check(lab.AtRest,corrective+" resets to neutral");
            } finally {UnityEngine.Object.DestroyImmediate(baked);}
        }
        static void Capture(string name){ScreenCapture.CaptureScreenshot(Path.Combine(Dir,name+".png"));}
        static void Advance()
        {
            try {
                if(!EditorApplication.isPlaying||lab==null)throw new Exception("Play Mode ended");
                double now=EditorApplication.timeSinceStartup;if(now>deadline)throw new Exception("Timed out at "+step);if(now<next)return;
                if(step==0) {
                    var root=lab.GetComponent<UIDocument>().rootVisualElement;var preview=root.Q("character-preview").worldBound;var reset=root.Q("character-reset").worldBound;
                    Check(preview.height>=150&&reset.yMax<=root.worldBound.yMax+1,"portrait preview and fixed reset visible");Capture("neutral");step=1;next=now+.5;return;
                }
                if(step==1){lab.Pose("Blink");step=2;next=now+.5;return;}
                if(step==2){Capture("blink");step=3;next=now+.5;return;}
                if(step==3){lab.Pose("Open");step=4;next=now+.5;return;}
                if(step==4){Capture("open");step=5;next=now+.5;return;}
                if(step==5){lab.PlayTone();epoch=lab.Epoch;step=6;return;}
                if(step==6&&lab.GetComponent<AudioSource>().timeSamples>8000&&!lab.AtRest){Check(lab.Playing,"audio sample clock drives native face");
                    int before=lab.GetComponent<AudioSource>().timeSamples;lab.Stop();
                    var report=lab.LastPlayback;
                    Check(report.observedSamples>=before&&report.observedSamples<report.durationSamples,"interrupt captures actual sample cursor before reset");
                    Check(report.epoch==epoch&&report.terminal=="interrupted"&&report.sampleRate>0,"report retains old playback epoch and format");
                    Check(lab.GetComponent<AudioSource>().timeSamples==0,"source cursor resets after report captured");
                    File.WriteAllText(Path.Combine(Dir,"playback-report.json"),JsonUtility.ToJson(report,true));
                    lab.Stop();Check(lab.LastPlayback.utteranceId==report.utteranceId&&lab.LastPlayback.observedSamples==report.observedSamples,"duplicate stop preserves finalized report");
                    Check(lab.Epoch>epoch&&!lab.Playing&&lab.AtRest,"interrupt advances epoch and clears audio/face");lab.PlayTone();step=7;return;}
                if(step==7&&!lab.Playing){Check(lab.AtRest,"natural completion restores neutral");
                    Check(lab.LastPlayback.terminal=="source_stopped"&&lab.LastPlayback.observedSamples>0&&lab.LastPlayback.observedSamples<=lab.LastPlayback.durationSamples,"source end retains observed cursor without inferring full hearing");lab.PlayTone();lab.SendMessage("OnApplicationPause",true);Check(!lab.Playing&&lab.AtRest,"background simulation stops playback");for(int i=0;i<20;i++){lab.PlayTone();lab.Stop();}Check(!lab.Playing&&lab.AtRest,"20 sessions clean up");lab.Stop("Checks passed • ready to test");Finish();}
            } catch(Exception e){Fail(e);}
        }
        static void Fail(Exception e){evidence.Add("FAIL "+e.Message);lab?.Stop("Check failed");Finish();Debug.LogException(e);}
        static void Finish(){EditorApplication.update-=Advance;File.WriteAllLines(Path.Combine(Dir,"checks.txt"),new[]{DateTime.UtcNow.ToString("O"),"Unity "+Application.unityVersion+" Editor; native CC test mapping, synthetic audio; no device/production validation"});File.AppendAllLines(Path.Combine(Dir,"checks.txt"),evidence);Debug.Log(string.Join("\n",evidence));}
    }
}
