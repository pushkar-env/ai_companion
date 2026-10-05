using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Companion.Core;
using Companion.Presentation;
using UnityEditor;
using UnityEngine;
namespace Companion.Editor
{
    public static class AccountHistoryChecks
    {
        static readonly List<string> lines=new List<string>();
        static SyntheticHistoryTransport client;static IEnumerator routine;static HttpListener server;
        static Task work;static double deadline;static int phase;
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/evidence/m2/unity-history"));
        static void Check(bool ok,string label){if(!ok)throw new Exception(label);lines.Add("PASS "+label);}
        static string Frame(AccountEvent e)=>"id: "+e.sequence+"\nevent: "+e.type+"\ndata: "+JsonUtility.ToJson(e)+"\n\n";
        [MenuItem("Companion/Run Synthetic Account History Checks")]
        public static void Run()
        {
            if(client!=null)throw new InvalidOperationException("Check already running");
            lines.Clear();Directory.CreateDirectory(Folder);
            try {
                var accepted=new AccountEvent {event_id=Guid.NewGuid().ToString(),turn_id=Guid.NewGuid().ToString(),sequence=1,version=1,type="turn.accepted",text="Synthetic नमस्ते 🌼"};
                var completed=new AccountEvent {event_id=Guid.NewGuid().ToString(),turn_id=accepted.turn_id,sequence=2,version=2,type="turn.completed",text="[SYNTHETIC] Unity reply"};
                var history=new AccountHistory();var decoder=new AccountEventDecoder(JsonUtility.FromJson<AccountEvent>,e=>history.Apply(e));
                byte[] bytes=Encoding.UTF8.GetBytes(": comment\n\n"+Frame(accepted));
                foreach(byte b in bytes)decoder.Feed(new[]{b},1);
                Check(history.Cursor==1 && history.Snapshot[0].UserText==accepted.text,"Editor parser preserves byte-fragmented Hindi and emoji");
                var partial=Encoding.UTF8.GetBytes(Frame(completed).TrimEnd('\n'));decoder.Feed(partial,partial.Length);
                Check(history.Cursor==1,"partial terminal frame does not advance cursor");
                decoder=new AccountEventDecoder(JsonUtility.FromJson<AccountEvent>,e=>history.Apply(e));bytes=Encoding.UTF8.GetBytes(Frame(accepted)+Frame(completed));decoder.Feed(bytes,bytes.Length);
                Check(history.Cursor==2 && history.Snapshot.Length==1 && history.Snapshot[0].AssistantText==completed.text,"duplicate replay converges on one canonical reply");
                Check(!ReferenceEquals(history.Snapshot[0],history.Snapshot[0]),"history exposes detached snapshots");
                bool denied=false;try{new SyntheticHistoryTransport("http://example.invalid/",new string('a',64),Guid.NewGuid().ToString(),true);}catch(ArgumentException){denied=true;}
                Check(denied,"non-loopback transport configuration rejected");
                var socket=new TcpListener(IPAddress.Loopback,0);socket.Start();int port=((IPEndPoint)socket.LocalEndpoint).Port;socket.Stop();
                string root="http://127.0.0.1:"+port+"/",conv=Guid.NewGuid().ToString();
                string page="{\"conversation_id\":\""+conv+"\",\"mode\":\"local-synthetic-accounts\",\"items\":["+JsonUtility.ToJson(accepted)+"],\"next_cursor\":1,\"has_more\":false}";
                string terminalFrame=Frame(completed),admissionFrame=Frame(accepted);
                server=new HttpListener();server.Prefixes.Add(root);server.Start();
                work=Task.Run(async()=>{
                    for(int i=0;i<3;i++) {
                        var context=await server.GetContextAsync();
                        if(i>0 && context.Request.Headers["Last-Event-ID"]!="1")throw new Exception("Wrong reconnect cursor");
                        context.Response.ContentType=i==0?"application/json":"text/event-stream";
                        var payload=Encoding.UTF8.GetBytes(i==0?page:i==1?terminalFrame.TrimEnd('\n'):admissionFrame+terminalFrame);
                        await context.Response.OutputStream.WriteAsync(payload,0,payload.Length);context.Response.Close();
                    }
                });
                client=new SyntheticHistoryTransport(root,new string('a',64),conv,true);
                routine=client.Load();phase=0;deadline=EditorApplication.timeSinceStartup+20;EditorApplication.update+=Tick;
            } catch(Exception e){Finish(e.Message);}
        }
        static void Tick()
        {
            try {
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Editor socket check timed out");
                if(work.IsFaulted)throw new Exception("Loopback server validation failed");
                if(client.Error!=null)throw new Exception(client.Error);
                bool running=routine.MoveNext();
                if(phase==0 && !running) {
                    Check(client.State=="ready" && client.History.Cursor==1,"UnityWebRequest hydrates synthetic history in stopped Editor");
                    routine=client.Follow();phase=1;
                } else if(phase==1 && client.History.Cursor==2) {
                    Check(client.History.Snapshot.Length==1 && client.History.Snapshot[0].State=="completed","UnityWebRequest reconnect recovers truncated frame and suppresses duplicates");
                    client.Stop();while(routine.MoveNext()){}
                    Check(client.State=="stopped","Stop aborts stream and prevents future reconnect");
                    Check(work.IsCompleted && !work.IsFaulted,"actual socket requests resume with last applied cursor");
                    Finish(null);
                }
            } catch(Exception e){Finish(e.Message);}
        }
        static void Finish(string error)
        {
            EditorApplication.update-=Tick;client?.Stop();(routine as IDisposable)?.Dispose();client?.Dispose();client=null;routine=null;
            server?.Stop();server?.Close();server=null;
            if(error!=null)lines.Add("FAIL "+error);
            File.WriteAllLines(Path.Combine(Folder,"checks.txt"),lines);
        }
    }
}
