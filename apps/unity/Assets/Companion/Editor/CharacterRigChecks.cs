using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Companion.Core;
using Companion.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Companion.Editor
{
    // Validates a Blender-rigged character on an isolated hidden copy (stopped Editor), then in the real
    // portrait app (Play). Writes evidence under docs/evidence/m1/<name>. Never changes Editor layout.
    public static class CharacterRigChecks
    {
        static readonly string[] Arkit={"browDownLeft","browDownRight","browInnerUp","browOuterUpLeft","browOuterUpRight","cheekPuff","cheekSquintLeft","cheekSquintRight",
            "eyeBlinkLeft","eyeBlinkRight","eyeLookDownLeft","eyeLookDownRight","eyeLookInLeft","eyeLookInRight","eyeLookOutLeft","eyeLookOutRight","eyeLookUpLeft","eyeLookUpRight",
            "eyeSquintLeft","eyeSquintRight","eyeWideLeft","eyeWideRight","jawForward","jawLeft","jawOpen","jawRight","mouthClose","mouthDimpleLeft","mouthDimpleRight",
            "mouthFrownLeft","mouthFrownRight","mouthFunnel","mouthLeft","mouthLowerDownLeft","mouthLowerDownRight","mouthPressLeft","mouthPressRight","mouthPucker","mouthRight",
            "mouthRollLower","mouthRollUpper","mouthShrugLower","mouthShrugUpper","mouthSmileLeft","mouthSmileRight","mouthStretchLeft","mouthStretchRight",
            "mouthUpperUpLeft","mouthUpperUpRight","noseSneerLeft","noseSneerRight","tongueOut"};
        static readonly string[] Expressions={"Eye_Blink_L","Eye_Blink_R","Eye_Widen_L","Eye_Widen_R","Mouth_Corner_Pull_L","Mouth_Corner_Pull_R",
            "Mouth_Corner_Depress_L","Mouth_Corner_Depress_R","Brow_Raise_In_L","Brow_Raise_In_R","Brow_Raise_Outer_L","Brow_Raise_Outer_R"};
        static readonly List<string> lines=new List<string>();
        static void Check(bool ok,string label){if(!ok)throw new Exception(label);lines.Add("PASS "+label);}
        static void Note(string text)=>lines.Add("INFO "+text);
        static Vector3[][] Bake(SkinnedMeshRenderer[] renderers)
        {
            var mesh=new Mesh();
            try{return renderers.Select(r=>{r.BakeMesh(mesh);var m=r.transform.localToWorldMatrix;return mesh.vertices.Select(v=>m.MultiplyPoint3x4(v)).ToArray();}).ToArray();}
            finally{UnityEngine.Object.DestroyImmediate(mesh);}
        }
        static float Max(Vector3[][] a,Vector3[][] b){float m=0;for(int r=0;r<a.Length;r++)for(int i=0;i<a[r].Length;i++)m=Mathf.Max(m,(a[r][i]-b[r][i]).magnitude);return m;}
        static void Set(SkinnedMeshRenderer[] renderers,string name,float weight){foreach(var r in renderers){int i=r.sharedMesh.GetBlendShapeIndex(name);if(i>=0)r.SetBlendShapeWeight(i,weight);}}
        static void Clear(SkinnedMeshRenderer[] renderers){foreach(var r in renderers)for(int i=0;i<r.sharedMesh.blendShapeCount;i++)r.SetBlendShapeWeight(i,0);}

        public static void Rig(CharacterSpec spec)
        {
            lines.Clear();GameObject copy=null,cameraObject=null;RenderTexture target=null;Texture2D image=null;
            CompanionBodyIdle idle=null;CompanionGaze gaze=null;string folder=spec.EvidenceFolder,name=spec.Name;
            try {
                if(EditorApplication.isPlaying)throw new Exception("Run the isolated rig checks while stopped");
                var app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>(FindObjectsInactive.Include);
                var option=app.characters.FirstOrDefault(c=>c.name==name);
                Check(app.characters.Length>=2&&app.characters[0].name=="Alita"&&app.character==app.characters[0].model&&option!=null&&option.model!=null,
                    $"Alita stays the scene default; {name} is registered in the roster ({string.Join(", ",app.characters.Select(c=>c.name))})");
                var source=option.model.gameObject;
                Check(!source.activeSelf,name+" stays inactive until selected");
                Check(option.face!=null&&Mathf.Approximately(option.face.jawDegrees,spec.Face.jawDegrees)&&Mathf.Approximately(option.face.openGain,spec.Face.openGain)&&Mathf.Approximately(option.face.sealGain,spec.Face.sealGain),
                    $"face tuning from the spec is on the roster (jaw {option.face?.jawDegrees}°, seal {option.face?.sealGain})");
                copy=UnityEngine.Object.Instantiate(source);copy.hideFlags=HideFlags.HideAndDontSave;copy.SetActive(true);
                var model=copy.transform;
                var all=copy.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                // garments (modular wardrobes) are separate renderers; the face rig lives on the base body
                var renderers=all.Where(r=>r.GetComponentInParent<CompanionGarment>(true)==null).ToArray();
                Check(renderers.Select(r=>r.name).OrderBy(n=>n).SequenceEqual(new[]{name+"_Body",name+"_Eyes",name+"_Mouth"}),"body, eyeball and mouth renderers present");
                if(spec.Modular) {
                    var bound=copy.GetComponentsInChildren<CompanionGarment>(true);
                    Check(bound.Length==spec.Garments.Length&&spec.Garments.All(g=>bound.Any(b=>b.id==g.Id&&b.name==g.Object&&b.GetComponent<SkinnedMeshRenderer>()?.sharedMesh!=null)),
                        $"{bound.Length} modular garments bound to {name}'s skeleton ({string.Join(", ",bound.Select(b=>b.name))})");
                }
                Check(all.All(r=>r.sharedMaterials.All(m=>m!=null&&m.shader!=null&&m.shader.name=="Universal Render Pipeline/Lit")),"every material slot uses URP Lit");
                Check(TalkingCharacter.CanAnimate(model),"ten speech channels plus CC head and jaw bones");
                var names=new HashSet<string>(renderers.SelectMany(r=>Enumerable.Range(0,r.sharedMesh.blendShapeCount).Select(i=>r.sharedMesh.GetBlendShapeName(i))));
                Check(Expressions.All(names.Contains),"blink, widen, smile, frown and brow channels used by the app");
                var missing=Arkit.Where(n=>!names.Contains(n)).ToArray();
                Check(missing.Length==0,"all 52 FACE-01 ARKit-style channels present"+(missing.Length>0?" (missing "+string.Join(",",missing)+")":""));
                Check(!names.Any(n=>n.Contains("__f")),"in-between helper shapes are folded into frames");
                var body=renderers.First(r=>r.name==spec.Body);var mesh=body.sharedMesh;int blink=mesh.GetBlendShapeIndex("Eye_Blink_L");
                Check(mesh.GetBlendShapeFrameCount(blink)==4&&Enumerable.Range(0,4).Select(f=>mesh.GetBlendShapeFrameWeight(blink,f)).SequenceEqual(new float[]{25,50,75,100}),"blink follows the eyeball arc with 25/50/75/100 frames");
                Note($"body {mesh.vertexCount} vertices, {mesh.triangles.Length/3} triangles, {mesh.subMeshCount} material slots, {mesh.blendShapeCount} channels, {body.bones.Length} skinned bones");
                foreach(var r in renderers.Where(r=>r!=body))Note($"{r.name} {r.sharedMesh.vertexCount} vertices, {r.sharedMesh.triangles.Length/3} triangles, {r.sharedMesh.blendShapeCount} channels");
                var neutral=Bake(renderers);
                foreach(var channel in SpeechMouthMotion.Channels.Concat(Expressions).Concat(Arkit.Where(n=>!n.StartsWith("eyeLook")))) {
                    Set(renderers,channel,60);var posed=Bake(renderers);Set(renderers,channel,0);
                    float moved=Max(neutral,posed);
                    Check(moved>.0005f&&moved<.05f,channel+" deforms the face ("+(moved*1000).ToString("F1")+" mm at 60%)");
                }
                foreach(var channel in Arkit.Where(n=>n.StartsWith("eyeLook"))){Set(renderers,channel,60);var posed=Bake(renderers);Set(renderers,channel,0);Check(Max(neutral,posed)>.002f,channel+" rotates the eyeball");}
                Check(Max(neutral,Bake(renderers))<1e-6f,"every channel returns exactly to neutral");
                var bones=copy.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
                var jaw=bones["CC_Base_JawRoot"];var head=bones["CC_Base_Head"];var jawRest=jaw.localRotation;
                var before=Bake(new[]{body})[0];
                // chin (below the lower lip); the overhanging upper lip belongs to the head, not the jaw
                int chin=Enumerable.Range(0,before.Length).Where(i=>Mathf.Abs(before[i].x-model.position.x)<.012f&&before[i].y<jaw.position.y-.058f&&before[i].y>jaw.position.y-.09f)
                    .OrderByDescending(i=>Vector3.Dot(before[i]-jaw.position,model.forward)).First();
                float nose=before.Where(v=>v.y>jaw.position.y-.02f&&v.y<head.position.y+.09f).Max(v=>Vector3.Dot(v-head.position,model.forward));
                Check(nose>.08f,name+" faces the model forward axis used for camera framing and knee bends");
                jaw.localRotation=jawRest*Quaternion.Euler(0,0,-16);var after=Bake(new[]{body})[0];jaw.localRotation=jawRest;
                Check(after[chin].y<before[chin].y-.004f,"the app's jaw rotation (local Z, negative) opens the chin downward ("+((before[chin].y-after[chin].y)*1000).ToString("F1")+" mm)");
                // procedural idle, IK feet and fingers from the existing CC-bone systems
                idle=new CompanionBodyIdle(model,7);Check(idle.IsBound,"CC body, arm, leg and finger chains bind for the procedural idle");
                var feet=new[]{bones["CC_Base_L_Foot"],bones["CC_Base_R_Foot"]};
                idle.Sample(0);var anchors=feet.Select(f=>f.position).ToArray();float slip=0;
                for(int frame=0;frame<=1800;frame++){idle.Sample(frame/60f);for(int i=0;i<2;i++)slip=Mathf.Max(slip,Vector3.Distance(anchors[i],feet[i].position));}
                Check(slip<.0006f,"analytic leg IK keeps both feet planted over 30 s (max "+(slip*1000).ToString("F2")+" mm)");
                idle.Sample(0);
                foreach(var side in new[]{"L","R"})Check(bones["CC_Base_"+side+"_Hand"].position.y<bones["CC_Base_"+side+"_Upperarm"].position.y-.25f,"relaxed "+side+" arm lowers from the T-pose bind");
                var fingers=(from side in new[]{"L","R"} from digit in new[]{"Index","Mid","Ring","Pinky","Thumb"} from j in new[]{1,2,3} select bones["CC_Base_"+side+"_"+digit+j]).ToArray();
                Check(fingers.Length==30,"30 finger and thumb joints present");
                gaze=new CompanionGaze(model);var eye=bones["CC_Base_L_Eye"];var eyeRest=eye.localRotation;gaze.Sample(4.9f,1/60f,false,false);
                Check(Quaternion.Angle(eyeRest,eye.localRotation)>2,"gaze rotates the separate eyeballs");gaze.Restore();
                using(var wardrobe=new CompanionWardrobe(model,false)) {
                    Check(wardrobe.HasSkin&&wardrobe.HasTop&&wardrobe.HasBottom&&wardrobe.HasHair&&wardrobe.HasShoes==spec.HasShoes&&!wardrobe.HasSeparates,spec.RoleSummary+"; Alita-fitted separates not attached");
                    wardrobe.Apply(new CompanionWardrobe.Look{topColor=1,bottomColor=3,hairColor=2,shoeColor=4,skinTone=3});
                    var mats=all.SelectMany(r=>r.sharedMaterials).ToArray();
                    Check(mats.First(m=>m.name==spec.TopMaterial).GetColor("_BaseColor")==CompanionWardrobe.Palette[1]&&mats.First(m=>m.name==spec.BottomMaterial).GetColor("_BaseColor")==CompanionWardrobe.Palette[3],"top and bottom tint independently");
                    // untinted reference slots: jewellery and trims (a character may have neither, e.g. Arjun has no earrings)
                    var untinted=mats.Where(m=>m.name==name+"_Earrings"||m.name==spec.TrimMaterial).ToArray();
                    Check(mats.First(m=>m.name==name+"_Skin").GetColor("_BaseColor")!=Color.white&&untinted.All(m=>m.GetColor("_BaseColor")==Color.white),"skin tone applies to skin only");
                    if(spec.TrimMaterial!=null)Check(mats.First(m=>m.name==spec.TrimMaterial).GetColor("_BaseColor")==Color.white,"trim details ("+spec.TrimMaterial+") keep their colour under tints");
                }
                Check(all.All(r=>r.sharedMaterials.All(m=>AssetDatabase.Contains(m))),"wardrobe disposal restores the shared material assets");
                // springs: hair, earrings and loose cloth
                var motion=copy.GetComponent<CompanionSecondaryMotion>();
                Check(motion!=null&&motion.Bind(),"spring chains bind from bind poses");
                // every rig.json joint binds; the floor only catches a missing chain set (short-haired Arjun has 34)
                Check(motion.JointCount==motion.chains.Sum(c=>c.bones.Length)&&motion.JointCount>=16&&motion.ColliderCount>=10,$"{motion.JointCount} spring joints and {motion.ColliderCount} body colliders bound");
                idle.Dispose();idle=new CompanionBodyIdle(model,3);var hip=bones["CC_Base_Hip"];
                float peak=0;bool finite=true;motion.Step(1/60f,false);
                for(int frame=0;frame<900;frame++) {
                    idle.Advance(1/60f,false,false);
                    if(frame<480)hip.rotation=Quaternion.AngleAxis(9*Mathf.Sin(frame/60f*Mathf.PI*1.2f),model.up)*hip.rotation;
                    head.rotation=Quaternion.AngleAxis(12*Mathf.Sin(frame/60f*2.1f),model.up)*head.rotation;
                    motion.Step(1/60f,false);peak=Mathf.Max(peak,motion.MaxDeviation);finite&=motion.Finite;
                }
                Check(finite,"spring simulation stays finite for 15 s of idle, gestures, hip sway and head turns");
                Check(peak>.006f&&peak<.25f,"hair, earrings and cloth respond with bounded motion (peak "+(peak*100).ToString("F1")+" cm)");
                for(int frame=0;frame<360;frame++){idle.Sample(0);motion.Step(1/60f,false);}
                Check(motion.MaxStepMotion<.0005f,"chains settle when the body is still ("+(motion.MaxStepMotion*1000).ToString("F3")+" mm/frame)");
                Check(motion.DeepestPenetration()>-.004f,"settled chains stay outside body colliders ("+(motion.DeepestPenetration()*1000).ToString("F1")+" mm)");
                motion.Step(0,true);Check(motion.MaxDeviation==0,"reduced motion holds chains at rest");
                // review renders (baked skin, isolated layer; scene and Game view untouched)
                foreach(var t in copy.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
                cameraObject=new GameObject(name+" review camera"){hideFlags=HideFlags.HideAndDontSave};var camera=cameraObject.AddComponent<Camera>();
                camera.CopyFrom(app.portraitCamera);camera.enabled=false;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.055f,.065f,.07f,1);
                target=new RenderTexture(600,900,24){antiAliasing=4};target.Create();camera.targetTexture=target;camera.aspect=2f/3;camera.fieldOfView=30;
                image=new Texture2D(600,900,TextureFormat.RGB24,false);Directory.CreateDirectory(folder);
                idle.Sample(2);for(int frame=0;frame<240;frame++){idle.Sample(2);motion.Step(1/60f,false);}
                var focus=model.position+model.up*.82f;
                camera.transform.position=focus+model.forward*3.9f;camera.transform.LookAt(focus);camera.Render();   // warm-up: the first render after layer changes can be empty
                // live GPU-skinned draw (not baked): catches mesh layout/skinning faults that BakeMesh hides
                camera.Render();camera.Render();
                {var old=RenderTexture.active;RenderTexture.active=target;image.ReadPixels(new Rect(0,0,600,900),0,0);image.Apply();RenderTexture.active=old;
                 var bg=new Color32(14,17,18,255);int covered=image.GetPixels32().Count(p=>Mathf.Abs(p.r-bg.r)+Mathf.Abs(p.g-bg.g)+Mathf.Abs(p.b-bg.b)>24);
                 Check(covered>600*900/40,"live skinned renderers draw the full body ("+covered+" covered pixels)");}
                foreach(var (view,dir) in new[]{("front",model.forward),("side",model.right),("back",-model.forward),("three-quarter",(model.forward+model.right).normalized)}) {
                    camera.transform.position=focus+dir*3.9f;camera.transform.LookAt(focus);ExpressiveIdleReview.Capture(camera,copy,target,image,Path.Combine(folder,"idle-"+view+".png"));
                }
                camera.fieldOfView=12;var face=head.position+model.up*.05f;camera.transform.position=face+model.forward*1.6f;camera.transform.LookAt(face);
                // Speech poses as the app drives them: lip shape plus jaw rotation, with mouthClose sealing
                // the jaw gap on rounded and closed lips (lip-only shapes look almost closed without the jaw).
                (string,float)[] W(params (string,float)[] w)=>w;
                var sheet=new (string name,(string shape,float weight)[] shapes,float jaw)[]{("neutral",W(),0),
                    ("speech-aa",W(("V_Open",70)),7),("speech-oo",W(("V_Tight_O",90),("mouthClose",13.5f)),3.5f),("speech-ee",W(("V_Wide",80)),2.5f),
                    ("speech-ff",W(("V_Dental_Lip",90),("mouthClose",2)),1.5f),("speech-pbm",W(("V_Explosive",90),("mouthClose",7.3f)),1.6f),
                    ("blink-half",W(("Eye_Blink_L",50),("Eye_Blink_R",50)),0),("blink",W(("Eye_Blink_L",100),("Eye_Blink_R",100)),0),
                    ("happy",W(("Mouth_Corner_Pull_L",50),("Mouth_Corner_Pull_R",50),("cheekSquintLeft",35),("cheekSquintRight",35),("eyeSquintLeft",22),("eyeSquintRight",22),("Brow_Raise_Outer_L",12),("Brow_Raise_Outer_R",12)),0),
                    ("concerned",W(("Brow_Raise_In_L",55),("Brow_Raise_In_R",55),("Brow_Drop_L",18),("Brow_Drop_R",18),("Mouth_Corner_Depress_L",20),("Mouth_Corner_Depress_R",20)),0),
                    ("curious",W(("Brow_Raise_Outer_L",48),("Brow_Raise_Outer_R",22),("Brow_Raise_In_L",18),("Brow_Raise_In_R",18),("Eye_Widen_L",16),("Eye_Widen_R",16)),0),
                    ("jaw-open",W(),12),("pucker",W(("mouthPucker",100)),0)};
                foreach(var shot in sheet) {
                    Clear(renderers);foreach(var s in shot.shapes)Set(renderers,s.shape,s.weight);jaw.localRotation=jawRest*Quaternion.Euler(0,0,-shot.jaw);
                    ExpressiveIdleReview.Capture(camera,copy,target,image,Path.Combine(folder,"face-"+shot.name+".png"));
                }
                jaw.localRotation=jawRest;
                Clear(renderers);
                camera.fieldOfView=30;
                foreach(var gesture in new[]{CompanionBodyIdle.Gesture.Yawn,CompanionBodyIdle.Gesture.SideStretch,CompanionBodyIdle.Gesture.HipTurn}) {
                    for(int frame=0;frame<120;frame++){idle.SampleGesture(3,gesture,.38f+frame*.001f);motion.Step(1/60f,false);}
                    camera.transform.position=focus+model.forward*3.9f;camera.transform.LookAt(focus);
                    ExpressiveIdleReview.Capture(camera,copy,target,image,Path.Combine(folder,"gesture-"+gesture+".png"));
                }
                if(spec.Modular)ModularWardrobe(spec,copy,motion,idle,camera,target,image,Path.Combine(folder,"wardrobe"),bones);
                Note("review renders written to docs/evidence/m1/"+name.ToLowerInvariant()+" (baked skin, isolated layer 31)");
            }
            catch(Exception e){lines.Add("FAIL "+e.Message);throw;}
            finally {
                gaze?.Dispose();idle?.Dispose();
                if(copy!=null)UnityEngine.Object.DestroyImmediate(copy);if(cameraObject!=null)UnityEngine.Object.DestroyImmediate(cameraObject);
                if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}if(image!=null)UnityEngine.Object.DestroyImmediate(image);
                Directory.CreateDirectory(folder);File.WriteAllLines(Path.Combine(folder,"rig-checks.txt"),lines);
            }
        }

        // ---- Modular wardrobe (hidden copy): outfits, mix & match, category rule, chains, occlusion, renders ----
        static void ModularWardrobe(CharacterSpec spec,GameObject copy,CompanionSecondaryMotion motion,CompanionBodyIdle idle,Camera camera,RenderTexture target,Texture2D image,string folder,Dictionary<string,Transform> bones)
        {
            var model=copy.transform;
            var garments=copy.GetComponentsInChildren<CompanionGarment>(true);
            var profile=copy.GetComponent<CompanionWardrobeProfile>();
            Check(profile!=null&&profile.category==spec.Category&&profile.bodyFamily==spec.BodyFamily&&garments.All(g=>g.category==spec.Category&&g.bodyFamily==spec.BodyFamily),
                $"wardrobe profile: {spec.Category} body '{spec.BodyFamily}'; all {garments.Length} garments are {spec.Category} and fitted to it");
            CompanionGarment G(string id)=>garments.First(g=>g.id==id);
            string[] Active()=>garments.Where(g=>g.gameObject.activeSelf).Select(g=>g.id).OrderBy(x=>x).ToArray();
            string Slot(CompanionWardrobeProfile.Outfit o,GarmentSlot s)=>o.garments.FirstOrDefault(id=>G(id).slot==s);
            var signature=spec.Outfits[0];var other=spec.Outfits[1];
            Directory.CreateDirectory(folder);
            var focus=model.position+model.up*.86f;
            void Shot(string file,Vector3 dir,float distance,Vector3 at,float fov)
            {
                camera.fieldOfView=fov;camera.transform.position=at+dir*distance;camera.transform.LookAt(at);
                ExpressiveIdleReview.Capture(camera,copy,target,image,Path.Combine(folder,file+".png"));
            }
            void Settle(){for(int frame=0;frame<180;frame++){idle.Sample(2);motion.Step(1/60f,false);}}
            void Pose(CompanionBodyIdle.Gesture gesture,float progress){for(int frame=0;frame<120;frame++){idle.SampleGesture(3,gesture,progress);motion.Step(1/60f,false);}}
            // share joints: half the upper arm's rotation, so the underarm fold bends instead of webbing
            var shareBones=motion.shares.Select(s=>(share:bones.TryGetValue(s.bone,out var b)?b:null,source:bones.TryGetValue(s.source,out var u)?u:null,amount:s.amount)).ToArray();
            idle.Restore();motion.ResetPose();
            var shareRest=shareBones.Select(s=>(s.share?.localRotation??Quaternion.identity,s.source?.localRotation??Quaternion.identity)).ToArray();
            Pose(CompanionBodyIdle.Gesture.Yawn,.45f);
            var shareErr=shareBones.Select((s,i)=>s.share==null?999:Mathf.Abs(Quaternion.Angle(shareRest[i].Item1,s.share.localRotation)-s.amount*Quaternion.Angle(shareRest[i].Item2,s.source.localRotation))).ToArray();
            Check(motion.ShareCount==2&&shareBones.All(s=>s.share!=null&&s.source!=null&&s.share.parent==s.source.parent)&&shareErr.All(e=>e<.5f)&&Quaternion.Angle(shareRest[0].Item2,shareBones[0].source.localRotation)>30,
                $"shoulder share joints turn half the upper arm through a raised-arm yawn (error {shareErr.DefaultIfEmpty(0).Max():F2}°)");
            using(var w=new CompanionWardrobe(model,false)) {
                Check(w.HasModularWardrobe&&w.Garments.Count==garments.Length&&w.Current.outfitId==signature.id&&Active().SequenceEqual(signature.garments.OrderBy(x=>x)),
                    "default look is the "+signature.displayName+" ("+string.Join(", ",Active())+")");
                Check(w.Outfits.Select(o=>o.id).SequenceEqual(spec.Outfits.Select(o=>o.id)),"preset outfits: "+string.Join(", ",spec.Outfits.Select(o=>o.displayName)));
                Check(garments.All(g=>g.chains.All(c=>motion.IsChainPaused(c)==!g.gameObject.activeSelf)),"only the worn shirt's spring chains run ("+motion.ActiveJointCount+" of "+motion.JointCount+" joints)");
                foreach(var (o,tag) in new[]{(signature,"signature"),(other,other.id)}) {
                    Check(w.WearOutfit(o.id)&&w.Current.outfitId==o.id&&Active().SequenceEqual(o.garments.OrderBy(x=>x)),o.displayName+" wears "+string.Join(", ",o.garments.Select(id=>G(id).displayName)));
                    Settle();
                    foreach(var (view,dir) in new[]{("front",model.forward),("three-quarter",(model.forward+model.right).normalized),("back",-model.forward),("side",model.right)})
                        Shot(tag+"-"+view,dir,3.9f,focus,30);
                    Shot(tag+"-chest",(model.forward*3+model.right).normalized,1.6f,model.position+model.up*1.18f,15);
                    var topRenderer=G(Slot(o,GarmentSlot.Top)).GetComponent<SkinnedMeshRenderer>();
                    var body=copy.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r=>r.name==spec.Body);
                    // neck: the base body's skin, not the shirt, faces the camera between the collar and the jaw
                    var (covered,rays)=NeckCover(body,topRenderer,model,bones["CC_Base_NeckTwist02"]);
                    Check(covered==rays,$"{o.displayName}: skin covers the front of the neck above the collar ({covered} of {rays} rays hit skin first)");
                    var neck=bones["CC_Base_NeckTwist02"].position;
                    Shot(tag+"-neck",(model.forward*2+model.right).normalized,1.1f,neck-model.up*.02f,14);
                    // armpits: the side panel stays with the chest when the arms rise (no web from elbow to waist)
                    var drift=new[]{(CompanionBodyIdle.Gesture.Yawn,"yawn"),(CompanionBodyIdle.Gesture.SideStretch,"side stretch")}
                        .Select(g=>{Pose(g.Item1,.45f);return (g.Item2,SideDrift(topRenderer,model,bones["CC_Base_Spine02"],bones["CC_Base_R_Upperarm"]));}).ToArray();
                    Check(drift.All(d=>d.Item2<.03f),$"{o.displayName}: the shirt's side below the armpit stays with the chest as the arms rise ("+string.Join(", ",drift.Select(d=>d.Item1+" "+(d.Item2*100).ToString("F1")+" cm"))+")");
                    var shoulder=model.InverseTransformPoint(bones["CC_Base_R_Upperarm"].position);
                    var pit=model.TransformPoint(new Vector3(shoulder.x+.06f,shoulder.y-.10f,shoulder.z));
                    Pose(CompanionBodyIdle.Gesture.Yawn,.17f);Shot(tag+"-armpit-level",model.forward,1.25f,pit,24);
                    Pose(CompanionBodyIdle.Gesture.Yawn,.45f);Shot(tag+"-armpit-raised",model.forward,1.25f,pit+model.up*.06f,24);
                    Pose(CompanionBodyIdle.Gesture.SideStretch,.45f);Shot(tag+"-armpit-stretch",(model.forward*2+model.right).normalized,1.4f,pit+model.up*.06f,26);
                    Settle();
                }
                // swing: the open shirt's edge chains respond to motion while the brown shirt's chains rest
                var paused=garments.Where(g=>!g.gameObject.activeSelf).SelectMany(g=>g.chains).ToArray();
                var running=garments.Where(g=>g.gameObject.activeSelf).SelectMany(g=>g.chains).ToArray();
                var hip=bones["CC_Base_Hip"];float peak=0;motion.Step(1/60f,false);
                for(int frame=0;frame<600;frame++){idle.Advance(1/60f,false,false);hip.rotation=Quaternion.AngleAxis(9*Mathf.Sin(frame/60f*Mathf.PI*1.2f),model.up)*hip.rotation;motion.Step(1/60f,false);peak=Mathf.Max(peak,motion.MaxDeviation);}
                Check(running.Length>0&&paused.Length>0&&running.All(c=>!motion.IsChainPaused(c))&&paused.All(motion.IsChainPaused)&&peak>.006f&&peak<.25f&&motion.Finite,
                    other.displayName+": "+running.Length+" garment chains swing (peak "+(peak*100).ToString("F1")+" cm) while "+paused.Length+" chains of the stored shirt stay at rest");
                Settle();
                Check(motion.DeepestPenetration()>-.004f,"settled garment chains stay outside the body colliders ("+(motion.DeepestPenetration()*1000).ToString("F1")+" mm)");
                foreach(var gesture in new[]{CompanionBodyIdle.Gesture.Yawn,CompanionBodyIdle.Gesture.SideStretch,CompanionBodyIdle.Gesture.HipTurn}) {
                    for(int frame=0;frame<120;frame++){idle.SampleGesture(3,gesture,.38f+frame*.001f);motion.Step(1/60f,false);}
                    Shot(other.id+"-gesture-"+gesture,model.forward,3.9f,focus,30);
                }
                // the watch (or any accessory) rides the wrist rigidly through the gestures
                var accessory=Slot(other,GarmentSlot.Accessory);
                if(accessory!=null) {
                    var r=G(accessory).GetComponent<SkinnedMeshRenderer>();var wrist=bones["CC_Base_L_Forearm"];var mesh=new Mesh();float drift=0;Vector3 rest=Vector3.zero;bool first=true;
                    foreach(var gesture in new[]{CompanionBodyIdle.Gesture.Yawn,CompanionBodyIdle.Gesture.SideStretch,CompanionBodyIdle.Gesture.HipTurn})
                        for(int frame=0;frame<90;frame+=15){
                            idle.SampleGesture(3,gesture,.38f+frame*.002f);r.BakeMesh(mesh);
                            var c=Vector3.zero;foreach(var v in mesh.vertices)c+=r.transform.TransformPoint(v);c/=mesh.vertexCount;
                            var local=wrist.InverseTransformPoint(c);if(first){rest=local;first=false;}drift=Mathf.Max(drift,(local-rest).magnitude);
                        }
                    UnityEngine.Object.DestroyImmediate(mesh);
                    Check(drift<.003f,G(accessory).displayName+" stays on the forearm through gestures (drift "+(drift*1000).ToString("F1")+" mm)");
                }
                // mix & match and the optional accessory
                var top=Slot(signature,GarmentSlot.Top);var bottom=Slot(other,GarmentSlot.Bottom);
                Check(w.WearOutfit(other.id)&&w.Equip(top)&&w.Current.outfitId==CompanionWardrobe.CustomOutfit&&G(top).gameObject.activeSelf&&G(bottom).gameObject.activeSelf&&!G(Slot(other,GarmentSlot.Top)).gameObject.activeSelf,
                    "mix & match: "+G(top).displayName+" with "+G(bottom).displayName+" (one garment per slot)");
                Settle();Shot("mix-front",model.forward,3.9f,focus,30);
                w.ClearAccessory();
                Check(w.Worn(GarmentSlot.Accessory)==null&&garments.Where(g=>g.slot==GarmentSlot.Accessory).All(g=>!g.gameObject.activeSelf),"the accessory slot can be emptied");
                Check(w.Equip(Slot(signature,GarmentSlot.Bottom))&&w.Equip(Slot(signature,GarmentSlot.Top))&&w.Current.outfitId==signature.id,"wearing the signature pieces again is recognised as the signature outfit");
                Check(!w.Equip("unknown.garment")&&!w.WearOutfit("unknown-outfit")&&w.Current.outfitId==signature.id,"unknown garments and outfits are refused");
                // 50 switches: same garments, no new materials or objects
                int materials=Resources.FindObjectsOfTypeAll<Material>().Length,objects=copy.GetComponentsInChildren<Transform>(true).Length;
                for(int i=0;i<50;i++)w.WearOutfit(i%2==0?other.id:signature.id);
                Check(Resources.FindObjectsOfTypeAll<Material>().Length==materials&&copy.GetComponentsInChildren<Transform>(true).Length==objects&&Active().SequenceEqual(signature.garments.OrderBy(x=>x)),
                    "50 outfit switches create no materials or objects and end on the signature look");
                // tints follow whichever garments are worn; untinted parts keep their colour
                w.WearOutfit(other.id);w.Apply(new CompanionWardrobe.Look{outfitId=other.id,topColor=1,bottomColor=2});
                var worn=garments.Where(g=>g.gameObject.activeSelf).SelectMany(g=>g.GetComponent<SkinnedMeshRenderer>().sharedMaterials).ToArray();
                Check(worn.Where(m=>m.name.EndsWith("_Top")).All(m=>m.GetColor("_BaseColor")==CompanionWardrobe.Palette[1])&&worn.Where(m=>m.name.EndsWith("_Bottom")).All(m=>m.GetColor("_BaseColor")==CompanionWardrobe.Palette[2])&&
                      worn.Where(m=>!m.name.EndsWith("_Top")&&!m.name.EndsWith("_Bottom")&&!m.name.EndsWith("_Shoes")).All(m=>m.GetColor("_BaseColor")==Color.white),
                    "top and bottom colours tint the worn shirt and chinos; tee, buttons and watch keep their colours");
            }
            Check(Active().SequenceEqual(signature.garments.OrderBy(x=>x))&&garments.All(g=>g.chains.All(c=>motion.IsChainPaused(c)==!g.gameObject.activeSelf)),
                "disposing the wardrobe restores the scene's signature garments and their chains");
            // category rule, both ways: a garment never shows on, nor is offered to, the other category's body
            var sample=garments.First(g=>g.slot==GarmentSlot.Top);
            var injected=UnityEngine.Object.Instantiate(sample.gameObject,copy.transform);injected.name="Injected other-category top";injected.SetActive(true);
            var ig=injected.GetComponent<CompanionGarment>();ig.id="test.other-category.top";ig.category=spec.Category==WardrobeCategory.Male?WardrobeCategory.Female:WardrobeCategory.Male;
            using(var w=new CompanionWardrobe(model,false))
                Check(!w.Garments.Contains(ig)&&!injected.activeSelf&&!w.Equip(ig.id)&&w.Options(GarmentSlot.Top).All(g=>g.category==spec.Category),
                    $"a {ig.category} garment placed on {spec.Name} ({spec.Category}) is hidden, never offered and cannot be equipped");
            UnityEngine.Object.DestroyImmediate(injected);
            var host=new GameObject("other-category body"){hideFlags=HideFlags.HideAndDontSave};
            try {
                var p=host.AddComponent<CompanionWardrobeProfile>();p.category=ig.category;p.bodyFamily="test";
                var stray=UnityEngine.Object.Instantiate(sample.gameObject,host.transform);stray.SetActive(true);
                using(var w=new CompanionWardrobe(host.transform,false))
                    Check(!w.HasModularWardrobe&&!stray.activeSelf&&!w.Equip(sample.id),$"a {spec.Category} garment placed on a {ig.category} body is hidden and never offered");
                UnityEngine.Object.DestroyImmediate(p);stray.SetActive(true);
                using(var w=new CompanionWardrobe(host.transform,false))
                    Check(!w.HasModularWardrobe&&!stray.activeSelf,"a body without a wardrobe profile never wears modular garments");
            }
            finally{UnityEngine.Object.DestroyImmediate(host);}
            // occlusion masks: a worn garment hides the body part it covers, and only while worn
            var probe=UnityEngine.Object.Instantiate(garments.First(g=>g.slot==GarmentSlot.Accessory).gameObject,copy.transform);
            var pg=probe.GetComponent<CompanionGarment>();pg.id="test.occlusion";pg.hides=new[]{spec.Name+"_Eyes"};probe.SetActive(false);
            var eyes=copy.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r=>r.name==spec.Name+"_Eyes");
            using(var w=new CompanionWardrobe(model,false)) {
                w.Equip(pg.id);bool hidden=!eyes.enabled;w.ClearAccessory();
                Check(hidden&&eyes.enabled,"a garment's occlusion mask hides the covered body renderer only while worn");
            }
            UnityEngine.Object.DestroyImmediate(probe);
            Note("wardrobe renders written to "+folder);
        }
        // Rays toward the neck axis from the front round to each side, just above the collar (±50°) and under
        // the jaw (±70°). The base body must be hit first: a garment face there is neck skin left inside the
        // garment, which shows the garment's fabric on the neck (the chambray collar showed blue skin).
        static (int covered,int rays) NeckCover(SkinnedMeshRenderer body,SkinnedMeshRenderer top,Transform model,Transform neck)
        {
            var b=BakeTriangles(body);var t=BakeTriangles(top);
            var n=model.InverseTransformPoint(neck.position);int covered=0,rays=0;
            foreach(var (dy,span) in new[]{(-.005f,50),(.015f,70)})
                for(int a=-span;a<=span;a+=10) {
                    var dir=model.TransformDirection(new Vector3(Mathf.Sin(a*Mathf.Deg2Rad),0,Mathf.Cos(a*Mathf.Deg2Rad)));
                    var origin=model.TransformPoint(new Vector3(n.x,n.y+dy,n.z))+dir*.25f;
                    rays++;if(Nearest(b,origin,-dir)<Nearest(t,origin,-dir))covered++;
                }
            return (covered,rays);
        }
        static Vector3[] BakeTriangles(SkinnedMeshRenderer r)
        {
            var mesh=new Mesh();
            try{r.BakeMesh(mesh);var m=r.transform.localToWorldMatrix;var v=mesh.vertices;return mesh.triangles.Select(i=>m.MultiplyPoint3x4(v[i])).ToArray();}
            finally{UnityEngine.Object.DestroyImmediate(mesh);}
        }
        // Möller–Trumbore over a flat triangle list; float.MaxValue when nothing is hit.
        static float Nearest(Vector3[] tris,Vector3 origin,Vector3 dir)
        {
            float best=float.MaxValue;
            for(int i=0;i<tris.Length;i+=3) {
                Vector3 e1=tris[i+1]-tris[i],e2=tris[i+2]-tris[i],p=Vector3.Cross(dir,e2);float det=Vector3.Dot(e1,p);
                if(Mathf.Abs(det)<1e-12f)continue;
                float inv=1/det;Vector3 s=origin-tris[i];float u=Vector3.Dot(s,p)*inv;if(u<0||u>1)continue;
                Vector3 q=Vector3.Cross(s,e1);float v=Vector3.Dot(dir,q)*inv;if(v<0||u+v>1)continue;
                float d=Vector3.Dot(e2,q)*inv;if(d>0&&d<best)best=d;
            }
            return best;
        }
        // Largest gap between where the shirt's side band below the armpit is skinned and where the chest
        // alone would carry it. Underarm weights that reach down the side drag this band up into a web.
        static float SideDrift(SkinnedMeshRenderer r,Transform model,Transform chest,Transform upperArm)
        {
            var mesh=r.sharedMesh;var verts=mesh.vertices;var bind=mesh.bindposes;
            int ic=Array.IndexOf(r.bones,chest),iu=Array.IndexOf(r.bones,upperArm);
            if(ic<0||iu<0)return float.MaxValue;
            var rest=model.worldToLocalMatrix*r.transform.localToWorldMatrix;
            var s=rest.MultiplyPoint3x4(bind[iu].inverse.MultiplyPoint3x4(Vector3.zero));
            var baked=new Mesh();r.BakeMesh(baked);var posed=baked.vertices;UnityEngine.Object.DestroyImmediate(baked);
            var chestRigid=chest.localToWorldMatrix*bind[ic];var world=r.transform.localToWorldMatrix;
            float sx=Mathf.Abs(s.x),worst=0;int n=0;
            for(int i=0;i<verts.Length;i++) {
                var p=rest.MultiplyPoint3x4(verts[i]);float ax=Mathf.Abs(p.x);
                if(ax<.81f*sx||ax>1.31f*sx||p.y<s.y-.305f||p.y>s.y-.185f)continue;
                n++;worst=Mathf.Max(worst,(world.MultiplyPoint3x4(posed[i])-chestRigid.MultiplyPoint3x4(verts[i])).magnitude);
            }
            return n>20?worst:float.MaxValue;
        }

        // ---- In-app checks (Play): picker, labels, draft, springs, speech shapes, wardrobe, captures ----
        static CharacterSpec current;static TalkingCharacter app;static UIDocument doc;static PanelSettings originalPanel,panel;static RenderTexture uiTarget;static Texture2D uiImage;
        static double next;static int step;static string savedCharacter,savedLook;static readonly List<string> appLines=new List<string>();
        static string LookPreference=>CompanionWardrobe.Preference+"."+current.Name;
        public static void InApp(CharacterSpec spec)
        {
            if(!EditorApplication.isPlaying||app!=null)throw new Exception("Run in Play mode");
            current=spec;
            app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>();doc=app.GetComponent<UIDocument>();originalPanel=doc.panelSettings;
            savedCharacter=PlayerPrefs.GetString(TalkingCharacter.CharacterPreference,"");savedLook=PlayerPrefs.GetString(LookPreference,"");
            // run from the default look whatever this device saved (Finish restores the owner's look)
            PlayerPrefs.DeleteKey(LookPreference);
            panel=UnityEngine.Object.Instantiate(originalPanel);panel.scaleMode=PanelScaleMode.ConstantPixelSize;panel.scale=1;
            uiTarget=new RenderTexture(390,844,24);uiTarget.Create();panel.targetTexture=uiTarget;doc.panelSettings=panel;doc.rootVisualElement.style.width=390;doc.rootVisualElement.style.height=844;
            uiImage=new Texture2D(390,844,TextureFormat.RGB24,false);Directory.CreateDirectory(spec.EvidenceFolder);step=0;appLines.Clear();next=EditorApplication.timeSinceStartup+2;EditorApplication.update+=Tick;
        }
        static void AppCheck(bool ok,string label){appLines.Add((ok?"PASS ":"FAIL ")+label);if(!ok)throw new Exception(label);}
        static void CaptureUi(string file){var old=RenderTexture.active;RenderTexture.active=uiTarget;uiImage.ReadPixels(new Rect(0,0,390,844),0,0);uiImage.Apply();RenderTexture.active=old;File.WriteAllBytes(Path.Combine(current.EvidenceFolder,file+".png"),uiImage.EncodeToPNG());}
        static void Tick()
        {
            EditorApplication.QueuePlayerLoopUpdate();if(EditorApplication.timeSinceStartup<next)return;
            try {
                var root=doc.rootVisualElement;var picker=root.Q<DropdownField>("character-picker");
                string name=current.Name,lower=name.ToLowerInvariant();int index=Array.FindIndex(app.characters,c=>c.name==name);
                // modular wardrobes add an outfit round-trip (choose, save, reopen) before the new-chat step
                var plan=current.Modular?new[]{0,1,2,3,4,10,11,5,6}:new[]{0,1,2,3,4,5,6};
                if(step>=plan.Length){Finish(null);return;}
                switch(plan[step++]) {
                    case 0:
                        app.SelectCharacter(0);root.Q<TextField>("message-input").value="Keep this draft";
                        AppCheck(picker!=null&&picker.choices.SequenceEqual(app.characters.Select(c=>c.name))&&picker.choices[0]=="Alita"&&index>0,"settings lists the roster ("+string.Join(", ",picker?.choices??new List<string>())+")");
                        picker.index=index;break;
                    case 1:
                        AppCheck(app.character.name==name&&app.SelectedCharacterName==name,"picker selects "+name);
                        AppCheck(app.Draft=="Keep this draft","switching appearance keeps the draft");
                        AppCheck(root.Q<Label>("character-title").text==name&&root.Q<TextField>("message-input").textEdition.placeholder.Contains(name),"header and composer name "+name);
                        AppCheck(PlayerPrefs.GetString(TalkingCharacter.CharacterPreference,"")==name,"selection is remembered on this device");
                        AppCheck(app.SelectedVoice==current.Voice,name+" speaks with the "+current.Voice+" local voice");
                        AppCheck(app.SecondaryMotion!=null&&app.SecondaryMotion.IsBound,"hair, earring and cloth springs run in the app");
                        AppCheck(app.FullBodyBounds.size.y>1.5f&&app.FullBodyBounds.size.y<2.2f,"portrait framing rebakes "+name+"'s full-body bounds ("+app.FullBodyBounds.size.y.ToString("F2")+" m)");
                        {   // what the portrait camera actually draws (alpha coverage of the character render target)
                            var rt=app.portraitCamera.targetTexture;var probe=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);
                            app.portraitCamera.Render();var old=RenderTexture.active;RenderTexture.active=rt;probe.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);probe.Apply();RenderTexture.active=old;
                            int covered=probe.GetPixels32().Count(p=>p.a>200);UnityEngine.Object.DestroyImmediate(probe);
                            AppCheck(covered>rt.width*rt.height/30,"portrait camera draws "+name+"'s full body ("+covered+" opaque pixels)");
                        }
                        CaptureUi("app-"+lower);break;
                    case 2: {
                        var shape=typeof(TalkingCharacter).GetMethod("Shape",BindingFlags.Instance|BindingFlags.NonPublic);var reset=typeof(TalkingCharacter).GetMethod("ResetFace",BindingFlags.Instance|BindingFlags.NonPublic);
                        var body=app.character.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name==current.Body);
                        foreach(var channel in new[]{"V_Open","Eye_Blink_L","Mouth_Corner_Pull_L","Brow_Raise_In_L","Mouth_Corner_Depress_L","Eye_Widen_L"}) {
                            var before=new Mesh();var after=new Mesh();body.BakeMesh(before);shape.Invoke(app,new object[]{channel,.5f});body.BakeMesh(after);
                            var a=before.vertices;var b=after.vertices;AppCheck(a.Where((v,i)=>(v-b[i]).sqrMagnitude>1e-10f).Any(),channel+" drives "+name+"'s face from the app");
                            reset.Invoke(app,null);AppCheck(app.ShapeWeight(channel)==0,channel+" resets immediately");UnityEngine.Object.DestroyImmediate(before);UnityEngine.Object.DestroyImmediate(after);
                        }
                        app.OpenWardrobe();break;
                    }
                    case 3: {
                        AppCheck(app.WardrobeOpen&&root.Q<Label>("wardrobe-title").text==name+"’s wardrobe","Style opens "+name+"'s wardrobe");
                        if(current.Modular) {
                            var outfit=root.Q<DropdownField>("outfit");
                            AppCheck(outfit!=null&&outfit.choices.SequenceEqual(current.Outfits.Select(o=>o.displayName).Append("Mix & match"))&&outfit.index==0,
                                "outfit picker offers "+string.Join(", ",outfit?.choices??new List<string>())+"; the signature look is worn");
                            var offered=new[]{"top","bottom","shoes","accessory"}.Select(n=>root.Q<DropdownField>(n)).Where(f=>f!=null).SelectMany(f=>f.choices).Where(c=>c!="None").Distinct().ToArray();
                            var mine=current.Garments.Select(g=>g.DisplayName).ToArray();
                            AppCheck(offered.Length>0&&offered.All(mine.Contains)&&app.character.GetComponentsInChildren<CompanionGarment>(true).All(g=>g.category==current.Category),
                                "garment pickers offer only "+current.Category+" garments fitted to "+name+" ("+string.Join(", ",offered)+")");
                        }
                        else AppCheck(root.Q("outfit")==null&&root.Q("top")==null&&root.Q("bottom")==null,"Alita-only garment choices are hidden");
                        AppCheck(new[]{"top-color","bottom-color","hair-color","skin-tone-0"}.All(n=>root.Q(n)!=null)&&(root.Q("shoe-color")!=null)==current.HasShoes,
                            "colour and skin-tone choices offered"+(current.HasShoes?"":"; no shoe colour for a barefoot character"));
                        root.Q<DropdownField>("top-color").index=1;root.Q<DropdownField>("hair-color").index=3;break;
                    }
                    case 4:
                        CaptureUi("app-"+lower+"-wardrobe");app.CloseWardrobe(false);
                        AppCheck(!PlayerPrefs.HasKey(LookPreference),"cancelled preview does not save "+name+"'s look");
                        if(current.Modular){app.OpenWardrobe();root.Q<DropdownField>("outfit").index=1;}
                        else app.NewChat();
                        break;
                    case 10: {
                        var outfit=current.Outfits[1];
                        var worn=app.character.GetComponentsInChildren<CompanionGarment>(true).Where(g=>g.gameObject.activeSelf).Select(g=>g.id).OrderBy(x=>x);
                        AppCheck(worn.SequenceEqual(outfit.garments.OrderBy(x=>x)),"choosing "+outfit.displayName+" in Style dresses "+name+" in "+string.Join(", ",outfit.garments));
                        AppCheck(app.SecondaryMotion!=null&&app.SecondaryMotion.IsBound&&app.SecondaryMotion.ActiveJointCount<app.SecondaryMotion.JointCount,
                            "only the worn garments' springs run ("+app.SecondaryMotion?.ActiveJointCount+" of "+app.SecondaryMotion?.JointCount+" joints)");
                        CaptureUi("app-"+lower+"-"+outfit.id+"-wardrobe");app.CloseWardrobe(true);
                        AppCheck(PlayerPrefs.GetString(LookPreference,"").Contains("\"outfitId\":\""+outfit.id+"\""),"Save look keeps "+outfit.displayName+" on this device");
                        break;
                    }
                    case 11: {
                        var outfit=current.Outfits[1];
                        CaptureUi("app-"+lower+"-"+outfit.id);app.OpenWardrobe();
                        AppCheck(root.Q<DropdownField>("outfit").value==outfit.displayName,"reopening Style shows "+outfit.displayName);
                        app.CloseWardrobe(false);app.NewChat();break;
                    }
                    case 5:
                        AppCheck(root.Q<ScrollView>("conversation").contentContainer.Children().First().Q<Label>().text.Contains("Hi, I'm "+name),"new chat greets as "+name);
                        CaptureUi("app-"+lower+"-new-chat");picker.index=0;break;
                    case 6:
                        AppCheck(app.character.name=="Alita"&&app.SecondaryMotion==null&&root.Q<Label>("character-title").text=="Alita","picker returns to Alita and releases "+name+"'s springs");
                        break;
                    default:Finish(null);return;
                }
                next=EditorApplication.timeSinceStartup+1.5;
            }catch(Exception e){Finish(e.ToString());}
        }
        static void Finish(string error)
        {
            EditorApplication.update-=Tick;if(error!=null)appLines.Add("FAIL "+error);File.WriteAllLines(Path.Combine(current.EvidenceFolder,"app-checks.txt"),appLines);
            if(app!=null&&app.WardrobeOpen)app.CloseWardrobe(false);
            if(savedCharacter.Length==0)PlayerPrefs.DeleteKey(TalkingCharacter.CharacterPreference);else PlayerPrefs.SetString(TalkingCharacter.CharacterPreference,savedCharacter);
            if(savedLook.Length==0)PlayerPrefs.DeleteKey(LookPreference);else PlayerPrefs.SetString(LookPreference,savedLook);PlayerPrefs.Save();
            if(doc!=null){doc.rootVisualElement.style.width=StyleKeyword.Null;doc.rootVisualElement.style.height=Length.Percent(100);doc.panelSettings=originalPanel;}
            uiTarget.Release();UnityEngine.Object.DestroyImmediate(uiTarget);UnityEngine.Object.DestroyImmediate(uiImage);UnityEngine.Object.DestroyImmediate(panel);app=null;
        }
    }
}
