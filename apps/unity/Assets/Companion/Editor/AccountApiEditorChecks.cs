using System;
using System.Collections.Generic;
using System.IO;
using Companion.Presentation;
using UnityEditor;
using UnityEngine;
namespace Companion.Editor
{
    public static class AccountApiEditorChecks
    {
        static SyntheticHistoryView view;static HistoryFixture fixture;static int phase;static double deadline;
        static readonly List<string> lines=new List<string>();
        [MenuItem("Companion/Run Real Local Account API Checks")]
        public static void Run()
        {
            if(view!=null)throw new InvalidOperationException("Already running");
            fixture=HistoryFixture.Read();lines.Clear();view=new SyntheticHistoryView();
            view.Configure(fixture.endpoint,fixture.token,fixture.conversation);view.Connect();phase=0;deadline=EditorApplication.timeSinceStartup+30;
            Check(view.StatusText.Contains("Loading"),"history screen exposes loading state");EditorApplication.update+=Tick;
        }
        static void Check(bool ok,string text){if(!ok)throw new Exception(text);lines.Add("PASS "+text);}
        static void Tick()
        {
            try {
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Editor API check timed out: "+view.DiagnosticCode+" cursor="+view.Cursor);
                view.Tick();
                if(phase<2 && view.HasError)throw new Exception(view.DiagnosticCode);
                if(phase==0 && view.Cursor==2 && view.StatusText.StartsWith("Live")) {
                    Check(view.RenderedTurns==1 && view.RenderedText.Contains("cancelled"),"actual PostgreSQL API renders one canonical cancelled turn");
                    Check(view.RenderedText.Contains("नमस्ते") && view.RenderedText.Contains("🌼"),"actual API Unicode history reaches UI text");
                    Check(!view.RenderedText.Contains("Companion\n"),"cancelled turn does not fabricate assistant bubble");
                    view.Stop();Check(view.StatusText.Contains("stopped") && view.RenderedTurns==1,"Stop preserves visible history and ends listening");
                    view.Connect();phase=1;
                } else if(phase==1 && view.StatusText.StartsWith("Live")) {
                    Check(view.Cursor==2 && view.RenderedTurns==1,"Retry resumes durable cursor without duplicate UI rows");
                    view.Configure(fixture.endpoint,fixture.otherToken,fixture.conversation);Check(view.RenderedTurns==0,"account switch clears prior account history");
                    view.Connect();phase=2;
                } else if(phase==2 && view.HasError) {
                    Check(view.Cursor==0 && view.RenderedTurns==0 && view.StatusText.Contains("unavailable"),"other account is denied and UI offers recovery");
                    Finish(null);
                }
            } catch(Exception e){Finish(e.Message);}
        }
        static void Finish(string error)
        {
            EditorApplication.update-=Tick;view?.Dispose();view=null;
            if(error!=null)lines.Add("FAIL "+error);
            var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/evidence/m2/unity-account-api"));Directory.CreateDirectory(folder);File.WriteAllLines(Path.Combine(folder,"checks.txt"),lines);
            File.WriteAllText(Path.Combine(HistoryFixture.Folder,"result.json"),JsonUtility.ToJson(new Result {runId=fixture.runId,passed=error==null,checks=lines.Count}));
        }
        [Serializable] sealed class Result {public string runId;public bool passed;public int checks;}
    }
}
