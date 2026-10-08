using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Companion.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Companion.Editor
{
    public static class ConversationCommandChecks
    {
        static IEnumerator routine;static double deadline;static HistoryFixture fixture;
        static SyntheticConversationCommands commands,denied;static SyntheticHistoryTransport history;
        static UnityWebRequest lost;static readonly List<string> lines=new List<string>();
        [MenuItem("Companion/Run Durable Conversation Command Checks")]
        public static void Run()
        {
            if(routine!=null)throw new InvalidOperationException("Already running");
            fixture=HistoryFixture.Read();lines.Clear();deadline=EditorApplication.timeSinceStartup+60;
            routine=Steps();EditorApplication.update+=Tick;
        }
        static void Check(bool value,string label){if(!value)throw new Exception(label);lines.Add("PASS "+label);}
        static IEnumerator Steps()
        {
            commands=new SyntheticConversationCommands(fixture.endpoint,fixture.token,fixture.runtimeConversation,true);
            commands.Prepare("[SYNTHETIC] Durable retry नमस्ते 🌼");
            string key=commands.IdempotencyKey,clientId=commands.ClientMessageId;
            bool blocked=false;try{commands.Prepare("Must not replace pending text");}catch(InvalidOperationException){blocked=true;}
            Check(blocked,"Pending command cannot be overwritten");
            // Commit through the actual API but deliberately never deliver its receipt
            // to the adapter, modeling lost acknowledgement/client interruption.
            lost=new UnityWebRequest(fixture.endpoint+"local/v1/conversations/"+fixture.runtimeConversation+"/admissions","POST");
            lost.redirectLimit=0;lost.timeout=10;lost.downloadHandler=new DownloadHandlerBuffer();
            lost.SetRequestHeader("Authorization","Bearer "+fixture.token);lost.SetRequestHeader("Idempotency-Key",key);
            lost.SetRequestHeader("Content-Type","application/json");
            lost.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Admission {client_message_id=clientId,text="[SYNTHETIC] Durable retry नमस्ते 🌼"})));
            var op=lost.SendWebRequest();while(!op.isDone)yield return null;
            Check(lost.responseCode==202,"Actual API accepts first submission before receipt loss");lost.Dispose();lost=null;
            var work=commands.Submit();while(work.MoveNext())yield return null;
            Check(commands.Error==null&&commands.Status=="accepted"&&commands.Version==1,"Retry reconciles durable accepted turn after lost receipt");
            string turn=commands.TurnId;
            Check(commands.IdempotencyKey==key&&commands.ClientMessageId==clientId,"Retry retains both request identities");
            work=commands.Submit();while(work.MoveNext())yield return null;
            Check(commands.TurnId==turn&&commands.Status=="accepted","Repeated submit converges on same turn");
            commands.Stop();
            Check(commands.Status=="accepted","Local Stop does not claim server cancellation");
            work=commands.Cancel();while(work.MoveNext())yield return null;
            Check(commands.Error==null&&commands.Status=="cancelled"&&commands.Version==2,"Versioned cancellation reconciles actual terminal state");
            work=commands.Cancel();while(work.MoveNext())yield return null;
            Check(commands.Status=="cancelled"&&commands.TurnId==turn,"Repeated cancellation remains canonical");
            work=commands.Submit();while(work.MoveNext())yield return null;
            Check(commands.Status=="cancelled","Retry cannot resurrect cancelled turn");
            history=new SyntheticHistoryTransport(fixture.endpoint,fixture.token,fixture.runtimeConversation,true);
            work=history.Load();while(work.MoveNext())yield return null;
            var turns=history.History.Snapshot;
            Check(history.Error==null&&turns.Length==7,"Durable history contains one new turn after repeated retries");
            int matches=0;foreach(var t in turns)if(t.Id==turn&&t.State=="cancelled"&&t.UserText.Contains("नमस्ते")&&t.AssistantText==null)matches++;
            Check(matches==1,"Unicode canonical history has no fabricated assistant response");
            denied=new SyntheticConversationCommands(fixture.endpoint,fixture.otherToken,fixture.runtimeConversation,true);denied.Prepare("[SYNTHETIC] unauthorized");
            work=denied.Submit();while(work.MoveNext())yield return null;
            Check(denied.Error=="command_http_404"&&denied.TurnId==null,"Other account cannot admit into this conversation");
            commands.ClearTerminal();Check(!commands.HasPending&&commands.TurnId==null,"Only reconciled terminal work can be cleared");
            denied.Dispose();denied=new SyntheticConversationCommands(fixture.endpoint,new string('0',64),fixture.runtimeConversation,true);
            denied.Prepare("[SYNTHETIC] invalid session");work=denied.Submit();while(work.MoveNext())yield return null;
            Check(denied.Error=="command_http_401"&&denied.RequiresSessionReload,"Rejected session requires credential reload");
            denied.Stop();blocked=false;try{denied.Submit().MoveNext();}catch(InvalidOperationException){blocked=true;}
            Check(blocked,"Stop cannot bypass rejected-session retry gate");
            blocked=false;try{denied.Prepare("Cannot bypass auth");}catch(InvalidOperationException){blocked=true;}
            Check(blocked,"New draft cannot bypass rejected-session gate");
        }
        static void Tick()
        {
            try {if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Command checks timed out");if(!routine.MoveNext())Finish(null);}
            catch(Exception e){Finish(e.Message);}
        }
        static void Finish(string error)
        {
            EditorApplication.update-=Tick;(routine as IDisposable)?.Dispose();routine=null;
            commands?.Dispose();denied?.Dispose();history?.Dispose();lost?.Dispose();lost=null;
            if(error!=null)lines.Add("FAIL "+error);
            string folder=Path.GetFullPath(Application.dataPath+"/../../../docs/evidence/production/p02");Directory.CreateDirectory(folder);
            File.WriteAllLines(Path.Combine(folder,"editor-checks.txt"),lines);
            File.WriteAllText(Path.Combine(HistoryFixture.Folder,"command-result.json"),JsonUtility.ToJson(new Result {runId=fixture.runId,passed=error==null,checks=lines.Count}));
        }
        [Serializable] sealed class Admission {public string client_message_id,text;}
        [Serializable] sealed class Result {public string runId;public bool passed;public int checks;}
    }
}
