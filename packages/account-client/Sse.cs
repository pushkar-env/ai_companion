using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
namespace Companion.AccountClient;

public static class Sse {
    public static async IAsyncEnumerable<StoredEvent> Read(Stream stream,[EnumeratorCancellation] CancellationToken cancellation=default) {
        using var reader=new StreamReader(stream,new UTF8Encoding(false,true),false,1024,true);
        var buffer=new char[1024];var line=new StringBuilder();var data=new StringBuilder();
        string? id=null,type=null;int frameSize=0;
        while(true) {
            int count=await reader.ReadAsync(buffer.AsMemory(),cancellation);
            if(count==0)yield break; // An interrupted frame must be replayed, never checkpointed.
            for(int i=0;i<count;i++) {
                if(++frameSize>131072)throw new InvalidDataException("SSE frame exceeds local limit.");
                var c=buffer[i];
                if(c!='\n'){line.Append(c);continue;}
                string value=line.ToString().TrimEnd('\r');line.Clear();
                if(value.Length==0) {
                    if(data.Length>0) {
                        var item=JsonSerializer.Deserialize<StoredEvent>(data.ToString())??throw new InvalidDataException("Missing event.");
                        if(!long.TryParse(id,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out var cursor)
                            || cursor!=item.sequence || type!=item.type)throw new InvalidDataException("Event envelope mismatch.");
                        yield return item;
                    }
                    id=null;type=null;data.Clear();frameSize=0;continue;
                }
                if(value[0]==':')continue;
                int colon=value.IndexOf(':');string field=colon<0?value:value[..colon];
                string content=colon<0?"":value[(colon+1)..];if(content.StartsWith(' '))content=content[1..];
                if(field=="id")id=content;
                else if(field=="event")type=content;
                else if(field=="data"){if(data.Length>0)data.Append('\n');data.Append(content);}
            }
        }
    }
}
