using System;
using Companion.Core;
using UnityEngine;

namespace Companion.Presentation
{
    // Per-character facial calibration. Defaults suit Alita's CC5 morphs; the Tripo rigs set their own.
    [Serializable] public sealed class FaceTuning
    {
        [Tooltip("Jaw rotation in degrees at full articulation (an open 'aa' at full loudness).")] public float jawDegrees=9.5f;
        public float openGain=.75f,roundGain=.8f,wideGain=.7f,tightGain=.5f,affricateGain=.55f,dentalGain=.7f,explosiveGain=.8f,tongueGain=.6f;
        [Tooltip("ARKit mouthClose against the jaw gap (rigs that have it): seals p/b/m and rounds 'oo'.")] public float sealGain=1f;
        public float expressionGain=1f,smileGain=1f,browGain=1f;
        [Tooltip("Speech head nods and sway (0 disables).")] public float headMotion=1f;
        public FaceTuning Clone()=>(FaceTuning)MemberwiseClone();
    }

    public struct FaceInput
    {
        public float time,dt;
        public bool speaking;      // a reply is being voiced, including short gaps between sentences
        public bool preparing;     // reply text is in, its voice has not started yet
        public bool thinking;      // waiting for the reply text
        public bool listening;     // the user is composing or recording
        public bool reduced;       // Reduce idle motion
        public string emotion;
        public bool question;      // the current sentence is a question
        public float yawn,glanceBlink;
    }

    // The face performance layer: maps speech articulation onto the rig and adds what makes a talking
    // face read as alive: emotion with onset and linger, Duchenne smiles, brow emphasis on stressed
    // syllables, question brows and tilt, speech-paced blinks, and small speech-driven head motion.
    // Writes through TalkingCharacter.Shape (batched per frame), so Stop/Reset stay immediate.
    public sealed class CompanionFace
    {
        readonly Transform model,head,jaw;readonly Quaternion jawRest;
        readonly Func<string,bool> has;readonly Action<string,float> set;readonly FaceTuning tuning;
        readonly System.Random random;
        readonly string smileL,smileR,frownL,frownR,browInL,browInR,browOutL,browOutR,browDownL,browDownR,wideL,wideR,
            cheekL,cheekR,squintL,squintR,pressL,pressR,dimpleL,dimpleR,seal;
        readonly bool squintFallback;
        readonly float[] emotions=new float[4];static readonly string[] EmotionNames={"neutral","happy","concerned","curious"};
        float linger,lingerAge=99,attentive,thinkingWeight,questionWeight,speechMotion,blinkAt=-1,nextBlink,doubleBlinkAt=-1,blinkScale=1,lastBlink=-9;
        bool wasSpeaking,wasPause=true,sentenceStarted;float asideUntil=-1;Vector2 aside,asideTarget;
        public float Blink {get;private set;}
        public float JawDegrees {get;private set;}
        /// <summary>Eye/head look-away for the gaze layer (thinking, sentence onsets).</summary>
        public Vector2 GazeAside=>aside;
        public float EmotionWeight(string emotion){int i=Array.IndexOf(EmotionNames,emotion);return i<0?0:emotions[i];}

        public CompanionFace(Transform model,Transform head,Transform jaw,Quaternion jawRest,FaceTuning tuning,Func<string,bool> has,Action<string,float> set,int? seed=null)
        {
            this.model=model;this.head=head;this.jaw=jaw;this.jawRest=jawRest;this.tuning=tuning??new FaceTuning();this.has=has;this.set=set;
            random=new System.Random(seed??Guid.NewGuid().GetHashCode());
            string Pick(params string[] names){foreach(var n in names)if(has(n))return n;return null;}
            smileL=Pick("Mouth_Corner_Pull_L","mouthSmileLeft");smileR=Pick("Mouth_Corner_Pull_R","mouthSmileRight");
            frownL=Pick("Mouth_Corner_Depress_L","mouthFrownLeft");frownR=Pick("Mouth_Corner_Depress_R","mouthFrownRight");
            browInL=Pick("Brow_Raise_In_L");browInR=Pick("Brow_Raise_In_R");browOutL=Pick("Brow_Raise_Outer_L","browOuterUpLeft");browOutR=Pick("Brow_Raise_Outer_R","browOuterUpRight");
            browDownL=Pick("Brow_Down_L","Brow_Drop_L","browDownLeft");browDownR=Pick("Brow_Down_R","Brow_Drop_R","browDownRight");
            wideL=Pick("Eye_Widen_L","eyeWideLeft");wideR=Pick("Eye_Widen_R","eyeWideRight");
            cheekL=Pick("Cheek_Raise_L","cheekSquintLeft");cheekR=Pick("Cheek_Raise_R","cheekSquintRight");
            squintL=Pick("Eye_Squint_L","eyeSquintLeft");squintR=Pick("Eye_Squint_R","eyeSquintRight");
            pressL=Pick("Mouth_Press_L","mouthPressLeft");pressR=Pick("Mouth_Press_R","mouthPressRight");
            dimpleL=Pick("Mouth_Dimple_L","mouthDimpleLeft");dimpleR=Pick("Mouth_Dimple_R","mouthDimpleRight");
            seal=Pick("mouthClose","Mouth_Close");
            squintFallback=squintL==null;   // rigs without squint narrow the lids slightly through the blink
            nextBlink=Range(.8f,2.5f);
        }
        float Range(float a,float b)=>a+(float)random.NextDouble()*(b-a);
        static float Ease(float x){x=Mathf.Clamp01(x);return x*x*(3-2*x);}
        static float Approach(float value,float target,float dt,float rise,float fall)=>value+(target-value)*(1-Mathf.Exp(-dt/(target>value?rise:fall)));

        /// <summary>Immediate neutral (Stop): emotions, linger, blinks and head motion clear.</summary>
        public void Reset()
        {
            Array.Clear(emotions,0,emotions.Length);linger=attentive=thinkingWeight=questionWeight=speechMotion=0;lingerAge=99;
            blinkAt=doubleBlinkAt=-1;Blink=0;aside=asideTarget=Vector2.zero;asideUntil=-1;wasSpeaking=false;wasPause=true;JawDegrees=0;
        }
        /// <summary>A new sentence clip begins (blink and glance opportunity).</summary>
        public void OnSentenceStart(){sentenceStarted=true;}

        public void Step(in FaceInput input,SpeechMouthMotion mouth)
        {
            float dt=Mathf.Clamp(input.dt,0,.1f),t=input.time;
            frame.Clear();
            UpdateEmotion(input,dt);
            UpdateBlink(input,mouth,t,dt);
            UpdateAside(input,t,dt);
            float rounding=Mathf.Clamp01(mouth.Weight(1)+.7f*mouth.Weight(5));
            // --- speech articulation onto the rig ---
            gains[0]=tuning.openGain;gains[1]=tuning.roundGain;gains[2]=tuning.wideGain;gains[3]=tuning.tightGain;gains[4]=gains[6]=gains[8]=tuning.tongueGain;
            gains[5]=tuning.affricateGain;gains[7]=tuning.dentalGain;gains[9]=tuning.explosiveGain;
            for(int i=0;i<SpeechMouthMotion.Channels.Length;i++)Write(SpeechMouthMotion.Channels[i],mouth.Weight(i)*gains[i]);
            float jawDeg=mouth.Jaw*tuning.jawDegrees;
            if(input.yawn>0){jawDeg=Mathf.Max(jawDeg,input.yawn*.72f*16);Write("V_Open",Mathf.Max(mouth.Weight(0)*gains[0],input.yawn*.48f));}
            JawDegrees=jawDeg;
            if(seal!=null)Write(seal,mouth.Seal*tuning.sealGain*mouth.Jaw*tuning.jawDegrees/22f*(1-input.yawn));
            if(pressL!=null){float p=mouth.Weight(9)*.3f;Add(pressL,p);Add(pressR,p);}
            // --- emotion, attention and speech emphasis ---
            Expression(input,mouth,rounding);
            // --- blink (own schedule, gaze glances, yawn) ---
            float blink=Mathf.Max(Blink,Mathf.Max(input.glanceBlink,input.yawn*.85f));
            Write("Eye_Blink_L",blink);Write("Eye_Blink_R",blink);Write("C_BlinkL",blink);Write("C_BlinkR",blink);
            // --- bones: jaw, then speech head motion on top of gaze ---
            if(jaw!=null)jaw.localRotation=jawRest*Quaternion.Euler(0,0,-jawDeg);
            HeadMotion(input,mouth,t,dt);
            sentenceStarted=false;
        }
        readonly float[] gains=new float[10];

        void UpdateEmotion(in FaceInput input,float dt)
        {
            if(wasSpeaking&&!input.speaking){linger=1;lingerAge=0;}
            if(input.speaking){linger=0;lingerAge=99;}
            wasSpeaking=input.speaking;lingerAge+=dt;
            // After a reply the expression lingers, then softens away.
            float after=lingerAge<2.4f?.5f:Mathf.Lerp(.5f,0,(lingerAge-2.4f)/4.5f);
            float level=input.speaking?1:input.preparing?.55f:Mathf.Max(0,after*linger);
            int current=Mathf.Max(0,Array.IndexOf(EmotionNames,input.emotion??"neutral"));
            for(int i=0;i<emotions.Length;i++)emotions[i]=Approach(emotions[i],i==current?level:0,dt,.32f,.55f);
            attentive=Approach(attentive,input.listening&&!input.speaking?1:0,dt,.5f,.6f);
            thinkingWeight=Approach(thinkingWeight,input.thinking?1:0,dt,.45f,.35f);
            questionWeight=Approach(questionWeight,input.speaking&&input.question?1:0,dt,.6f,.5f);
            speechMotion=Approach(speechMotion,input.speaking&&!input.reduced?1:0,dt,.4f,.8f);
        }

        void UpdateBlink(in FaceInput input,SpeechMouthMotion mouth,float t,float dt)
        {
            bool pause=mouth.InPause;
            if(input.speaking) {
                // Blinks cluster at phrase boundaries and sentence onsets while talking.
                if(pause&&!wasPause&&t-lastBlink>.9f&&random.NextDouble()<.6)StartBlink(t,input.speaking,input.listening);
                if(sentenceStarted&&t-lastBlink>1.2f&&random.NextDouble()<.45)StartBlink(t+.05f,input.speaking,input.listening);
            }
            wasPause=pause;
            if(blinkAt<0&&t>=nextBlink)StartBlink(t,input.speaking,input.listening);
            if(doubleBlinkAt>0&&t>=doubleBlinkAt){doubleBlinkAt=-1;blinkAt=t;blinkScale=.85f;}
            Blink=0;
            if(blinkAt>=0) {
                // fast close, brief hold, slower open
                float a=t-blinkAt;const float close=.075f,hold=.03f,open=.16f;
                Blink=a<0?0:a<close?Ease(a/close):a<close+hold?1:a<close+hold+open?1-Ease((a-close-hold)/open):0;
                Blink*=blinkScale;
                if(a>=close+hold+open){blinkAt=-1;}
            }
        }

        void StartBlink(float at,bool speaking,bool listening)
        {
            if(blinkAt>=0)return;
            blinkAt=at;lastBlink=at;blinkScale=random.NextDouble()<.1?.65f:1;
            if(random.NextDouble()<.12)doubleBlinkAt=at+.42f;
            // Talking blinks come more often than attentive listening blinks.
            float min=speaking?1.6f:listening?2.6f:2.2f,max=speaking?4.2f:listening?5.6f:6.2f;
            nextBlink=at+Range(min,max);
        }

        void UpdateAside(in FaceInput input,float t,float dt)
        {
            // Thinking looks up and away; some sentences start with a brief glance aside.
            if(input.speaking&&SentenceStartGlance(t))asideTarget=new Vector2(Range(-7,7),Range(-3,1));
            Vector2 goal=input.thinking?new Vector2(-9,-6):(t<asideUntil?asideTarget:Vector2.zero);   // thinking looks up and away
            if(input.reduced)goal=Vector2.zero;
            aside=Vector2.Lerp(aside,goal,1-Mathf.Exp(-dt/(goal==Vector2.zero?.25f:.18f)));
        }
        bool SentenceStartGlance(float t)
        {
            if(!sentenceStarted||t<asideUntil+1.5f||random.NextDouble()>.35)return false;
            asideUntil=t+Range(.35f,.7f);return true;
        }

        void Expression(in FaceInput input,SpeechMouthMotion mouth,float rounding)
        {
            float g=tuning.expressionGain,neutral=emotions[0],happy=emotions[1],concerned=emotions[2],curious=emotions[3];
            float smile=(.5f*happy+.12f*curious+.07f*neutral+.1f*attentive)*tuning.smileGain*g;
            smile*=1-.65f*rounding;   // a smile cannot hold through rounded lips
            Add(smileL,smile);Add(smileR,smile);
            Add(cheekL,.35f*happy*g);Add(cheekR,.35f*happy*g);
            float squint=(.22f*happy+.08f*concerned)*g;
            if(squintFallback)Blink=Mathf.Max(Blink,squint*.4f);else{Add(squintL,squint);Add(squintR,squint);}
            Add(dimpleL,.12f*happy*g);Add(dimpleR,.12f*happy*g);
            float frown=.2f*concerned*g;Add(frownL,frown);Add(frownR,frown);
            Add(pressL,(.12f*concerned+.12f*thinkingWeight)*g);Add(pressR,(.12f*concerned+.12f*thinkingWeight)*g);
            // Brows: emotion, attention, stressed syllables and the end of a question.
            float emphasis=input.speaking?mouth.Emphasis:0;
            float b=tuning.browGain*g;
            float inner=(.55f*concerned+.18f*curious+.08f*attentive+.06f*neutral)*b+(.12f+.18f*concerned)*emphasis*tuning.browGain+.18f*questionWeight*tuning.browGain+.12f*input.yawn;
            float outerL=(.1f*happy+.48f*curious+.05f*attentive)*b+(.16f+.12f*(happy+curious))*emphasis*tuning.browGain+.2f*questionWeight*tuning.browGain;
            float outerR=(.1f*happy+.22f*curious+.05f*attentive)*b+(.16f+.12f*(happy+curious))*emphasis*tuning.browGain+.2f*questionWeight*tuning.browGain;
            Add(browInL,inner);Add(browInR,inner);Add(browOutL,outerL);Add(browOutR,outerR);
            float down=(.18f*concerned+.14f*thinkingWeight)*b;
            if(browDownL!=null){Add(browDownL,down);Add(browDownR,down);}
            float wide=(.16f*curious+.06f*attentive)*g;Add(wideL,wide);Add(wideR,wide);
        }

        void HeadMotion(in FaceInput input,SpeechMouthMotion mouth,float t,float dt)
        {
            if(head==null||input.reduced||tuning.headMotion<=0)return;
            float m=speechMotion*tuning.headMotion,energy=.55f+.45f*mouth.Energy;
            // Low-frequency sway while talking, a small nod on stressed syllables, tilt on questions/curiosity.
            float yaw=m*energy*(1.1f*Mathf.Sin(t*1.05f+.4f)+.6f*Mathf.Sin(t*2.3f+1.7f));
            float tilt=Mathf.Min(4f,2f*questionWeight+2.5f*emotions[3]+1.5f*thinkingWeight);   // curious/question/thinking tilt, capped
            float roll=m*(.8f*Mathf.Sin(t*.83f+2.1f)+.45f*Mathf.Sin(t*1.9f))+tilt*tuning.headMotion;
            // positive pitch nods down: a small beat on stressed syllables; thinking lifts the chin a little
            float pitch=m*(1.5f*(input.speaking?mouth.Emphasis:0)+.5f*Mathf.Sin(t*1.37f+.9f))-1.0f*thinkingWeight*tuning.headMotion;
            head.rotation=Quaternion.AngleAxis(pitch,model.right)*Quaternion.AngleAxis(yaw,model.up)*Quaternion.AngleAxis(roll,model.forward)*head.rotation;
        }

        void Write(string name,float value){if(name==null)return;frame[name]=value;set(name,value);}
        // Accumulates on top of whatever this frame already wrote to the channel (speech, other roles).
        readonly System.Collections.Generic.Dictionary<string,float> frame=new System.Collections.Generic.Dictionary<string,float>();
        void Add(string name,float value){if(name==null||value<=0)return;frame.TryGetValue(name,out var v);v+=value;frame[name]=v;set(name,v);}
    }
}
