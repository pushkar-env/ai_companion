using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Networking;

namespace Companion.Presentation
{
    // Bounded newline framing; parsing/Unity objects stay on the main thread.
    public sealed class LocalSpeechStream : DownloadHandlerScript
    {
        readonly Decoder decoder=new UTF8Encoding(false,true).GetDecoder();
        readonly char[] chars=new char[8194];readonly StringBuilder line=new StringBuilder();
        readonly Queue<string> lines=new Queue<string>();readonly object gate=new object();int bytes;
        public bool Invalid {get;private set;}
        public LocalSpeechStream():base(new byte[8192]){}
        protected override bool ReceiveData(byte[] data,int length)
        {
            lock(gate) {
                if(data==null||length<0||bytes+length>8000000){Invalid=true;return false;}bytes+=length;
                try {
                    int count=decoder.GetChars(data,0,length,chars,0,false);
                    for(int i=0;i<count;i++) {
                        if(chars[i]=='\n') {if(lines.Count>=1024)throw new InvalidOperationException();lines.Enqueue(line.ToString());line.Clear();}
                        else {line.Append(chars[i]);if(line.Length>6000000)throw new InvalidOperationException();}
                    }
                    return true;
                }catch{Invalid=true;return false;}
            }
        }
        protected override void CompleteContent(){lock(gate){try{decoder.GetChars(Array.Empty<byte>(),0,0,chars,0,true);if(line.Length!=0)Invalid=true;}catch{Invalid=true;}}}
        public bool TryRead(out string value){lock(gate){if(lines.Count==0){value=null;return false;}value=lines.Dequeue();return true;}}
    }
}
