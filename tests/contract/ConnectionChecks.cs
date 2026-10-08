using Companion.Core;

static class ConnectionChecks
{
    public static void Run(Action<string,Action> check)
    {
        string token=new string('a',64);
        void Require(bool condition){if(!condition)throw new Exception("Connection boundary check failed");}
        bool Accept(string url,string bearer=null,bool editor=true)=>ConversationConnection.TryLocalEditor(editor,url,bearer??token,out _);
        check("CONFIG-01 player builds reject local credentials",()=>{
            Require(!Accept("http://127.0.0.1:4567",editor:false));
            var unavailable=new UnconfiguredConversationSource();
            Require(!unavailable.TryGet(out var c)&&c==null&&!unavailable.UnavailableMessage.Contains("Companion >"));
        });
        check("CONFIG-02 reject remote, ambiguous and credential-bearing URLs",()=>{
            foreach(string url in new[]{"https://127.0.0.1:4567","http://localhost:4567","http://example.com:4567",
                "http://127.0.0.1.example.com:4567","http://127.0.0.1:4567@evil.test","http://user@127.0.0.1:4567",
                "http://127.0.0.1:4567/path","http://127.0.0.1:4567/?token=x","http://127.0.0.1:4567/#x",
                "http://2130706433:4567","http://127.1:4567","http://127.0.0.1:0"," http://127.0.0.1:4567",
                "http://127.0.0.1:4567/a/..","file:///tmp/service","","garbage"})Require(!Accept(url));
        });
        check("CONFIG-03 reject missing malformed or header-injection credentials",()=>{
            foreach(string value in new[]{"",new string('a',63),new string('a',65),new string('z',64),new string('a',62)+"\r\n"})
                Require(!Accept("http://127.0.0.1:4567",value));
            Require(!ConversationConnection.TryLocalEditor(true,"http://127.0.0.1:4567",null,out _));
        });
        check("CONFIG-04 canonical routes and immutable redacted snapshot",()=>{
            Require(ConversationConnection.TryLocalEditor(true,"http://127.0.0.1:4567/",token,out var c));
            Require(c.Url(ConversationOperation.Turn)=="http://127.0.0.1:4567/turn-stream");
            Require(c.Url(ConversationOperation.Readiness).EndsWith("/readiness"));
            Require(c.Url(ConversationOperation.Transcription).EndsWith("/transcribe"));
            Require(c.Authorization=="Bearer "+token&&!c.ToString().Contains(token));
            bool rejected=false;try{c.Url((ConversationOperation)999);}catch(ArgumentOutOfRangeException){rejected=true;}Require(rejected);
        });
        check("CONFIG-05 failed refresh returns no previous connection",()=>{
            Require(ConversationConnection.TryLocalEditor(true,"http://127.0.0.1:4567",token,out var c));
            Require(!ConversationConnection.TryLocalEditor(true,"http://evil.test",token,out c)&&c==null);
        });
    }
}
