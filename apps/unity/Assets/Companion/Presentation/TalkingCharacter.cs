using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Companion.Core;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace Companion.Presentation
{
    // Local Editor evaluation: real local text inference + installed Windows speech.
    public sealed class TalkingCharacter : MonoBehaviour
    {
        public Transform character;
        public Camera portraitCamera;
        [Serializable] public class Cue { public float ms, durationMs; public int id; }
        [Serializable] public class Reply { public string text, emotion, pcm; public int sampleRate; public Cue[] cues; }
        [Serializable] class Session { public string url, token, model; }
        [Serializable] public class Message { public string role, content; }
        [Serializable] class Turn { public string message; public List<Message> history; }
        readonly Dictionary<string,List<(SkinnedMeshRenderer,int)>> shapes=new Dictionary<string,List<(SkinnedMeshRenderer,int)>>();
        readonly List<Message> history=new List<Message>();
        Transform jaw,head,leftArm,rightArm; Quaternion jawRest,headRest,leftRest,rightRest;
        AudioSource audioSource; AudioClip clip; RenderTexture texture;
        VisualElement root; ScrollView transcript; TextField input; Label stateLabel,replyLabel;
        Button send,stop,retry; UnityWebRequest request; Coroutine routine;
        Session session; Reply reply; string lastPrompt; float started,expressionWeight;
        readonly SpeechMouthMotion mouth=new SpeechMouthMotion();
        bool speaking,priorBackground;
        public string State {get;private set;}="Ready";
        public string LastResponse {get;private set;}="";
        public string Expression {get;private set;}="neutral";
        public bool IsSpeaking=>speaking;
        public int PlayedSamples=>audioSource==null||audioSource.clip==null?0:audioSource.timeSamples;
        public int CueCount=>reply?.cues?.Length??0;
        public float JawAngle=>jaw==null?0:Quaternion.Angle(jawRest,jaw.localRotation);
        public float ShapeWeight(string name)=>shapes.TryGetValue(name,out var list)?list[0].Item1.GetBlendShapeWeight(list[0].Item2):0;
        void OnEnable()
        {
            priorBackground=Application.runInBackground;Application.runInBackground=true;
            foreach(var t in character.GetComponentsInChildren<Transform>()) {if(t.name=="CC_Base_JawRoot")jaw=t;if(t.name=="CC_Base_Head")head=t;if(t.name=="CC_Base_L_Upperarm")leftArm=t;if(t.name=="CC_Base_R_Upperarm")rightArm=t;}
            jawRest=jaw.localRotation;headRest=head.localRotation;
            if(leftArm!=null){leftRest=leftArm.localRotation;leftArm.rotation=Quaternion.AngleAxis(65,Vector3.forward)*leftArm.rotation;}
            if(rightArm!=null){rightRest=rightArm.localRotation;rightArm.rotation=Quaternion.AngleAxis(-65,Vector3.forward)*rightArm.rotation;}
            foreach(var mesh in character.GetComponentsInChildren<SkinnedMeshRenderer>())for(int i=0;i<mesh.sharedMesh.blendShapeCount;i++) {
                string name=mesh.sharedMesh.GetBlendShapeName(i);if(!shapes.TryGetValue(name,out var list))shapes[name]=list=new List<(SkinnedMeshRenderer,int)>();list.Add((mesh,i));
            }
            audioSource=GetComponent<AudioSource>();audioSource.spatialBlend=0;audioSource.playOnAwake=false;audioSource.loop=false;
            texture=new RenderTexture(700,720,24);texture.Create();portraitCamera.targetTexture=texture;
            BuildUi();SetState("Ready — type a message");
            AudioSettings.OnAudioConfigurationChanged+=AudioChanged;
        }
        void SetState(string value) {State=value;if(stateLabel!=null)stateLabel.text=value;bool busy=speaking||request!=null;send?.SetEnabled(!busy);stop?.SetEnabled(busy);retry?.SetEnabled(!busy&&!string.IsNullOrEmpty(lastPrompt));}
        void Shape(string name,float value) {if(shapes.TryGetValue(name,out var list))foreach(var b in list)b.Item1.SetBlendShapeWeight(b.Item2,Mathf.Clamp01(value)*100);}
        void ResetFace() {foreach(var list in shapes.Values)foreach(var b in list)b.Item1.SetBlendShapeWeight(b.Item2,0);if(jaw!=null)jaw.localRotation=jawRest;if(head!=null)head.localRotation=headRest;}
        public void Submit(string text)
        {
            if(speaking||request!=null||string.IsNullOrWhiteSpace(text))return;
            if(text.Length>500){SetState("Please keep messages under 500 characters.");return;}
            lastPrompt=text;AddMessage("You",text);input?.SetValueWithoutNotify("");routine=StartCoroutine(Respond(text));
        }
        public void Retry() {if(!string.IsNullOrEmpty(lastPrompt))Submit(lastPrompt);}
        bool LoadSession()
        {
            try {
#if UNITY_EDITOR_WIN
                string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../artifacts/talking-character/session.json"));
                session=JsonUtility.FromJson<Session>(File.ReadAllText(path));
                var uri=new Uri(session.url);
                return uri.Scheme=="http"&&uri.Host=="127.0.0.1"&&session.token?.Length==64;
#else
                return false;
#endif
            }catch{return false;}
        }
        IEnumerator Respond(string text)
        {
            if(!LoadSession()){SetState("Local service unavailable. Use Companion > Start Local Talking Service, then Retry.");yield break;}
            request=new UnityWebRequest(session.url+"/turn","POST");request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Turn {message=text,history=history})));
            request.downloadHandler=new DownloadHandlerBuffer();request.SetRequestHeader("Content-Type","application/json");request.SetRequestHeader("Authorization","Bearer "+session.token);request.timeout=100;
            SetState("Thinking locally, then preparing speech…");
            yield return request.SendWebRequest();
            bool ok=request.result==UnityWebRequest.Result.Success;
            string body=ok?request.downloadHandler.text:null;long code=request.responseCode;request.Dispose();request=null;
            if(!ok){SetState("Local reply unavailable ("+code+"). Check Ollama/service, then Retry.");yield break;}
            if(!PrepareSpeech(body)){SetState("Invalid local speech response. Retry.");yield break;}
            LastResponse=reply.text;Expression=reply.emotion;replyLabel=AddMessage("Companion",reply.text);
            history.Add(new Message {role="user",content=text});while(history.Count>7)history.RemoveAt(0);
            speaking=true;started=Time.unscaledTime;audioSource.clip=clip;audioSource.Play();SetState("Speaking • "+Expression+" • local AI / Windows voice");routine=null;
        }
        bool PrepareSpeech(string body)
        {
            try {
                var next=JsonUtility.FromJson<Reply>(body);
                if(string.IsNullOrEmpty(next.text)||next.text.Length>600||next.sampleRate!=16000||next.cues==null||next.cues.Length>2000)return false;
                byte[] pcm=Convert.FromBase64String(next.pcm);if(pcm.Length<2||pcm.Length>16000*2*90||pcm.Length%2!=0)return false;
                float last=-1;foreach(var cue in next.cues){if(cue.ms<last||cue.ms<0||float.IsNaN(cue.ms)||float.IsInfinity(cue.ms)||cue.ms>pcm.Length/32f+100||cue.id<0||cue.id>21)return false;last=cue.ms;}
                var samples=new float[pcm.Length/2];for(int i=0;i<samples.Length;i++)samples[i]=(short)(pcm[i*2]|pcm[i*2+1]<<8)/32768f;
                var positions=new double[next.cues.Length];var ids=new int[next.cues.Length];for(int i=0;i<ids.Length;i++){positions[i]=next.cues[i].ms;ids[i]=next.cues[i].id;}mouth.SetCues(positions,ids);
                if(clip!=null)Destroy(clip);clip=AudioClip.Create("Local synthesized reply",samples.Length,1,next.sampleRate,false);clip.SetData(samples,0);reply=next;return true;
            }catch{return false;}
        }
        public void Interrupt()
        {
            if(request!=null){request.Abort();request.Dispose();request=null;}
            if(routine!=null){StopCoroutine(routine);routine=null;}
            if(speaking&&replyLabel!=null)replyLabel.text+="\n[Playback interrupted; reply omitted from next-turn context]";
            speaking=false;audioSource?.Stop();mouth.Reset();Expression="neutral";expressionWeight=0;ResetFace();SetState("Stopped — ready for another message");
        }
        void Update()
        {
            if(root==null)return;
            float scale=390f/Mathf.Max(1,Screen.width);var safe=Screen.safeArea;root.style.paddingTop=8+(Screen.height-safe.yMax)*scale;root.style.paddingBottom=8+safe.yMin*scale;
            ResetFace();
            float blinkPhase=Time.unscaledTime%4.3f;float blink=blinkPhase<.18f?Mathf.Sin(blinkPhase/.18f*Mathf.PI):0;
            Shape("Eye_Blink_L",blink);Shape("Eye_Blink_R",blink);Shape("C_BlinkL",1-Mathf.Abs(2*blink-1));Shape("C_BlinkR",1-Mathf.Abs(2*blink-1));
            head.localRotation=headRest*Quaternion.Euler(Mathf.Sin(Time.unscaledTime*.9f)*1.1f,Mathf.Sin(Time.unscaledTime*.6f)*1.8f,Mathf.Sin(Time.unscaledTime*.7f)*.7f);
            expressionWeight=Mathf.MoveTowards(expressionWeight,speaking?1:0,Time.unscaledDeltaTime*3);
            if(Expression=="happy") {Shape("Mouth_Corner_Pull_L",.48f*expressionWeight);Shape("Mouth_Corner_Pull_R",.48f*expressionWeight);Shape("Brow_Raise_Outer_L",.12f*expressionWeight);Shape("Brow_Raise_Outer_R",.12f*expressionWeight);}
            if(Expression=="concerned") {Shape("Brow_Raise_In_L",.45f*expressionWeight);Shape("Brow_Raise_In_R",.45f*expressionWeight);Shape("Mouth_Corner_Depress_L",.16f*expressionWeight);Shape("Mouth_Corner_Depress_R",.16f*expressionWeight);}
            if(Expression=="curious") {Shape("Brow_Raise_Outer_L",.5f*expressionWeight);Shape("Brow_Raise_Outer_R",.24f*expressionWeight);Shape("Eye_Widen_L",.12f*expressionWeight);Shape("Eye_Widen_R",.12f*expressionWeight);}
            if(speaking&&!audioSource.isPlaying&&Time.unscaledTime-started>.1f) {
                speaking=false;history.Add(new Message {role="assistant",content=reply.text});while(history.Count>8)history.RemoveAt(0);SetState("Ready — reply finished");
            }
            double ms=speaking?audioSource.timeSamples*1000.0/clip.frequency:0;
            mouth.Step(ms,Time.unscaledDeltaTime,speaking);
            for(int i=0;i<SpeechMouthMotion.Channels.Length;i++)Shape(SpeechMouthMotion.Channels[i],mouth.Weight(i));
            jaw.localRotation=jawRest*Quaternion.Euler(0,0,-16*mouth.Jaw);
        }
        void AudioChanged(bool changed)=>Interrupt();
        void OnApplicationPause(bool paused){if(paused)Interrupt();}
        void OnDisable()
        {
            AudioSettings.OnAudioConfigurationChanged-=AudioChanged;Interrupt();Application.runInBackground=priorBackground;
            if(leftArm!=null)leftArm.localRotation=leftRest;if(rightArm!=null)rightArm.localRotation=rightRest;
            if(portraitCamera!=null)portraitCamera.targetTexture=null;if(clip!=null)Destroy(clip);if(texture!=null){texture.Release();Destroy(texture);}root?.Clear();shapes.Clear();history.Clear();session=null;
        }
        Label AddMessage(string who,string text) {var label=Text(who+"\n"+text,15);label.style.backgroundColor=who=="You"?new Color(.13f,.21f,.29f):new Color(.12f,.27f,.25f);label.style.paddingLeft=10;label.style.paddingRight=10;label.style.paddingTop=8;label.style.paddingBottom=8;transcript.Add(label);transcript.schedule.Execute(()=>transcript.ScrollTo(label));return label;}
        void BuildUi()
        {
            root=GetComponent<UIDocument>().rootVisualElement;root.Clear();root.style.flexGrow=1;root.style.height=Length.Percent(100);root.style.minHeight=0;root.style.backgroundColor=new Color(.035f,.055f,.085f);root.style.paddingLeft=12;root.style.paddingRight=12;
            root.Add(Text("Companion",23));root.Add(Text("LOCAL AI • WINDOWS SPEECH • ENGLISH PROTOTYPE",10));
            var image=new Image {image=texture,scaleMode=ScaleMode.ScaleToFit,name="talking-character"};image.style.height=Length.Percent(40);image.style.minHeight=120;image.style.flexShrink=0;root.Add(image);
            stateLabel=Text("Ready",12);stateLabel.name="talking-status";stateLabel.style.minHeight=32;root.Add(stateLabel);
            transcript=new ScrollView {name="conversation"};transcript.style.flexBasis=0;transcript.style.flexGrow=1;transcript.style.flexShrink=1;transcript.style.minHeight=0;root.Add(transcript);AddMessage("Companion","Hi! Type a message below. My replies run on this PC. Try sharing good news or telling me about your day.");
            var prompts=new VisualElement();prompts.style.flexDirection=FlexDirection.Row;prompts.style.flexShrink=0;
            prompts.Add(MakeButton("Good news",()=>Submit("I got a new job today!")));prompts.Add(MakeButton("Rough day",()=>Submit("I had a difficult day and could use a kind word.")));prompts.Add(MakeButton("Tell a story",()=>Submit("Tell me a very short cheerful story.")));root.Add(prompts);
            input=new TextField {name="message-input",maxLength=500};input.style.minHeight=40;input.style.flexShrink=0;input.style.fontSize=16;input.tooltip="Type a message, then Send";input.RegisterCallback<KeyDownEvent>(e=>{if(e.keyCode==KeyCode.Return){Submit(input.value);e.StopPropagation();}});root.Add(input);
            var actions=new VisualElement();actions.name="chat-actions";actions.style.flexDirection=FlexDirection.Row;actions.style.flexShrink=0;send=MakeButton("Send",()=>Submit(input.value));stop=MakeButton("Stop",Interrupt);retry=MakeButton("Retry",Retry);actions.Add(send);actions.Add(stop);actions.Add(retry);root.Add(actions);
        }
        static Label Text(string text,int size){var label=new Label(text);label.style.flexShrink=0;label.style.fontSize=size;label.style.color=new Color(.87f,.95f,.94f);label.style.whiteSpace=WhiteSpace.Normal;label.style.marginBottom=5;return label;}
        static Button MakeButton(string text,Action action){var b=new Button(action){text=text};b.style.minHeight=42;b.style.flexGrow=1;b.style.marginBottom=5;b.style.backgroundColor=new Color(.16f,.29f,.34f);b.style.color=Color.white;return b;}
    }
}
