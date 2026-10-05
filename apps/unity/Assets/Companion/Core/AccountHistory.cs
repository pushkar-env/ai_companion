using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
namespace Companion.Core
{
    [Serializable] public sealed class AccountEvent
    {
        public string event_id,turn_id,type,text;
        public long sequence,version;
    }
    public sealed class AccountTurn
    {
        public string Id { get; internal set; }
        public string UserText { get; internal set; }
        public string AssistantText { get; internal set; }
        public string State { get; internal set; }
        public long Version { get; internal set; }
    }
    // One memory-only projection per authenticated conversation; no Unity dependencies.
    public sealed class AccountHistory
    {
        readonly Dictionary<string,AccountTurn> turns=new Dictionary<string,AccountTurn>();
        public long Cursor { get; private set; }
        public AccountTurn[] Snapshot { get { var result=new AccountTurn[turns.Count];int i=0;
            foreach(var t in turns.Values)result[i++]=new AccountTurn {Id=t.Id,UserText=t.UserText,AssistantText=t.AssistantText,State=t.State,Version=t.Version};return result; } }
        public bool Apply(AccountEvent e)
        {
            if(e==null)throw new InvalidDataException("Missing event");
            if(e.sequence<=Cursor)return false;
            Guid id,turn;
            if(e.sequence!=Cursor+1 || !Guid.TryParse(e.event_id,out id) || id==Guid.Empty || !Guid.TryParse(e.turn_id,out turn) || turn==Guid.Empty)
                throw new InvalidDataException("Invalid event order");
            string key=turn.ToString();
            if(e.type=="turn.accepted") {
                if(e.version!=1 || e.text==null || turns.ContainsKey(key) || turns.Count>=1000)throw new InvalidDataException("Invalid admission");
                turns.Add(key,new AccountTurn {Id=key,UserText=e.text,State="accepted",Version=1});
            } else {
                AccountTurn t;
                if(!turns.TryGetValue(key,out t) || t.State!="accepted" || e.version!=t.Version+1
                    || (e.type!="turn.completed" && e.type!="turn.cancelled" && e.type!="turn.failed")
                    || (e.type=="turn.completed" ? e.text==null : e.text!=null))throw new InvalidDataException("Invalid terminal event");
                t.State=e.type.Substring(5);t.AssistantText=e.text;t.Version=e.version;
            }
            Cursor=e.sequence;return true;
        }
    }
    // Incremental UTF-8 decoder: fragmented bytes and incomplete EOF frames are safe.
    public sealed class AccountEventDecoder
    {
        readonly Decoder utf8=new UTF8Encoding(false,true).GetDecoder();
        readonly char[] chars=new char[1024];
        readonly StringBuilder line=new StringBuilder(),data=new StringBuilder();
        readonly Func<string,AccountEvent> deserialize;readonly Action<AccountEvent> apply;
        string id,type;int frameSize;
        public AccountEventDecoder(Func<string,AccountEvent> deserialize,Action<AccountEvent> apply){this.deserialize=deserialize;this.apply=apply;}
        public void Feed(byte[] bytes,int count)
        {
            int offset=0;
            while(offset<count) {
                int used,written;bool complete;
                utf8.Convert(bytes,offset,count-offset,chars,0,chars.Length,false,out used,out written,out complete);
                offset+=used;
                for(int i=0;i<written;i++) {
                    if(++frameSize>131072)throw new InvalidDataException("SSE frame exceeds local limit");
                    if(chars[i]!='\n'){line.Append(chars[i]);continue;}
                    string value=line.ToString().TrimEnd('\r');line.Length=0;
                    if(value.Length==0) {
                        if(data.Length>0) {
                            var e=deserialize(data.ToString());long cursor;
                            if(e==null || !long.TryParse(id,NumberStyles.None,CultureInfo.InvariantCulture,out cursor) || cursor!=e.sequence || type!=e.type)
                                throw new InvalidDataException("Event envelope mismatch");
                            apply(e);
                        }
                        data.Length=0;id=null;type=null;frameSize=0;continue;
                    }
                    if(value[0]==':')continue;
                    int colon=value.IndexOf(':');string field=colon<0?value:value.Substring(0,colon);
                    string content=colon<0?"":value.Substring(colon+1);if(content.StartsWith(" ",StringComparison.Ordinal))content=content.Substring(1);
                    if(field=="id")id=content;else if(field=="event")type=content;
                    else if(field=="data"){if(data.Length>0)data.Append('\n');data.Append(content);}
                }
            }
        }
        // Discard this decoder on EOF/reconnect; only complete events were applied.
    }
}
