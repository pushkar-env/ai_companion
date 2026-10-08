using System;
using System.IO;
using System.Collections.Generic;
using Companion.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Companion.Editor
{
    public static class SkinToneUiChecks
    {
        static TalkingCharacter app;static UIDocument doc;static PanelSettings original,panel;static RenderTexture target;static Texture2D image;
        static CompanionWardrobe.Look initialLook;
        static double next;static int step;static string saved,folder;static readonly List<string> checks=new List<string>();
        public static void Run()
        {
            if(!EditorApplication.isPlaying||app!=null)throw new Exception("Run in Play mode");
            app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>();app.SelectCharacter(0);doc=app.GetComponent<UIDocument>();original=doc.panelSettings;initialLook=app.CurrentLook;saved=PlayerPrefs.GetString(CompanionWardrobe.Preference,"");
            panel=UnityEngine.Object.Instantiate(original);panel.scaleMode=PanelScaleMode.ConstantPixelSize;panel.scale=1;
            target=new RenderTexture(390,844,24);target.Create();panel.targetTexture=target;doc.panelSettings=panel;doc.rootVisualElement.style.width=390;doc.rootVisualElement.style.height=844;
            image=new Texture2D(390,844,TextureFormat.RGB24,false);folder=Path.GetFullPath(Application.dataPath+"/../../../docs/evidence/skin-tones");Directory.CreateDirectory(folder);step=0;checks.Clear();next=EditorApplication.timeSinceStartup+2;EditorApplication.update+=Tick;
        }
        static void Check(bool value,string label){checks.Add((value?"PASS ":"FAIL ")+label);if(!value)throw new Exception(label);}
        static void Capture(string name){var old=RenderTexture.active;RenderTexture.active=target;image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();RenderTexture.active=old;File.WriteAllBytes(Path.Combine(folder,name+".png"),image.EncodeToPNG());}
        static void Submit(Button button){button.Focus();using(var evt=NavigationSubmitEvent.GetPooled()){button.SendEvent(evt);}}
        static void Tick()
        {
            EditorApplication.QueuePlayerLoopUpdate();if(EditorApplication.timeSinceStartup<next)return;
            try {
                var root=doc.rootVisualElement;
                switch(step++) {
                    case 0:app.OpenWardrobe();break;
                    case 1:
                        Check(app.WardrobeOpen,"Style opens wardrobe");
                        for(int i=0;i<6;i++)Check(root.Q<Button>("skin-tone-"+i).worldBound.height>=44,"Tone "+i+" has 44px touch height");
                        Submit(root.Q<Button>("skin-tone-4"));break;
                    case 2:
                        Check(app.CurrentLook.skinTone==4,"Swatch activation previews Brown");
                        Check(root.Q("skin-tone-4").ClassListContains("selected"),"Selected swatch updates");
                        Capture("style-skin-tone");Check(root.Q("save-look").worldBound.yMax<844,"Save visible in portrait");
                        Check(PlayerPrefs.GetString(CompanionWardrobe.Preference,"")==saved,"Preview leaves preferences unchanged");
                        app.CloseWardrobe(false);break;
                    case 3:
                        Check(JsonUtility.ToJson(app.CurrentLook)==JsonUtility.ToJson(initialLook),"Cancel restores complete prior look");
                        app.OpenWardrobe();Submit(root.Q<Button>("skin-tone-5"));break;
                    case 4:
                        app.CloseWardrobe(true);
                        Check(JsonUtility.FromJson<CompanionWardrobe.Look>(PlayerPrefs.GetString(CompanionWardrobe.Preference)).skinTone==5,"Save persists Deep");
                        var clone=UnityEngine.Object.Instantiate(app.character.gameObject);clone.SetActive(false);
                        using(var loaded=new CompanionWardrobe(clone.transform)){Check(loaded.Current.skinTone==5,"Fresh wardrobe reloads saved tone");}
                        UnityEngine.Object.Destroy(clone);
                        app.OpenWardrobe();Check(root.Q("skin-tone-5").ClassListContains("selected"),"Reopen highlights saved tone");
                        Submit(root.Q<Button>("reset-look"));break;
                    case 5:
                        Check(app.CurrentLook.skinTone==0,"Restore signature look resets skin");app.CloseWardrobe(false);
                        Check(app.CurrentLook.skinTone==5,"Cancel after reset restores saved skin");
                        target.Release();target.width=360;target.height=640;target.Create();UnityEngine.Object.DestroyImmediate(image);image=new Texture2D(360,640,TextureFormat.RGB24,false);doc.rootVisualElement.style.width=360;doc.rootVisualElement.style.height=640;app.OpenWardrobe();break;
                    case 6:
                        Check(root.Q("save-look").worldBound.yMax<640,"Save visible at 360x640");
                        for(int i=0;i<6;i++){var bounds=root.Q("skin-tone-"+i).worldBound;Check(bounds.xMin>=0&&bounds.xMax<=360&&bounds.yMax<640,"Compact swatch "+i+" stays within viewport");}
                        Capture("style-skin-compact");app.CloseWardrobe(false);break;
                    default:Finish(null);return;
                }
                next=EditorApplication.timeSinceStartup+2;
            }catch(Exception e){Finish(e.ToString());}
        }
        static void Finish(string error)
        {
            EditorApplication.update-=Tick;if(error!=null)checks.Add("FAIL "+error);File.WriteAllLines(Path.Combine(folder,"ui-checks.txt"),checks);
            if(saved.Length==0)PlayerPrefs.DeleteKey(CompanionWardrobe.Preference);else PlayerPrefs.SetString(CompanionWardrobe.Preference,saved);PlayerPrefs.Save();
            if(app!=null&&app.WardrobeOpen)app.CloseWardrobe(false);if(doc!=null){doc.rootVisualElement.style.width=StyleKeyword.Null;doc.rootVisualElement.style.height=Length.Percent(100);doc.panelSettings=original;}
            target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(panel);app=null;
        }
    }
}
