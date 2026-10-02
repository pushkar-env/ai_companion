using System;
using System.Collections.Generic;
using System.IO;
using Companion.Core;
using Companion.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Companion.Editor
{
    public static class PortraitLayoutChecks
    {
        static readonly Vector2Int[] sizes={new Vector2Int(360,640),new Vector2Int(390,844),new Vector2Int(1080,1920)};
        static readonly List<string> evidence=new List<string>();
        static MockCompanionApp app;
        static int phase;
        static double ready;
        [MenuItem("Companion/Run Portrait Layout Checks")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play Mode in the mock scene first.");
            app=UnityEngine.Object.FindAnyObjectByType<MockCompanionApp>();
            if(app==null)throw new InvalidOperationException("Mock app missing");
            evidence.Clear();phase=0;SetPhase();EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        }
        static void SetPhase()
        {
            var size=sizes[Math.Min(phase,2)];if(phase>=3)size=sizes[1];
            PortraitPreview.SetSize(size.x,size.y);app.SimulateInsets(phase>=3?new Vector4(0,44,0,34):Vector4.zero,phase==4?260:0);
            app.ClearSession();app.SendMessage("Can we plan a relaxing evening?",MockScenario.Normal);
            if(phase==5) {
                app.CancelResponse();app.ClearSession();
                var root=app.GetComponent<UIDocument>().rootVisualElement;
                root.Q<Toggle>("larger-text").value=true;
                app.SendMessage(string.Concat(System.Linq.Enumerable.Repeat("A longer conversation for portrait scrolling. ",100)),MockScenario.Normal);
                root.Q<TextField>("message-input").value="A multiline draft\nremains editable\ninside the bounded composer.";
            }
            ready=EditorApplication.timeSinceStartup+6;
        }
        static void Require(bool condition,string description) {if(!condition)throw new Exception(description);evidence.Add("PASS "+description);}
        static void Tick()
        {
            if(EditorApplication.timeSinceStartup<ready)return;
            try
            {
                if(!EditorApplication.isPlaying||app==null)throw new Exception("Play mode interrupted");
                var root=app.GetComponent<UIDocument>().rootVisualElement;
                var bounds=root.worldBound;
                float top=phase>=3?44:0, bottom=phase==4?260:phase>=3?34:0;
                foreach(var name in new[]{"avatar-card","character-portrait","message-input","send","cancel","retry","replay","demo-controls"})
                {
                    var element=root.Q<VisualElement>(name);var b=element.worldBound;
                    Require(element.resolvedStyle.display!=DisplayStyle.None && b.width>0 && b.height>0 && b.xMin>=bounds.xMin-.5f && b.xMax<=bounds.xMax+.5f && b.yMin>=top-.5f && b.yMax<=bounds.yMax-bottom+.5f,"viewport "+Screen.width+"x"+Screen.height+" phase "+phase+" contains "+name);
                }
                Require(root.Q<ScrollView>("transcript").worldBound.height>24,"phase "+phase+" retains scrollable chat viewport");
                Require(app.Session.Status==ChatStatus.Completed,"phase "+phase+" stream completes in portrait");
                if(phase==0) {
                    app.SetSettingsVisible(true);
                    ready=EditorApplication.timeSinceStartup+1;
                    EditorApplication.update-=Tick;EditorApplication.update+=CheckSettings;return;
                }
                CaptureAndAdvance();
            }
            catch(Exception e){Finish(e.Message);}
        }
        static void CheckSettings()
        {
            if(EditorApplication.timeSinceStartup<ready)return;
            EditorApplication.update-=CheckSettings;
            try {
                var root=app.GetComponent<UIDocument>().rootVisualElement;var b=root.Q<VisualElement>("demo-settings").worldBound;
                Require(b.yMin>=0&&b.yMax<=root.worldBound.yMax,"small phone demo settings fit viewport");
                app.SetSettingsVisible(false);CaptureAndAdvance();
            }catch(Exception e){Finish(e.Message);}
        }
        static void CaptureAndAdvance()
        {
            string dir=Path.GetFullPath("../../docs/evidence/portrait");Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir,"layout-"+phase+".png"));
            phase++;
            // Defer resize until the current screenshot has reached the end of frame.
            if(phase>=6){Finish(null);return;}
            ready=EditorApplication.timeSinceStartup+1;EditorApplication.update-=Tick;EditorApplication.update+=Next;
        }
        static void Next(){if(EditorApplication.timeSinceStartup<ready)return;EditorApplication.update-=Next;SetPhase();EditorApplication.update+=Tick;}
        static void Finish(string error)
        {
            EditorApplication.update-=Tick;EditorApplication.update-=Next;EditorApplication.update-=CheckSettings;
            if(error!=null)evidence.Add("FAIL "+error);
            var dir=Path.GetFullPath("../../docs/evidence/portrait");Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir,"layout-checks.txt"),"UTC "+DateTime.UtcNow.ToString("O")+"\nUnity "+Application.unityVersion+"; Editor simulated safe area/keyboard only\n"+string.Join("\n",evidence)+"\n");
            if(error==null)Debug.Log("Portrait layout checks passed");else Debug.LogError("Portrait layout checks failed: "+error);
        }
    }
}
