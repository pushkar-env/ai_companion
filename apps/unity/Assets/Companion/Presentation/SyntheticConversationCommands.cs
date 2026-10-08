using System;
using System.Collections;
using System.Text;
using Companion.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Companion.Presentation
{
    // One immutable synthetic account/conversation scope. Dispose on account switch.
    // Stop aborts transport, not the durable server turn. Cancel is an explicit command.
    public sealed class SyntheticConversationCommands : IDisposable
    {
        readonly string origin,token,conversation;
        UnityWebRequest request;
        bool busy,stopped,disposed,authFailed;
        string message,key,clientId;
        public string TurnId {get;private set;}
        public string Status {get;private set;}="draft";
        public long Version {get;private set;}
        public string Error {get;private set;}
        public string ClientMessageId=>clientId;
        public string IdempotencyKey=>key;
        public bool Busy=>busy;
        public bool RequiresSessionReload=>authFailed;
        public bool HasPending=>message!=null;

        public SyntheticConversationCommands(string endpoint,string bearer,string conversationId,bool syntheticOnly)
        {
            if(!syntheticOnly||!Application.isEditor||
                !ConversationConnection.TryLocalEditor(true,endpoint,bearer,out _)||
                !Guid.TryParse(conversationId,out var id)||id==Guid.Empty)
                throw new ArgumentException("Explicit Editor synthetic account configuration required");
            origin=endpoint.TrimEnd('/');token=bearer;conversation=id.ToString();
        }
        public void Prepare(string text)
        {
            Guard();
            if(busy||HasPending)throw new InvalidOperationException("Resolve the pending command before preparing another");
            if(string.IsNullOrWhiteSpace(text)||text.Length>8000)throw new ArgumentException("Invalid message");
            message=text;key=Guid.NewGuid().ToString();clientId=Guid.NewGuid().ToString();
            TurnId=null;Version=0;Status="queued";Error=null;
        }
        void Guard()
        {
            if(disposed)throw new ObjectDisposedException(nameof(SyntheticConversationCommands));
            if(authFailed)throw new InvalidOperationException("Reload authenticated session");
        }
        UnityWebRequest Create(string path,string json=null)
        {
            var value=new UnityWebRequest(origin+"/local/v1/"+path,json==null?"GET":"POST");
            value.redirectLimit=0;value.timeout=15;value.downloadHandler=new DownloadHandlerBuffer();
            value.SetRequestHeader("Authorization","Bearer "+token);
            if(json!=null){value.SetRequestHeader("Content-Type","application/json");value.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));}
            return value;
        }
        public IEnumerator Submit()=>Run(false);
        public IEnumerator Cancel()=>Run(true);
        IEnumerator Run(bool cancel)
        {
            Guard();
            if(busy||!HasPending)throw new InvalidOperationException("No available pending command");
            busy=true;stopped=false;Error=null;
            try {
                if(TurnId==null) {
                    Status="sending";
                    request=Create("conversations/"+conversation+"/admissions",JsonUtility.ToJson(new Admission {client_message_id=clientId,text=message}));
                    request.SetRequestHeader("Idempotency-Key",key);
                    var op=request.SendWebRequest();while(!op.isDone&&!stopped)yield return null;
                    if(stopped)yield break;
                    if(request.responseCode!=202||request.result!=UnityWebRequest.Result.Success){Fail();yield break;}
                    Receipt receipt=null;try{receipt=JsonUtility.FromJson<Receipt>(request.downloadHandler.text);}catch{}
                    if(receipt==null||receipt.mode!="local-synthetic-accounts"||receipt.status!="accepted"||!Guid.TryParse(receipt.turn_id,out var id)||id==Guid.Empty){Error="invalid_admission";Status="uncertain";yield break;}
                    TurnId=id.ToString();Release();
                }
                // Even an idempotent admission receipt says accepted after completion.
                // Reconcile before rendering state or issuing versioned cancellation.
                var get=Reconcile();while(get.MoveNext())yield return get.Current;
                if(stopped||Error!=null||!cancel||Status!="accepted")yield break;
                request=Create("turns/"+TurnId+"/cancel",JsonUtility.ToJson(new Cancellation {expected_version=Version}));
                var cancellation=request.SendWebRequest();while(!cancellation.isDone&&!stopped)yield return null;
                if(stopped)yield break;
                if(request.responseCode==401||request.responseCode==403){Fail();yield break;}
                // A lost cancel acknowledgement or terminal race needs server truth.
                Release();get=Reconcile();while(get.MoveNext())yield return get.Current;
            } finally {
                Release();busy=false;
                if(stopped)Status="uncertain";
            }
        }
        IEnumerator Reconcile()
        {
            request=Create("turns/"+TurnId);
            var op=request.SendWebRequest();while(!op.isDone&&!stopped)yield return null;
            if(stopped)yield break;
            if(request.responseCode!=200||request.result!=UnityWebRequest.Result.Success){Fail();yield break;}
            Turn value=null;try{value=JsonUtility.FromJson<Turn>(request.downloadHandler.text);}catch{}
            bool terminal=value!=null&&(value.status=="completed"||value.status=="cancelled"||value.status=="failed");
            if(value==null||value.turn_id!=TurnId||value.conversation_id!=conversation||
                (!terminal&&value.status!="accepted")||value.version!=(terminal?2:1)||value.version<Version){Error="invalid_turn";Status="uncertain";yield break;}
            Status=value.status;Version=value.version;Release();
        }
        void Fail(){long code=request.responseCode;authFailed=code==401||code==403;Error="command_http_"+code;Status="uncertain";}
        public void ClearTerminal()
        {
            Guard();if(busy||(Status!="completed"&&Status!="cancelled"&&Status!="failed"))throw new InvalidOperationException("Turn is unresolved");
            message=key=clientId=TurnId=null;Version=0;Status="draft";Error=null;
        }
        public void Stop(){stopped=true;request?.Abort();}
        void Release(){request?.Dispose();request=null;}
        public void Dispose(){if(disposed)return;Stop();Release();disposed=true;message=key=clientId=TurnId=null;}
        [Serializable] sealed class Admission {public string client_message_id,text;}
        [Serializable] sealed class Receipt {public string turn_id,status,mode;}
        [Serializable] sealed class Turn {public string turn_id,conversation_id,status;public long version;}
        [Serializable] sealed class Cancellation {public long expected_version;}
    }
}
