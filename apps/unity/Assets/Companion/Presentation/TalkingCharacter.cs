using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Companion.Core;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;
using Unity.Profiling;

namespace Companion.Presentation
{
    // Local Editor evaluation: real local text inference + installed Windows speech.
    public sealed partial class TalkingCharacter : MonoBehaviour
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
            secondary?.ResetPose();secondary=null;
            gaze?.Dispose();gaze=null;
            wardrobe?.Dispose();wardrobe=null;
            bodyIdle?.Dispose();bodyIdle=null;
            ResetFace();if(leftArm!=null)leftArm.localRotation=leftRest;if(rightArm!=null)rightArm.localRotation=rightRest;
            shapes.Clear();jaw=head=leftArm=rightArm=null;
        }
        void BindCharacter()
        {
            foreach(var t in character.GetComponentsInChildren<Transform>(true)) {if(t.name=="CC_Base_JawRoot")jaw=t;if(t.name=="CC_Base_Head")head=t;if(t.name=="CC_Base_L_Upperarm")leftArm=t;if(t.name=="CC_Base_R_Upperarm")rightArm=t;}
            jawRest=jaw.localRotation;headRest=head.localRotation;
            if(leftArm!=null)leftRest=leftArm.localRotation;
            if(rightArm!=null)rightRest=rightArm.localRotation;
            // Optional hair/earring/cloth springs, bound from bind poses before any idle posing.
            secondary=character.GetComponent<CompanionSecondaryMotion>();
            if(secondary!=null&&!secondary.Bind())secondary=null;
            wardrobe=new CompanionWardrobe(character,true,AppearancePreference);
            gaze=new CompanionGaze(character);
            bodyIdle=new CompanionBodyIdle(character);
            if(!bodyIdle.IsBound)Debug.LogWarning("Full-body idle unavailable: character is missing required CC body bones.");
            foreach(var mesh in character.GetComponentsInChildren<SkinnedMeshRenderer>(true))if(mesh.sharedMesh!=null)
                for(int i=0;i<mesh.sharedMesh.blendShapeCount;i++) {
                    string name=mesh.sharedMesh.GetBlendShapeName(i);
                    if(ExpressionAliases.TryGetValue(name,out var alias)&&mesh.sharedMesh.GetBlendShapeIndex(alias)<0)name=alias;
                    if(!shapes.TryGetValue(name,out var list))shapes[name]=list=new List<ShapeBinding>();list.Add(new ShapeBinding {renderer=mesh,index=i,last=mesh.GetBlendShapeWeight(i)});
                }
            CacheBodyBounds();
        }
        public bool SelectCharacter(int index)
        {
            if(index<0||index>=characters.Length||!CanAnimate(characters[index].model))return false;
            if(character==characters[index].model)return true;
            if(WardrobeOpen)CloseWardrobe(false);
            RestoreCharacter();character.gameObject.SetActive(false);
            character=characters[index].model;SelectedCharacter=index;character.gameObject.SetActive(true);BindCharacter();
            characterPicker?.SetValueWithoutNotify(SelectedCharacterName);
            RefreshCharacterLabels();
            // Appearance only: keep audio clock, microphone, requests, draft and context intact.
            return true;
        }
        // Device-local preference; tests and scripts select through SelectCharacter without persisting.
        public const string CharacterPreference="Companion.Character.v1";
        string AppearancePreference=>SelectedCharacter==0?CompanionWardrobe.Preference:CompanionWardrobe.Preference+"."+SelectedCharacterName;
        public string Greeting=>"Hi, I'm "+SelectedCharacterName+". How has your day been?";
        public CompanionSecondaryMotion SecondaryMotion=>secondary;
        CompanionSecondaryMotion secondary;
        public Camera portraitCamera;
        [Serializable] public class Cue { public float ms, durationMs; public int id; }
        [Serializable] public class Reply { public string type,text,emotion,pcm,error; public int sampleRate,sequence; public Cue[] cues; }
        [Serializable] class ServiceError {public string error;}
        string turnFailure;
        [Serializable] public class Message { public string role, content; }
        [Serializable] class Turn { public string message; public List<Message> history; }
        sealed class ShapeBinding {public SkinnedMeshRenderer renderer;public int index;public float last,target;}
        readonly Dictionary<string,List<ShapeBinding>> shapes=new Dictionary<string,List<ShapeBinding>>();
        bool batchingFace;
        public int ShapeWritesLastFrame {get;private set;}
        readonly List<Message> history=new List<Message>();
        Transform jaw,head,leftArm,rightArm; Quaternion jawRest,headRest,leftRest,rightRest;
        AudioSource audioSource; AudioClip clip; RenderTexture texture;
        SyntheticHistoryNavigation historyNavigation;
        VisualElement root; ScrollView transcript; TextField input; Label stateLabel,replyLabel;
        Button send,stop,retry,replay; UnityWebRequest request; Coroutine routine;
        Reply reply; string lastPrompt; float started,expressionWeight;
        readonly SpeechMouthMotion mouth=new SpeechMouthMotion();
        CompanionBodyIdle bodyIdle;
        CompanionGaze gaze;
        CompanionWardrobe wardrobe;
        readonly Queue<Reply> speechQueue=new Queue<Reply>();
        readonly List<Reply> replayAudio=new List<Reply>();
        bool replayReady,replaying;string replayEmotion;
        public bool CanReplay=>!appSuspended&&replayReady&&!speaking&&request==null&&!IsRecording;
        bool streamText,streamDone,textFinal,responding;int textSequence;float turnStarted;
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
            priorBackground=Application.runInBackground;Application.runInBackground=Application.isEditor;
            appSuspended=false;
            if(!CanAnimate(character)){Debug.LogError("Character is missing required speech controls.");enabled=false;return;}
            if(characters==null||characters.Length==0)characters=new[]{new CharacterOption{name="Original",model=character}};
            string saved=PlayerPrefs.GetString(CharacterPreference,"");
            foreach(var option in characters)if(option.model!=null&&option.name==saved&&CanAnimate(option.model))character=option.model;
            for(int i=0;i<characters.Length;i++)if(characters[i].model!=null){characters[i].model.gameObject.SetActive(characters[i].model==character);if(characters[i].model==character)SelectedCharacter=i;}
            BindCharacter();
            audioSource=GetComponent<AudioSource>();audioSource.spatialBlend=0;audioSource.playOnAwake=false;audioSource.loop=false;
            InitializePortrait();
            BuildUi();SetState("Ready — type a message");
            CheckSetup();
            AudioSettings.OnAudioConfigurationChanged+=AudioChanged;
        }
        void SetState(string value) {State=value;RefreshChatState();}

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
            if(appSuspended||speaking||request!=null||IsRecording||string.IsNullOrWhiteSpace(text))return;
            if(text.Length>500){SetState("Please keep messages under 500 characters.");return;}
            if(!RequireConnection())return;
            ClearReplay();ResetSpeechTimings();lastPrompt=text;AddMessage("You",text);input?.SetValueWithoutNotify("");routine=StartCoroutine(Respond(text));
        }
        public void Retry()
        {
            if(appSuspended||string.IsNullOrEmpty(lastPrompt)||speaking||request!=null||IsRecording)return;
            if(!RequireConnection())return;
            replyLabel?.parent?.RemoveFromHierarchy();replyLabel=null;
            ClearReplay();ResetSpeechTimings();routine=StartCoroutine(Respond(lastPrompt));
        }
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
            if(appSuspended||setupRequest!=null)return;
            setupRoutine=StartCoroutine(CheckLocalSetup());
        }
        void SetSetup(string message){SetupStatus=message;if(setup!=null)setup.text=(ConnectionSource.DisplayName=="Local AI"?"LOCAL ENGLISH • ":"")+message;}
        IEnumerator CheckLocalSetup()
        {
            if(!LoadSession()){MarkAvailability(false);SetSetup(ConnectionSource.UnavailableMessage);yield break;}
            // Separate request: a setup check must never cancel or overwrite a chat/draft.
            setupRequest=ConversationRequests.Create(session,ConversationOperation.Readiness);
            setupRequest.downloadHandler=new DownloadHandlerBuffer();
            setup.SetEnabled(false);SetSetup("Checking local AI…");
            yield return setupRequest.SendWebRequest();
            bool ok=setupRequest.result==UnityWebRequest.Result.Success;
            string body=ok?setupRequest.downloadHandler.text:null;
            setupRequest.Dispose();setupRequest=null;setupRoutine=null;setup.SetEnabled(true);
            if(!ok){MarkAvailability(false);SetSetup("Local service unavailable • click to recheck");yield break;}
            Readiness result=null;try{result=JsonUtility.FromJson<Readiness>(body);}catch{}
            MarkAvailability(result?.ai=="ready");
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
            followConversation=true;if(newMessages!=null)newMessages.style.display=DisplayStyle.None;
            transcript?.Clear();promptChips?.SetEnabled(true);
            AddMessage("Companion",Greeting);
            SetState("Ready — new local chat");
        }
        public void ToggleRecording()
        {
            if(appSuspended)return;
            if(request!=null)return;
            if(IsRecording){var pcm=microphone.Finish();if(pcm==null)SetState(microphone.Error??"No recording");else TranscribePcm(pcm);return;}
            if(!RequireConnection())return;
            Interrupt();
            if(!microphonePermission.Granted){ShowPermissionPanel();return;}
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
            if(appSuspended||request!=null||IsRecording||speaking)return;
            if(pcm==null||pcm.Length<8000||pcm.Length>640000||pcm.Length%2!=0){SetState("Recording must be 0.25–20 seconds.");return;}
            routine=StartCoroutine(Transcribe(pcm));
        }
        IEnumerator Transcribe(byte[] pcm)
        {
            if(!LoadSession()){Array.Clear(pcm,0,pcm.Length);SetState(ConnectionSource.UnavailableMessage);yield break;}
            byte[] body=Encoding.UTF8.GetBytes(JsonUtility.ToJson(new RecordedAudio {pcm=Convert.ToBase64String(pcm)}));Array.Clear(pcm,0,pcm.Length);
            request=ConversationRequests.Create(session,ConversationOperation.Transcription);request.uploadHandler=new UploadHandlerRaw(body);request.downloadHandler=new DownloadHandlerBuffer();
            Array.Clear(body,0,body.Length);
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
        IEnumerator Respond(string text)
        {
            if(!LoadSession()){MarkAvailability(false);SetState(ConnectionSource.UnavailableMessage);yield break;}
            speechQueue.Clear();streamText=streamDone=textFinal=false;responding=true;textSequence=0;LastResponse="";turnFailure=null;SpeechChunksReceived=SpeechChunksPlayed=0;ResetSpeechTimings();turnStarted=Time.realtimeSinceStartup;replyLabel=null;
            request=ConversationRequests.Create(session,ConversationOperation.Turn);request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Turn {message=text,history=history})));
            var stream=new LocalSpeechStream();request.downloadHandler=stream;
            SetState("Thinking locally…");var operation=request.SendWebRequest();
            while(!operation.isDone){if(!ReadSpeechFrames(stream)){FailStream();yield break;}yield return null;}
            bool framesOk=ReadSpeechFrames(stream);
            bool ok=request.result==UnityWebRequest.Result.Success&&framesOk&&streamDone;
            if(!ok&&turnFailure==null)turnFailure=LocalServiceFailure.FromHttp(request.responseCode,RequestTimedOut(request),false);
            request.Dispose();request=null;routine=null;
            if(!ok){FailStream();yield break;}
            responding=false;StreamFinishedSeconds=Time.realtimeSinceStartup-turnStarted;RefreshPresence();
            // Playback may still be running; only Update finalizes context after the last clip.
        }
        bool ReadSpeechFrames(LocalSpeechStream stream)
        {
            if(stream.Invalid)return false;
            while(stream.TryRead(out var line)) {
                Reply frame;try{frame=JsonUtility.FromJson<Reply>(line);}catch{return false;}
                if(frame==null||streamDone)return false;
                if(frame.type=="error"){turnFailure=LocalServiceFailure.Explain(frame.error,false);return false;}
                if(frame.type=="delta") {
                    if(textFinal||frame.sequence!=textSequence||string.IsNullOrEmpty(frame.text)||LastResponse.Length+frame.text.Length>600)return false;
                    textSequence++;MarkAvailability(true);
                    if(!streamText){FirstTextSeconds=Time.realtimeSinceStartup-turnStarted;streamText=true;replyLabel=AddMessage("Companion","");}
                    LastResponse+=frame.text;UpdateStreamingBubble();RefreshPresence();
                } else if(frame.type=="text") {
                    if(textFinal||string.IsNullOrWhiteSpace(frame.text)||frame.text.Length>600||Array.IndexOf(new[]{"neutral","happy","concerned","curious"},frame.emotion)<0)return false;
                    if(streamText&&LastResponse!=frame.text)return false;
                    if(!streamText){FirstTextSeconds=Time.realtimeSinceStartup-turnStarted;replyLabel=AddMessage("Companion",frame.text);}
                    streamText=textFinal=true;LastResponse=frame.text;Expression=frame.emotion;MarkAvailability(true);UpdateStreamingBubble();
                    if(!speaking)SetState("Preparing voice…");
                } else if(frame.type=="audio") {
                    if(!streamText||frame.sequence!=SpeechChunksReceived||SpeechChunksReceived>=3||speechQueue.Count>=3)return false;
                    SpeechChunksReceived++;speechQueue.Enqueue(frame);
                } else if(frame.type=="done") {if(!textFinal||SpeechChunksReceived==0||frame.sequence!=SpeechChunksReceived)return false;streamDone=true;}
                else return false;
            }
            return true;
        }
        static bool RequestTimedOut(UnityWebRequest value)=>value.error!=null&&(value.error.IndexOf("timeout",StringComparison.OrdinalIgnoreCase)>=0||value.error.IndexOf("timed out",StringComparison.OrdinalIgnoreCase)>=0);
        void FailStream(){MarkAvailability(false);string message=turnFailure??LocalServiceFailure.Explain(null,false);Interrupt();SetState(message);}
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
            ClosePermissionPanel();microphone.Cancel();
            if(request!=null){request.Abort();request.Dispose();request=null;}
            if(routine!=null){StopCoroutine(routine);routine=null;}
            if(!replaying&&(speaking||speechQueue.Count>0||streamText&&!streamDone)&&replyLabel!=null)replyLabel.text+="\n[Playback interrupted; this exchange is omitted from next-turn context]";
            replaying=false;if(!replayReady)replayAudio.Clear();
            speechGapStarted=-1;
            speechQueue.Clear();responding=false;streamText=streamDone=textFinal=false;speaking=false;audioSource?.Stop();mouth.Reset();Expression="neutral";expressionWeight=0;ResetFace();SetState("Stopped — ready for another message");
        }
        static readonly ProfilerMarker UpdateMarker=new ProfilerMarker("Companion.CharacterUpdate");
        void Update()
        {
            if(appSuspended)return;
            using(UpdateMarker.Auto()) {
                ShapeWritesLastFrame=0;batchingFace=true;
                try{UpdateCharacter();}finally{batchingFace=false;FlushFace();}
            }
        }
        void UpdateCharacter()
        {
            if(root==null)return;
            bodyIdle?.Advance(Time.unscaledDeltaTime,speaking||request!=null||IsRecording,reduceMotion);
            historyNavigation?.Tick();
            if(IsRecording&&!microphonePermission.Granted){Interrupt();SetState("Microphone access changed — tap the mic to review permissions, or type instead");}
            if(IsRecording){microphone.Tick();if(!IsRecording)SetState(microphone.Error);else if(microphone.LimitReached)ToggleRecording();else SetState("Recording "+microphone.Seconds.ToString("0.0")+" / 20 s • level "+(microphone.Level*100).ToString("0")+"% • Stop discards");}
            UpdateChatLayout();RefreshPresence();
            ResetFace();
            gaze?.Sample(Time.unscaledTime,Time.unscaledDeltaTime,speaking||request!=null||IsRecording||(bodyIdle?.StretchWeight??0)>.1f,reduceMotion);
            float yawn=speaking||request!=null||IsRecording?0:bodyIdle?.YawnWeight??0;
            float headTilt=bodyIdle?.HeadTiltWeight??0;
            if(headTilt>0)head.rotation=Quaternion.AngleAxis(-6*headTilt,character.right)*head.rotation;
            float blink=Mathf.Max(gaze?.Blink??0,yawn*.85f);
            Shape("Eye_Blink_L",blink);Shape("Eye_Blink_R",blink);Shape("C_BlinkL",blink);Shape("C_BlinkR",blink);
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
            if(yawn>0){Shape("V_Open",Mathf.Max(mouth.Weight(0),yawn*.48f));Shape("Brow_Raise_In_L",yawn*.12f);Shape("Brow_Raise_In_R",yawn*.12f);}
            jaw.localRotation=jawRest*Quaternion.Euler(0,0,-16*Mathf.Max(mouth.Jaw,yawn*.72f));
            secondary?.Step(Time.unscaledDeltaTime,reduceMotion);
        }
        void AudioChanged(bool changed)=>HandleAudioInterruption();
        void OnApplicationPause(bool paused)=>SetApplicationSuspended(paused);
        void OnApplicationFocus(bool focused){if(!focused&&IsRecording)Interrupt();}
        void OnDisable()
        {
            RestoreLifecycleCamera();appSuspended=false;
            historyNavigation?.Dispose();historyNavigation=null;
            if(setupRequest!=null){setupRequest.Abort();setupRequest.Dispose();setupRequest=null;}
            if(setupRoutine!=null){StopCoroutine(setupRoutine);setupRoutine=null;}
            AudioSettings.OnAudioConfigurationChanged-=AudioChanged;Interrupt();ClearReplay();Application.runInBackground=priorBackground;
            RestoreCharacter();
            RestorePortrait();if(clip!=null)Destroy(clip);if(texture!=null){texture.Release();Destroy(texture);}root?.Clear();shapes.Clear();history.Clear();session=null;
        }
    }
}
