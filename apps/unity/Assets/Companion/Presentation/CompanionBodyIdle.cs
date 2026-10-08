using System;
using System.Collections.Generic;
using UnityEngine;

namespace Companion.Presentation
{
    // Owned by TalkingCharacter. Does not touch the face, jaw, root, or source assets.
    // Samples absolute time from a cached bind pose, so frame rate and Stop do not accumulate drift.
    public sealed partial class CompanionBodyIdle : IDisposable
    {
        readonly Transform model,hip,waist,spine,chest;
        readonly List<Pose> poses=new List<Pose>();
        readonly Arm leftArm,rightArm;readonly Leg leftLeg,rightLeg;
        readonly Vector3 hipPosition;
        struct Pose {public Transform bone;public Vector3 position;public Quaternion rotation;}
        sealed class Arm {public Transform shoulder,upper,lower,hand;public float side;public readonly List<FingerJoint> fingers=new List<FingerJoint>();}
        struct FingerJoint {public Transform bone;public Vector3 curlAxis,oppositionAxis;public float angle,gain,phase,opposition;}
        sealed class Leg {public Transform upper,lower,foot;public Vector3 anchor;public Quaternion rotation;}
        public bool IsBound {get;}
        public CompanionBodyIdle(Transform model,int? randomSeed=null)
        {
            random=new System.Random(randomSeed??Guid.NewGuid().GetHashCode());
            this.model=model;
            var bones=new Dictionary<string,Transform>();foreach(var t in model.GetComponentsInChildren<Transform>())bones[t.name]=t;
            Transform Find(string name)=>bones.TryGetValue("CC_Base_"+name,out var t)?t:null;
            hip=Find("Hip");waist=Find("Waist");spine=Find("Spine01");chest=Find("Spine02");
            Arm BindArm(string side,float sign)=>new Arm {shoulder=Find(side+"_Clavicle"),upper=Find(side+"_Upperarm"),lower=Find(side+"_Forearm"),hand=Find(side+"_Hand"),side=sign};
            Leg BindLeg(string side)=>new Leg {upper=Find(side+"_Thigh"),lower=Find(side+"_Calf"),foot=Find(side+"_Foot")};
            leftArm=BindArm("L",-1);rightArm=BindArm("R",1);leftLeg=BindLeg("L");rightLeg=BindLeg("R");
            bool Valid(Arm a)=>a.shoulder!=null&&a.upper!=null&&a.lower!=null&&a.hand!=null;
            bool ValidLeg(Leg l)=>l.upper!=null&&l.lower!=null&&l.foot!=null;
            IsBound=hip!=null&&waist!=null&&spine!=null&&chest!=null&&Valid(leftArm)&&Valid(rightArm)&&ValidLeg(leftLeg)&&ValidLeg(rightLeg);
            if(!IsBound)return;
            hipPosition=hip.localPosition;
            foreach(var t in new[]{hip,waist,spine,chest,leftArm.shoulder,leftArm.upper,leftArm.lower,leftArm.hand,rightArm.shoulder,rightArm.upper,rightArm.lower,rightArm.hand,leftLeg.upper,leftLeg.lower,leftLeg.foot,rightLeg.upper,rightLeg.lower,rightLeg.foot})
                poses.Add(new Pose {bone=t,position=t.localPosition,rotation=t.localRotation});
            BindFingers(leftArm,"L",Find);BindFingers(rightArm,"R",Find);
            foreach(var leg in new[]{leftLeg,rightLeg}){leg.anchor=model.InverseTransformPoint(leg.foot.position);leg.rotation=Quaternion.Inverse(model.rotation)*leg.foot.rotation;}
            Sample(0);
        }
        public void Sample(float seconds)
        {
            if(!IsBound)return;
            Restore();
            // A continuous 24-second cycle: five breaths, one unhurried weight shift.
            float cycle=seconds*(2*Mathf.PI/24f);
            float breath=Mathf.Sin(cycle*5);
            float shift=.35f+.65f*Mathf.Sin(cycle);
            float drift=Mathf.Sin(cycle*2+.7f);
            // Keep a slight knee bend throughout the shift so IK never hyperextends either leg.
            hip.position=hip.parent.TransformPoint(hipPosition)+model.TransformVector(new Vector3(.012f*shift,-.009f+.0015f*breath,.002f*drift));
            Rotate(hip,Vector3.forward,-.65f*shift);Rotate(hip,Vector3.up,.65f*drift);
            Rotate(waist,Vector3.forward,.5f*shift);Rotate(spine,Vector3.right,.5f*breath);
            Rotate(chest,Vector3.right,.65f*breath);Rotate(chest,Vector3.forward,.35f*shift);Rotate(chest,Vector3.up,-.45f*drift);
            AnimateArm(leftArm,cycle,breath);AnimateArm(rightArm,cycle+.55f,breath);
            Plant(leftLeg);Plant(rightLeg);
        }
        void Rotate(Transform bone,Vector3 axis,float angle)=>bone.rotation=Quaternion.AngleAxis(angle,model.TransformDirection(axis))*bone.rotation;
        void BindFingers(Arm arm,string side,Func<string,Transform> find)
        {
            var index=find(side+"_Index1");var pinky=find(side+"_Pinky1");
            if(index==null||pinky==null)return;
            // Anatomical palm normal from the actual rig; mirrored hands need opposite signs.
            Vector3 palm=Vector3.Cross(index.position-arm.hand.position,pinky.position-arm.hand.position).normalized*(-arm.side);
            string[] digits={"Index","Mid","Ring","Pinky","Thumb"};
            float[,] angles={{13,27,13},{19,35,17},{25,42,21},{31,46,24},{16,23,16}};
            for(int d=0;d<digits.Length;d++)for(int j=1;j<=3;j++) {
                var bone=find(side+"_"+digits[d]+j);if(bone==null)continue;
                var child=j<3?find(side+"_"+digits[d]+(j+1)):null;
                Vector3 direction=child!=null?child.position-bone.position:bone.position-bone.parent.position;
                var axis=bone.InverseTransformDirection(Vector3.Cross(direction.normalized,palm).normalized);
                poses.Add(new Pose {bone=bone,position=bone.localPosition,rotation=bone.localRotation});
                Vector3 towardsIndex=(index.position-bone.position).normalized;
                arm.fingers.Add(new FingerJoint {bone=bone,curlAxis=axis,angle=angles[d,j-1]+(arm.side>0?2:0),gain=j==2?1:.6f,phase=d*.16f,
                    oppositionAxis=bone.InverseTransformDirection(Vector3.Cross(direction.normalized,towardsIndex).normalized),
                    opposition=d==4&&j==1?Vector3.Angle(direction,towardsIndex)*.55f:0});
            }
        }
        void AnimateArm(Arm arm,float time,float breath)
        {
            float swing=Mathf.Sin(time*2);float settle=Mathf.Sin(time);
            Rotate(arm.shoulder,Vector3.forward,arm.side*(.45f*breath+.25f*settle));
            Aim(arm.upper,arm.lower,model.TransformDirection(new Vector3(arm.side*(.23f+.018f*swing),-1,.025f+.015f*settle)));
            // Soft elbows: forearms point a little forward, with hands clear of the dress.
            Aim(arm.lower,arm.hand,model.TransformDirection(new Vector3(arm.side*(.16f+.02f*settle),-1,(arm.side>0?.31f:.22f)+.03f*swing)));
            // A little wrist extension and inward roll keeps the hands soft rather than paddle-flat.
            Rotate(arm.hand,Vector3.right,-7+2f*breath+1.5f*settle);
            Rotate(arm.hand,Vector3.up,arm.side*(8+2*settle));
            Rotate(arm.hand,Vector3.forward,arm.side*(3+1.5f*swing));
            foreach(var joint in arm.fingers) {
                float release=Mathf.Pow(.5f+.5f*Mathf.Sin(time-joint.phase),4);
                float flex=(2.5f*breath-8*release)*joint.gain;
                if(joint.opposition>0)joint.bone.localRotation*=Quaternion.AngleAxis(joint.opposition-2*release,joint.oppositionAxis);
                joint.bone.localRotation*=Quaternion.AngleAxis(joint.angle+flex,joint.curlAxis);
            }
        }
        static void Aim(Transform bone,Transform child,Vector3 direction)
        {
            var from=child.position-bone.position;if(from.sqrMagnitude<1e-10f||direction.sqrMagnitude<1e-10f)return;
            bone.rotation=Quaternion.FromToRotation(from,direction)*bone.rotation;
        }
        void Plant(Leg leg)=>Plant(leg,Vector3.zero,0);
        void Plant(Leg leg,Vector3 offset,float yaw)
        {
            Vector3 target=model.TransformPoint(leg.anchor+offset),start=leg.upper.position;
            float upper=Vector3.Distance(start,leg.lower.position),lower=Vector3.Distance(leg.lower.position,leg.foot.position);
            Vector3 delta=target-start;float distance=Mathf.Clamp(delta.magnitude,Mathf.Abs(upper-lower)+.0001f,upper+lower-.0001f);
            var direction=delta.normalized;
            var bend=Vector3.ProjectOnPlane(model.forward,direction).normalized;
            float along=(upper*upper-lower*lower+distance*distance)/(2*distance);
            float outwards=Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));
            var knee=start+direction*along+bend*outwards;
            Aim(leg.upper,leg.lower,knee-start);Aim(leg.lower,leg.foot,target-leg.lower.position);
            leg.foot.rotation=Quaternion.AngleAxis(yaw,model.up)*model.rotation*leg.rotation;
        }
        public void Restore(){foreach(var pose in poses)if(pose.bone!=null){pose.bone.localPosition=pose.position;pose.bone.localRotation=pose.rotation;}}
        public void Dispose()=>Restore();
    }
}
