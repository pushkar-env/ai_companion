using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Companion.Core;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;
using Unity.Profiling;

namespace Companion.Presentation
{
    // Local Editor evaluation: real local text inference + installed Windows speech.
    public sealed class TalkingCharacter : MonoBehaviour
    {
        public Transform character;
        [Serializable] public class CharacterOption { public string name; public Transform model; public float portraitDistance=1.25f; }
        public CharacterOption[] characters = Array.Empty<CharacterOption>();
        DropdownField characterPicker;
        public int SelectedCharacter {get;private set;}
        public string SelectedCharacterName=>characters.Length>SelectedCharacter?characters[SelectedCharacter].name:"Original";
        // CC5 exports use both traditional and extended expression names.
        public static readonly Dictionary<string,string> ExpressionAliases=new Dictionary<string,string> {
            {"Mouth_Smile_L","Mouth_Corner_Pull_L"},{"Mouth_Smile_R","Mouth_Corner_Pull_R"},
            {"Mouth_Frown_L","Mouth_Corner_Depress_L"},{"Mouth_Frown_R","Mouth_Corner_Depress_R"},
            {"Brow_Raise_Inner_L","Brow_Raise_In_L"},{"Brow_Raise_Inner_R","Brow_Raise_In_R"},
            {"Eye_Wide_L","Eye_Widen_L"},{"Eye_Wide_R","Eye_Widen_R"},
            {"Brow_Drop_L","Brow_Down_L"},{"Brow_Drop_R","Brow_Down_R"},{"Mouth_L","Mouth_Left"},{"Mouth_R","Mouth_Right"}
        };
        public static bool CanAnimate(Transform model)
        {
            if(model==null)return false;
            bool hasHead=false,hasJaw=false;var channels=new HashSet<string>();
            foreach(var t in model.GetComponentsInChildren<Transform>(true)){hasHead|=t.name=="CC_Base_Head";hasJaw|=t.name=="CC_Base_JawRoot";}
            foreach(var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))if(r.sharedMesh!=null)
                for(int i=0;i<r.sharedMesh.blendShapeCount;i++)channels.Add(r.sharedMesh.GetBlendShapeName(i));
            foreach(var c in SpeechMouthMotion.Channels)if(!channels.Contains(c))return false;
            return hasHead&&hasJaw;
        }
        void RestoreCharacter()
        {
            ResetFace();if(leftArm!=null)leftArm.localRotation=leftRest;if(rightArm!=null)rightArm.localRotation=rightRest;
            shapes.Clear();jaw=head=leftArm=rightArm=null;
        }
        void BindCharacter()
        {
            foreach(var t in character.GetComponentsInChildren<Transform>(true)) {if(t.name=="CC_Base_JawRoot")jaw=t;if(t.name=="CC_Base_Head")head=t;if(t.name=="CC_Base_L_Upperarm")leftArm=t;if(t.name=="CC_Base_R_Upperarm")rightArm=t;}
            jawRest=jaw.localRotation;headRest=head.localRotation;
            if(leftArm!=null){leftRest=leftArm.localRotation;leftArm.rotation=Quaternion.AngleAxis(65,Vector3.forward)*leftArm.rotation;}
            if(rightArm!=null){rightRest=rightArm.localRotation;rightArm.rotation=Quaternion.AngleAxis(-65,Vector3.forward)*rightArm.rotation;}
            foreach(var mesh in character.GetComponentsInChildren<SkinnedMeshRenderer>(true))if(mesh.sharedMesh!=null)
                for(int i=0;i<mesh.sharedMesh.blendShapeCount;i++) {
                    string name=mesh.sharedMesh.GetBlendShapeName(i);
                    if(ExpressionAliases.TryGetValue(name,out var alias)&&mesh.sharedMesh.GetBlendShapeIndex(alias)<0)name=alias;
                    if(!shapes.TryGetValue(name,out var list))shapes[name]=list=new List<ShapeBinding>();list.Add(new ShapeBinding {renderer=mesh,index=i,last=mesh.GetBlendShapeWeight(i)});
                }
            var focus=head.position+new Vector3(0,.035f,0);
            float distance=characters.Length>SelectedCharacter?characters[SelectedCharacter].portraitDistance:1.25f;
            portraitCamera.transform.position=focus+new Vector3(0,0,Mathf.Clamp(distance,.5f,3));portraitCamera.transform.LookAt(focus);
        }
        public bool SelectCharacter(int index)
        {
            if(index<0||index>=characters.Length||!CanAnimate(characters[index].model))return false;
            if(character==characters[index].model)return true;
            RestoreCharacter();character.gameObject.SetActive(false);
            character=characters[index].model;SelectedCharacter=index;character.gameObject.SetActive(true);BindCharacter();
            characterPicker?.SetValueWithoutNotify(SelectedCharacterName);
            // Appearance only: keep audio clock, microphone, requests, draft and context intact.
            return true;
        }
        public Camera portraitCamera;
        [Serializable] public class Cue { public float ms, durationMs; public int id; }
        [Serializable] public class Reply { public string type,text,emotion,pcm,error; public int sampleRate,sequence; public Cue[] cues; }
        [Serializable] class ServiceError {public string error;}
        string turnFailure;
        [Serializable] class Session { public string url, token, model; }
        [Serializable] public class Message { public string role, content; }
        [Serializable] class Turn { public string message; public List<Message> history; }
        sealed class ShapeBinding {public SkinnedMeshRenderer renderer;public int index;public float last,target;}
        readonly Dictionary<string,List<ShapeBinding>> shapes=new Dictionary<string,List<ShapeBinding>>();
        bool batchingFace;
        public int ShapeWritesLastFrame {get;private set;}
        readonly List<Message> history=new List<Message>();
        Transform jaw,head,leftArm,rightArm; Quaternion jawRest,headRest,leftRest,rightRest;
        AudioSource audioSource; AudioClip clip; RenderTexture texture;
        VisualElement root; ScrollView transcript; TextField input; Label stateLabel,replyLabel;
        Button send,stop,retry,replay; UnityWebRequest request; Coroutine routine;
        Session session; Reply reply; string lastPrompt; float started,expressionWeight;
        readonly SpeechMouthMotion mouth=new SpeechMouthMotion();
        readonly Queue<Reply> speechQueue=new Queue<Reply>();
        readonly List<Reply> replayAudio=new List<Reply>();
        bool replayReady,replaying;string replayEmotion;
        public bool CanReplay=>replayReady&&!speaking&&request==null&&!IsRecording;
        bool streamText,streamDone;float turnStarted;
        public int SpeechChunksReceived {get;private set;}
        public int SpeechChunksPlayed {get;private set;}
        public float FirstAudioSeconds {get;private set;}=-1;
        public float StreamFinishedSeconds {get;private set;}=-1;
        public float FirstTextSeconds {get;private set;}=-1;
        public float ReplyCompletedSeconds {get;private set;}=-1;
        // Main-thread observed inter-clip waiting; not acoustic/device latency.
        public float MaxSpeechGapSeconds {get;private set;}
        float speechGapStarted=-1;
        void ResetSpeechTimings(){FirstTextSeconds=FirstAudioSeconds=StreamFinishedSeconds=ReplyCompletedSeconds=-1;MaxSpeechGapSeconds=0;speechGapStarted=-1;}
        readonly LocalMicrophoneCapture microphone=new LocalMicrophoneCapture();
        Button record,refreshMicrophones;DropdownField microphoneDevice;
        MicrophoneSelection microphoneSelection;
        const string MicrophonePreference="Companion.LocalPrototype.Microphone";
        public string SelectedMicrophone=>microphoneSelection?.Selected??"";
        Button setup;UnityWebRequest setupRequest;Coroutine setupRoutine;
        [Serializable] class Readiness {public string ai,transcription;}
        public string SetupStatus {get;private set;}="Check setup";
        [Serializable] class RecordedAudio {public string pcm;public int sampleRate=MicrophonePcm.SampleRate;}
        [Serializable] class Transcript {public string text,mode,warning;public float confidence;public bool reviewRequired;}
        public bool IsRecording=>microphone.Recording;
        public string Draft=>input?.value??"";
        bool speaking,priorBackground;
        public string State {get;private set;}="Ready";
        public string LastResponse {get;private set;}="";
        public string Expression {get;private set;}="neutral";
        public bool IsSpeaking=>speaking;
        public int PlayedSamples=>audioSource==null||audioSource.clip==null?0:audioSource.timeSamples;
        public int CueCount=>reply?.cues?.Length??0;
        public float JawAngle=>jaw==null?0:Quaternion.Angle(jawRest,jaw.localRotation);
        public float ShapeWeight(string name)=>shapes.TryGetValue(name,out var list)?list[0].renderer.GetBlendShapeWeight(list[0].index):0;
        void OnEnable()
        {
            priorBackground=Application.runInBackground;Application.runInBackground=true;
            if(!CanAnimate(character)){Debug.LogError("Character is missing required speech controls.");enabled=false;return;}
            if(characters==null||characters.Length==0)characters=new[]{new CharacterOption{name="Original",model=character}};
            for(int i=0;i<characters.Length;i++)if(characters[i].model!=null){characters[i].model.gameObject.SetActive(characters[i].model==character);if(characters[i].model==character)SelectedCharacter=i;}
            BindCharacter();
            audioSource=GetComponent<AudioSource>();audioSource.spatialBlend=0;audioSource.playOnAwake=false;audioSource.loop=false;
            texture=new RenderTexture(700,720,24){antiAliasing=4};texture.Create();portraitCamera.targetTexture=texture;
            BuildUi();SetState("Ready — type a message");
            CheckSetup();
            AudioSettings.OnAudioConfigurationChanged+=AudioChanged;
        }
        void SetState(string value) {State=value;if(stateLabel!=null)stateLabel.text=value;bool busy=speaking||request!=null||IsRecording;send?.SetEnabled(!busy);stop?.SetEnabled(busy);retry?.SetEnabled(!busy&&!string.IsNullOrEmpty(lastPrompt));replay?.SetEnabled(CanReplay);record?.SetEnabled(request==null);microphoneDevice?.SetEnabled(!IsRecording&&request==null);refreshMicrophones?.SetEnabled(!IsRecording&&request==null);if(record!=null)record.text=IsRecording?"Finish & review":"Record voice";}
        void WriteShape(ShapeBinding binding)
        {
            if(binding.renderer==null||Mathf.Abs(binding.target-binding.last)<.001f)return;
            binding.renderer.SetBlendShapeWeight(binding.index,binding.target);binding.last=binding.target;ShapeWritesLastFrame++;
        }
        void Shape(string name,float value)
        {
            if(shapes.TryGetValue(name,out var list))foreach(var binding in list){binding.target=Mathf.Clamp01(value)*100;if(!batchingFace)WriteShape(binding);}
        }
        void FlushFace(){foreach(var list in shapes.Values)foreach(var binding in list)WriteShape(binding);}
        void ResetFace()
        {
            foreach(var list in shapes.Values)foreach(var binding in list){binding.target=0;if(!batchingFace)WriteShape(binding);}
            if(jaw!=null)jaw.localRotation=jawRest;if(head!=null)head.localRotation=headRest;
        }
        public void Submit(string text)
        {
            if(speaking||request!=null||IsRecording||string.IsNullOrWhiteSpace(text))return;
            if(text.Length>500){SetState("Please keep messages under 500 characters.");return;}
            ClearReplay();ResetSpeechTimings();lastPrompt=text;AddMessage("You",text);input?.SetValueWithoutNotify("");routine=StartCoroutine(Respond(text));
        }
        public void Retry() {if(!string.IsNullOrEmpty(lastPrompt))Submit(lastPrompt);}
        void ClearReplay(){replayAudio.Clear();replayReady=replaying=false;replayEmotion=null;}
        public void Replay()
        {
            if(!CanReplay)return;
            Interrupt();replaying=true;streamDone=true;Expression=replayEmotion;
            SpeechChunksPlayed=0;
            foreach(var frame in replayAudio)speechQueue.Enqueue(frame);
            StartNextSpeech();
        }
        public void CheckSetup()
        {
            if(setupRequest!=null)return;
            setupRoutine=StartCoroutine(CheckLocalSetup());
        }
        void SetSetup(string message){SetupStatus=message;if(setup!=null)setup.text="LOCAL ENGLISH • "+message;}
        IEnumerator CheckLocalSetup()
        {
            if(!LoadSession()){SetSetup("Start local service, then click to recheck");yield break;}
            // Separate request: a setup check must never cancel or overwrite a chat/draft.
            setupRequest=UnityWebRequest.Get(session.url+"/readiness");setupRequest.timeout=5;
            setupRequest.SetRequestHeader("Authorization","Bearer "+session.token);
            setup.SetEnabled(false);SetSetup("Checking local AI…");
            yield return setupRequest.SendWebRequest();
            bool ok=setupRequest.result==UnityWebRequest.Result.Success;
            string body=ok?setupRequest.downloadHandler.text:null;
            setupRequest.Dispose();setupRequest=null;setupRoutine=null;setup.SetEnabled(true);
            if(!ok){SetSetup("Local service unavailable • click to recheck");yield break;}
            Readiness result=null;try{result=JsonUtility.FromJson<Readiness>(body);}catch{}
            if(result?.ai=="engine_unavailable")SetSetup("Start Ollama • click to recheck");
            else if(result?.ai=="model_missing")SetSetup("AI model missing • check setup guide");
            else if(result?.ai=="ready")SetSetup("AI ready • "+(result.transcription=="whisper_configured"?"Whisper":"Windows recognition")+" configured");
            else SetSetup("Restart local service • click to recheck");
        }
        public void NewChat()
        {
            Interrupt();ClearReplay();
            history.Clear();lastPrompt=null;LastResponse="";reply=null;replyLabel=null;
            input?.SetValueWithoutNotify("");
            if(audioSource!=null)audioSource.clip=null;
            if(clip!=null){Destroy(clip);clip=null;}
            mouth.SetCues(Array.Empty<double>(),Array.Empty<int>());
            SpeechChunksReceived=SpeechChunksPlayed=0;ResetSpeechTimings();
            transcript?.Clear();
            AddMessage("Companion","New chat. Type a message or record your voice to begin.");
            SetState("Ready — new local chat");
        }
        public void ToggleRecording()
        {
            if(request!=null)return;
            if(IsRecording){var pcm=microphone.Finish();if(pcm==null)SetState(microphone.Error??"No recording");else TranscribePcm(pcm);return;}
            Interrupt();
            if(!microphone.Begin(SelectedMicrophone)){RefreshMicrophones();SetState(microphone.Error+" Use Refresh after reconnecting.");return;}
            SetState("Recording locally • finish to review • Stop discards");
        }
        public void RefreshMicrophones()
        {
            if(IsRecording||request!=null||microphoneSelection==null)return;
            microphoneSelection.Refresh(Microphone.devices);
            microphoneDevice.choices=microphoneSelection.Choices;
            microphoneDevice.SetValueWithoutNotify(microphoneSelection.Display);
        }
        // Also accepts generated PCM fixtures for tests without accessing a physical microphone.
        public void TranscribePcm(byte[] pcm)
        {
            if(request!=null||IsRecording||speaking)return;
            if(pcm==null||pcm.Length<8000||pcm.Length>640000||pcm.Length%2!=0){SetState("Recording must be 0.25–20 seconds.");return;}
            routine=StartCoroutine(Transcribe(pcm));
        }
        IEnumerator Transcribe(byte[] pcm)
        {
            if(!LoadSession()){Array.Clear(pcm,0,pcm.Length);SetState("Local service unavailable. Start it, then record again.");yield break;}
            byte[] body=Encoding.UTF8.GetBytes(JsonUtility.ToJson(new RecordedAudio {pcm=Convert.ToBase64String(pcm)}));Array.Clear(pcm,0,pcm.Length);
            request=new UnityWebRequest(session.url+"/transcribe","POST");request.uploadHandler=new UploadHandlerRaw(body);request.downloadHandler=new DownloadHandlerBuffer();request.timeout=35;
            request.SetRequestHeader("Content-Type","application/json");request.SetRequestHeader("Authorization","Bearer "+session.token);Array.Clear(body,0,body.Length);
            SetState("Transcribing locally • review before sending…");yield return request.SendWebRequest();
            bool ok=request.result==UnityWebRequest.Result.Success;string response=request.downloadHandler.text;
            string failure=null;if(!ok){ServiceError error=null;try{error=JsonUtility.FromJson<ServiceError>(response);}catch{}failure=error?.error!=null?LocalServiceFailure.Explain(error.error,true):LocalServiceFailure.FromHttp(request.responseCode,RequestTimedOut(request),true);}
            request.Dispose();request=null;routine=null;
            if(!ok){SetState(failure);yield break;}
            Transcript result=null;try{result=JsonUtility.FromJson<Transcript>(response);}catch{}
            if(result==null||!result.reviewRequired||result.text==null||result.text.Length>500){SetState("Invalid transcription. Record again or type instead.");yield break;}
            if(string.IsNullOrWhiteSpace(result.text)){SetState("No clear speech. Check selected microphone, move closer and record again.");yield break;}
            input.SetValueWithoutNotify(result.text);input.Focus();
            string engine=result.mode=="local-whisper-english"?"Whisper":"Windows";
            string guidance=result.warning=="quiet"?"Quiet recording — move closer or check microphone level.":result.warning=="clipped"?"Distorted recording — lower microphone gain.":result.warning=="uncertain"?"Some words are uncertain — edit or record again.":"Edit any mistakes, then Send.";
            SetState("Review • "+engine+" • "+guidance);
        }
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
            speechQueue.Clear();streamText=streamDone=false;turnFailure=null;SpeechChunksReceived=SpeechChunksPlayed=0;ResetSpeechTimings();turnStarted=Time.realtimeSinceStartup;replyLabel=null;
            request=new UnityWebRequest(session.url+"/turn-stream","POST");request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Turn {message=text,history=history})));
            var stream=new LocalSpeechStream();request.downloadHandler=stream;request.SetRequestHeader("Content-Type","application/json");request.SetRequestHeader("Authorization","Bearer "+session.token);request.timeout=100;
            SetState("Thinking locally…");var operation=request.SendWebRequest();
            while(!operation.isDone){if(!ReadSpeechFrames(stream)){FailStream();yield break;}yield return null;}
            bool framesOk=ReadSpeechFrames(stream);
            bool ok=request.result==UnityWebRequest.Result.Success&&framesOk&&streamDone;
            if(!ok&&turnFailure==null)turnFailure=LocalServiceFailure.FromHttp(request.responseCode,RequestTimedOut(request),false);
            request.Dispose();request=null;routine=null;
            if(!ok){FailStream();yield break;}
            StreamFinishedSeconds=Time.realtimeSinceStartup-turnStarted;
            // Playback may still be running; only Update finalizes context after the last clip.
        }
        bool ReadSpeechFrames(LocalSpeechStream stream)
        {
            if(stream.Invalid)return false;
            while(stream.TryRead(out var line)) {
                Reply frame;try{frame=JsonUtility.FromJson<Reply>(line);}catch{return false;}
                if(frame==null||streamDone)return false;
                if(frame.type=="error"){turnFailure=LocalServiceFailure.Explain(frame.error,false);return false;}
                if(frame.type=="text") {
                    if(streamText||string.IsNullOrWhiteSpace(frame.text)||frame.text.Length>600||Array.IndexOf(new[]{"neutral","happy","concerned","curious"},frame.emotion)<0)return false;
                    FirstTextSeconds=Time.realtimeSinceStartup-turnStarted;
                    streamText=true;LastResponse=frame.text;Expression=frame.emotion;replyLabel=AddMessage("Companion",frame.text);
                    SetState("Preparing the first spoken sentence…");
                } else if(frame.type=="audio") {
                    if(!streamText||frame.sequence!=SpeechChunksReceived||SpeechChunksReceived>=3||speechQueue.Count>=3)return false;
                    SpeechChunksReceived++;speechQueue.Enqueue(frame);
                } else if(frame.type=="done") {if(!streamText||SpeechChunksReceived==0||frame.sequence!=SpeechChunksReceived)return false;streamDone=true;}
                else return false;
            }
            return true;
        }
        static bool RequestTimedOut(UnityWebRequest value)=>value.error!=null&&(value.error.IndexOf("timeout",StringComparison.OrdinalIgnoreCase)>=0||value.error.IndexOf("timed out",StringComparison.OrdinalIgnoreCase)>=0);
        void FailStream(){string message=turnFailure??LocalServiceFailure.Explain(null,false);Interrupt();SetState(message);}
        bool StartNextSpeech()
        {
            if(speechQueue.Count==0)return false;
            var next=speechQueue.Dequeue();
            if(!PrepareSpeech(next)){FailStream();return false;}
            if(!replaying)replayAudio.Add(next);
            if(!replaying&&speechGapStarted>=0)MaxSpeechGapSeconds=Mathf.Max(MaxSpeechGapSeconds,Time.realtimeSinceStartup-speechGapStarted);
            speechGapStarted=-1;
            SpeechChunksPlayed++;if(FirstAudioSeconds<0)FirstAudioSeconds=Time.realtimeSinceStartup-turnStarted;
            speaking=true;started=Time.unscaledTime;audioSource.clip=clip;audioSource.Play();SetState((replaying?"Replaying":"Speaking")+" • "+Expression+" • sentence "+SpeechChunksPlayed);return true;
        }
        bool PrepareSpeech(Reply next)
        {
            try {
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
            microphone.Cancel();
            if(request!=null){request.Abort();request.Dispose();request=null;}
            if(routine!=null){StopCoroutine(routine);routine=null;}
            if(!replaying&&(speaking||speechQueue.Count>0||streamText&&!streamDone)&&replyLabel!=null)replyLabel.text+="\n[Playback interrupted; this exchange is omitted from next-turn context]";
            replaying=false;if(!replayReady)replayAudio.Clear();
            speechGapStarted=-1;
            speechQueue.Clear();streamText=streamDone=false;speaking=false;audioSource?.Stop();mouth.Reset();Expression="neutral";expressionWeight=0;ResetFace();SetState("Stopped — ready for another message");
        }
        static readonly ProfilerMarker UpdateMarker=new ProfilerMarker("Companion.CharacterUpdate");
        void Update()
        {
            using(UpdateMarker.Auto()) {
                ShapeWritesLastFrame=0;batchingFace=true;
                try{UpdateCharacter();}finally{batchingFace=false;FlushFace();}
            }
        }
        void UpdateCharacter()
        {
            if(root==null)return;
            if(IsRecording){microphone.Tick();if(!IsRecording)SetState(microphone.Error);else if(microphone.LimitReached)ToggleRecording();else SetState("Recording "+microphone.Seconds.ToString("0.0")+" / 20 s • level "+(microphone.Level*100).ToString("0")+"% • Stop discards");}
            float scale=390f/Mathf.Max(1,Screen.width);var safe=Screen.safeArea;root.style.paddingTop=8+(Screen.height-safe.yMax)*scale;root.style.paddingBottom=8+safe.yMin*scale;
            ResetFace();
            float blinkPhase=Time.unscaledTime%4.3f;float blink=blinkPhase<.18f?Mathf.Sin(blinkPhase/.18f*Mathf.PI):0;
            Shape("Eye_Blink_L",blink);Shape("Eye_Blink_R",blink);Shape("C_BlinkL",1-Mathf.Abs(2*blink-1));Shape("C_BlinkR",1-Mathf.Abs(2*blink-1));
            head.localRotation=headRest*Quaternion.Euler(Mathf.Sin(Time.unscaledTime*.9f)*1.1f,Mathf.Sin(Time.unscaledTime*.6f)*1.8f,Mathf.Sin(Time.unscaledTime*.7f)*.7f);
            expressionWeight=Mathf.MoveTowards(expressionWeight,speaking?1:0,Time.unscaledDeltaTime*3);
            if(Expression=="happy") {Shape("Mouth_Corner_Pull_L",.48f*expressionWeight);Shape("Mouth_Corner_Pull_R",.48f*expressionWeight);Shape("Brow_Raise_Outer_L",.12f*expressionWeight);Shape("Brow_Raise_Outer_R",.12f*expressionWeight);}
            if(Expression=="concerned") {Shape("Brow_Raise_In_L",.45f*expressionWeight);Shape("Brow_Raise_In_R",.45f*expressionWeight);Shape("Mouth_Corner_Depress_L",.16f*expressionWeight);Shape("Mouth_Corner_Depress_R",.16f*expressionWeight);}
            if(Expression=="curious") {Shape("Brow_Raise_Outer_L",.5f*expressionWeight);Shape("Brow_Raise_Outer_R",.24f*expressionWeight);Shape("Eye_Widen_L",.12f*expressionWeight);Shape("Eye_Widen_R",.12f*expressionWeight);}
            if(!speaking&&speechQueue.Count>0)StartNextSpeech();
            if(speaking&&!audioSource.isPlaying&&Time.unscaledTime-started>.1f) {
                if(!replaying&&speechGapStarted<0)speechGapStarted=Time.realtimeSinceStartup;
                if(speechQueue.Count>0)StartNextSpeech();
                else if(streamDone&&request==null){
                    speaking=false;
                    if(replaying){replaying=false;SetState("Ready — replay finished");}
                    else {ReplyCompletedSeconds=Time.realtimeSinceStartup-turnStarted;speechGapStarted=-1;history.Add(new Message {role="user",content=lastPrompt});history.Add(new Message {role="assistant",content=LastResponse});while(history.Count>8)history.RemoveRange(0,2);replayReady=true;replayEmotion=Expression;SetState("Ready — reply finished");}
                }
                else SetState("Preparing the next spoken sentence…");
            }
            bool playing=speaking&&audioSource.isPlaying;
            double ms=playing?audioSource.timeSamples*1000.0/clip.frequency:0;
            mouth.Step(ms,Time.unscaledDeltaTime,playing);
            for(int i=0;i<SpeechMouthMotion.Channels.Length;i++)Shape(SpeechMouthMotion.Channels[i],mouth.Weight(i));
            jaw.localRotation=jawRest*Quaternion.Euler(0,0,-16*mouth.Jaw);
        }
        void AudioChanged(bool changed)=>Interrupt();
        void OnApplicationPause(bool paused){if(paused)Interrupt();}
        void OnApplicationFocus(bool focused){if(!focused&&IsRecording)Interrupt();}
        void OnDisable()
        {
            if(setupRequest!=null){setupRequest.Abort();setupRequest.Dispose();setupRequest=null;}
            if(setupRoutine!=null){StopCoroutine(setupRoutine);setupRoutine=null;}
            AudioSettings.OnAudioConfigurationChanged-=AudioChanged;Interrupt();ClearReplay();Application.runInBackground=priorBackground;
            RestoreCharacter();
            if(portraitCamera!=null)portraitCamera.targetTexture=null;if(clip!=null)Destroy(clip);if(texture!=null){texture.Release();Destroy(texture);}root?.Clear();shapes.Clear();history.Clear();session=null;
        }
        Label AddMessage(string who,string text) {var label=Text(who+"\n"+text,15);label.style.backgroundColor=who=="You"?new Color(.13f,.21f,.29f):new Color(.12f,.27f,.25f);label.style.paddingLeft=10;label.style.paddingRight=10;label.style.paddingTop=8;label.style.paddingBottom=8;transcript.Add(label);transcript.schedule.Execute(()=>{if(transcript.Contains(label))transcript.ScrollTo(label);});return label;}
        void BuildUi()
        {
            root=GetComponent<UIDocument>().rootVisualElement;root.Clear();root.style.flexGrow=1;root.style.height=Length.Percent(100);root.style.minHeight=0;root.style.backgroundColor=new Color(.035f,.055f,.085f);root.style.paddingLeft=12;root.style.paddingRight=12;
            var header=new VisualElement();header.style.flexDirection=FlexDirection.Row;header.style.flexShrink=0;
            if(characters.Length>1) {
            characterPicker=new DropdownField {name="character-picker",choices=new List<string>()};
            foreach(var option in characters)characterPicker.choices.Add(option.name);
            characterPicker.SetValueWithoutNotify(SelectedCharacterName);characterPicker.style.flexGrow=1;characterPicker.style.flexBasis=0;characterPicker.style.minWidth=0;characterPicker.style.minHeight=42;characterPicker.style.fontSize=18;
            characterPicker.tooltip="Switch character appearance. Your chat, microphone and current voice stay the same.";
            characterPicker.RegisterValueChangedCallback(e=>{if(!SelectCharacter(characterPicker.choices.IndexOf(e.newValue)))characterPicker.SetValueWithoutNotify(SelectedCharacterName);});header.Add(characterPicker);
            } else {var title=Text(SelectedCharacterName,23);title.name="character-title";title.style.flexGrow=1;title.style.unityFontStyleAndWeight=FontStyle.Bold;header.Add(title);}
            var newChat=MakeButton("New chat",NewChat);newChat.name="new-chat";newChat.style.flexGrow=0;newChat.style.minWidth=88;
            newChat.tooltip="Clear this local chat and draft, and stop speech or recording.";header.Add(newChat);root.Add(header);
            setup=MakeButton("LOCAL ENGLISH • Check setup",CheckSetup);setup.name="check-setup";setup.style.fontSize=10;setup.style.minHeight=42;setup.style.whiteSpace=WhiteSpace.Normal;setup.style.flexGrow=0;setup.style.flexShrink=0;
            setup.tooltip="Click to recheck local AI and model availability. Recognition is configuration-only; microphone and speech playback are not tested.";root.Add(setup);
            var image=new Image {image=texture,scaleMode=ScaleMode.ScaleToFit,name="talking-character"};image.style.height=Length.Percent(40);image.style.minHeight=120;image.style.flexShrink=0;root.Add(image);
            stateLabel=Text("Ready",12);stateLabel.name="talking-status";stateLabel.style.minHeight=32;root.Add(stateLabel);
            transcript=new ScrollView {name="conversation"};transcript.style.flexBasis=0;transcript.style.flexGrow=1;transcript.style.flexShrink=1;transcript.style.minHeight=0;root.Add(transcript);AddMessage("Companion","Hi! Type a message below. My replies run on this PC. Try sharing good news or telling me about your day.");
            var prompts=new VisualElement();prompts.style.flexDirection=FlexDirection.Row;prompts.style.flexShrink=0;
            prompts.Add(MakeButton("Good news",()=>Submit("I got a new job today!")));prompts.Add(MakeButton("Rough day",()=>Submit("I had a difficult day and could use a kind word.")));prompts.Add(MakeButton("Tell a story",()=>Submit("Tell me a very short cheerful story.")));root.Add(prompts);
            input=new TextField {name="message-input",maxLength=500};input.style.minHeight=40;input.style.flexShrink=0;input.style.fontSize=16;input.tooltip="Type a message, then Send";StyleField(input);input.RegisterCallback<KeyDownEvent>(e=>{if(e.keyCode==KeyCode.Return){Submit(input.value);e.StopPropagation();}});root.Add(input);
            microphoneSelection=new MicrophoneSelection(PlayerPrefs.GetString(MicrophonePreference,""));
            var deviceRow=new VisualElement();deviceRow.style.flexDirection=FlexDirection.Row;deviceRow.style.flexShrink=0;deviceRow.name="microphone-row";
            microphoneDevice=new DropdownField {name="microphone-device"};microphoneDevice.style.flexGrow=1;microphoneDevice.style.minWidth=0;microphoneDevice.style.flexBasis=0;microphoneDevice.style.minHeight=42;
            StyleField(microphoneDevice);
            microphoneDevice.tooltip="Choose the actual input you want. Your choice is remembered on this machine.";
            microphoneDevice.RegisterValueChangedCallback(e=>{if(microphoneSelection.Select(e.newValue)){PlayerPrefs.SetString(MicrophonePreference,e.newValue);PlayerPrefs.Save();}});
            deviceRow.Add(microphoneDevice);
            refreshMicrophones=MakeButton("Refresh",RefreshMicrophones);refreshMicrophones.name="refresh-microphones";refreshMicrophones.style.flexGrow=0;refreshMicrophones.style.width=76;
            refreshMicrophones.tooltip="Find connected microphones without changing your selected input or opening it.";deviceRow.Add(refreshMicrophones);root.Add(deviceRow);RefreshMicrophones();
            record=MakeButton("Record voice",ToggleRecording);record.name="record-voice";record.style.flexGrow=0;record.style.flexShrink=0;record.style.height=42;record.tooltip="English, local recognition. Maximum 20 seconds. Review before Send; Stop discards.";root.Add(record);
            var actions=new VisualElement();actions.name="chat-actions";actions.style.flexDirection=FlexDirection.Row;actions.style.flexShrink=0;send=MakeButton("Send",()=>Submit(input.value));stop=MakeButton("Stop",Interrupt);retry=MakeButton("Retry",Retry);replay=MakeButton("Replay",Replay);replay.name="replay-reply";replay.tooltip="Play the last completed reply again without contacting the AI.";actions.Add(send);actions.Add(stop);actions.Add(retry);actions.Add(replay);root.Add(actions);
        }
        static void StyleField(VisualElement field)
        {
            var box=field.Q(className:"unity-base-field__input");if(box==null)return;
            box.style.backgroundColor=new Color(.10f,.17f,.22f);box.style.color=new Color(.90f,.95f,.96f);
            box.style.borderTopLeftRadius=6;box.style.borderTopRightRadius=6;box.style.borderBottomLeftRadius=6;box.style.borderBottomRightRadius=6;
        }
        static Label Text(string text,int size){var label=new Label(text);label.style.flexShrink=0;label.style.fontSize=size;label.style.color=new Color(.87f,.95f,.94f);label.style.whiteSpace=WhiteSpace.Normal;label.style.marginBottom=5;return label;}
        static Button MakeButton(string text,Action action){var b=new Button(action){text=text};b.style.minHeight=42;b.style.flexGrow=1;b.style.marginBottom=5;b.style.backgroundColor=new Color(.16f,.29f,.34f);b.style.color=Color.white;return b;}
    }
}
