using System;
using System.Collections;
using System.Globalization;
using Companion.Core;
using UnityEngine;
using UnityEngine.Networking;
namespace Companion.Presentation
{
    // Explicit synthetic loopback adapter. Never attaches to the talking scene automatically.
    public sealed class SyntheticHistoryTransport : IDisposable
    {
        readonly string endpoint,token,conversation;UnityWebRequest request;bool busy,stopped;
        public AccountHistory History {get;}=new AccountHistory();
        public string State {get;private set;}="idle";
        public string Error {get;private set;}
        public bool RequiresSessionReload=>Error=="history_http_401" || Error=="stream_http_401" || Error=="history_http_403" || Error=="stream_http_403";
        public bool CanRetry=>!RequiresSessionReload && Error!="history_http_404" && Error!="stream_http_404" && Error!="history_http_400" && Error!="stream_http_400" && Error!="invalid_history" && Error!="invalid_stream" && Error!="invalid_stream_type";
        public SyntheticHistoryTransport(string endpoint,string token,string conversation,bool syntheticOnly)
        {
            Uri uri;Guid id;
            if(!syntheticOnly || (!Application.isEditor && !Debug.isDebugBuild) || !Uri.TryCreate(endpoint,UriKind.Absolute,out uri)
                || uri.Scheme!="http" || uri.Host!="127.0.0.1" || uri.AbsolutePath!="/" || uri.UserInfo!="" || uri.Query!="" || uri.Fragment!=""
                || !Guid.TryParse(conversation,out id) || id==Guid.Empty || token==null || token.Length!=64)
                throw new ArgumentException("Explicit loopback synthetic configuration required");
            foreach(char c in token)if(!Uri.IsHexDigit(c))throw new ArgumentException("Invalid synthetic credential");
            this.endpoint=uri.AbsoluteUri;this.token=token;this.conversation=id.ToString();
        }
        UnityWebRequest Create(string suffix)
        {
            var value=UnityWebRequest.Get(endpoint+"local/v1/conversations/"+conversation+"/"+suffix);
            value.SetRequestHeader("Authorization","Bearer "+token);value.redirectLimit=0;value.timeout=40;return value;
        }
        public IEnumerator Load()
        {
            if(busy)throw new InvalidOperationException("History operation already active");busy=true;stopped=false;Error=null;State="loading";
            try {
                while(!stopped) {
                    request=Create("events?after="+History.Cursor.ToString(CultureInfo.InvariantCulture)+"&limit=50");
                    var operation=request.SendWebRequest();while(!operation.isDone && !stopped)yield return null;
                    if(stopped)yield break;
                    if(request.result!=UnityWebRequest.Result.Success){Fail("history_http_"+request.responseCode);yield break;}
                    Page page=null;
                    try {
                        page=JsonUtility.FromJson<Page>(request.downloadHandler.text);
                        if(page==null || page.conversation_id!=conversation || page.mode!="local-synthetic-accounts" || page.items==null)throw new Exception();
                        foreach(var item in page.items)History.Apply(Normalize(item));
                        if(page.next_cursor!=History.Cursor || (page.has_more && page.items.Length==0))throw new Exception();
                    } catch {Fail("invalid_history");}
                    if(Error!=null)yield break;
                    Release();if(!page.has_more){State="ready";yield break;}
                }
            } finally {Release();busy=false;if(stopped)State="stopped";}
        }
        public IEnumerator Follow()
        {
            if(busy)throw new InvalidOperationException("History operation already active");busy=true;stopped=false;Error=null;
            try {
                for(int attempt=0;attempt<4 && !stopped;attempt++) {
                    State=attempt==0?"connecting":"reconnecting";
                    var handler=new Receiver(new AccountEventDecoder(ParseEvent,e=>History.Apply(e)));
                    request=Create("stream");request.downloadHandler.Dispose();request.downloadHandler=handler;
                    request.SetRequestHeader("Last-Event-ID",History.Cursor.ToString(CultureInfo.InvariantCulture));
                    var operation=request.SendWebRequest();
                    while(!operation.isDone && !stopped){if(handler.Received && request.responseCode==200)State="connected";yield return null;}
                    if(stopped)yield break;
                    if(handler.Invalid){Fail("invalid_stream");yield break;}
                    long code=request.responseCode;
                    if(code!=0 && code!=200 && code!=429 && code!=503){Fail("stream_http_"+code);yield break;}
                    if(code==200 && request.GetResponseHeader("Content-Type")!="text/event-stream"){Fail("invalid_stream_type");yield break;}
                    Release();
                    if(attempt==3){Fail("reconnect_limit");yield break;}
                    State="reconnecting";double until=Time.realtimeSinceStartupAsDouble+(1<<attempt)+UnityEngine.Random.Range(0f,.25f);
                    while(!stopped && Time.realtimeSinceStartupAsDouble<until)yield return null;
                }
            } finally {Release();busy=false;if(stopped)State="stopped";}
        }
        // JsonUtility maps JSON null strings to empty strings; terminal absence stays canonical.
        public static AccountEvent ParseEvent(string json)=>Normalize(JsonUtility.FromJson<AccountEvent>(json));
        static AccountEvent Normalize(AccountEvent item)
        {
            if(item!=null && (item.type=="turn.cancelled" || item.type=="turn.failed") && item.text==string.Empty)item.text=null;
            return item;
        }
        public void Stop(){stopped=true;request?.Abort();State="stopped";}
        public void Dispose(){Stop();Release();}
        void Release(){request?.Dispose();request=null;}
        void Fail(string code){Error=code;State="error";}
        [Serializable] sealed class Page {public string conversation_id,mode;public AccountEvent[] items;public long next_cursor;public bool has_more;}
        sealed class Receiver:DownloadHandlerScript
        {
            readonly AccountEventDecoder decoder;public bool Invalid,Received;
            public Receiver(AccountEventDecoder decoder):base(new byte[4096]){this.decoder=decoder;}
            protected override bool ReceiveData(byte[] bytes,int length){try{Received=true;decoder.Feed(bytes,length);return true;}catch{Invalid=true;return false;}}
        }
    }
}
