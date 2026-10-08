using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Companion.Presentation;
using UnityEditor;
using UnityEngine;

namespace Companion.Editor
{
    public static class BodyIdleChecks
    {
        [MenuItem("Companion/Run Full Body Idle Checks")]
        public static void Run()
        {
            if(EditorApplication.isPlaying)throw new Exception("Run the isolated rig checks while stopped");
            var source=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>();
            var copy=UnityEngine.Object.Instantiate(source.character.gameObject);copy.hideFlags=HideFlags.HideAndDontSave;
            var lines=new List<string>();CompanionBodyIdle idle=null;
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/evidence/body-idle"));Directory.CreateDirectory(folder);
            void Check(bool value,string label){if(!value)throw new Exception(label);lines.Add("PASS "+label);}
            try {
                var bones=copy.GetComponentsInChildren<Transform>().ToDictionary(t=>t.name);
                Transform Bone(string name)=>bones["CC_Base_"+name];
                var original=bones.Values.Select(t=>(t,t.localPosition,t.localRotation)).ToArray();
                var feet=new[]{Bone("L_Foot"),Bone("R_Foot")};var anchors=feet.Select(t=>t.position).ToArray();var feetRot=feet.Select(t=>t.rotation).ToArray();
                var rootPosition=copy.transform.position;var rootRotation=copy.transform.rotation;
                var head=Bone("Head").localRotation;var jaw=Bone("JawRoot").localRotation;
                idle=new CompanionBodyIdle(copy.transform);Check(idle.IsBound,"all required body chains bind on Alita");
                foreach(var side in new[]{"L","R"}) {
                    var shoulder=Bone(side+"_Upperarm");var hand=Bone(side+"_Hand");var elbow=Bone(side+"_Forearm");
                    Check(hand.position.y<shoulder.position.y-.4f,"relaxed "+side+" hand lowered beside thigh");
                    Check(Mathf.Abs(hand.position.x-rootPosition.x)<.32f,"relaxed "+side+" arm no longer held in A-pose");
                    Check(Vector3.Angle(elbow.position-shoulder.position,hand.position-elbow.position)>5,"relaxed "+side+" elbow has soft bend");
                }
                var tracked=new[]{"Hip","Waist","Spine01","Spine02","L_Clavicle","R_Clavicle","L_Upperarm","R_Upperarm","L_Forearm","R_Forearm","L_Hand","R_Hand","L_Thigh","R_Thigh","L_Calf","R_Calf"}.Select(Bone).ToArray();
                var initial=tracked.Select(t=>t.localRotation).ToArray();var maxMotion=new float[tracked.Length];var previous=tracked.Select(t=>t.localRotation).ToArray();
                float slip=0,turn=0,step=0;
                var renderer=copy.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name=="CC_Base_Body");
                var mesh=new Mesh();renderer.BakeMesh(mesh);var firstVertices=mesh.vertices;
                for(int frame=0;frame<=1800;frame++) {
                    idle.Sample(frame/60f);
                    for(int i=0;i<tracked.Length;i++){maxMotion[i]=Mathf.Max(maxMotion[i],Quaternion.Angle(initial[i],tracked[i].localRotation));step=Mathf.Max(step,Quaternion.Angle(previous[i],tracked[i].localRotation));previous[i]=tracked[i].localRotation;}
                    for(int i=0;i<feet.Length;i++){slip=Mathf.Max(slip,Vector3.Distance(anchors[i],feet[i].position));turn=Mathf.Max(turn,Quaternion.Angle(feetRot[i],feet[i].rotation));}
                }
                foreach(var pair in tracked.Select((t,i)=>(t,i)))Check(maxMotion[pair.i]>.1f,pair.t.name+" animates across 30 seconds");
                Check(slip<.0005f,"feet remain planted within 0.5 mm (max "+slip.ToString("F6")+" m)");
                Check(turn<.1f,"foot orientation stays stable (max "+turn.ToString("F4")+" degrees)");
                Check(step<1,"body motion stays continuous at 60 Hz (max step "+step.ToString("F4")+" degrees)");
                Check(copy.transform.position==rootPosition&&Quaternion.Angle(copy.transform.rotation,rootRotation)<.001f,"no character root drift");
                Check(Quaternion.Angle(head,Bone("Head").localRotation)<.001f&&Quaternion.Angle(jaw,Bone("JawRoot").localRotation)<.001f,"idle leaves head and jaw speech channels untouched");
                renderer.BakeMesh(mesh);Check(mesh.vertices.Where((v,i)=>(v-firstVertices[i]).sqrMagnitude>1e-9f).Count()>500,"idle changes actual skinned body vertices");UnityEngine.Object.DestroyImmediate(mesh);
                idle.Sample(1.23f);var pose=tracked.Select(t=>t.localRotation).ToArray();idle.Sample(10000);idle.Sample(1.23f);
                Check(tracked.Select((t,i)=>Quaternion.Angle(t.localRotation,pose[i])).Max()<.05f,"absolute-time sampling has no accumulated pose drift");
                var fingers=(from side in new[]{"L","R"} from digit in new[]{"Index","Mid","Ring","Pinky","Thumb"} from joint in new[]{1,2,3} select Bone(side+"_"+digit+joint)).ToArray();
                idle.Sample(0);var relaxed=fingers.Select(t=>t.localRotation).ToArray();
                Check(fingers.All(t=>Quaternion.Angle(t.localRotation,original.First(p=>p.t==t).localRotation)>5),"all 30 finger/thumb joints leave the straight bind pose");
                idle.Sample(6);
                Check(fingers.Select((t,i)=>Quaternion.Angle(t.localRotation,relaxed[i])).All(a=>a>.2f),"all finger/thumb joints animate through the release and settling cycle");
                idle.Sample(0);var loop=bones.Values.Select(t=>(t,t.localPosition,t.localRotation)).ToArray();idle.Sample(24);
                Check(loop.All(p=>Vector3.Distance(p.t.localPosition,p.localPosition)<.00001f&&Quaternion.Angle(p.t.localRotation,p.localRotation)<.05f),"24-second loop closes across body, wrists and fingers without a pose jump");
                idle.Dispose();idle=null;
                Check(original.All(p=>Vector3.Distance(p.t.localPosition,p.localPosition)<1e-6f&&Quaternion.Angle(p.t.localRotation,p.localRotation)<.05f),"dispose restores original rig pose");
            }catch(Exception e){lines.Add("FAIL "+e.Message);Debug.LogError(e.Message);}
            finally{idle?.Dispose();UnityEngine.Object.DestroyImmediate(copy);File.WriteAllLines(Path.Combine(folder,"rig-checks.txt"),lines);}
        }
    }
}
