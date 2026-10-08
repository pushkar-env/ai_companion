using System;

namespace Companion.Core
{
    public enum ConversationOperation { Readiness, Turn, Transcription }

    // An immutable credential snapshot. A future authenticated adapter must implement
    // its own protocol; a hosted URL cannot opt into the local development protocol.
    public sealed class ConversationConnection
    {
        readonly string origin, token;
        ConversationConnection(string origin, string token) { this.origin=origin;this.token=token; }

        public static bool TryLocalEditor(bool windowsEditor, string endpoint, string bearer,
            out ConversationConnection connection)
        {
            connection=null;
            if(!windowsEditor || string.IsNullOrEmpty(endpoint) || endpoint.Length>256 ||
                bearer==null || bearer.Length!=64) return false;
            foreach(char c in bearer)
                if(!((c>='0'&&c<='9')||(c>='a'&&c<='f')||(c>='A'&&c<='F')))return false;
            if(!Uri.TryCreate(endpoint,UriKind.Absolute,out var uri) || uri.Scheme!="http" ||
                uri.Host!="127.0.0.1" || uri.Port<1 || !string.IsNullOrEmpty(uri.UserInfo) ||
                uri.AbsolutePath!="/" || !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment)) return false;
            // Require canonical spelling too; reject URI parser aliases/normalization.
            string authority="http://127.0.0.1"+(uri.IsDefaultPort?"":":"+uri.Port);
            if(endpoint!=authority && endpoint!=authority+"/")return false;
            connection=new ConversationConnection(authority,bearer);return true;
        }

        public string Url(ConversationOperation operation)
        {
            switch(operation) {
                case ConversationOperation.Readiness:return origin+"/readiness";
                case ConversationOperation.Turn:return origin+"/turn-stream";
                case ConversationOperation.Transcription:return origin+"/transcribe";
                default:throw new ArgumentOutOfRangeException(nameof(operation));
            }
        }
        public string Authorization=>"Bearer "+token;
        public override string ToString()=>"Local conversation connection (credentials redacted)";
    }

    public interface IConversationConnectionSource
    {
        string DisplayName {get;}
        string UnavailableMessage {get;}
        bool TryGet(out ConversationConnection connection);
    }

    public sealed class UnconfiguredConversationSource : IConversationConnectionSource
    {
        public string DisplayName=>"AI";
        public string UnavailableMessage=>"Chat service is not configured for this build. Your draft is kept.";
        public bool TryGet(out ConversationConnection connection){connection=null;return false;}
    }
}
