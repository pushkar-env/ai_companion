using System;
using UnityEngine;

namespace Companion.Presentation
{
    public sealed partial class CompanionBodyIdle
    {
        public enum Gesture {None,FootAdjust,HipTurn,ShoulderRoll,SideStretch,Yawn}
        readonly System.Random random;
        float elapsed,phaseTime,eventAge,eventDuration,wait=5,blend=1,side=1,strength=1;
        float lastYawn=0,lastStretch=-15;
        Gesture current,previous,beforePrevious;
        bool cancelling;
        public Gesture CurrentGesture=>current;
        public float YawnWeight {get;private set;}
        public float StretchWeight {get;private set;}
        public float HeadTiltWeight {get;private set;}
        public float GestureProgress=>current==Gesture.None?0:eventAge/eventDuration;
#if UNITY_EDITOR
        public void PreviewGesture(Gesture gesture,float mirror=1){Begin(gesture,mirror,1);eventDuration=8;}
#endif
        float Range(float min,float max)=>Mathf.Lerp(min,max,(float)random.NextDouble());
        static float Smooth(float x){x=Mathf.Clamp01(x);return x*x*x*(x*(x*6-15)+10);}
        static float Envelope(float p)=>Smooth(p/.27f)*(1-Smooth((p-.67f)/.33f));

        // Session-local scheduling: unequal pauses, cooldowns, mirrored variants and no
        // repeated member of the last two gestures. No global Unity random state changes.
        public void Advance(float delta,bool engaged,bool reduced)
        {
            delta=Mathf.Clamp(delta,0,.1f);elapsed+=delta;
            phaseTime+=delta*(1+.12f*Mathf.Sin(elapsed*.117f)+.07f*Mathf.Sin(elapsed*.053f));
            if(reduced){current=Gesture.None;wait=Range(6,12);YawnWeight=StretchWeight=HeadTiltWeight=0;Sample(0);return;}
            if(current==Gesture.None) {
                if(engaged)wait=Mathf.Max(wait,4);
                else if((wait-=delta)<=0) {
                    Gesture chosen;
                    int attempts=0;
                    do {chosen=(Gesture)random.Next(1,6);attempts++;}
                    while(attempts<40&&(chosen==previous||chosen==beforePrevious||chosen==Gesture.Yawn&&elapsed-lastYawn<80||chosen==Gesture.SideStretch&&elapsed-lastStretch<35));
                    if(attempts<40)Begin(chosen,Range(0,1)>.5f?1:-1,Range(.88f,1.06f));else wait=3;
                }
            }else {
                eventAge+=delta;
                if(engaged&&current!=Gesture.FootAdjust)cancelling=true;
                if(cancelling)blend=Mathf.MoveTowards(blend,0,delta/1.25f);
                if(eventAge>=eventDuration||blend<=0) {beforePrevious=previous;previous=current;current=Gesture.None;wait=Range(6,15);}
            }
            SampleGesture(phaseTime,current,GestureProgress,side,strength*blend);
            if(engaged)YawnWeight=0;
        }
        void Begin(Gesture gesture,float mirror,float amplitude)
        {
            current=gesture;side=mirror;strength=amplitude;eventAge=0;blend=1;cancelling=false;
            eventDuration=(gesture==Gesture.Yawn?8.2f:gesture==Gesture.SideStretch?7.2f:gesture==Gesture.FootAdjust?5.2f:6.1f)*Range(.9f,1.15f);
            if(gesture==Gesture.Yawn)lastYawn=elapsed;if(gesture==Gesture.SideStretch)lastStretch=elapsed;
        }
        // Deterministic art/rig review entry point; does not mutate the scheduler.
        public void SampleGesture(float seconds,Gesture gesture,float progress,float mirror=1,float amplitude=1)
        {
            Sample(seconds);YawnWeight=StretchWeight=HeadTiltWeight=0;if(!IsBound||gesture==Gesture.None)return;
            float p=Mathf.Clamp01(progress),w=Envelope(p)*Mathf.Clamp(amplitude,0,1.1f),s=mirror<0?-1:1;
            Vector3 leftOffset=Vector3.zero,rightOffset=Vector3.zero;float leftYaw=0,rightYaw=0;
            if(gesture==Gesture.FootAdjust) {
                // Transfer weight before lifting; step out, settle, lift and return.
                float outPhase=Mathf.Clamp01((p-.16f)/.22f),backPhase=Mathf.Clamp01((p-.61f)/.24f);
                float travel=Smooth(outPhase)*(1-Smooth(backPhase));
                float lift=(Mathf.Sin(outPhase*Mathf.PI)+Mathf.Sin(backPhase*Mathf.PI))*.022f*amplitude;
                var offset=new Vector3(s*.055f*travel,lift,.025f*travel)*amplitude;
                hip.position+=model.TransformVector(new Vector3(-s*.023f*w,-.008f*w,0));
                Rotate(hip,Vector3.up,s*3*w);Rotate(chest,Vector3.up,-s*2*w);
                if(s<0){leftOffset=offset;leftYaw=-7*travel*amplitude;}else{rightOffset=offset;rightYaw=7*travel*amplitude;}
            }
            else if(gesture==Gesture.HipTurn) {
                hip.position+=model.TransformVector(new Vector3(s*.017f*w,-.005f*w,0));
                Rotate(hip,Vector3.up,s*11*w);Rotate(waist,Vector3.up,-s*4*w);Rotate(chest,Vector3.up,-s*3*w);
                Rotate(hip,Vector3.forward,-s*2*w);
            }
            else if(gesture==Gesture.ShoulderRoll) {
                float roll=Mathf.Sin(p*Mathf.PI*2)*w;
                Rotate(leftArm.shoulder,Vector3.forward,-5*w);Rotate(rightArm.shoulder,Vector3.forward,5*w);
                Rotate(leftArm.shoulder,Vector3.up,5*roll);Rotate(rightArm.shoulder,Vector3.up,-5*roll);
                Rotate(chest,Vector3.right,-2*w);
            }
            else if(gesture==Gesture.SideStretch) {
                StretchWeight=w;Rotate(hip,Vector3.forward,-s*2*w);Rotate(waist,Vector3.forward,s*3*w);Rotate(chest,Vector3.forward,s*5*w);
                Rotate(chest,Vector3.right,-2*w);
                var raised=s<0?leftArm:rightArm;
                Reach(raised,new Vector3(raised.side*.5f,.72f,.12f),new Vector3(-raised.side*.25f,1,.12f),w);
                SoftenHand(raised,w);
            }
            else if(gesture==Gesture.Yawn) {
                StretchWeight=w;YawnWeight=Smooth((p-.21f)/.18f)*(1-Smooth((p-.54f)/.18f))*amplitude;
                HeadTiltWeight=YawnWeight;
                Rotate(waist,Vector3.right,-1.5f*w);Rotate(chest,Vector3.right,-3*w);
                Reach(leftArm,new Vector3(-.65f,.62f,.08f),new Vector3(.10f,1,.10f),w);
                Reach(rightArm,new Vector3(.65f,.62f,.08f),new Vector3(-.10f,1,.10f),Smooth((p-.018f)/.27f)*(1-Smooth((p-.67f)/.33f))*amplitude);
                SoftenHand(leftArm,w);SoftenHand(rightArm,w);
            }
            Plant(leftLeg,leftOffset,leftYaw);Plant(rightLeg,rightOffset,rightYaw);
        }
        void Reach(Arm arm,Vector3 upperDirection,Vector3 lowerDirection,float weight)
        {
            Rotate(arm.shoulder,Vector3.forward,arm.side*7*weight);
            BlendAim(arm.upper,arm.lower,model.TransformDirection(upperDirection),weight);
            BlendAim(arm.lower,arm.hand,model.TransformDirection(lowerDirection),weight);
        }
        static void BlendAim(Transform bone,Transform child,Vector3 direction,float weight)
        {
            var original=bone.rotation;Aim(bone,child,direction);bone.rotation=Quaternion.Slerp(original,bone.rotation,Mathf.Clamp01(weight));
        }
        static void SoftenHand(Arm arm,float weight)
        {
            foreach(var joint in arm.fingers)joint.bone.localRotation*=Quaternion.AngleAxis(-joint.angle*.25f*weight,joint.curlAxis);
        }
    }
}
