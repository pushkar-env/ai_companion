using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Companion.Core;
using Companion.Presentation;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Companion.Editor
{
    // Frame-exact review of the talking face. Recorded local-speech fixtures (ignored
    // artifacts/face-review/turn*.json: one clip per sentence, like the app) play through the runtime
    // SpeechMouthMotion, CompanionFace and CompanionGaze on an isolated copy of each roster character.
    // Frames render head-and-shoulders; when ffmpeg is on PATH they are muxed with the audio into one
    // side-by-side video (roster order). Stopped Editor only; the scene and Game view are untouched.
    public static class FacePerformanceReview
    {
        [Serializable] public sealed class Turn {public string emotion;public TalkingCharacter.Reply[] clips;}
        sealed class Clip {public TalkingCharacter.Reply reply;public float start,end;public float[] samples;public string emotion;public bool question;public int turn;}
        sealed class Span {public float think,first,last;public string emotion;}
        public static string Root=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
        public const int Width=360,Height=480;

        [MenuItem("Companion/Characters/Render Face Performance Review")]
        public static void RunMenu()=>Debug.Log(Render(Path.Combine(Root,"docs/evidence/m1/face-performance"),30,null));

        public static string Render(string outFolder,int fps,string[] only)
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Render the face review while stopped");
            var files=Directory.GetFiles(Path.Combine(Root,"artifacts/face-review"),"turn*.json").OrderBy(f=>f).ToArray();
            if(files.Length==0)throw new InvalidOperationException("No speech fixtures in artifacts/face-review (see docs/evidence/m1/face-performance/README.md)");
            var turns=files.Select(f=>JsonUtility.FromJson<Turn>(File.ReadAllText(f))).ToArray();
            // Timeline: idle lead, then per reply: thinking, its sentence clips back to back, a linger.
            var clips=new List<Clip>();var spans=new List<Span>();float t=.6f;
            for(int k=0;k<turns.Length;k++) {
                var span=new Span{think=t,emotion=turns[k].emotion};t+=.7f;span.first=t;
                foreach(var reply in turns[k].clips) {
                    var pcm=Convert.FromBase64String(reply.pcm);var samples=new float[pcm.Length/2];
                    for(int i=0;i<samples.Length;i++)samples[i]=(short)(pcm[i*2]|pcm[i*2+1]<<8)/32768f;
                    var clip=new Clip{reply=reply,start=t,samples=samples,emotion=turns[k].emotion,turn=k,question=reply.text.TrimEnd().EndsWith("?",StringComparison.Ordinal)};
                    clip.end=t+samples.Length/(float)reply.sampleRate;clips.Add(clip);t=clip.end+.15f;
                }
                span.last=clips[clips.Count-1].end;spans.Add(span);t=span.last+1.4f;
            }
            float total=t+1.2f;int frames=Mathf.CeilToInt(total*fps);
            Directory.CreateDirectory(outFolder);
            string work=Path.Combine(Root,"apps/unity/Library/FaceReview");if(Directory.Exists(work))Directory.Delete(work,true);Directory.CreateDirectory(work);
            WriteWav(Path.Combine(work,"audio.wav"),clips,total,16000);
            var app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>(FindObjectsInactive.Include);
            var report=new StringBuilder();var rendered=new List<string>();
            var target=new RenderTexture(Width,Height,24){antiAliasing=4};target.Create();
            var image=new Texture2D(Width,Height,TextureFormat.RGB24,false);
            var cameraObject=new GameObject("Face review camera"){hideFlags=HideFlags.HideAndDontSave};var camera=cameraObject.AddComponent<Camera>();
            camera.CopyFrom(app.portraitCamera);camera.enabled=false;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.055f,.065f,.07f,1);camera.targetTexture=target;camera.aspect=Width/(float)Height;camera.fieldOfView=14;
            try {
                foreach(var option in app.characters.Where(o=>o.model!=null&&(only==null||only.Contains(o.name)))) {
                    string dir=Path.Combine(work,option.name);Directory.CreateDirectory(dir);
                    var stats=RenderCharacter(option,clips,spans,frames,fps,camera,target,image,dir);
                    rendered.Add(option.name);report.AppendLine(option.name+": "+stats);
                }
            }finally{UnityEngine.Object.DestroyImmediate(cameraObject);target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);}
            report.AppendLine(Mux(work,outFolder,rendered,fps));
            File.WriteAllText(Path.Combine(work,"report.txt"),report.ToString());
            return report.ToString();
        }

        static string RenderCharacter(TalkingCharacter.CharacterOption option,List<Clip> clips,List<Span> spans,int frames,int fps,Camera camera,RenderTexture target,Texture2D image,string dir)
        {
            var rig=new FaceRig(option);
            CompanionBodyIdle idle=null;CompanionGaze gaze=null;
            try {
                var model=rig.model;var head=rig.head;var jaw=rig.jaw;var jawRest=rig.jawRest;var eyeL=rig.eyeL;var eyeR=rig.eyeR;var copy=rig.copy;
                idle=new CompanionBodyIdle(model,5);gaze=new CompanionGaze(model);
                var face=rig.Face(option.face,7);
                var mouth=new SpeechMouthMotion();
                int current=-1;float dt=1f/fps,maxJaw=0;int blinks=0;bool wasBlink=false;
                var trace=new StringBuilder().AppendLine("t,speaking,emotion,blink,jaw,open,round,wide,explosive,smileL,browInL,browOutL,browDownL,eyeBlinkL,happy,concerned,curious");
                float W(string n)=>rig.Weight(n);
                for(int f=0;f<frames;f++) {
                    float t=f*dt;
                    rig.ResetFace();
                    int c=clips.FindIndex(x=>t>=x.start&&t<x.end);
                    if(c>=0&&c!=current) {
                        var reply=clips[c].reply;current=c;
                        mouth.SetCues(reply.cues.Select(q=>(double)q.ms).ToArray(),reply.cues.Select(q=>q.id).ToArray(),reply.cues.Select(q=>(double)q.durationMs).ToArray());
                        mouth.SetEnvelope(SpeechMouthMotion.Envelope(clips[c].samples,reply.sampleRate,10),10);face.OnSentenceStart();
                    }
                    var span=spans.LastOrDefault(s=>t>=s.think);
                    bool speaking=span!=null&&t>=span.first&&t<span.last+.1f;
                    bool thinking=span!=null&&t>=span.think&&t<span.first-.25f;
                    bool preparing=span!=null&&t>=span.first-.25f&&t<span.first;
                    string emotion=spans.LastOrDefault(s=>t>=s.first-.25f)?.emotion??"neutral";
                    bool playing=c>=0;double ms=playing?(t-clips[c].start)*1000:0;
                    idle.Sample(t);
                    gaze.Sample(t,dt,speaking||thinking||preparing,false,face.GazeAside);
                    mouth.Step(ms,dt,playing);
                    face.Step(new FaceInput{time=t,dt=dt,speaking=speaking,preparing=preparing,thinking=thinking,emotion=emotion,
                        question=current>=0&&clips[current].question&&speaking,glanceBlink=gaze.Blink},mouth);
                    maxJaw=Mathf.Max(maxJaw,face.JawDegrees);if(face.Blink>.9f&&!wasBlink)blinks++;wasBlink=face.Blink>.9f;
                    trace.AppendLine(string.Join(",",new[]{t.ToString("F3"),speaking?"1":"0",emotion,face.Blink.ToString("F3"),face.JawDegrees.ToString("F2"),mouth.Weight(0).ToString("F3"),mouth.Weight(1).ToString("F3"),mouth.Weight(2).ToString("F3"),mouth.Weight(9).ToString("F3"),
                        W("Mouth_Corner_Pull_L").ToString("F3"),W("Brow_Raise_In_L").ToString("F3"),W("Brow_Raise_Outer_L").ToString("F3"),W("Brow_Down_L").ToString("F3"),W("Eye_Blink_L").ToString("F3"),
                        face.EmotionWeight("happy").ToString("F3"),face.EmotionWeight("concerned").ToString("F3"),face.EmotionWeight("curious").ToString("F3")}));
                    // Frame from the eyes so every rig gets the same forehead-to-chin portrait.
                    var focus=(eyeL.position+eyeR.position)/2-model.up*.045f;
                    camera.transform.position=focus+model.forward*1.25f+model.up*.02f;camera.transform.LookAt(focus);
                    if(f==0)camera.Render();   // warm-up: the first render after layer changes can be empty
                    ExpressiveIdleReview.Capture(camera,copy,target,image,Path.Combine(dir,$"f_{f:00000}.png"));
                }
                File.WriteAllText(Path.Combine(dir,"trace.csv"),trace.ToString());
                return $"{frames} frames, peak jaw {maxJaw:F1} deg, {blinks} blinks";
            }finally{gaze?.Dispose();idle?.Dispose();rig.Dispose();}
        }

        /// <summary>Per-frame articulation of one recorded clip (TalkingCharacter.Reply JSON) as CSV.</summary>
        public static string SimulateCsv(string replyJson,int fps)
        {
            var reply=JsonUtility.FromJson<TalkingCharacter.Reply>(replyJson);
            var pcm=Convert.FromBase64String(reply.pcm);var samples=new float[pcm.Length/2];
            for(int i=0;i<samples.Length;i++)samples[i]=(short)(pcm[i*2]|pcm[i*2+1]<<8)/32768f;
            var m=new SpeechMouthMotion();
            m.SetCues(reply.cues.Select(q=>(double)q.ms).ToArray(),reply.cues.Select(q=>q.id).ToArray(),reply.cues.Select(q=>(double)q.durationMs).ToArray());
            m.SetEnvelope(SpeechMouthMotion.Envelope(samples,reply.sampleRate,10),10);
            var sb=new StringBuilder("ms,"+string.Join(",",SpeechMouthMotion.Channels)+",jaw,seal,emph,energy,pause\n");
            double total=samples.Length*1000.0/reply.sampleRate;
            for(int f=0;f*1000.0/fps<total+600;f++) {
                double ms=f*1000.0/fps;m.Step(ms,1.0/fps,ms<total);
                sb.Append(ms.ToString("F1",System.Globalization.CultureInfo.InvariantCulture));
                for(int k=0;k<SpeechMouthMotion.Channels.Length;k++)sb.Append(","+m.Weight(k).ToString("F3",System.Globalization.CultureInfo.InvariantCulture));
                sb.Append(","+string.Join(",",new[]{m.Jaw,m.Seal,m.Emphasis,m.Energy}.Select(v=>v.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)))+","+(m.InPause?1:0)+"\n");
            }
            return sb.ToString();
        }

        static void WriteWav(string path,List<Clip> clips,float total,int rate)
        {
            var buffer=new short[Mathf.CeilToInt(total*rate)];
            foreach(var c in clips) {
                if(c.reply.sampleRate!=rate)throw new InvalidOperationException("Fixtures must be 16 kHz");
                int start=Mathf.RoundToInt(c.start*rate);
                for(int i=0;i<c.samples.Length&&start+i<buffer.Length;i++)buffer[start+i]=(short)Mathf.Clamp(c.samples[i]*32767,-32768,32767);
            }
            using var w=new BinaryWriter(File.Create(path));
            w.Write(Encoding.ASCII.GetBytes("RIFF"));w.Write(36+buffer.Length*2);w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data"));w.Write(buffer.Length*2);foreach(var s in buffer)w.Write(s);
        }

        // Side-by-side video in roster order (left to right) with the speech audio.
        static string Mux(string work,string outFolder,List<string> names,int fps)
        {
            if(names.Count==0)return "No frames rendered";
            var inputs=new StringBuilder();foreach(var n in names)inputs.Append($"-framerate {fps} -i \"{Path.Combine(work,n,"f_%05d.png")}\" ");
            string stack=names.Count>1?string.Concat(Enumerable.Range(0,names.Count).Select(i=>$"[{i}:v]"))+$"hstack=inputs={names.Count}[v]":"[0:v]null[v]";
            string output=Path.Combine(outFolder,"face-performance.mp4");
            string args=$"-y -loglevel error {inputs}-i \"{Path.Combine(work,"audio.wav")}\" -filter_complex \"{stack}\" -map \"[v]\" -map {names.Count}:a -c:v libx264 -pix_fmt yuv420p -crf 27 -preset slow -c:a aac -b:a 64k -shortest \"{output}\"";
            try {
                using var process=Process.Start(new ProcessStartInfo("ffmpeg",args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true});
                string error=process.StandardError.ReadToEnd();process.WaitForExit();
                return process.ExitCode==0?"Video: "+output+" ("+string.Join(", ",names)+", left to right)":"ffmpeg failed: "+error;
            }catch(Exception e){return "ffmpeg unavailable ("+e.Message+"); frames kept in "+work;}
        }
    }
}
