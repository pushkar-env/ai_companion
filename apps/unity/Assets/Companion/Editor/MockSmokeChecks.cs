using System;
using System.Collections.Generic;
using System.IO;
using Companion.Core;
using Companion.Presentation;
using UnityEditor;
using UnityEngine;

namespace Companion.Editor
{
    public static class MockSmokeChecks
    {
        static MockCompanionApp app;
        static int step;
        static double deadline;
        static readonly List<string> evidence=new List<string>();
        [MenuItem("Companion/Run M0 Play Mode Checks")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying) throw new InvalidOperationException("Open MockCompanion scene and enter Play Mode first.");
            app=UnityEngine.Object.FindAnyObjectByType<MockCompanionApp>();
            if(app==null||!app.UiReady) throw new InvalidOperationException("M0 UI not ready.");
            evidence.Clear(); step=0; deadline=EditorApplication.timeSinceStartup+15;
            app.ClearSession(); app.SendMessage("Can we plan a relaxing evening?",MockScenario.Normal);
            Check(app.Session.Status==ChatStatus.Queued,"UNITY-M0-01 queued/loading visible");
            EditorApplication.update-=Advance; EditorApplication.update+=Advance;
        }
        static void Check(bool value,string name) {if(!value)throw new Exception(name);evidence.Add("PASS "+name);}
        static void Advance()
        {
            try
            {
                if(!EditorApplication.isPlaying||app==null)throw new Exception("Play Mode ended during checks");
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Timed out at step "+step);
                if(step==0 && app.Session.Status==ChatStatus.Streaming) {Check(app.ResponseText.Contains("MOCK COMPANION"),"UNITY-M0-02 stream rendered");step=1;}
                if(step==1 && app.Session.Status==ChatStatus.Completed) {
                    Check(app.ResponseText.Contains("quiet evening"),"UNITY-M0-03 normal completion");
                    app.SendMessage("test",MockScenario.Slow); app.CancelResponse();
                    Check(app.Session.Status==ChatStatus.Cancelled,"UNITY-M0-04 cancellation during loading");
                    app.RetryResponse();step=2;deadline=EditorApplication.timeSinceStartup+15;
                }
                if(step==2 && app.Session.Status==ChatStatus.Completed) {
                    Check(app.Session.Attempt==1,"UNITY-M0-05 explicit retry completes");
                    app.SendMessage("test",MockScenario.FailBefore);step=3;deadline=EditorApplication.timeSinceStartup+15;
                }
                if(step==3 && app.Session.Status==ChatStatus.Failed) {
                    Check(app.Session.Text.Length==0&&app.StatusText.Contains("Error"),"UNITY-M0-06 failure before output");
                    app.SendMessage("test",MockScenario.FailMidway);step=4;deadline=EditorApplication.timeSinceStartup+15;
                }
                if(step==4 && app.Session.Status==ChatStatus.Failed) {
                    Check(app.Session.Text.Length>0&&app.ResponseText.Contains("Incomplete"),"UNITY-M0-07 partial failure labeled");
                    app.RetryResponse();step=5;deadline=EditorApplication.timeSinceStartup+15;
                }
                if(step==5 && app.Session.Status==ChatStatus.Completed) {
                    Check(app.ResponseText.Contains("scripted demo"),"UNITY-M0-08 recovery completes");
                    app.ReplayResponse();step=6;deadline=EditorApplication.timeSinceStartup+15;
                }
                if(step==6 && app.Session.Status==ChatStatus.Streaming) {
                    app.CancelResponse(); var text=app.Session.Text;
                    for(int i=0;i<100;i++)app.Session.Tick();
                    Check(app.Session.Status==ChatStatus.Cancelled&&app.Session.Text==text,"UNITY-M0-09 cancelled stream stays terminal");
                    app.ClearSession();Check(app.Session.Text==""&&app.Session.Status==ChatStatus.Draft,"UNITY-M0-10 clear resets session");
                    app.SendMessage("Can we plan a relaxing evening?",MockScenario.Normal);step=7;deadline=EditorApplication.timeSinceStartup+15;
                }
                if(step==7 && app.Session.Status==ChatStatus.Completed) {
                    Check(app.character!=null,"UNITY-M0-11 placeholder character exists");Finish(true);ScreenCapture.CaptureScreenshot(Path.GetFullPath("../../docs/evidence/m0-complete.png"));
                }
            }
            catch(Exception e) {evidence.Add("FAIL "+e.Message);Finish(false);}
        }
        static void Finish(bool passed)
        {
            EditorApplication.update-=Advance;
            string dir=Path.GetFullPath("../../docs/evidence");Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir,"unity-smoke.txt"),"UTC "+DateTime.UtcNow.ToString("O")+"\nUnity "+Application.unityVersion+"\n"+string.Join("\n",evidence)+"\n");
            if(passed)Debug.Log("M0 Play Mode checks passed: "+evidence.Count);else Debug.LogError("M0 Play Mode checks failed; see docs/evidence/unity-smoke.txt");
        }
    }
}
