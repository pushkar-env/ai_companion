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
    // Validates the Blender-rigged Meera on an isolated hidden copy (stopped Editor), then in the real
    // portrait app (Play). Writes evidence under docs/evidence/m1/meera. Never changes Editor layout.
    public static class MeeraRigChecks
    {
        static readonly string[] Arkit={"browDownLeft","browDownRight","browInnerUp","browOuterUpLeft","browOuterUpRight","cheekPuff","cheekSquintLeft","cheekSquintRight",
            "eyeBlinkLeft","eyeBlinkRight","eyeLookDownLeft","eyeLookDownRight","eyeLookInLeft","eyeLookInRight","eyeLookOutLeft","eyeLookOutRight","eyeLookUpLeft","eyeLookUpRight",
            "eyeSquintLeft","eyeSquintRight","eyeWideLeft","eyeWideRight","jawForward","jawLeft","jawOpen","jawRight","mouthClose","mouthDimpleLeft","mouthDimpleRight",
            "mouthFrownLeft","mouthFrownRight","mouthFunnel","mouthLeft","mouthLowerDownLeft","mouthLowerDownRight","mouthPressLeft","mouthPressRight","mouthPucker","mouthRight",
            "mouthRollLower","mouthRollUpper","mouthShrugLower","mouthShrugUpper","mouthSmileLeft","mouthSmileRight","mouthStretchLeft","mouthStretchRight",
            "mouthUpperUpLeft","mouthUpperUpRight","noseSneerLeft","noseSneerRight","tongueOut"};
        static readonly string[] Expressions={"Eye_Blink_L","Eye_Blink_R","Eye_Widen_L","Eye_Widen_R","Mouth_Corner_Pull_L","Mouth_Corner_Pull_R",
            "Mouth_Corner_Depress_L","Mouth_Corner_Depress_R","Brow_Raise_In_L","Brow_Raise_In_R","Brow_Raise_Outer_L","Brow_Raise_Outer_R"};
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/evidence/m1/meera"));
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

        [MenuItem("Companion/Characters/Check Meera Rig")]
        public static void Rig()
        {
            lines.Clear();GameObject copy=null,cameraObject=null;RenderTexture target=null;Texture2D image=null;
            CompanionBodyIdle idle=null;CompanionGaze gaze=null;
            try {
                if(EditorApplication.isPlaying)throw new Exception("Run the isolated rig checks while stopped");
                var app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>(FindObjectsInactive.Include);
                Check(app.characters.Length==2&&app.characters[0].name=="Alita"&&app.characters[1].name=="Meera"&&app.character==app.characters[0].model,"Alita stays the scene default; Meera is registered as a second appearance");
                var source=app.characters[1].model.gameObject;
                Check(!source.activeSelf,"Meera stays inactive until selected");
                copy=UnityEngine.Object.Instantiate(source);copy.hideFlags=HideFlags.HideAndDontSave;copy.SetActive(true);
                var model=copy.transform;
                var renderers=copy.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Check(renderers.Select(r=>r.name).OrderBy(n=>n).SequenceEqual(new[]{"Meera_Body","Meera_Eyes","Meera_Mouth"}),"body, eyeball and mouth renderers present");
                Check(renderers.All(r=>r.sharedMaterials.All(m=>m!=null&&m.shader!=null&&m.shader.name=="Universal Render Pipeline/Lit")),"every material slot uses URP Lit");
                Check(TalkingCharacter.CanAnimate(model),"ten speech channels plus CC head and jaw bones");
                var names=new HashSet<string>(renderers.SelectMany(r=>Enumerable.Range(0,r.sharedMesh.blendShapeCount).Select(i=>r.sharedMesh.GetBlendShapeName(i))));
                Check(Expressions.All(names.Contains),"blink, widen, smile, frown and brow channels used by the app");
                var missing=Arkit.Where(n=>!names.Contains(n)).ToArray();
                Check(missing.Length==0,"all 52 FACE-01 ARKit-style channels present"+(missing.Length>0?" (missing "+string.Join(",",missing)+")":""));
                Check(!names.Any(n=>n.Contains("__f")),"in-between helper shapes are folded into frames");
                var body=renderers.First(r=>r.name=="Meera_Body");var mesh=body.sharedMesh;int blink=mesh.GetBlendShapeIndex("Eye_Blink_L");
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
                Check(nose>.08f,"Meera faces the model forward axis used for camera framing and knee bends");
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
                    Check(wardrobe.HasSkin&&wardrobe.HasTop&&wardrobe.HasBottom&&wardrobe.HasHair&&wardrobe.HasShoes&&!wardrobe.HasSeparates,"skin, kurti, palazzo, hair and sneaker roles found; Alita-fitted separates not attached");
                    wardrobe.Apply(new CompanionWardrobe.Look{topColor=1,bottomColor=3,hairColor=2,shoeColor=4,skinTone=3});
                    var mats=body.sharedMaterials;
                    Check(mats.First(m=>m.name=="Meera_Kurti").GetColor("_BaseColor")==CompanionWardrobe.Palette[1]&&mats.First(m=>m.name=="Meera_Palazzo").GetColor("_BaseColor")==CompanionWardrobe.Palette[3],"kurti and palazzo tint independently");
                    Check(mats.First(m=>m.name=="Meera_Skin").GetColor("_BaseColor")!=Color.white&&mats.First(m=>m.name=="Meera_Earrings").GetColor("_BaseColor")==Color.white,"skin tone applies to skin only");
                }
                Check(body.sharedMaterials.All(m=>AssetDatabase.Contains(m)),"wardrobe disposal restores the shared material assets");
                // springs: hair, earrings, kurti panels and bell sleeves
                var motion=copy.GetComponent<CompanionSecondaryMotion>();
                Check(motion!=null&&motion.Bind(),"spring chains bind from bind poses");
                Check(motion.JointCount==motion.chains.Sum(c=>c.bones.Length)&&motion.JointCount>=40&&motion.ColliderCount>=10,$"{motion.JointCount} spring joints and {motion.ColliderCount} body colliders bound");
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
                cameraObject=new GameObject("Meera review camera"){hideFlags=HideFlags.HideAndDontSave};var camera=cameraObject.AddComponent<Camera>();
                camera.CopyFrom(app.portraitCamera);camera.enabled=false;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.055f,.065f,.07f,1);
                target=new RenderTexture(600,900,24){antiAliasing=4};target.Create();camera.targetTexture=target;camera.aspect=2f/3;camera.fieldOfView=30;
                image=new Texture2D(600,900,TextureFormat.RGB24,false);Directory.CreateDirectory(Folder);
                idle.Sample(2);for(int frame=0;frame<240;frame++){idle.Sample(2);motion.Step(1/60f,false);}
                var focus=model.position+model.up*.82f;
                camera.transform.position=focus+model.forward*3.9f;camera.transform.LookAt(focus);camera.Render();   // warm-up: the first render after layer changes can be empty
                // live GPU-skinned draw (not baked): catches mesh layout/skinning faults that BakeMesh hides
                camera.Render();camera.Render();
                {var old=RenderTexture.active;RenderTexture.active=target;image.ReadPixels(new Rect(0,0,600,900),0,0);image.Apply();RenderTexture.active=old;
                 var bg=new Color32(14,17,18,255);int covered=image.GetPixels32().Count(p=>Mathf.Abs(p.r-bg.r)+Mathf.Abs(p.g-bg.g)+Mathf.Abs(p.b-bg.b)>24);
                 Check(covered>600*900/40,"live skinned renderers draw the full body ("+covered+" covered pixels)");}
                foreach(var (view,dir) in new[]{("front",model.forward),("side",model.right),("back",-model.forward),("three-quarter",(model.forward+model.right).normalized)}) {
                    camera.transform.position=focus+dir*3.9f;camera.transform.LookAt(focus);ExpressiveIdleReview.Capture(camera,copy,target,image,Path.Combine(Folder,"idle-"+view+".png"));
                }
                camera.fieldOfView=12;var face=head.position+model.up*.05f;camera.transform.position=face+model.forward*1.6f;camera.transform.LookAt(face);
                var sheet=new (string name,string[] shapes,float weight)[]{("neutral",new string[0],0),("speech-aa",new[]{"V_Open"},45),("speech-oo",new[]{"V_Tight_O"},70),("speech-ee",new[]{"V_Wide"},70),
                    ("speech-ff",new[]{"V_Dental_Lip"},80),("blink-half",new[]{"Eye_Blink_L","Eye_Blink_R"},50),("blink",new[]{"Eye_Blink_L","Eye_Blink_R"},100),
                    ("happy",new[]{"Mouth_Corner_Pull_L","Mouth_Corner_Pull_R","Brow_Raise_Outer_L","Brow_Raise_Outer_R"},60),("concerned",new[]{"Brow_Raise_In_L","Brow_Raise_In_R","Mouth_Corner_Depress_L","Mouth_Corner_Depress_R"},70),
                    ("curious",new[]{"Brow_Raise_Outer_L","Eye_Widen_L","Eye_Widen_R"},80),("jaw-open",new[]{"jawOpen"},100),("pucker",new[]{"mouthPucker"},100)};
                foreach(var shot in sheet){Clear(renderers);foreach(var s in shot.shapes)Set(renderers,s,shot.weight);ExpressiveIdleReview.Capture(camera,copy,target,image,Path.Combine(Folder,"face-"+shot.name+".png"));}
                Clear(renderers);
                camera.fieldOfView=30;
                foreach(var gesture in new[]{CompanionBodyIdle.Gesture.Yawn,CompanionBodyIdle.Gesture.SideStretch,CompanionBodyIdle.Gesture.HipTurn}) {
                    for(int frame=0;frame<120;frame++){idle.SampleGesture(3,gesture,.38f+frame*.001f);motion.Step(1/60f,false);}
                    camera.transform.position=focus+model.forward*3.9f;camera.transform.LookAt(focus);
                    ExpressiveIdleReview.Capture(camera,copy,target,image,Path.Combine(Folder,"gesture-"+gesture+".png"));
                }
                Note("review renders written to docs/evidence/m1/meera (baked skin, isolated layer 31)");
            }
            catch(Exception e){lines.Add("FAIL "+e.Message);throw;}
            finally {
                gaze?.Dispose();idle?.Dispose();
                if(copy!=null)UnityEngine.Object.DestroyImmediate(copy);if(cameraObject!=null)UnityEngine.Object.DestroyImmediate(cameraObject);
                if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}if(image!=null)UnityEngine.Object.DestroyImmediate(image);
                Directory.CreateDirectory(Folder);File.WriteAllLines(Path.Combine(Folder,"rig-checks.txt"),lines);
            }
        }

        // ---- In-app checks (Play): picker, labels, draft, springs, speech shapes, wardrobe, captures ----
        static TalkingCharacter app;static UIDocument doc;static PanelSettings originalPanel,panel;static RenderTexture uiTarget;static Texture2D uiImage;
        static double next;static int step;static string savedCharacter,savedLook;static readonly List<string> appLines=new List<string>();
        [MenuItem("Companion/Characters/Check Meera In App")]
        public static void InApp()
        {
            if(!EditorApplication.isPlaying||app!=null)throw new Exception("Run in Play mode");
            app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>();doc=app.GetComponent<UIDocument>();originalPanel=doc.panelSettings;
            savedCharacter=PlayerPrefs.GetString(TalkingCharacter.CharacterPreference,"");savedLook=PlayerPrefs.GetString(CompanionWardrobe.Preference+".Meera","");
            panel=UnityEngine.Object.Instantiate(originalPanel);panel.scaleMode=PanelScaleMode.ConstantPixelSize;panel.scale=1;
            uiTarget=new RenderTexture(390,844,24);uiTarget.Create();panel.targetTexture=uiTarget;doc.panelSettings=panel;doc.rootVisualElement.style.width=390;doc.rootVisualElement.style.height=844;
            uiImage=new Texture2D(390,844,TextureFormat.RGB24,false);Directory.CreateDirectory(Folder);step=0;appLines.Clear();next=EditorApplication.timeSinceStartup+2;EditorApplication.update+=Tick;
        }
        static void AppCheck(bool ok,string label){appLines.Add((ok?"PASS ":"FAIL ")+label);if(!ok)throw new Exception(label);}
        static void CaptureUi(string name){var old=RenderTexture.active;RenderTexture.active=uiTarget;uiImage.ReadPixels(new Rect(0,0,390,844),0,0);uiImage.Apply();RenderTexture.active=old;File.WriteAllBytes(Path.Combine(Folder,name+".png"),uiImage.EncodeToPNG());}
        static void Tick()
        {
            EditorApplication.QueuePlayerLoopUpdate();if(EditorApplication.timeSinceStartup<next)return;
            try {
                var root=doc.rootVisualElement;var picker=root.Q<DropdownField>("character-picker");
                switch(step++) {
                    case 0:
                        app.SelectCharacter(0);root.Q<TextField>("message-input").value="Keep this draft";
                        AppCheck(picker!=null&&picker.choices.SequenceEqual(new[]{"Alita","Meera"}),"settings lists Alita and Meera");
                        picker.index=1;break;
                    case 1:
                        AppCheck(app.character.name=="Meera"&&app.SelectedCharacterName=="Meera","picker selects Meera");
                        AppCheck(app.Draft=="Keep this draft","switching appearance keeps the draft");
                        AppCheck(root.Q<Label>("character-title").text=="Meera"&&root.Q<TextField>("message-input").textEdition.placeholder.Contains("Meera"),"header and composer name Meera");
                        AppCheck(PlayerPrefs.GetString(TalkingCharacter.CharacterPreference,"")=="Meera","selection is remembered on this device");
                        AppCheck(app.SecondaryMotion!=null&&app.SecondaryMotion.IsBound,"hair, earring and cloth springs run in the app");
                        AppCheck(app.FullBodyBounds.size.y>1.5f&&app.FullBodyBounds.size.y<2.2f,"portrait framing rebakes Meera's full-body bounds ("+app.FullBodyBounds.size.y.ToString("F2")+" m)");
                        {   // what the portrait camera actually draws (alpha coverage of the character render target)
                            var rt=app.portraitCamera.targetTexture;var probe=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);
                            app.portraitCamera.Render();var old=RenderTexture.active;RenderTexture.active=rt;probe.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);probe.Apply();RenderTexture.active=old;
                            int covered=probe.GetPixels32().Count(p=>p.a>200);UnityEngine.Object.DestroyImmediate(probe);
                            AppCheck(covered>rt.width*rt.height/30,"portrait camera draws Meera's full body ("+covered+" opaque pixels)");
                        }
                        CaptureUi("app-meera");break;
                    case 2: {
                        var shape=typeof(TalkingCharacter).GetMethod("Shape",BindingFlags.Instance|BindingFlags.NonPublic);var reset=typeof(TalkingCharacter).GetMethod("ResetFace",BindingFlags.Instance|BindingFlags.NonPublic);
                        var body=app.character.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name=="Meera_Body");
                        foreach(var channel in new[]{"V_Open","Eye_Blink_L","Mouth_Corner_Pull_L","Brow_Raise_In_L","Mouth_Corner_Depress_L","Eye_Widen_L"}) {
                            var before=new Mesh();var after=new Mesh();body.BakeMesh(before);shape.Invoke(app,new object[]{channel,.5f});body.BakeMesh(after);
                            var a=before.vertices;var b=after.vertices;AppCheck(a.Where((v,i)=>(v-b[i]).sqrMagnitude>1e-10f).Any(),channel+" drives Meera's face from the app");
                            reset.Invoke(app,null);AppCheck(app.ShapeWeight(channel)==0,channel+" resets immediately");UnityEngine.Object.DestroyImmediate(before);UnityEngine.Object.DestroyImmediate(after);
                        }
                        app.OpenWardrobe();break;
                    }
                    case 3:
                        AppCheck(app.WardrobeOpen&&root.Q<Label>("wardrobe-title").text=="Meera’s wardrobe","Style opens Meera's wardrobe");
                        AppCheck(root.Q("outfit")==null&&root.Q("top")==null&&root.Q("bottom")==null,"Alita-only garment choices are hidden");
                        AppCheck(new[]{"top-color","bottom-color","hair-color","shoe-color","skin-tone-0"}.All(n=>root.Q(n)!=null),"colour and skin-tone choices offered");
                        root.Q<DropdownField>("top-color").index=1;root.Q<DropdownField>("hair-color").index=3;break;
                    case 4:
                        CaptureUi("app-meera-wardrobe");app.CloseWardrobe(false);
                        AppCheck(PlayerPrefs.GetString(CompanionWardrobe.Preference+".Meera","")==savedLook,"cancelled preview does not save Meera's look");
                        app.NewChat();break;
                    case 5:
                        AppCheck(root.Q<ScrollView>("conversation").contentContainer.Children().First().Q<Label>().text.Contains("Hi, I'm Meera"),"new chat greets as Meera");
                        CaptureUi("app-meera-new-chat");picker.index=0;break;
                    case 6:
                        AppCheck(app.character.name=="Alita"&&app.SecondaryMotion==null&&root.Q<Label>("character-title").text=="Alita","picker returns to Alita and releases Meera's springs");
                        break;
                    default:Finish(null);return;
                }
                next=EditorApplication.timeSinceStartup+1.5;
            }catch(Exception e){Finish(e.ToString());}
        }
        static void Finish(string error)
        {
            EditorApplication.update-=Tick;if(error!=null)appLines.Add("FAIL "+error);File.WriteAllLines(Path.Combine(Folder,"app-checks.txt"),appLines);
            if(app!=null&&app.WardrobeOpen)app.CloseWardrobe(false);
            if(savedCharacter.Length==0)PlayerPrefs.DeleteKey(TalkingCharacter.CharacterPreference);else PlayerPrefs.SetString(TalkingCharacter.CharacterPreference,savedCharacter);
            if(savedLook.Length==0)PlayerPrefs.DeleteKey(CompanionWardrobe.Preference+".Meera");else PlayerPrefs.SetString(CompanionWardrobe.Preference+".Meera",savedLook);PlayerPrefs.Save();
            if(doc!=null){doc.rootVisualElement.style.width=StyleKeyword.Null;doc.rootVisualElement.style.height=Length.Percent(100);doc.panelSettings=originalPanel;}
            uiTarget.Release();UnityEngine.Object.DestroyImmediate(uiTarget);UnityEngine.Object.DestroyImmediate(uiImage);UnityEngine.Object.DestroyImmediate(panel);app=null;
        }
    }
}
