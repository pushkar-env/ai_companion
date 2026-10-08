using System;
using UnityEngine;

namespace Companion.Presentation
{
    // Model-space rotations avoid assumptions about the imported CC bone axes.
    public sealed class CompanionGaze : IDisposable
    {
        readonly Transform model,head,left,right;
        readonly Quaternion headRest,leftRest,rightRest;
        float attention;
        static readonly float[] Times={0,4.8f,9.1f,15.7f,19.3f,26.4f,31.8f,38.2f};
        static readonly Vector2[] Looks={Vector2.zero,new Vector2(-18,-3),new Vector2(-7,4),Vector2.zero,new Vector2(21,-5),new Vector2(10,3),Vector2.zero,new Vector2(-12,2)};
        public float Blink {get;private set;}
        public CompanionGaze(Transform model)
        {
            this.model=model;
            foreach(var t in model.GetComponentsInChildren<Transform>()) {if(t.name=="CC_Base_Head")head=t;if(t.name=="CC_Base_L_Eye")left=t;if(t.name=="CC_Base_R_Eye")right=t;}
            if(head!=null)headRest=head.localRotation;if(left!=null)leftRest=left.localRotation;if(right!=null)rightRest=right.localRotation;
        }
        static float Ease(float t){t=Mathf.Clamp01(t);return t*t*t*(t*(t*6-15)+10);}
        public void Sample(float seconds,float delta,bool engaged,bool reduced)
        {
            Restore();float phase=Mathf.Repeat(seconds,5.7f);
            Blink=Mathf.Max(Pulse(phase-.7f,.19f),Pulse(phase-3.9f,.16f));if(head==null||reduced)return;
            attention=Mathf.MoveTowards(attention,engaged?1:0,Mathf.Max(0,delta)*(engaged?2.5f:.55f));
            float time=Mathf.Repeat(seconds,44);int i=Times.Length-1;while(i>0&&time<Times[i])i--;
            int previous=(i+Looks.Length-1)%Looks.Length;float age=time-Times[i];
            var eyes=Vector2.Lerp(Looks[previous],Looks[i],Ease(age/.22f));
            var turn=Vector2.Lerp(Looks[previous],Looks[i],Ease((age-.12f)/.95f))*.72f;
            eyes*=1-attention;turn*=1-attention;
            turn.y+=Mathf.Sin(seconds*1.2f)*.5f*attention;
            Rotate(head,turn);
            // Eyes retain a small share of the glance after the head has settled.
            Rotate(left,eyes-turn);Rotate(right,eyes-turn);
            if(i!=0)Blink=Mathf.Max(Blink,Pulse(age-.06f,.20f));
        }
        static float Pulse(float t,float duration)=>t>=0&&t<duration?Mathf.Sin(t/duration*Mathf.PI):0;
        void Rotate(Transform t,Vector2 degrees){if(t==null)return;t.rotation=Quaternion.AngleAxis(degrees.x,model.up)*Quaternion.AngleAxis(degrees.y,model.right)*t.rotation;}
        public void Restore(){if(head!=null)head.localRotation=headRest;if(left!=null)left.localRotation=leftRest;if(right!=null)right.localRotation=rightRest;}
        public void Dispose()=>Restore();
    }
}
