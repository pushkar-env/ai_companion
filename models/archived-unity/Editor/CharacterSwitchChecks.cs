using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Companion.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Companion.Editor
{
    public static class CharacterSwitchChecks
    {
        static TalkingCharacter app;static int phase,replayIndex;static double deadline,next;
        static readonly List<string> lines=new List<string>();
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/evidence/m1/character-roster"));
        static object Field(string name)=>typeof(TalkingCharacter).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(app);
        static void Check(bool ok,string label){if(!ok)throw new Exception(label);lines.Add("PASS "+label);}
        static void Shape(string name,float weight)=>typeof(TalkingCharacter).GetMethod("Shape",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(app,new object[]{name,weight});
        static void ValidateRig()
        {
            var body=app.character.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name=="CC_Base_Body");
            foreach(string channel in new[]{"V_Open","Eye_Blink_L","Mouth_Corner_Pull_L","Brow_Raise_In_L","Mouth_Corner_Depress_L","Eye_Widen_L"}) {
                var before=new Mesh();var after=new Mesh();Shape(channel,0);body.BakeMesh(before);Shape(channel,.5f);body.BakeMesh(after);
                var a=before.vertices;var b=after.vertices;bool moved=false;for(int i=0;i<a.Length;i++)if((a[i]-b[i]).sqrMagnitude>1e-12f){moved=true;break;}
                Shape(channel,0);UnityEngine.Object.DestroyImmediate(before);UnityEngine.Object.DestroyImmediate(after);
                Check(moved,app.SelectedCharacterName+" actual body deformation: "+channel);
            }
        }
        [MenuItem("Companion/Run Character Switching Checks")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying)throw new Exception("Enter TalkingCompanion Play mode first");
            app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>();if(app==null)throw new Exception("Talking scene required");
            EditorApplication.update-=Tick;lines.Clear();Directory.CreateDirectory(Folder);app.NewChat();
            try {
                Check(app.characters.Length==3,"three supplied characters registered");
                var root=app.GetComponent<UIDocument>().rootVisualElement;var picker=root.Q<DropdownField>("character-picker");
                string mic=app.SelectedMicrophone;root.Q<TextField>("message-input").value="draft survives switching";
                for(int i=0;i<3;i++) {
                    picker.value=app.characters[i].name;
                    Check(app.character==app.characters[i].model&&app.characters.Count(c=>c.model.gameObject.activeSelf)==1,app.characters[i].name+" selector activates exactly one model");
                    Check(app.Draft=="draft survives switching"&&app.SelectedMicrophone==mic&&!app.IsRecording,app.SelectedCharacterName+" retains draft/input choice without recording");
                    ValidateRig();
                }
                Check(!app.SelectCharacter(-1)&&!app.SelectCharacter(3)&&app.SelectedCharacter==2,"invalid selection leaves active model unchanged");
                Check(picker.worldBound.height>=40&&picker.worldBound.xMax<=root.worldBound.xMax&&root.Q("chat-actions").worldBound.yMax<=root.worldBound.yMax&&Screen.height>Screen.width,"selector and existing controls fit portrait");
                app.SelectCharacter(0);app.NewChat();app.Submit("Please say exactly these two sentences: Welcome to a bright new day. We can take a small happy step together.");
                var request=Field("request");app.SelectCharacter(1);
                Check(request!=null&&ReferenceEquals(request,Field("request")),"switch while generating preserves in-flight request");
                phase=0;deadline=EditorApplication.timeSinceStartup+120;EditorApplication.update+=Tick;
            }catch(Exception e){Fail(e);}
        }
        static void Tick()
        {
            try {
                if(!EditorApplication.isPlaying||app==null)throw new Exception("Play stopped");
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Timeout: "+app.State);
                if(phase==0&&app.PlayedSamples>1000&&app.JawAngle>0) {
                    int sample=app.PlayedSamples;var clip=app.GetComponent<AudioSource>().clip;
                    app.SelectCharacter(2);
                    Check(app.IsSpeaking&&app.PlayedSamples>=sample&&app.GetComponent<AudioSource>().clip==clip,"switch during speech preserves clip and audio clock");
                    next=EditorApplication.timeSinceStartup+.15;phase=1;
                } else if(phase==1&&EditorApplication.timeSinceStartup>next&&app.JawAngle>0) {
                    Check(app.GetComponent<AudioSource>().isPlaying,"Cosmos animates during continuing speech");phase=2;
                } else if(phase==2&&app.State.Contains("reply finished")) {
                    Check(app.CanReplay&&((List<TalkingCharacter.Message>)Field("history")).Count==2,"switching retains one completed exchange and Replay cache");
                    replayIndex=0;BeginReplay();
                } else if(phase==3&&app.PlayedSamples>1000&&app.JawAngle>0) {
                    Check(app.GetComponent<AudioSource>().isPlaying&&Field("request")==null,app.SelectedCharacterName+" Replay drives speech and face without another AI request");
                    ScreenCapture.CaptureScreenshot(Path.Combine(Folder,app.SelectedCharacterName.ToLowerInvariant()+".png"));phase=4;
                } else if(phase==4&&app.State.Contains("replay finished")) {
                    Check(((List<TalkingCharacter.Message>)Field("history")).Count==2,app.SelectedCharacterName+" Replay preserves context without duplication");
                    if(++replayIndex<3)BeginReplay();
                    else {
                        app.Replay();app.SelectCharacter(1);app.Interrupt();Check(!app.IsSpeaking&&!app.GetComponent<AudioSource>().isPlaying&&app.CanReplay,"Stop after switching silences speech and keeps Replay");
                        app.Retry();Check(Field("request")!=null,"Retry starts another request after switching");app.SelectCharacter(2);app.NewChat();
                        Check(!app.IsSpeaking&&Field("request")==null&&!app.CanReplay&&app.Draft==""&&((List<TalkingCharacter.Message>)Field("history")).Count==0&&app.SelectedCharacter==2,"New chat cancels switched request and clears session, keeping appearance");
                        app.SelectCharacter(0);Finish();
                    }
                }
            }catch(Exception e){Fail(e);}
        }
        static void BeginReplay(){app.SelectCharacter(replayIndex);app.Replay();phase=3;deadline=EditorApplication.timeSinceStartup+45;}
        static void Fail(Exception e){lines.Add("FAIL "+e.Message);Finish();Debug.LogError(e.Message);}
        static void Finish(){EditorApplication.update-=Tick;File.WriteAllLines(Path.Combine(Folder,"editor-checks.txt"),lines);Debug.Log("Character switching checks saved");}
    }
}
