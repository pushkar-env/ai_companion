$ErrorActionPreference = 'Stop'
[Console]::InputEncoding = [Text.Encoding]::UTF8
[Console]::OutputEncoding = New-Object Text.UTF8Encoding($false)
Add-Type -AssemblyName System.Speech
Add-Type -ReferencedAssemblies System.Speech -TypeDefinition @'
using System;
using System.IO;
using System.Collections.Generic;
using System.Speech.Synthesis;
using System.Speech.AudioFormat;
public class LocalSpeechCue { public double ms; public int id; public double durationMs; }
public class LocalSpeechResult { public string pcm; public int sampleRate = 16000; public List<LocalSpeechCue> cues = new List<LocalSpeechCue>(); }
public static class LocalSpeech {
    public static LocalSpeechResult Render(string text) {
        var result = new LocalSpeechResult();
        using (var speech = new SpeechSynthesizer()) using (var audio = new MemoryStream()) {
            speech.SelectVoice("Microsoft Zira Desktop");
            speech.Rate = 0;
            speech.VisemeReached += (s,e) => result.cues.Add(new LocalSpeechCue { ms=e.AudioPosition.TotalMilliseconds, id=e.Viseme, durationMs=e.Duration.TotalMilliseconds });
            speech.SetOutputToAudioStream(audio, new SpeechAudioFormatInfo(16000, AudioBitsPerSample.Sixteen, AudioChannel.Mono));
            speech.Speak(text);
            result.pcm = Convert.ToBase64String(audio.ToArray());
        }
        return result;
    }
}
'@
$speechInput = [Console]::In.ReadToEnd() | ConvertFrom-Json
if (-not ($speechInput.text -is [string]) -or $speechInput.text.Length -gt 600 -or $speechInput.text.Length -lt 1) { throw 'Invalid speech input' }
[LocalSpeech]::Render($speechInput.text) | ConvertTo-Json -Depth 5 -Compress
