using System;
using System.Collections.Generic;
using Companion.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Companion.Presentation
{
    public static class SyntheticRigFactory
    {
        public static Mesh Create()
        {
            var mesh=new Mesh {name="Synthetic52ChannelBoard_NotAnAvatar"};
            var vertices=new Vector3[52*4];var triangles=new int[52*6];
            for(int i=0;i<52;i++) {
                float x=(i%13-6)*.3f,y=(1.5f-i/13)*.4f;int v=i*4,t=i*6;
                vertices[v]=new Vector3(x-.10f,y-.1f,0);vertices[v+1]=new Vector3(x-.10f,y+.1f,0);
                vertices[v+2]=new Vector3(x+.10f,y+.1f,0);vertices[v+3]=new Vector3(x+.10f,y-.1f,0);
                triangles[t]=v;triangles[t+1]=v+1;triangles[t+2]=v+2;triangles[t+3]=v;triangles[t+4]=v+2;triangles[t+5]=v+3;
            }
            mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();
            for(int i=0;i<52;i++) {
                var delta=new Vector3[vertices.Length];delta[i*4+1]=new Vector3(0,.2f,0);delta[i*4+2]=new Vector3(0,.2f,0);
                mesh.AddBlendShapeFrame(FacialProfile.Channels[i],100,delta,new Vector3[vertices.Length],new Vector3[vertices.Length]);
            }
            mesh.bounds=new Bounds(Vector3.zero,new Vector3(4.2f,2.3f,1));return mesh;
        }
    }
    [RequireComponent(typeof(UIDocument),typeof(AudioSource))]
    public sealed class FacialLab : MonoBehaviour
    {
        public SkinnedMeshRenderer rig;
        public Camera rigCamera;
        public VisemePlayback Playback {get;}=new VisemePlayback();
        public bool Playing=>source!=null&&source.isPlaying;
        public bool Ready=>rig!=null&&rig.sharedMesh!=null&&rig.sharedMesh.blendShapeCount==52;
        readonly float[] weights=new float[52];
        AudioSource source;
        AudioClip clip;
        RenderTexture texture;
        VisualElement root;
        Label status,selected;
        DropdownField channels;
        Slider slider;
        int sweep=-1;
        float nextSweep;
        bool priorBackground;
        public string Status=>status==null?"":status.text;
        void OnEnable()
        {
            AudioSettings.OnAudioConfigurationChanged+=OnAudioConfigurationChanged;
            priorBackground=Application.runInBackground;Application.runInBackground=true;
            source=GetComponent<AudioSource>();source.playOnAwake=false;source.loop=false;source.spatialBlend=0;
            var pcm=new SyntheticVoiceAgent().Tone();clip=AudioClip.Create("Synthetic diagnostic tone - NOT SPEECH",pcm.Length,1,SyntheticVoiceAgent.SampleRate,false);clip.SetData(pcm,0);source.clip=clip;
            texture=new RenderTexture(600,300,24);texture.Create();rigCamera.targetTexture=texture;
            BuildUi();StopPlayback("Ready • synthetic diagnostic only");
        }
        void OnDisable()
        {
            AudioSettings.OnAudioConfigurationChanged-=OnAudioConfigurationChanged;
            StopPlayback("Session ended");Application.runInBackground=priorBackground;
            if(rigCamera!=null)rigCamera.targetTexture=null;
            if(source!=null)source.clip=null;
            if(clip!=null)Destroy(clip);if(texture!=null){texture.Release();Destroy(texture);}root?.Clear();
        }
        void OnApplicationPause(bool paused){if(paused)StopPlayback("Backgrounded • stopped and flushed");}
        void OnAudioConfigurationChanged(bool changed){StopPlayback("Audio route changed • stopped and flushed");}
        void BuildUi()
        {
            root=GetComponent<UIDocument>().rootVisualElement;root.Clear();root.style.flexGrow=1;root.style.backgroundColor=new Color(.035f,.055f,.085f);root.style.color=Color.white;
            root.style.paddingLeft=14;root.style.paddingRight=14;root.style.paddingTop=12;root.style.paddingBottom=12;
            root.Add(Label("FACIAL + VOICE LAB",20));root.Add(Label("M1 • SYNTHETIC / NO MICROPHONE",12));
            root.Add(Label("52 independent test pads. Not a face, rig calibration, speech, or production lip sync.",13));
            var image=new Image {image=texture,scaleMode=ScaleMode.ScaleToFit};image.style.height=170;image.style.flexShrink=0;root.Add(image);
            status=Label("Ready",13);status.name="lab-status";status.style.minHeight=42;root.Add(status);
            selected=Label("",14);root.Add(selected);
            channels=new DropdownField(new List<string>(FacialProfile.Channels),0);channels.name="channel-selector";channels.style.height=44;root.Add(channels);
            slider=new Slider(0,1);slider.name="channel-weight";slider.style.height=40;root.Add(slider);
            channels.RegisterValueChangedCallback(_=>SetChannel(channels.index,slider.value));
            slider.RegisterValueChangedCallback(e=>SetChannel(channels.index,e.newValue));
            var row=new VisualElement();row.style.flexDirection=FlexDirection.Row;root.Add(row);
            row.Add(Button("Previous",()=>channels.index=(channels.index+51)%52));row.Add(Button("Next",()=>channels.index=(channels.index+1)%52));
            root.Add(Button("Sweep all 52 channels",StartSweep));root.Add(Button("Play synthetic tone + visemes",PlayTone));
            root.Add(Button("Interrupt / reset",()=>StopPlayback("Interrupted • audio stopped, epoch advanced")));
            var notes=new ScrollView();notes.style.flexGrow=1;notes.style.minHeight=0;
            notes.Add(Label("The tone and 15 viseme cues share the same sample timeline. AudioSource.timeSamples drives the board; output-device latency is uncalibrated.\n\nNo provider, network, voice likeness, or microphone is used. Manual weights are normalized 0–1; Unity receives 0–100.",12));root.Add(notes);
            SetChannel(0,0);
        }
        static Label Label(string text,int size){var l=new Label(text);l.style.fontSize=size;l.style.color=new Color(.8f,.94f,.9f);l.style.whiteSpace=WhiteSpace.Normal;l.style.marginBottom=8;return l;}
        static Button Button(string text,Action action){var b=new Button(action){text=text};b.style.minHeight=44;b.style.flexShrink=0;b.style.flexGrow=0;b.style.marginBottom=6;b.style.backgroundColor=new Color(.16f,.27f,.33f);b.style.color=Color.white;return b;}
        public void SetChannel(int index,float weight)
        {
            if(index<0||index>=52||!FacialProfile.ValidWeight(weight))throw new ArgumentOutOfRangeException();
            if(Playing)StopPlayback("Manual channel mode");
            Array.Clear(weights,0,52);weights[index]=weight;ApplyWeights();
            channels?.SetValueWithoutNotify(FacialProfile.Channels[index]);slider?.SetValueWithoutNotify(weight);
            if(selected!=null)selected.text=(index+1)+" / 52  "+FacialProfile.Channels[index]+" = "+weight.ToString("0.00");
        }
        public void PlayTone()
        {
            StopPlayback("Playing synthetic tone — not speech");var epoch=Playback.Begin("synthetic-tone-v1");
            foreach(var frame in new SyntheticVoiceAgent().DiagnosticFrames())if(!Playback.Enqueue(epoch,Playback.Utterance,frame))throw new InvalidOperationException("Fixture rejected");
            source.timeSamples=0;source.Play();
        }
        public void StartSweep(){StopPlayback("Sweeping 52 synthetic channels");sweep=0;nextSweep=Time.unscaledTime;}
        public void StopPlayback(string message)
        {
            if(source!=null)source.Stop();Playback.Cancel();sweep=-1;Array.Clear(weights,0,52);ApplyWeights();if(status!=null)status.text=message;
            slider?.SetValueWithoutNotify(0);
            if(selected!=null&&channels!=null)selected.text=(channels.index+1)+" / 52  "+channels.value+" = 0.00";
        }
        void ApplyWeights(){if(rig==null||rig.sharedMesh==null)return;for(int i=0;i<Math.Min(52,rig.sharedMesh.blendShapeCount);i++)rig.SetBlendShapeWeight(i,weights[i]*100);}
        void Update()
        {
            if(sweep>=0&&Time.unscaledTime>=nextSweep){if(sweep==52){StopPlayback("Sweep complete • all channels returned to rest");return;}SetChannel(sweep++,1);nextSweep=Time.unscaledTime+.15f;}
            if(Playing){Playback.Sample(source.timeSamples,clip.frequency,weights);ApplyWeights();status.text="Synthetic tone • sample "+source.timeSamples+" • epoch "+Playback.Epoch;}
            else if(Playback.Active)StopPlayback("Tone complete • buffer flushed and channels at rest");
        }
    }
}
