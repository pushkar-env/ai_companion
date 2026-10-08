using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Companion.Presentation;

namespace Companion.Editor
{
    public static class ExpressiveIdleChecks
    {
        public static void Run()
        {
            if(EditorApplication.isPlaying)throw new Exception("Run isolated checks while stopped");
            var source=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>();var go=UnityEngine.Object.Instantiate(source.character.gameObject);go.hideFlags=HideFlags.HideAndDontSave;
            var bones=go.GetComponentsInChildren<Transform>().ToDictionary(t=>t.name);Transform Bone(string n)=>bones["CC_Base_"+n];
            var original=bones.Values.Select(t=>(t,t.localPosition,t.localRotation)).ToArray();var results=new List<string>();var idle=new CompanionBodyIdle(go.transform,8416);
            void Check(bool ok,string text){results.Add((ok?"PASS ":"FAIL ")+text);if(!ok)throw new Exception(text);}
            try {
                var gestures=(CompanionBodyIdle.Gesture[])Enum.GetValues(typeof(CompanionBodyIdle.Gesture));
                var left=Bone("L_Foot");var right=Bone("R_Foot");var anchors=new[]{left.position,right.position};
                var tracked=new[]{Bone("L_Hand"),Bone("R_Hand"),left,right};
                foreach(var gesture in gestures.Where(g=>g!=CompanionBodyIdle.Gesture.None)) {
                    float slip=0,lift=0,travel=0,step=0;
                    foreach(float mirror in new[]{-1f,1f}) {
                        idle.SampleGesture(0,gesture,0,mirror);var previous=tracked.Select(t=>t.position).ToArray();
                        for(int frame=0;frame<=480;frame++) {
                            idle.SampleGesture(frame/60f,gesture,frame/480f,mirror);
                            var support=gesture==CompanionBodyIdle.Gesture.FootAdjust?(mirror<0?right:left):left;
                            var supportIndex=support==left?0:1;slip=Mathf.Max(slip,Vector3.Distance(support.position,anchors[supportIndex]));
                            if(gesture!=CompanionBodyIdle.Gesture.FootAdjust)slip=Mathf.Max(slip,Vector3.Distance(right.position,anchors[1]));
                            var moving=mirror<0?left:right;var anchor=anchors[mirror<0?0:1];lift=Mathf.Max(lift,moving.position.y-anchor.y);travel=Mathf.Max(travel,Vector3.ProjectOnPlane(moving.position-anchor,Vector3.up).magnitude);
                            CheckFinite(left.position);CheckFinite(right.position);
                            if(left.position.y<anchors[0].y-.0005f||right.position.y<anchors[1].y-.0005f)throw new Exception("Foot penetrates ground: "+gesture);
                            for(int i=0;i<tracked.Length;i++){step=Mathf.Max(step,Vector3.Distance(previous[i],tracked[i].position));previous[i]=tracked[i].position;}
                        }
                    }
                    Check(slip<.0006f,gesture+" support foot planted within 0.6 mm");
                    Check(step<.04f,gesture+" no hand/foot jumps at 60 Hz (max "+step.ToString("F4")+" m)");
                    if(gesture==CompanionBodyIdle.Gesture.FootAdjust)Check(lift>.015f&&travel>.045f,"foot adjustment lifts sole and changes stance before returning");
                    idle.Sample(8);var neutral=tracked.Select(t=>t.position).ToArray();idle.SampleGesture(8,gesture,1);
                    Check(tracked.Select((t,i)=>Vector3.Distance(t.position,neutral[i])).Max()<.0001f,gesture+" joins neutral with no endpoint snap");
                }
                idle.SampleGesture(0,CompanionBodyIdle.Gesture.Yawn,.5f);
                Check(Bone("L_Hand").position.y>Bone("Head").position.y+.25f&&Bone("R_Hand").position.y>Bone("Head").position.y+.25f,"yawn raises both hands above head");
                Check(idle.YawnWeight>.95f,"yawn supplies facial envelope at held stretch");
                var started=new List<(CompanionBodyIdle.Gesture kind,float time)>();var durations=new List<float>();var last=CompanionBodyIdle.Gesture.None;float start=0;
                for(int frame=0;frame<36000;frame++) {
                    idle.Advance(1/60f,false,false);var now=idle.CurrentGesture;
                    if(now!=last&&now!=CompanionBodyIdle.Gesture.None){started.Add((now,frame/60f));start=frame/60f;}
                    if(now==CompanionBodyIdle.Gesture.None&&last!=now)durations.Add(frame/60f-start);last=now;
                }
                Check(started.Select(x=>x.kind).Distinct().Count()==5,"ten-minute schedule includes every gesture");
                Check(started.Select((x,i)=>i<2||x.kind!=started[i-1].kind&&x.kind!=started[i-2].kind).All(x=>x),"no repeats within the last two gestures");
                var yawns=started.Where(x=>x.kind==CompanionBodyIdle.Gesture.Yawn).Select(x=>x.time).ToArray();
                Check(yawns.Select((x,i)=>i==0||x-yawns[i-1]>=79.9f).All(x=>x),"yawns have at least 80-second cooldown");
                Check(durations.Max()-durations.Min()>2,"gesture durations vary");
                Check(yawns.Length>0&&yawns[0]>=79.9f,"no yawn in the first 80 seconds after launch");
                idle.PreviewGesture(CompanionBodyIdle.Gesture.Yawn);
                for(int i=0;i<240;i++)idle.Advance(1/60f,false,false);
                idle.Advance(1/60f,true,false);
                Check(idle.YawnWeight==0&&idle.HeadTiltWeight>.8f,"speech takes facial channels immediately while head tilt eases out");
                for(int i=0;i<480;i++)idle.Advance(1/60f,true,false);
                Check(idle.CurrentGesture==CompanionBodyIdle.Gesture.None&&idle.YawnWeight==0,"conversation settles large gestures and releases face");
                for(int i=0;i<1200;i++)idle.Advance(1/60f,true,false);
                Check(idle.CurrentGesture==CompanionBodyIdle.Gesture.None,"conversation prevents new decorative gestures");
                idle.Advance(.1f,false,true);var reduced=tracked.Select(t=>t.position).ToArray();idle.Advance(.1f,false,true);
                Check(tracked.Select((t,i)=>Vector3.Distance(t.position,reduced[i])).Max()<.00001f&&idle.YawnWeight==0,"reduced motion holds the relaxed pose");
                Check(go.transform.position==source.character.position&&Quaternion.Angle(go.transform.rotation,source.character.rotation)<.001f,"root has no accumulated drift after ten simulated minutes");
                idle.Dispose();Check(original.All(p=>Vector3.Distance(p.t.localPosition,p.localPosition)<1e-6f&&Quaternion.Angle(p.t.localRotation,p.localRotation)<.05f),"dispose restores all original rig transforms");
            }catch(Exception e){results.Add("FAIL "+e.Message);Debug.LogError(e.Message);}
            finally{idle.Dispose();UnityEngine.Object.DestroyImmediate(go);string folder=Path.GetFullPath(Application.dataPath+"/../../../docs/evidence/expressive-idle");Directory.CreateDirectory(folder);File.WriteAllLines(Path.Combine(folder,"checks.txt"),results);}
        }
        static void CheckFinite(Vector3 p){if(float.IsNaN(p.x)||float.IsNaN(p.y)||float.IsNaN(p.z))throw new Exception("Non-finite IK pose");}
    }
}
