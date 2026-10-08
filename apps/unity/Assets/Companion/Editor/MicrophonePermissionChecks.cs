using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Companion.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Companion.Editor
{
    public static class MicrophonePermissionChecks
    {
        sealed class Fake:IMicrophonePermission
        {
            public bool granted,done;public int requests,settings;
            public bool Granted=>granted;public bool CanOpenSettings=>true;
            public IEnumerator Request(){requests++;while(!done)yield return null;}
            public bool OpenSettings(){settings++;return true;}
        }
        static TalkingCharacter app;static Fake fake;static object originalPermission;static string draft;
        static UIDocument doc;static PanelSettings original,panel;static RenderTexture target;
        static StyleLength width,height;static double next;static int step;
        static readonly List<string> lines=new List<string>();
        static readonly BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
        static string Folder=>Path.GetFullPath(Application.dataPath+"/../../../docs/evidence/microphone-permission");
        static void Check(bool ok,string label){lines.Add((ok?"PASS ":"FAIL ")+label);if(!ok)throw new Exception(label);}
        static void Click(string name){var b=doc.rootVisualElement.Q<Button>(name);b.Focus();using(var e=NavigationSubmitEvent.GetPooled()){b.SendEvent(e);}}
        public static void Run()
        {
            if(!EditorApplication.isPlaying||app!=null)throw new Exception("Run once in Play mode");
            app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>();draft=app.Draft;doc=app.GetComponent<UIDocument>();original=doc.panelSettings;
            originalPermission=typeof(TalkingCharacter).GetField("microphonePermission",Flags).GetValue(app);fake=new Fake();typeof(TalkingCharacter).GetField("microphonePermission",Flags).SetValue(app,fake);
            panel=UnityEngine.Object.Instantiate(original);panel.scaleMode=PanelScaleMode.ConstantPixelSize;panel.scale=1;
            target=new RenderTexture(360,640,24);target.Create();panel.targetTexture=target;doc.panelSettings=panel;
            width=doc.rootVisualElement.style.width;height=doc.rootVisualElement.style.height;doc.rootVisualElement.style.width=360;doc.rootVisualElement.style.height=640;
            lines.Clear();Directory.CreateDirectory(Folder);step=0;next=EditorApplication.timeSinceStartup+1;EditorApplication.update+=Tick;
        }
        static void Capture(string name)
        {
            var prior=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(360,640,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,360,640),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Folder,name+".png"),image.EncodeToPNG());RenderTexture.active=prior;UnityEngine.Object.DestroyImmediate(image);
        }
        static void Tick()
        {
            EditorApplication.QueuePlayerLoopUpdate();if(EditorApplication.timeSinceStartup<next)return;
            try {
                var root=doc.rootVisualElement;
                switch(step++) {
                    case 0:
                        Check(fake.requests==0,"No permission request on app startup");
                        root.Q<TextField>("message-input").value="Keep this draft • नमस्ते";app.ToggleRecording();
                        Check(app.PermissionPanelOpen&&!app.IsRecording&&fake.requests==0,"Mic first shows explanation without requesting or capturing");break;
                    case 1:
                        foreach(var name in new[]{"permission-continue","permission-cancel"}){var b=root.Q(name).worldBound;Check(b.height>=44&&b.yMin>=0&&b.yMax<=640&&b.xMax<=360,name+" reachable at 360x640");}
                        Check(!root.Q("conversation-shell").enabledSelf,"Permission modal blocks underlying chat");Capture("explanation");Click("permission-continue");break;
                    case 2:
                        Check(fake.requests==1&&!app.IsRecording,"Explicit Continue requests once without recording");
                        Check(!root.Q<Button>("permission-continue").enabledSelf,"Pending request prevents repeated prompt");fake.done=true;break;
                    case 3:
                        Check(app.PermissionPanelOpen&&root.Q("permission-settings").style.display.value==DisplayStyle.Flex,"Denial offers device settings and typing fallback");Capture("denied");Click("permission-settings");break;
                    case 4:
                        Check(fake.settings==1,"Device settings requires explicit button action");
                        Check(app.HandleBack()&&!app.PermissionPanelOpen&&app.Draft=="Keep this draft • नमस्ते","Back dismisses permission UI and preserves draft");
                        fake.done=false;app.ToggleRecording();break;
                    case 5:Click("permission-continue");break;
                    case 6:
                        app.SetApplicationSuspended(true);fake.granted=true;fake.done=true;app.SetApplicationSuspended(false);break;
                    case 7:
                        Check(!app.PermissionPanelOpen&&!app.IsRecording,"Late grant after background cannot reopen UI or start capture");
                        fake.granted=false;fake.done=false;app.ToggleRecording();break;
                    case 8:Click("permission-continue");break;
                    case 9:fake.granted=true;fake.done=true;break;
                    case 10:
                        Check(!app.PermissionPanelOpen&&!app.IsRecording&&app.State.Contains("tap the mic"),"Grant closes dialog and requires another recording gesture");
                        Check(root.Q("conversation-shell").enabledSelf&&app.Draft=="Keep this draft • नमस्ते","Grant restores chat with intact Unicode draft");
                        var audio=app.GetComponent<AudioSource>();var clip=AudioClip.Create("Silent interruption fixture",16000,1,16000,false);audio.clip=clip;audio.Play();
                        typeof(TalkingCharacter).GetField("speaking",Flags).SetValue(app,true);app.HandleAudioInterruption();
                        Check(!audio.isPlaying&&!app.IsSpeaking&&app.State.Contains("Audio interrupted"),"Audio interruption stops playback with recovery guidance");audio.clip=null;UnityEngine.Object.Destroy(clip);
                        string manifest="<manifest xmlns:android='http://schemas.android.com/apk/res/android'><application><activity android:name='Existing'/></application></manifest>";
                        string configured=MicrophonePermissionBuildPolicy.ConfigureAndroidManifest(manifest);
                        Check(configured.Contains("SkipPermissionsDialog")&&configured.Contains("RECORD_AUDIO")&&configured.Contains("Existing"),"Android manifest suppresses automatic prompt and preserves existing activity");
                        Check(MicrophonePermissionBuildPolicy.ConfigureAndroidManifest(configured)==configured,"Android manifest update is idempotent");
                        Finish(null);return;
                }
                next=EditorApplication.timeSinceStartup+1;
            }catch(Exception e){Finish(e.ToString());}
        }
        static void Finish(string error)
        {
            EditorApplication.update-=Tick;if(error!=null)lines.Add("FAIL "+error);
            app.SetApplicationSuspended(false);app.Interrupt();typeof(TalkingCharacter).GetField("microphonePermission",Flags).SetValue(app,originalPermission);
            doc.rootVisualElement.Q<TextField>("message-input").value=draft;doc.rootVisualElement.style.width=width;doc.rootVisualElement.style.height=height;doc.panelSettings=original;
            target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(panel);File.WriteAllLines(Path.Combine(Folder,"checks.txt"),lines);app=null;
        }
    }
}
