$ErrorActionPreference = 'Stop'
[Console]::InputEncoding = [Text.Encoding]::UTF8
[Console]::OutputEncoding = New-Object Text.UTF8Encoding($false)
Add-Type -AssemblyName System.Speech
Add-Type -ReferencedAssemblies System.Speech -TypeDefinition @'
using System;
using System.IO;
using System.Text;
using System.Globalization;
using System.Threading;
using System.Speech.Recognition;
using System.Speech.AudioFormat;
public class LocalTranscript { public string text; public float confidence; }
public static class LocalRecognition {
    public static LocalTranscript Transcribe(byte[] pcm) {
        if(pcm.Length<8000||pcm.Length>640000||pcm.Length%2!=0)throw new ArgumentException("Invalid audio length");
        using(var audio=new MemoryStream(pcm))
        using(var engine=new SpeechRecognitionEngine(new CultureInfo("en-US"))) {
            engine.LoadGrammar(new DictationGrammar());
            engine.SetInputToAudioStream(audio,new SpeechAudioFormatInfo(16000,AudioBitsPerSample.Sixteen,AudioChannel.Mono));
            engine.InitialSilenceTimeout=TimeSpan.Zero;
            engine.EndSilenceTimeout=TimeSpan.FromMilliseconds(500);
            engine.BabbleTimeout=TimeSpan.Zero;
            var text=new StringBuilder();float confidence=1;Exception error=null;
            using(var done=new ManualResetEvent(false)) {
                engine.SpeechRecognized+=(s,e)=>{if(text.Length<501){if(text.Length>0)text.Append(" ");text.Append(e.Result.Text);confidence=Math.Min(confidence,e.Result.Confidence);}};
                engine.RecognizeCompleted+=(s,e)=>{error=e.Error;done.Set();};
                engine.RecognizeAsync(RecognizeMode.Multiple);
                if(!done.WaitOne(20000)){engine.RecognizeAsyncCancel();done.WaitOne(2000);throw new TimeoutException("Recognition timed out");}
            }
            if(error!=null)throw error;
            if(text.Length>500)throw new ArgumentException("Transcript too long");
            return new LocalTranscript {text=text.ToString(),confidence=text.Length==0?0:confidence};
        }
    }
}
'@
$audioInput = [Console]::In.ReadToEnd() | ConvertFrom-Json
if ($audioInput.sampleRate -ne 16000 -or -not ($audioInput.pcm -is [string]) -or $audioInput.pcm.Length -gt 853336) { throw 'Invalid audio input' }
[LocalRecognition]::Transcribe([Convert]::FromBase64String($audioInput.pcm)) | ConvertTo-Json -Compress
