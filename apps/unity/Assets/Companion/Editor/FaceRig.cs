using System;
using System.Collections.Generic;
using System.Linq;
using Companion.Presentation;
using UnityEngine;

namespace Companion.Editor
{
    // An isolated, hidden copy of a roster character bound the way TalkingCharacter binds its face
    // (alias-resolved blendshape names across all renderers), for offline face checks and reviews.
    public sealed class FaceRig : IDisposable
    {
        public readonly GameObject copy;public readonly Transform model,head,jaw,eyeL,eyeR;public readonly Quaternion jawRest,headRest;
        public readonly SkinnedMeshRenderer[] renderers;
        readonly Dictionary<string,List<(SkinnedMeshRenderer r,int i)>> map=new Dictionary<string,List<(SkinnedMeshRenderer,int)>>();
        public FaceRig(TalkingCharacter.CharacterOption option,int layer=31)
        {
            copy=UnityEngine.Object.Instantiate(option.model.gameObject);copy.hideFlags=HideFlags.HideAndDontSave;copy.SetActive(true);
            model=copy.transform;foreach(var t in copy.GetComponentsInChildren<Transform>(true))t.gameObject.layer=layer;
            var bones=copy.GetComponentsInChildren<Transform>(true).GroupBy(b=>b.name).ToDictionary(g=>g.Key,g=>g.First());
            head=bones["CC_Base_Head"];jaw=bones["CC_Base_JawRoot"];eyeL=bones["CC_Base_L_Eye"];eyeR=bones["CC_Base_R_Eye"];jawRest=jaw.localRotation;headRest=head.localRotation;
            renderers=copy.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach(var r in renderers)for(int i=0;i<r.sharedMesh.blendShapeCount;i++) {
                string name=r.sharedMesh.GetBlendShapeName(i);
                if(TalkingCharacter.ExpressionAliases.TryGetValue(name,out var alias)&&r.sharedMesh.GetBlendShapeIndex(alias)<0)name=alias;
                if(!map.TryGetValue(name,out var list))map[name]=list=new List<(SkinnedMeshRenderer,int)>();list.Add((r,i));
            }
        }
        public bool Has(string name)=>map.ContainsKey(name);
        public void Set(string name,float value){if(map.TryGetValue(name,out var list))foreach(var b in list)b.r.SetBlendShapeWeight(b.i,Mathf.Clamp01(value)*100);}
        public float Weight(string name)=>map.TryGetValue(name,out var l)?l[0].r.GetBlendShapeWeight(l[0].i)/100:-1;
        /// <summary>Neutral face and jaw/head rest, like TalkingCharacter.ResetFace at the start of a frame.</summary>
        public void ResetFace(){foreach(var r in renderers)for(int i=0;i<r.sharedMesh.blendShapeCount;i++)r.SetBlendShapeWeight(i,0);jaw.localRotation=jawRest;head.localRotation=headRest;}
        public CompanionFace Face(FaceTuning tuning,int seed)=>new CompanionFace(model,head,jaw,jawRest,tuning,Has,Set,seed);
        public void Dispose(){if(copy!=null)UnityEngine.Object.DestroyImmediate(copy);}
    }
}
