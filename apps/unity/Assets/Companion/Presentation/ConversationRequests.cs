using Companion.Core;
using UnityEngine.Networking;
#if UNITY_EDITOR_WIN
using System;
using System.IO;
using UnityEngine;
#endif

namespace Companion.Presentation
{
    public static class ConversationRequests
    {
        public static IConversationConnectionSource DefaultSource()
        {
#if UNITY_EDITOR_WIN
            return new EditorConversationSource();
#else
            return new UnconfiguredConversationSource();
#endif
        }

        public static UnityWebRequest Create(ConversationConnection connection,ConversationOperation operation)
        {
            if(connection==null)throw new System.ArgumentNullException(nameof(connection));
            var request=new UnityWebRequest(connection.Url(operation),operation==ConversationOperation.Readiness?"GET":"POST");
            // Never forward a bearer or recorded audio to a redirected destination.
            request.redirectLimit=0;
            request.timeout=operation==ConversationOperation.Readiness?5:operation==ConversationOperation.Turn?100:35;
            request.SetRequestHeader("Authorization",connection.Authorization);
            if(operation!=ConversationOperation.Readiness)request.SetRequestHeader("Content-Type","application/json");
            return request;
        }
    }

#if UNITY_EDITOR_WIN
    // Compiled out of every player, including development players. File contents and
    // exceptions must never be logged; configuration failure produces no connection.
    sealed class EditorConversationSource : IConversationConnectionSource
    {
        [Serializable] sealed class Session { public string url,token; }
        public string DisplayName=>"Local AI";
        public string UnavailableMessage=>"Local service unavailable. Start it from Companion > Start Local Talking Service. Your draft is kept.";
        public bool TryGet(out ConversationConnection connection)
        {
            connection=null;
            try {
                string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../artifacts/talking-character/session.json"));
                var file=new FileInfo(path);
                if(!file.Exists||file.Length>4096)return false;
                var session=JsonUtility.FromJson<Session>(File.ReadAllText(path));
                return session!=null&&ConversationConnection.TryLocalEditor(true,session.url,session.token,out connection);
            }catch {return false;}
        }
    }
#endif
}
