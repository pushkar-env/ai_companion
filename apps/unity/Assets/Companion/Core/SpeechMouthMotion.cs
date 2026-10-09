using System;
using System.Collections.Generic;

namespace Companion.Core
{
    // Speech articulation on the audio clock. Windows speech reports one viseme per phone; this turns
    // that track into continuous lip, jaw and tongue motion:
    //  - a sequential timeline from cue durations (a diphthong arrives as two cues with one timestamp),
    //  - dominance-weighted coarticulation (rounding anticipates; t/d/k/h borrow neighbouring lips),
    //  - guaranteed lip closure for p/b/m and lip-to-teeth contact for f/v, however short the phone,
    //  - loudness from the PCM envelope (stressed syllables open wider) and emphasis pulses,
    //  - critically damped smoothing integrated in fixed substeps, so results match at any frame rate.
    public sealed class SpeechMouthMotion
    {
        public static readonly string[] Channels={"V_Open","V_Tight_O","V_Wide","V_Tight","V_Tongue_up","V_Affricate","V_Tongue_Out","V_Dental_Lip","V_Tongue_Raise","V_Explosive"};
        const int Open=0,Round=1,Wide=2,Tight=3,TongueUp=4,Affricate=5,TongueOut=6,Dental=7,TongueRaise=8,Explosive=9;
        const int JawP=10,SealP=11,Count=12;
        // Articulator speeds (natural frequency, Hz) and look-ahead that cancels the smoothing lag.
        const double LipHz=13,JawHz=9.5,TongueHz=15,LipLead=22,JawLead=28,Substep=2,Carryover=28;
        static readonly int[] ClosureDamped={Open,Wide,Round,Affricate,TongueOut,Tight};

        sealed class Pose {public readonly float[] v=new float[Count];public float lipDom,jawDom,tongueDom,anticipation;}
        struct Segment {public double start,end;public int id;public float energy;}

        static Pose P(float open,float round,float wide,float tight,float tongueUp,float affricate,float tongueOut,float dental,float tongueRaise,float explosive,
            float jaw,float seal,float lipDom,float jawDom,float anticipation=26)
        {
            // Tongue phones lead the tongue channels; everything else pulls the tongue back to rest.
            var p=new Pose{lipDom=lipDom,jawDom=jawDom,anticipation=anticipation,tongueDom=tongueUp+tongueOut+tongueRaise>0.5f?.9f:.35f};
            float[] v={open,round,wide,tight,tongueUp,affricate,tongueOut,dental,tongueRaise,explosive,jaw,seal};Array.Copy(v,p.v,Count);return p;
        }
        // SAPI viseme ids 0-21. Columns: open round wide tight tongueUp affricate tongueOut dental tongueRaise
        // explosive | jaw seal | lip dominance, jaw dominance, anticipation (ms).
        static readonly Pose[] Poses={
            P(0,0,0,0,0,0,0,0,0,0, 0,0, .45f,.5f),                   // 0  silence: lips meet, jaw rests
            P(.55f,0,.25f,0,0,0,0,0,0,0, .55f,0, .55f,.8f),          // 1  ae ax ah
            P(.75f,.05f,0,0,0,0,0,0,0,0, .75f,0, .6f,.9f),           // 2  aa
            P(.45f,.45f,0,0,0,0,0,0,0,0, .6f,.25f, .65f,.85f,70),    // 3  ao
            P(.35f,.05f,.35f,0,0,0,0,0,0,0, .42f,0, .55f,.75f),      // 4  eh ey uh
            P(.15f,.45f,0,.25f,0,.1f,0,0,0,0, .25f,.3f, .6f,.6f,70), // 5  er
            P(.1f,0,.75f,0,.1f,0,0,0,0,0, .22f,0, .6f,.7f),          // 6  y iy ih
            P(.05f,1,0,.1f,0,0,0,0,0,0, .18f,.85f, .85f,.6f,110),    // 7  w uw: rounding starts early
            P(.35f,.7f,0,0,0,0,0,0,0,0, .45f,.45f, .7f,.8f,90),      // 8  ow
            P(.65f,.25f,0,0,0,0,0,0,0,0, .65f,.1f, .6f,.85f),        // 9  aw
            P(.4f,.45f,.15f,0,0,0,0,0,0,0, .5f,.2f, .6f,.8f),        // 10 oy
            P(.65f,0,.2f,0,0,0,0,0,0,0, .65f,0, .6f,.85f),           // 11 ay
            P(.3f,0,.1f,0,0,0,0,0,0,0, .35f,0, .15f,.3f),            // 12 h: borrows the next vowel's lips
            P(.1f,.55f,0,.2f,.2f,0,0,0,0,0, .22f,.3f, .65f,.55f,80), // 13 r
            P(.25f,0,.15f,0,.8f,0,0,0,0,0, .32f,0, .25f,.5f),        // 14 l
            P(.05f,0,.35f,.45f,0,0,0,0,0,0, .1f,0, .45f,.7f),        // 15 s z: teeth together, lips a little spread
            P(.1f,.2f,0,0,0,.9f,0,0,0,0, .12f,0, .8f,.7f,60),        // 16 sh ch jh zh
            P(.2f,0,.1f,0,0,0,.8f,0,0,0, .22f,0, .35f,.6f),          // 17 th dh
            P(0,0,.05f,0,0,0,0,0,0,0, .08f,0, .3f,.8f),              // 18 f v: lip-to-teeth contact added in ApplyContacts
            P(.2f,0,.1f,.1f,.7f,0,0,0,0,0, .2f,0, .2f,.55f),         // 19 t d n: lips borrow the vowels
            P(.25f,0,.05f,0,0,0,0,0,.75f,0, .28f,0, .2f,.55f),       // 20 k g ng
            P(0,0,0,0,0,0,0,0,0,0, .02f,0, .15f,1),                  // 21 p b m: closure added in ApplyContacts (fast release)
        };

        readonly float[] weights=new float[Count],velocity=new float[Count],target=new float[Count],acc=new float[Count];
        Segment[] segments=Array.Empty<Segment>();
        float[] envelope=Array.Empty<float>();double hopMs=10,durationMs;
        readonly List<(double ms,float strength)> peaks=new List<(double,float)>();
        double lastMs=-1;float energy;

        public float Jaw=>weights[JawP];
        /// <summary>How strongly the lips stay together against the jaw gap (p/b/m, rounded vowels).</summary>
        public float Seal=>weights[SealP];
        public float Weight(int channel)=>weights[channel];
        /// <summary>0-1 stress pulses aligned with loud syllables (brows, nods).</summary>
        public float Emphasis {get;private set;}
        /// <summary>Smoothed speech loudness, 0-1.</summary>
        public float Energy=>energy;
        /// <summary>Inside a silence of at least 120 ms, or after the clip: a natural blink point.</summary>
        public bool InPause {get;private set;}=true;
        public double DurationMs=>durationMs;
        public int EmphasisPeakCount=>peaks.Count;

        public void SetCues(double[] positions,int[] visemes)=>SetCues(positions,visemes,null);
        public void SetCues(double[] positions,int[] visemes,double[] durations)
        {
            if(positions==null||visemes==null||positions.Length!=visemes.Length||(durations!=null&&durations.Length!=visemes.Length))throw new ArgumentException("Invalid cues");
            for(int i=0;i<positions.Length;i++)
                if(double.IsNaN(positions[i])||double.IsInfinity(positions[i])||positions[i]<0||(i>0&&positions[i]<positions[i-1])||visemes[i]<0||visemes[i]>21||
                   (durations!=null&&(double.IsNaN(durations[i])||double.IsInfinity(durations[i])||durations[i]<0||durations[i]>5000)))throw new ArgumentException("Invalid cue");
            var list=new Segment[positions.Length];double cursor=0;
            for(int i=0;i<positions.Length;i++) {
                // Speech reports both halves of a diphthong at one position; the durations place them in turn.
                double start=Math.Max(positions[i],cursor);
                double length=durations!=null?durations[i]:(i+1<positions.Length&&positions[i+1]>start?positions[i+1]-start:80);
                list[i]=new Segment{start=start,end=start+Math.Max(length,1),id=visemes[i],energy=.7f};cursor=list[i].end;
            }
            segments=list;durationMs=cursor;envelope=Array.Empty<float>();peaks.Clear();
            // A new clip restarts the clock but keeps the current mouth pose: no snap between sentences.
            lastMs=-1;
        }
        /// <summary>RMS envelope of the current clip (any scale) at a fixed hop; drives loudness and emphasis.</summary>
        public void SetEnvelope(float[] rms,double hop)
        {
            if(rms==null||hop<=0||double.IsNaN(hop)||double.IsInfinity(hop))throw new ArgumentException("Invalid envelope");
            var voiced=new List<float>();foreach(var r in rms)if(r>.004f&&!float.IsNaN(r)&&!float.IsInfinity(r))voiced.Add(r);
            voiced.Sort();float reference=voiced.Count>0?voiced[Math.Min(voiced.Count-1,(int)(voiced.Count*.92f))]:1;
            envelope=new float[rms.Length];hopMs=hop;
            for(int i=0;i<rms.Length;i++)envelope[i]=float.IsNaN(rms[i])||float.IsInfinity(rms[i])?0:Math.Min(1.3f,rms[i]/Math.Max(reference,1e-5f));
            ScoreSegments();FindPeaks();
        }
        public static float[] Envelope(float[] samples,int sampleRate,double hop)
        {
            int size=Math.Max(1,(int)(sampleRate*hop/1000));var result=new float[(samples.Length+size-1)/size];
            for(int f=0;f<result.Length;f++){double sum=0;int n=0;for(int i=f*size;i<Math.Min(samples.Length,(f+1)*size);i++){sum+=samples[i]*samples[i];n++;}result[f]=(float)Math.Sqrt(sum/Math.Max(1,n));}
            return result;
        }
        /// <summary>Immediate neutral (Stop/interrupt).</summary>
        public void Reset(){Array.Clear(weights,0,Count);Array.Clear(velocity,0,Count);lastMs=-1;energy=0;Emphasis=0;InPause=true;}

        public void Step(double ms,double dt,bool playing)
        {
            dt=Math.Max(0,Math.Min(.1,dt));
            if(!playing||segments.Length==0) {
                Integrate(-1,dt*1000,false);lastMs=-1;Emphasis=Decay(Emphasis,dt,.25);energy=Decay(energy,dt,.12);InPause=true;return;
            }
            // Follow the audio clock in fixed substeps; after a jump (new clip, seek, hitch) settle from the new position.
            double from=lastMs>=0&&ms>=lastMs&&ms-lastMs<=250?lastMs:ms-dt*1000;
            int steps=Math.Max(1,(int)Math.Ceiling((ms-from)/Substep-1e-9));double h=(ms-from)/steps;
            for(int s=1;s<=steps;s++)Integrate(from+h*s,h,true);
            lastMs=ms;
            Emphasis=EmphasisAt(ms);energy=EnergyAt(ms,energy,dt);InPause=PauseAt(ms);
        }
        static float Decay(float value,double dt,double tau)=>(float)(value*Math.Exp(-dt/tau));

        void Integrate(double ms,double hMs,bool speaking)
        {
            if(speaking)Evaluate(ms);else Array.Clear(target,0,Count);
            double h=hMs/1000;
            for(int k=0;k<Count;k++) {
                double hz=k==JawP?JawHz:(k==TongueUp||k==TongueOut||k==TongueRaise)?TongueHz:LipHz;
                if(!speaking)hz*=.55;   // the mouth settles more gently than it articulates
                double w=2*Math.PI*hz;
                // Exact critically damped step toward a constant target over h.
                double x=weights[k]-target[k],v=velocity[k],e=Math.Exp(-w*h);
                weights[k]=(float)(target[k]+(x+(v+w*x)*h)*e);velocity[k]=(float)((v-(v+w*x)*w*h)*e);
                if(weights[k]<0){weights[k]=0;if(velocity[k]<0)velocity[k]=0;}
            }
            if(speaking)ApplyContacts(ms);
        }

        // Dominance blend (Cohen-Massaro style): each phone pulls toward its pose with a strength that
        // falls off outside its interval; rounding and labials pull harder and earlier than tongue consonants.
        enum Group {Lips,Jaw,Tongue}
        static Group GroupOf(int k)=>k==JawP?Group.Jaw:(k==TongueUp||k==TongueOut||k==TongueRaise)?Group.Tongue:Group.Lips;
        void Evaluate(double ms)
        {
            Array.Clear(acc,0,Count);
            double lipW=EvaluateGroup(ms+LipLead,Group.Lips),jawW=EvaluateGroup(ms+JawLead,Group.Jaw),tongueW=EvaluateGroup(ms+LipLead,Group.Tongue);
            for(int k=0;k<Count;k++){var g=GroupOf(k);double w=g==Group.Jaw?jawW:g==Group.Tongue?tongueW:lipW;target[k]=w>1e-6?(float)(acc[k]/w):0;}
        }
        double EvaluateGroup(double t,Group group)
        {
            double total=0;
            // Implicit rest outside the clip: its pull grows with the distance from the nearest phone.
            double outside=Math.Max(segments[0].start-t,t-segments[segments.Length-1].end);
            if(outside>0)total+=(group==Group.Jaw?.5:.45)*(1-Math.Exp(-outside/Carryover));
            for(int i=Lower(t-320);i<segments.Length&&segments[i].start<t+320;i++) {
                var seg=segments[i];var pose=Poses[seg.id];
                double tau=t<seg.start?seg.start-t:(t>seg.end?t-seg.end:0);
                double dom=group==Group.Jaw?pose.jawDom:group==Group.Tongue?pose.tongueDom:pose.lipDom;
                double d=Math.Exp(-tau/(t<seg.start?pose.anticipation:Carryover))*dom;
                if(d<1e-5)continue;total+=d;
                if(group==Group.Jaw){acc[JawP]+=(float)(d*pose.v[JawP]*Clamp(.65f+.5f*seg.energy,.6f,1.15f));continue;}
                float louder=Clamp(.8f+.3f*seg.energy,.75f,1.1f);
                for(int k=0;k<Count;k++)if(GroupOf(k)==group)acc[k]+=(float)(d*pose.v[k]*(k==Open||k==Wide?louder:1));
            }
            return total;
        }
        int Lower(double t){int lo=0,hi=segments.Length;while(lo<hi){int m=(lo+hi)/2;if(segments[m].end<t)lo=m+1;else hi=m;}return lo;}

        // Hard contacts the blend alone cannot guarantee for fast phones.
        void ApplyContacts(double ms)
        {
            float closure=0,dental=0;double t=ms+12;
            for(int i=Lower(t-80);i<segments.Length&&segments[i].start<t+80;i++) {
                var seg=segments[i];if(seg.id!=21&&seg.id!=18)continue;
                double inMs=seg.id==21?60:50,outMs=seg.id==21?40:35;
                float c=t<seg.start?Smooth((float)(1-(seg.start-t)/inMs)):t>seg.end?Smooth((float)(1-(t-seg.end)/outMs)):1;
                if(seg.id==21)closure=Math.Max(closure,c);else dental=Math.Max(dental,c);
            }
            if(closure>0) {
                weights[Explosive]=Math.Max(weights[Explosive],closure);weights[SealP]=Math.Max(weights[SealP],closure);
                // Lips seal against a jaw that only partly closes (the rig's mouthClose covers the gap).
                weights[JawP]=Lerp(weights[JawP],Math.Min(weights[JawP],.15f),closure);
                foreach(int k in ClosureDamped)weights[k]*=1-.85f*closure;
            }
            if(dental>0) {
                weights[Dental]=Math.Max(weights[Dental],.95f*dental);
                weights[JawP]=Lerp(weights[JawP],Math.Min(weights[JawP],.15f),dental);
                weights[Open]*=1-.7f*dental;weights[Round]*=1-.5f*dental;
            }
        }

        void ScoreSegments()
        {
            if(envelope.Length==0)return;
            for(int i=0;i<segments.Length;i++) {
                int a=Math.Max(0,(int)(segments[i].start/hopMs)),b=Math.Min(envelope.Length,(int)Math.Ceiling(segments[i].end/hopMs));
                float sum=0;int n=0;for(int f=a;f<b;f++){sum+=envelope[f];n++;}
                segments[i].energy=n>0?Clamp(sum/n,0,1.2f):.7f;
            }
        }
        // Syllable peaks: prominent local maxima of the loudness, at least 280 ms apart.
        void FindPeaks()
        {
            peaks.Clear();int window=Math.Max(1,(int)(80/hopMs)),lookback=Math.Max(1,(int)(200/hopMs));double last=-1e9;
            for(int i=1;i<envelope.Length-1;i++) {
                float e=envelope[i];if(e<.45f)continue;bool max=true;
                for(int k=Math.Max(0,i-window);k<=Math.Min(envelope.Length-1,i+window)&&max;k++)if(envelope[k]>e)max=false;
                if(!max)continue;float floor=e;for(int k=Math.Max(0,i-lookback);k<i;k++)floor=Math.Min(floor,envelope[k]);
                if(e-floor<.18f)continue;double ms=i*hopMs;float strength=Clamp((e-.35f)/.5f,0,1);
                if(ms-last<280){if(peaks.Count>0&&strength>peaks[peaks.Count-1].strength){peaks[peaks.Count-1]=(ms,strength);last=ms;}continue;}
                peaks.Add((ms,strength));last=ms;
            }
        }
        float EmphasisAt(double ms)
        {
            float best=0;foreach(var p in peaks) {
                double d=ms-p.ms;if(d<-90||d>380)continue;
                float g=d<0?Smooth((float)(1+d/90)):(float)Math.Pow(1-d/380,2);best=Math.Max(best,g*p.strength);
            }
            return best;
        }
        float EnergyAt(double ms,float current,double dt)
        {
            float value;
            if(envelope.Length>0){int f=(int)(ms/hopMs);value=f>=0&&f<envelope.Length?Math.Min(1,envelope[f]):0;}
            else{int i=Lower(ms);value=i<segments.Length&&segments[i].start<=ms&&segments[i].id!=0?.7f:0;}
            double tau=value>current?.03:.12;return (float)(current+(value-current)*(1-Math.Exp(-dt/tau)));
        }
        bool PauseAt(double ms)
        {
            int i=Lower(ms);if(i>=segments.Length)return true;
            var s=segments[i];return s.start<=ms&&s.id==0&&s.end-s.start>=120;
        }
        static float Smooth(float x){x=Clamp(x,0,1);return x*x*(3-2*x);}
        static float Clamp(float x,float a,float b)=>x<a?a:x>b?b:x;
        static float Lerp(float a,float b,float t)=>a+(b-a)*t;
    }
}
