using System;
using System.IO;
using System.Collections.Generic;
using Companion.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Companion.Editor
{
    public static class WardrobeUiChecks
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
            image=new Texture2D(390,844,TextureFormat.RGB24,false);folder=Path.GetFullPath(Application.dataPath+"/../../../docs/evidence/wardrobe");Directory.CreateDirectory(folder);step=0;checks.Clear();next=EditorApplication.timeSinceStartup+2;EditorApplication.update+=Tick;
        }
        static void Check(bool value,string label){checks.Add((value?"PASS ":"FAIL ")+label);if(!value)throw new Exception(label);}
        static void Capture(string name){var old=RenderTexture.active;RenderTexture.active=target;image.ReadPixels(new Rect(0,0,390,844),0,0);image.Apply();RenderTexture.active=old;File.WriteAllBytes(Path.Combine(folder,name+".png"),image.EncodeToPNG());}
        static void Tick()
        {
            EditorApplication.QueuePlayerLoopUpdate();if(EditorApplication.timeSinceStartup<next)return;
            try {
                var root=doc.rootVisualElement;
                switch(step++) {
                    case 0:app.OpenWardrobe();break;
                    case 1:Check(app.WardrobeOpen,"Style opens wardrobe");root.Q<DropdownField>("outfit").index=1;root.Q<DropdownField>("top").index=0;root.Q<DropdownField>("bottom").index=0;root.Q<DropdownField>("top-color").index=1;break;
                    case 2:Capture("wardrobe-ui");Check(root.Q("save-look").worldBound.yMax<844,"Save remains visible in portrait");Check(PlayerPrefs.GetString(CompanionWardrobe.Preference,"")==saved,"Preview does not save preferences");app.OpenWardrobe();app.CloseWardrobe(false);break;
                    case 3:Check(!app.WardrobeOpen,"Cancel closes preview");Check(JsonUtility.ToJson(app.CurrentLook)==JsonUtility.ToJson(initialLook),"Cancel restores complete prior look");Capture("idle-live");app.OpenWardrobe();root.Q<DropdownField>("outfit").index=1;root.Q<DropdownField>("top").index=1;root.Q<DropdownField>("bottom").index=1;app.CloseWardrobe(true);break;
                    case 4:Check(PlayerPrefs.GetString(CompanionWardrobe.Preference,"").Contains("\"outfit\":1"),"Save persists local loadout");Capture("saved-outfit");app.OpenWardrobe();Check(root.Q<DropdownField>("top").index==1&&root.Q<DropdownField>("bottom").index==1,"Reopen retains separate garment choices");app.CloseWardrobe(false);doc.rootVisualElement.style.width=360;doc.rootVisualElement.style.height=640;app.OpenWardrobe();break;
                    case 5:Check(root.Q("save-look").worldBound.yMax<640,"Save remains visible at 360x640");Capture("wardrobe-compact");app.CloseWardrobe(false);break;
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
