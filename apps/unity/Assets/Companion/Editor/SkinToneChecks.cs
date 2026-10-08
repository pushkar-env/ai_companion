using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Companion.Presentation;
using UnityEditor;
using UnityEngine;

namespace Companion.Editor
{
    public static class SkinToneChecks
    {
        public static void Run()
        {
            if(EditorApplication.isPlaying)throw new Exception("Run isolated skin review while stopped");
            var app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>();var model=UnityEngine.Object.Instantiate(app.character.gameObject);model.hideFlags=HideFlags.HideAndDontSave;
            var originals=model.GetComponentsInChildren<SkinnedMeshRenderer>().ToDictionary(r=>r,r=>r.sharedMaterials);
            var colors=originals.Values.SelectMany(x=>x).Distinct().ToDictionary(m=>m,m=>m.HasProperty("_BaseColor")?m.GetColor("_BaseColor"):Color.white);
            var wardrobe=new CompanionWardrobe(model.transform,false);var idle=new CompanionBodyIdle(model.transform);
            foreach(var t in model.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
            var go=new GameObject("Skin review camera"){hideFlags=HideFlags.HideAndDontSave};var camera=go.AddComponent<Camera>();camera.CopyFrom(app.portraitCamera);camera.enabled=false;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.055f,.065f,.07f,1);
            var target=new RenderTexture(600,900,24){antiAliasing=4};target.Create();camera.targetTexture=target;camera.aspect=2f/3;camera.fieldOfView=28;var image=new Texture2D(600,900,TextureFormat.RGB24,false);
            string folder=Path.GetFullPath(Application.dataPath+"/../../../docs/evidence/skin-tones");Directory.CreateDirectory(folder);var checks=new List<string>();
            void Check(bool value,string label){checks.Add((value?"PASS ":"FAIL ")+label);if(!value)throw new Exception(label);}
            try {
                var skin=model.GetComponentsInChildren<SkinnedMeshRenderer>().SelectMany(r=>r.sharedMaterials).Where(CompanionWardrobe.IsSkinMaterial).ToArray();
                Check(skin.Length==4,"all four head/body/arm/leg materials bind");
                var body=model.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name=="CC_Base_Body");
                Check(body.sharedMaterials.Where(m=>!CompanionWardrobe.IsSkinMaterial(m)).All(m=>originals[body].Contains(m)),"nails and eyelashes retain original material references");
                for(int tone=0;tone<CompanionWardrobe.SkinToneNames.Length;tone++) {
                    wardrobe.Apply(new CompanionWardrobe.Look{outfit=1,top=0,bottom=0,skinTone=tone});idle.Sample(3);
                    var first=skin[0].GetColor("_BaseColor");Check(skin.All(m=>m.GetColor("_BaseColor")==first),CompanionWardrobe.SkinToneNames[tone]+" matches head, torso and limbs");
                    foreach(var close in new[]{false,true}) {
                        var focus=model.transform.position+Vector3.up*(close?1.47f:.9f);camera.transform.position=focus+Vector3.forward*(close?1.3f:4.1f);camera.transform.LookAt(focus);
                        ExpressiveIdleReview.Capture(camera,model,target,image,Path.Combine(folder,$"tone-{tone}-{(close?"face":"body")}.png"));
                    }
                }
                wardrobe.Apply(new CompanionWardrobe.Look{skinTone=999});Check(wardrobe.Current.skinTone==0&&skin.All(m=>m.GetColor("_BaseColor")==Color.white),"invalid saved index safely restores original tone");
                var legacy=JsonUtility.FromJson<CompanionWardrobe.Look>("{\"outfit\":1,\"top\":1,\"bottom\":1}");wardrobe.Apply(legacy);Check(wardrobe.Current.skinTone==0&&wardrobe.Current.top==1,"older saved looks keep garments and default to original skin");
                var refs=body.sharedMaterials;for(int i=0;i<100;i++)wardrobe.Apply(new CompanionWardrobe.Look{skinTone=i%6});Check(body.sharedMaterials.SequenceEqual(refs),"100 tone switches reuse the same material instances");
                Check(colors.All(p=>!p.Key.HasProperty("_BaseColor")||p.Key.GetColor("_BaseColor")==p.Value),"source materials never change");
                wardrobe.Dispose();wardrobe=null;Check(originals.All(p=>p.Key.sharedMaterials.SequenceEqual(p.Value)),"dispose restores every original material reference");
            }catch(Exception e){checks.Add("FAIL "+e.Message);Debug.LogError(e.Message);}
            finally{wardrobe?.Dispose();idle.Dispose();UnityEngine.Object.DestroyImmediate(model);UnityEngine.Object.DestroyImmediate(go);target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);File.WriteAllLines(Path.Combine(folder,"material-checks.txt"),checks);}
        }
    }
}
