using System;
using System.Collections.Generic;
using Companion.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Companion.Presentation
{
    // Development-only native CC profile, deliberately not a calibrated canonical-52 adapter.
    public sealed class CCCharacterLab : MonoBehaviour
    {
        public Transform character;
        public Camera portraitCamera;
        public Vector3 jawOpenEuler=new Vector3(0,0,-16);
        public static readonly string[] Channels={"Eye_Blink_L","Eye_Blink_R","Brow_Down_L","Brow_Down_R","Brow_Raise_In_L","Brow_Raise_In_R","Brow_Raise_Outer_L","Brow_Raise_Outer_R","Eye_Widen_L","Eye_Widen_R","Mouth_Corner_Pull_L","Mouth_Corner_Pull_R","Mouth_Corner_Depress_L","Mouth_Corner_Depress_R","Mouth_Stretch_L","Mouth_Stretch_R","Mouth_Left","Mouth_Right","Jaw_Open","V_Open","V_Explosive","V_Dental_Lip","V_Tight_O","V_Tight","V_Wide","V_Affricate","V_Lip_Open","V_Tongue_up","V_Tongue_Out"};
        static readonly string[] Cues={"","V_Explosive","V_Dental_Lip","V_Tongue_Out","V_Tongue_up","V_Tongue_Raise","V_Affricate","V_Tight","V_Tongue_up","V_Tight_O","V_Open","V_Wide","V_Tight","V_Tight_O","V_Tight_O"};
        readonly Dictionary<string,List<(SkinnedMeshRenderer renderer,int index)>> bindings=new Dictionary<string,List<(SkinnedMeshRenderer,int)>>();
        SkinnedMeshRenderer[] renderers;
        Transform jaw;Quaternion jawRest;
        AudioSource source;AudioClip clip;RenderTexture texture;
        VisualElement root;Label status;DropdownField channel;Slider weight;Toggle assist;
        Slider jawCalibration;float jawDegrees;
        [Serializable] public struct PlaybackReport
        {
            public string utteranceId,terminal;
            public long epoch;
            public int observedSamples,sampleRate,durationSamples;
            public double ObservedMilliseconds=>observedSamples*1000.0/sampleRate;
        }
        string utteranceId;long playbackEpoch;int observedSamples;Label playbackStatus;
        public PlaybackReport LastPlayback {get;private set;}
        void ObservePlayback()
        {
            if(playing&&source!=null&&clip!=null)observedSamples=Math.Max(observedSamples,Mathf.Clamp(source.timeSamples,0,clip.samples));
        }
        void FinishPlayback(string terminal)
        {
            if(!playing)return;
            ObservePlayback();
            LastPlayback=new PlaybackReport {utteranceId=utteranceId,epoch=playbackEpoch,observedSamples=observedSamples,sampleRate=clip.frequency,durationSamples=clip.samples,terminal=terminal};
            playing=false;
            if(playbackStatus!=null)playbackStatus.text="Synthetic playback observed: "+LastPlayback.ObservedMilliseconds.ToString("0")+" ms • "+terminal+" • not proof of hearing";
        }
        bool priorBackground,playing;int sweep=-1;float nextSweep;
        public long Epoch {get;private set;}
        public bool Playing=>playing;
        public bool Ready=>bindings.Count>0&&jaw!=null;
        public string Status=>status?.text??"";
        public int BindingCount(string name)=>bindings.TryGetValue(name,out var list)?list.Count:0;
        public float JawAngle=>jaw==null?0:Quaternion.Angle(jawRest,jaw.localRotation);
        public float JawCalibrationDegrees=>jawDegrees;
        public bool AtRest {get {if(JawAngle>.01f)return false;foreach(var r in renderers)for(int i=0;i<r.sharedMesh.blendShapeCount;i++)if(Mathf.Abs(r.GetBlendShapeWeight(i))>.001f)return false;return true;}}
        void OnEnable()
        {
            priorBackground=Application.runInBackground;Application.runInBackground=true;
            AudioSettings.OnAudioConfigurationChanged+=AudioChanged;
            renderers=character.GetComponentsInChildren<SkinnedMeshRenderer>();bindings.Clear();
            foreach(var t in character.GetComponentsInChildren<Transform>())if(t.name=="CC_Base_JawRoot")jaw=t;
            if(jaw!=null)jawRest=jaw.localRotation;
            jawDegrees=Mathf.Clamp(jawOpenEuler.magnitude,0,30);
            foreach(var r in renderers)for(int i=0;i<r.sharedMesh.blendShapeCount;i++) {
                var name=r.sharedMesh.GetBlendShapeName(i);if(TalkingCharacter.ExpressionAliases.TryGetValue(name,out var alias)&&r.sharedMesh.GetBlendShapeIndex(alias)<0)name=alias;if(!bindings.TryGetValue(name,out var list))bindings[name]=list=new List<(SkinnedMeshRenderer,int)>();list.Add((r,i));
            }
            source=GetComponent<AudioSource>();var pcm=new SyntheticVoiceAgent().Tone();
            clip=AudioClip.Create("CC test tone - NOT SPEECH",pcm.Length,1,SyntheticVoiceAgent.SampleRate,false);clip.SetData(pcm,0);source.clip=clip;source.loop=false;source.playOnAwake=false;
            texture=new RenderTexture(600,650,24);texture.Create();portraitCamera.targetTexture=texture;
            BuildUi();Stop("Ready • local character test");
        }
        void OnDisable()
        {
            AudioSettings.OnAudioConfigurationChanged-=AudioChanged;Stop("Stopped");Application.runInBackground=priorBackground;
            if(portraitCamera!=null)portraitCamera.targetTexture=null;if(source!=null)source.clip=null;
            if(clip!=null)Destroy(clip);if(texture!=null){texture.Release();Destroy(texture);}root?.Clear();
        }
        void OnApplicationPause(bool paused){if(paused)Stop("Backgrounded • reset");}
        void AudioChanged(bool changed){Stop("Audio route changed • reset");}
        void Neutral()
        {
            if(renderers!=null)foreach(var r in renderers)for(int i=0;i<r.sharedMesh.blendShapeCount;i++)r.SetBlendShapeWeight(i,0);
            if(jaw!=null)jaw.localRotation=jawRest;
        }
        bool Apply(string name,float value)
        {
            if(!bindings.TryGetValue(name,out var list))return false;
            foreach(var b in list)b.renderer.SetBlendShapeWeight(b.index,value*100);
            // femaleCC.json Constraint/BlinkL and BlinkR: linear (0,0), (.5,1), (1,0).
            // Only these single-source Add correctives are implemented, not the full CC solver.
            if(name=="Eye_Blink_L")Apply("C_BlinkL",1-Mathf.Abs(2*value-1));
            if(name=="Eye_Blink_R")Apply("C_BlinkR",1-Mathf.Abs(2*value-1));
            if((name=="V_Open"||name=="Jaw_Open")&&(assist==null||assist.value))ApplyJaw(value);
            return true;
        }
        void ApplyJaw(float value)
        {
            if(jaw!=null)jaw.localRotation=jawRest*Quaternion.AngleAxis(jawDegrees*value,jawOpenEuler.sqrMagnitude>0?jawOpenEuler.normalized:Vector3.back);
        }
        // Session-only comparison controls; these do not claim to convert CC expression bones.
        public void SetJawCalibration(float degrees)
        {
            if(float.IsNaN(degrees)||float.IsInfinity(degrees)||degrees<0||degrees>30)throw new ArgumentOutOfRangeException(nameof(degrees));
            Stop("Jaw angle changed • neutral • provisional");jawDegrees=degrees;jawCalibration?.SetValueWithoutNotify(degrees);
        }
        public void PreviewJaw(float value)
        {
            if(!FacialProfile.ValidWeight(value))throw new ArgumentOutOfRangeException(nameof(value));
            Stop();ApplyJaw(value);status.text="Bone only • "+(jawDegrees*value).ToString("0.0")+"° • no facial morphs • provisional";
        }
        public void Stop(string message="Reset • neutral")
        {
            FinishPlayback("interrupted"); // Sample before AudioSource.Stop resets its cursor.
            source?.Stop();playing=false;sweep=-1;Epoch++;Neutral();weight?.SetValueWithoutNotify(0);if(status!=null)status.text=message;
        }
        public void SetChannel(string name,float value)
        {
            if(!FacialProfile.ValidWeight(value))throw new ArgumentOutOfRangeException(nameof(value));
            Stop();if(!Apply(name,value))throw new ArgumentException("Missing native channel: "+name);
            channel?.SetValueWithoutNotify(name);weight?.SetValueWithoutNotify(value);status.text=name+" • "+value.ToString("0.00")+" • "+BindingCount(name)+" meshes";
        }
        public void Pose(string name)
        {
            Stop();switch(name) {
                case "Blink":Apply("Eye_Blink_L",1);Apply("Eye_Blink_R",1);break;
                case "Half blink":Apply("Eye_Blink_L",.5f);Apply("Eye_Blink_R",.5f);break;
                case "Smile":Apply("Mouth_Corner_Pull_L",.65f);Apply("Mouth_Corner_Pull_R",.65f);break;
                case "Frown":Apply("Mouth_Corner_Depress_L",.6f);Apply("Mouth_Corner_Depress_R",.6f);break;
                case "Open":Apply("V_Open",.7f);break;
                default:throw new ArgumentException("Unknown pose");
            }status.text=name+" • native CC test pose";
        }
        public void PlayTone(){Stop();utteranceId=Guid.NewGuid().ToString();playbackEpoch=Epoch;observedSamples=0;source.timeSamples=0;playing=true;source.Play();status.text="Starting synthetic tone";}
        public void Sweep(){Stop();sweep=0;nextSweep=Time.unscaledTime;}
        void Update()
        {
            if(root!=null) {float scale=390f/Mathf.Max(1,Screen.width);var safe=Screen.safeArea;root.style.paddingTop=8+(Screen.height-safe.yMax)*scale;root.style.paddingBottom=8+safe.yMin*scale;}
            if(sweep>=0&&Time.unscaledTime>=nextSweep) {
                if(sweep>=Channels.Length){Stop("Sweep complete • neutral");return;}
                int next=sweep+1;SetChannel(Channels[sweep],.7f);sweep=next;nextSweep=Time.unscaledTime+.65f;
            }
            if(!playing)return;
            ObservePlayback();
            // A stopped source may have reset to zero; retain the observed high-water mark.
            // Do not infer that the whole clip was heard from isPlaying=false.
            if(!source.isPlaying){FinishPlayback("source_stopped");Stop("Tone stopped • neutral");return;}
            double ms=source.timeSamples*1000.0/clip.frequency;int slot=Math.Min(14,(int)(ms/250));double local=ms-slot*250;
            float envelope=(float)Math.Max(0,Math.Min(1,Math.Min(local/15,(220-local)/15)));
            Neutral();if(slot>0)Apply(Cues[slot],envelope*.65f);
            status.text="Synthetic cue: "+FacialProfile.Visemes[slot]+" • not speech";
        }
        void BuildUi()
        {
            root=GetComponent<UIDocument>().rootVisualElement;root.Clear();root.style.flexGrow=1;root.style.backgroundColor=new Color(.035f,.055f,.085f);root.style.paddingLeft=12;root.style.paddingRight=12;
            root.Add(Text("CHARACTER TEST LAB",20));root.Add(Text("CC CHARACTER • SYNTHETIC AUDIO • NO MIC",11));
            var image=new Image {image=texture,scaleMode=ScaleMode.ScaleToFit};image.name="character-preview";image.style.height=Length.Percent(40);image.style.minHeight=150;image.style.flexShrink=1;root.Add(image);
            status=Text("Ready",12);status.name="character-status";status.style.minHeight=32;root.Add(status);
            var scroll=new ScrollView();scroll.style.flexGrow=1;scroll.style.minHeight=0;root.Add(scroll);
            var row=new VisualElement();row.style.flexDirection=FlexDirection.Row;foreach(var p in new[]{"Blink","Smile","Frown","Open"})row.Add(Button(p,()=>Pose(p)));scroll.Add(row);
            scroll.Add(Button("Half blink • exported corrective curves",()=>Pose("Half blink")));
            channel=new DropdownField("Native facial channel",new List<string>(Channels),0);channel.style.color=Color.white;scroll.Add(channel);
            weight=new Slider("Weight",0,1);weight.style.minHeight=44;weight.style.color=Color.white;scroll.Add(weight);
            channel.RegisterValueChangedCallback(e=>SetChannel(e.newValue,weight.value));weight.RegisterValueChangedCallback(e=>SetChannel(channel.value,e.newValue));
            assist=new Toggle("Prototype jaw-bone assist"){value=true};assist.style.color=Color.white;assist.style.minHeight=32;assist.RegisterValueChangedCallback(_=>Stop());scroll.Add(assist);
            jawCalibration=new Slider("Provisional jaw angle (degrees)",0,30){value=jawDegrees,showInputField=true};jawCalibration.style.minHeight=44;jawCalibration.style.color=Color.white;
            jawCalibration.RegisterValueChangedCallback(e=>SetJawCalibration(e.newValue));scroll.Add(jawCalibration);
            scroll.Add(Button("Preview jaw bone only • 70%",()=>PreviewJaw(.7f)));
            scroll.Add(Button("Restore jaw angle • 16°",()=>SetJawCalibration(16)));
            scroll.Add(Button("Sweep native channels",Sweep));scroll.Add(Button("Play synthetic tone + mouth cues",PlayTone));
            playbackStatus=Text("Playback report: none • local synthetic diagnostic",12);playbackStatus.name="playback-report";scroll.Add(playbackStatus);
            scroll.Add(Text("Native CC mappings are provisional. Jaw assist is approximate; full expression-bone conversion, 52-channel calibration and mobile optimization remain pending. No real voice or microphone.",12));
            var stop=Button("Interrupt / reset to neutral",()=>Stop());stop.name="character-reset";root.Add(stop);
        }
        static Label Text(string value,int size){var l=new Label(value);l.style.fontSize=size;l.style.color=new Color(.83f,.94f,.91f);l.style.whiteSpace=WhiteSpace.Normal;l.style.marginBottom=5;return l;}
        static Button Button(string text,Action action){var b=new Button(action){text=text};b.style.minHeight=44;b.style.flexShrink=0;b.style.flexGrow=1;b.style.marginBottom=5;b.style.backgroundColor=new Color(.16f,.27f,.33f);b.style.color=Color.white;return b;}
    }
}
