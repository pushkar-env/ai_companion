using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Companion.Core;
using Companion.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
namespace Companion.Editor
{
    public static class HistoryRecoveryChecks
    {
        static GameObject host;static PanelSettings panel;static RenderTexture texture;static Scene scene;
        static SyntheticHistoryView view;static HttpListener listener;static CancellationTokenSource cancellation;static Task work;
        static string endpoint,conversation;static int requests,phase;static bool sawReconnect;static double deadline,ready;
        static readonly List<string> lines=new List<string>();
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/evidence/m2/history-recovery"));
        static string Frame(AccountEvent e)=>"id: "+e.sequence+"\nevent: "+e.type+"\ndata: "+JsonUtility.ToJson(e)+"\n\n";
        [MenuItem("Companion/Run Runtime History Recovery Checks")]
        public static void Run()
        {
            if(host!=null || !EditorApplication.isPlaying)throw new InvalidOperationException("Run once in Play mode");
            lines.Clear();requests=0;phase=0;sawReconnect=false;Directory.CreateDirectory(Folder);
            try {
                var socket=new TcpListener(IPAddress.Loopback,0);socket.Start();int port=((IPEndPoint)socket.LocalEndpoint).Port;socket.Stop();
                endpoint="http://127.0.0.1:"+port+"/";conversation=Guid.NewGuid().ToString();
                var accepted=new AccountEvent {event_id=Guid.NewGuid().ToString(),turn_id=Guid.NewGuid().ToString(),sequence=1,version=1,type="turn.accepted",text="[SYNTHETIC] A short recovery check."};
                var completed=new AccountEvent {event_id=Guid.NewGuid().ToString(),turn_id=accepted.turn_id,sequence=2,version=2,type="turn.completed",text="[SYNTHETIC] Your reply survived reconnect."};
                string a=Frame(accepted),b=Frame(completed);
                string prefix="{\"conversation_id\":\""+conversation+"\",\"mode\":\"local-synthetic-accounts\",\"items\":[";
                string first=prefix+JsonUtility.ToJson(accepted)+"],\"next_cursor\":1,\"has_more\":false}";
                string full=prefix+JsonUtility.ToJson(accepted)+","+JsonUtility.ToJson(completed)+"],\"next_cursor\":2,\"has_more\":false}";
                listener=new HttpListener();listener.Prefixes.Add(endpoint);listener.Start();cancellation=new CancellationTokenSource();var token=cancellation.Token;
                work=Task.Run(async()=>{
                    try {
                        while(!token.IsCancellationRequested) {
                            var context=await listener.GetContextAsync();int n=Interlocked.Increment(ref requests);
                            if(n==2 || n==3) {if(context.Request.Headers["Last-Event-ID"]!="1")throw new Exception("Wrong replay cursor");}
                            if(n==4 || n==6) {if(context.Request.Headers["Last-Event-ID"]!="2")throw new Exception("Wrong terminal cursor");}
                            string body;
                            if(n==1 || n==5 || n==7) {context.Response.ContentType="application/json";body=n==5?full:first;}
                            else if(n==4) {context.Response.StatusCode=401;context.Response.ContentType="application/json";body="{\"code\":\"unauthenticated\"}";}
                            else if(n>=8) {context.Response.StatusCode=503;context.Response.ContentType="application/json";body="{\"code\":\"storage_unavailable\"}";}
                            else {context.Response.ContentType="text/event-stream";body=n==2?b.TrimEnd('\n'):n==3?a+b:": heartbeat\n\n";}
                            byte[] bytes=Encoding.UTF8.GetBytes(body);await context.Response.OutputStream.WriteAsync(bytes,0,bytes.Length);await context.Response.OutputStream.FlushAsync();
                            if(n==3 || n==6)await Task.Delay(1500,token);
                            context.Response.Close();
                        }
                    } catch(Exception) when(token.IsCancellationRequested) { }
                });
                scene=SceneManager.CreateScene("History Recovery Offscreen Check");host=new GameObject("History recovery"){hideFlags=HideFlags.HideAndDontSave};SceneManager.MoveGameObjectToScene(host,scene);
                panel=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Companion/Settings/MockPanel.asset"));panel.hideFlags=HideFlags.HideAndDontSave;panel.scaleMode=PanelScaleMode.ConstantPixelSize;panel.scale=1;
                texture=new RenderTexture(360,640,0);texture.Create();panel.targetTexture=texture;var doc=host.AddComponent<UIDocument>();doc.panelSettings=panel;
                doc.rootVisualElement.style.width=360;doc.rootVisualElement.style.height=640;
                view=new SyntheticHistoryView();doc.rootVisualElement.Add(view);view.Configure(endpoint,new string('a',64),conversation);view.Connect();
                deadline=EditorApplication.timeSinceStartup+50;EditorApplication.update+=Tick;
            }catch(Exception e){Finish(e.Message);}
        }
        static void Check(bool ok,string text){if(!ok)throw new Exception(text);lines.Add("PASS "+text);}
        static void Tick()
        {
            try {
                if(work.IsFaulted)throw new Exception("Socket fixture validation failed");
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Recovery timed out: "+view.DiagnosticCode);
                view.Tick();if(view.StatusText.Contains("Reconnecting"))sawReconnect=true;
                if(phase==0 && view.HasError) {
                    Check(sawReconnect,"runtime screen exposes reconnection after interrupted stream");
                    Check(view.Cursor==2 && view.RenderedTurns==1 && view.RenderedText.Contains("survived reconnect"),"interrupted reply replays once without duplicate rows");
                    Check(view.DiagnosticCode=="stream_http_401" && view.StatusText.Contains("Session expired"),"401 shows session renewal guidance");
                    Check(!view.Q<Button>("history-connect").enabledSelf,"expired session disables same-credential Retry");
                    view.Connect();view.Stop();Check(!view.Q<Button>("history-connect").enabledSelf,"Connect and Stop cannot bypass expired-session gate");
                    // Allow one rendered frame before capturing the terminal recovery state.
                    phase=1;ready=EditorApplication.timeSinceStartup+2;
                } else if(phase==1 && EditorApplication.timeSinceStartup>=ready) {
                    Check(requests==4,"expired session issues no automatic or user retry request");Capture("expired");
                    view.Configure(endpoint,new string('c',64),conversation);Check(view.Cursor==0 && view.RenderedTurns==0,"fresh session clears prior projection before rehydration");view.Connect();phase=2;
                } else if(phase==2 && view.StatusText.StartsWith("Live")) {
                    Check(view.Cursor==2 && view.RenderedTurns==1,"fresh session restores canonical history and listening");
                    view.Stop();Check(view.StatusText.Contains("stopped"),"Stop ends renewed listening");
                    view.Configure(endpoint,new string('d',64),conversation);view.Connect();phase=3;
                } else if(phase==3 && view.HasError) {
                    Check(view.DiagnosticCode=="reconnect_limit" && requests==11,"four failed stream attempts exhaust bounded retries");
                    Check(view.StatusText.Contains("Connection unavailable") && view.Q<Button>("history-connect").enabledSelf,"transient exhaustion offers explicit Retry");
                    Check(view.Cursor==1 && view.RenderedTurns==1,"temporary outage retains already loaded history");
                    phase=4;ready=EditorApplication.timeSinceStartup+1;
                } else if(phase==4 && EditorApplication.timeSinceStartup>=ready){Capture("unavailable");Finish(null);}
            }catch(Exception e){Finish(e.Message);}
        }
        static void Capture(string name)
        {
            var previous=RenderTexture.active;RenderTexture.active=texture;var image=new Texture2D(360,640,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,360,640),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Folder,name+".png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=previous;
        }
        static void Finish(string error)
        {
            EditorApplication.update-=Tick;view?.Dispose();view=null;cancellation?.Cancel();listener?.Stop();listener?.Close();
            if(host!=null)UnityEngine.Object.DestroyImmediate(host);host=null;if(scene.IsValid())SceneManager.UnloadSceneAsync(scene);
            if(panel!=null)UnityEngine.Object.DestroyImmediate(panel);if(texture!=null){texture.Release();UnityEngine.Object.DestroyImmediate(texture);}
            if(error!=null)lines.Add("FAIL "+error);File.WriteAllLines(Path.Combine(Folder,"checks.txt"),lines);
        }
    }
}
