using System;
using System.Collections.Generic;
using UnityEngine;

namespace Companion.Presentation
{
    // Spring chains for hair, earrings and loose clothing (verlet, VRM-style) with sphere/capsule
    // body colliders. Rest data is authored in the character's model space and anchored through the
    // skinned bind poses, so binding never depends on the current idle pose. TalkingCharacter steps it
    // after body, gaze and jaw posing; it pauses with the app and never touches face or body bones.
    // Share joints are garment helper bones that turn a fixed fraction of a body bone's rotation
    // (half the upper arm at each shoulder), so a sleeve's underarm fold bends with the arm instead of
    // dragging the shirt's side up into a web. They follow the pose every step, including reduced motion.
    [DisallowMultipleComponent]
    public sealed class CompanionSecondaryMotion : MonoBehaviour
    {
        [Serializable] public sealed class Chain
        {
            public string name;
            public string[] bones=Array.Empty<string>();
            [Tooltip("Model-space rest position of the last bone's tail.")] public Vector3 tip;
            [Range(0,4)] public float stiffness=.5f;
            [Range(0,1)] public float drag=.3f;
            [Range(0,2)] public float gravity=.3f;
            public float radius=.02f;
        }
        [Serializable] public sealed class BodyCollider
        {
            public string bone;
            [Tooltip("Model-space rest centre (capsule start).")] public Vector3 center;
            [Tooltip("Model-space rest capsule end.")] public Vector3 tail;
            public bool capsule;
            public float radius=.05f;
        }
        [Serializable] public sealed class Share
        {
            [Tooltip("Helper bone; shares the source bone's parent and rest pose.")] public string bone;
            [Tooltip("Body bone whose rotation the helper follows.")] public string source;
            [Range(0,1)] public float amount=.5f;
        }
        public Chain[] chains=Array.Empty<Chain>();
        public BodyCollider[] colliders=Array.Empty<BodyCollider>();
        public Share[] shares=Array.Empty<Share>();
        [Tooltip("Model-space gravity direction.")] public Vector3 gravityDirection=Vector3.down;

        sealed class Joint {public Transform bone;public Quaternion restLocal;public Vector3 restTail;public Vector3 current,previous;public Chain chain;public bool paused;}
        sealed class Bound {public Transform bone;public Vector3 a,b;public bool capsule;public float radius;}
        sealed class Helper {public Transform bone,source;public Quaternion restLocal,sourceRest;public float amount;}
        readonly List<Joint> joints=new List<Joint>();
        readonly List<Bound> bounds=new List<Bound>();
        readonly List<Helper> helpers=new List<Helper>();
        public int ShareCount=>helpers.Count;
        // Chains of garments that are not worn: held at rest and skipped (they survive rebinding).
        readonly HashSet<string> pausedChains=new HashSet<string>();
        bool settled;
        public bool IsBound=>joints.Count>0;
        public int JointCount=>joints.Count;
        public int ActiveJointCount {get {int n=0;foreach(var j in joints)if(!j.paused)n++;return n;}}
        public int ColliderCount=>bounds.Count;
        public bool IsChainPaused(string chain)=>pausedChains.Contains(chain);
        public void SetChainPaused(string chain,bool paused)
        {
            if(string.IsNullOrEmpty(chain)||!(paused?pausedChains.Add(chain):pausedChains.Remove(chain)))return;
            foreach(var j in joints) {
                if(j.chain.name!=chain)continue;
                j.paused=paused;
                if(j.bone==null)continue;
                j.bone.localRotation=j.restLocal;
                j.current=j.previous=j.bone.TransformPoint(j.restTail);
            }
        }
        // Diagnostics (metres): largest tail offset from its rest-pose tail and largest tail travel this step.
        public float MaxDeviation {get;private set;}
        public float MaxStepMotion {get;private set;}
        public bool Finite {get;private set;}=true;
        // Most negative clearance between any simulated tail and a body collider (0 or more means no overlap).
        public float DeepestPenetration()
        {
            float deepest=float.MaxValue;
            foreach(var j in joints)if(!j.paused)foreach(var b in bounds) {
                Vector3 a=b.bone.TransformPoint(b.a),c=a;
                if(b.capsule){Vector3 e=b.bone.TransformPoint(b.b),d=e-a;float t=d.sqrMagnitude<1e-10f?0:Mathf.Clamp01(Vector3.Dot(j.current-a,d)/d.sqrMagnitude);c=a+d*t;}
                deepest=Mathf.Min(deepest,Vector3.Distance(j.current,c)-(b.radius*b.bone.lossyScale.x+j.chain.radius));
            }
            return deepest==float.MaxValue?0:deepest;
        }

        public bool Bind()
        {
            ResetPose();joints.Clear();bounds.Clear();helpers.Clear();
            var map=new Dictionary<string,Transform>();
            foreach(var t in GetComponentsInChildren<Transform>(true))if(!map.ContainsKey(t.name))map[t.name]=t;
            var rest=RestMatrices();
            Matrix4x4 Rest(Transform t)=>rest.TryGetValue(t,out var m)?m:transform.worldToLocalMatrix*t.localToWorldMatrix;
            Vector3 Local(Transform t,Vector3 model)=>Rest(t).inverse.MultiplyPoint3x4(model);
            foreach(var chain in chains) {
                if(chain==null||chain.bones==null)continue;
                var list=new List<Transform>();
                foreach(var n in chain.bones){if(!map.TryGetValue(n,out var t)){list=null;break;}list.Add(t);}
                if(list==null||list.Count==0)continue;
                for(int i=0;i<list.Count;i++) {
                    Vector3 tailModel=i+1<list.Count?Rest(list[i+1]).MultiplyPoint3x4(Vector3.zero):chain.tip;
                    var bone=list[i];
                    // Parent-relative bind rotation, so a rebind never captures a swinging pose as rest.
                    var restLocal=bone.parent!=null&&rest.ContainsKey(bone)&&rest.ContainsKey(bone.parent)?(Rest(bone.parent).inverse*Rest(bone)).rotation:bone.localRotation;
                    joints.Add(new Joint {bone=bone,restLocal=restLocal,restTail=Local(bone,tailModel),chain=chain,paused=pausedChains.Contains(chain.name)});
                }
            }
            foreach(var c in colliders) {
                if(c==null||!map.TryGetValue(c.bone,out var t))continue;
                bounds.Add(new Bound {bone=t,a=Local(t,c.center),b=Local(t,c.capsule?c.tail:c.center),capsule=c.capsule,radius=c.radius});
            }
            Quaternion RestLocal(Transform t)=>rest.ContainsKey(t)&&rest.ContainsKey(t.parent)?(Rest(t.parent).inverse*Rest(t)).rotation:t.localRotation;
            foreach(var s in shares) {
                if(s==null||!map.TryGetValue(s.bone??"",out var b)||!map.TryGetValue(s.source??"",out var src)||b.parent==null||src.parent==null)continue;
                helpers.Add(new Helper {bone=b,source=src,restLocal=RestLocal(b),sourceRest=RestLocal(src),amount=Mathf.Clamp01(s.amount)});
            }
            settled=false;Finite=true;
            return joints.Count>0||helpers.Count>0;
        }
        // Share joints take the given fraction of the source's rotation away from its rest pose, applied in
        // the shared parent's space, so they stay correct for any arm direction or twist.
        public void PoseHelpers()
        {
            foreach(var h in helpers) {
                if(h.bone==null||h.source==null)continue;
                var delta=h.source.localRotation*Quaternion.Inverse(h.sourceRest);
                h.bone.localRotation=Quaternion.Slerp(Quaternion.identity,delta,h.amount)*h.restLocal;
            }
        }
        // Bind poses give the rest skeleton independently of any pose currently applied.
        Dictionary<Transform,Matrix4x4> RestMatrices()
        {
            var result=new Dictionary<Transform,Matrix4x4>();
            foreach(var r in GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                if(r.sharedMesh==null)continue;
                var bind=r.sharedMesh.bindposes;var bones=r.bones;
                var toModel=transform.worldToLocalMatrix*r.transform.localToWorldMatrix;
                for(int i=0;i<bones.Length&&i<bind.Length;i++)if(bones[i]!=null&&!result.ContainsKey(bones[i]))result[bones[i]]=toModel*bind[i].inverse;
            }
            return result;
        }
        // Rest pose for the chain bones only; body bones belong to their own owners.
        public void ResetPose()
        {
            foreach(var j in joints)if(j.bone!=null)j.bone.localRotation=j.restLocal;
            foreach(var h in helpers)if(h.bone!=null)h.bone.localRotation=h.restLocal;
            settled=false;
        }
        void Settle()
        {
            foreach(var j in joints) {
                j.bone.localRotation=j.restLocal;
                j.current=j.previous=j.bone.TransformPoint(j.restTail);
            }
            settled=true;MaxDeviation=0;MaxStepMotion=0;
        }
        public void Step(float delta,bool reduced)
        {
            PoseHelpers();
            if(joints.Count==0)return;
            // Large gaps (first frame, resume, stalls) restart from rest instead of releasing stored energy.
            if(reduced||!settled||delta<=0||delta>.25f||!Finite){Finite=true;Settle();return;}
            float dt=Mathf.Min(delta,1/30f);
            Vector3 gravityWorld=transform.TransformDirection(gravityDirection).normalized;
            float deviation=0,travel=0;
            foreach(var j in joints) {
                if(j.paused)continue;
                var bone=j.bone;
                bone.localRotation=j.restLocal;
                Vector3 head=bone.position,restTail=bone.TransformPoint(j.restTail);
                Vector3 axis=restTail-head;float length=axis.magnitude;
                if(length<1e-5f)continue;
                axis/=length;
                Vector3 next=j.current+(j.current-j.previous)*(1-j.chain.drag)
                    +axis*(j.chain.stiffness*dt)+gravityWorld*(j.chain.gravity*dt);
                next=head+(next-head).normalized*length;
                foreach(var b in bounds)next=Collide(next,head,length,j.chain.radius,b);
                if(!(Valid(next))){Finite=false;continue;}
                travel=Mathf.Max(travel,Vector3.Distance(next,j.current));
                j.previous=j.current;j.current=next;
                bone.rotation=Quaternion.FromToRotation(axis,(next-head)/length)*bone.rotation;
                deviation=Mathf.Max(deviation,Vector3.Distance(next,restTail));
            }
            MaxDeviation=deviation;MaxStepMotion=travel;
        }
        static bool Valid(Vector3 v)=>!(float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.x)||float.IsInfinity(v.y)||float.IsInfinity(v.z));
        static Vector3 Collide(Vector3 tail,Vector3 head,float length,float jointRadius,Bound b)
        {
            Vector3 a=b.bone.TransformPoint(b.a);
            Vector3 c=a;
            if(b.capsule) {
                Vector3 e=b.bone.TransformPoint(b.b),d=e-a;float t=d.sqrMagnitude<1e-10f?0:Mathf.Clamp01(Vector3.Dot(tail-a,d)/d.sqrMagnitude);c=a+d*t;
            }
            float r=b.radius*b.bone.lossyScale.x+jointRadius;
            Vector3 offset=tail-c;float distance=offset.magnitude;
            if(distance>=r||distance<1e-6f)return tail;
            Vector3 pushed=c+offset/distance*r;
            return head+(pushed-head).normalized*length;
        }
        void OnDisable()=>ResetPose();
    }
}
