using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Companion.Core
{
    public static class FacialProfile
    {
        public const string Version="synthetic-52-v1";
        public static readonly ReadOnlyCollection<string> Channels=Array.AsReadOnly((
            "browDownLeft browDownRight browInnerUp browOuterUpLeft browOuterUpRight " +
            "cheekPuff cheekSquintLeft cheekSquintRight " +
            "eyeBlinkLeft eyeBlinkRight eyeLookDownLeft eyeLookDownRight eyeLookInLeft eyeLookInRight " +
            "eyeLookOutLeft eyeLookOutRight eyeLookUpLeft eyeLookUpRight eyeSquintLeft eyeSquintRight eyeWideLeft eyeWideRight " +
            "jawForward jawLeft jawOpen jawRight mouthClose mouthDimpleLeft mouthDimpleRight mouthFrownLeft mouthFrownRight " +
            "mouthFunnel mouthLeft mouthLowerDownLeft mouthLowerDownRight mouthPressLeft mouthPressRight mouthPucker mouthRight " +
            "mouthRollLower mouthRollUpper mouthShrugLower mouthShrugUpper mouthSmileLeft mouthSmileRight mouthStretchLeft mouthStretchRight " +
            "mouthUpperUpLeft mouthUpperUpRight noseSneerLeft noseSneerRight tongueOut").Split(' '));
        public static readonly ReadOnlyCollection<string> Visemes=Array.AsReadOnly("sil PP FF TH DD kk CH SS nn RR aa E ih oh ou".Split(' '));
        public static int Index(string name) => Channels.IndexOf(name);
        public static IReadOnlyList<string> ValidateBindings(IEnumerable<string> names)
        {
            var errors=new List<string>();var found=new HashSet<string>(StringComparer.Ordinal);
            foreach(string name in names) {
                if(string.IsNullOrEmpty(name)) { errors.Add("Empty binding");continue; }
                if(!found.Add(name))errors.Add("Duplicate binding: "+name);
                if(Index(name)<0)errors.Add("Unknown binding: "+name);
            }
            foreach(var channel in Channels)if(!found.Contains(channel))errors.Add("Missing binding: "+channel);
            return errors;
        }
        public static bool ValidWeight(float weight) => !float.IsNaN(weight)&&!float.IsInfinity(weight)&&weight>=0&&weight<=1;
        public static float SafeWeight(float weight) => float.IsNaN(weight)||float.IsInfinity(weight)?0:Math.Max(0,Math.Min(1,weight));
        // Illustrative diagnostic map only; a real rig requires calibrated per-avatar mappings.
        public static float[] DiagnosticMap(string viseme)
        {
            int index=Visemes.IndexOf(viseme);if(index<0)throw new ArgumentException("Unknown viseme");
            var result=new float[Channels.Count];if(index==0)return result;
            string[] primary={"mouthClose","mouthLowerDownLeft","tongueOut","jawOpen","jawForward","mouthFunnel","mouthStretchLeft","mouthClose","mouthPucker","jawOpen","mouthSmileLeft","mouthStretchRight","mouthFunnel","mouthPucker"};
            result[Index(primary[index-1])]=.8f;return result;
        }
        public static void Mix(float[] speech,float[] expression,float[] output,bool muted)
        {
            if(speech.Length!=Channels.Count||expression.Length!=Channels.Count||output.Length!=Channels.Count)throw new ArgumentException("52 channels required");
            for(int i=0;i<Channels.Count;i++) {
                bool mouth=Channels[i].StartsWith("mouth",StringComparison.Ordinal)||Channels[i].StartsWith("jaw",StringComparison.Ordinal)||Channels[i]=="tongueOut";
                output[i]=muted?0:SafeWeight(mouth?speech[i]:expression[i]);
            }
        }
    }
    public sealed class FacialFrame
    {
        public int OffsetMs {get;}
        public int DurationMs {get;}
        readonly float[] weights;
        public int ChannelCount=>weights.Length;
        public float Weight(int index)=>weights[index];
        public FacialFrame(int offsetMs,int durationMs,float[] values) {OffsetMs=offsetMs;DurationMs=durationMs;weights=(float[])values.Clone();}
    }
    public interface IVoiceAgentProvider { IReadOnlyList<FacialFrame> DiagnosticFrames(); }
    public sealed class SyntheticVoiceAgent : IVoiceAgentProvider
    {
        public const int SampleRate=24000, DurationMs=3750;
        public IReadOnlyList<FacialFrame> DiagnosticFrames()
        {
            var frames=new List<FacialFrame>();
            for(int i=0;i<FacialProfile.Visemes.Count;i++)frames.Add(new FacialFrame(i*250,220,FacialProfile.DiagnosticMap(FacialProfile.Visemes[i])));
            return frames;
        }
        public float[] Tone()
        {
            var pcm=new float[SampleRate*DurationMs/1000];
            for(int n=0;n<pcm.Length;n++) {
                double ms=n*1000.0/SampleRate;int slot=(int)(ms/250);double local=ms-slot*250;
                double envelope=slot==0||local>=220?0:Math.Min(1,Math.Min(local/15,(220-local)/15));
                pcm[n]=(float)(.06*envelope*Math.Sin(2*Math.PI*(180+slot*25)*n/SampleRate));
            }
            return pcm; // Recorded deterministic tone buffer; never speech or microphone capture.
        }
    }
    public sealed class VisemePlayback
    {
        readonly List<FacialFrame> frames=new List<FacialFrame>();
        public long Epoch {get;private set;}
        public string Utterance {get;private set;}="";
        public bool Active {get;private set;}
        public int BufferedFrames=>frames.Count;
        long lastSample=-1;
        public long Begin(string utterance)
        {
            if(string.IsNullOrWhiteSpace(utterance))throw new ArgumentException("Utterance required");
            Epoch++;frames.Clear();Utterance=utterance;Active=true;lastSample=-1;return Epoch;
        }
        public bool Enqueue(long epoch,string utterance,FacialFrame frame)
        {
            if(!Active||epoch!=Epoch||utterance!=Utterance||frame==null||frames.Count>=200||frame.ChannelCount!=52||frame.OffsetMs<0||frame.OffsetMs>60000||frame.DurationMs<1||frame.DurationMs>500)return false;
            for(int i=0;i<52;i++)if(!FacialProfile.ValidWeight(frame.Weight(i)))return false;
            if(frames.Count>0&&frame.OffsetMs<frames[frames.Count-1].OffsetMs+frames[frames.Count-1].DurationMs)return false;
            frames.Add(frame);return true;
        }
        public void Sample(long playedSamples,int sampleRate,float[] output)
        {
            if(output.Length!=52)throw new ArgumentException("52 channels required");Array.Clear(output,0,output.Length);
            if(!Active)return;
            if(sampleRate<8000||sampleRate>192000||playedSamples<lastSample||playedSamples<0){Cancel();return;}
            lastSample=playedSamples;double ms=playedSamples*1000.0/sampleRate;
            foreach(var f in frames) {
                if(ms<f.OffsetMs)break;
                if(ms>=f.OffsetMs+f.DurationMs)continue;
                float envelope=(float)Math.Min(1,Math.Min((ms-f.OffsetMs)/15,(f.OffsetMs+f.DurationMs-ms)/15));
                for(int i=0;i<52;i++)output[i]=f.Weight(i)*Math.Max(0,envelope);break;
            }
        }
        public void Cancel() { Epoch++;Active=false;frames.Clear();lastSample=-1; }
    }
    public enum TransportState { Idle, Connecting, Listening, Failed, Ended }
    public interface IRealtimeTransport : IDisposable
    {
        TransportState State {get;}
        bool CaptureActive {get;}
        void Connect(bool permissionGranted,bool simulateFailure);
        void End();
    }
    public sealed class MockRealtimeTransport : IRealtimeTransport
    {
        public TransportState State {get;private set;}=TransportState.Idle;
        public bool CaptureActive=>false;
        public void Connect(bool permissionGranted,bool simulateFailure)
        {
            if(State==TransportState.Listening)throw new InvalidOperationException("Already connected");
            State=TransportState.Connecting;State=!permissionGranted||simulateFailure?TransportState.Failed:TransportState.Listening;
        }
        public void End(){State=TransportState.Ended;}
        public void Dispose(){End();}
    }
}
