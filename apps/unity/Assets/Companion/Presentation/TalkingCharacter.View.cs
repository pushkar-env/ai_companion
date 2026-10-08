using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Companion.Presentation
{
    public sealed partial class TalkingCharacter
    {
        VisualElement shell,stage,drawer,settings,promptChips,recordingPanel,levelFill;
        Label recordingTime;
        Button newMessages;
        Image characterImage;
        bool largeText,opaque,reduceMotion;bool followConversation=true;
        Bounds bodyBounds;
        Color oldClear;CameraClearFlags oldFlags;RenderTexture oldTarget;
        Vector3 oldCameraPosition;Quaternion oldCameraRotation;float oldAspect;
        int renderWidth,renderHeight;
        // Explicit test geometry, never changes Game view or Editor windows.
        public float PreviewKeyboardInset {get;set;}
        public Vector2 PreviewSafeInsets {get;set;}
        public Bounds FullBodyBounds=>bodyBounds;

        void CacheBodyBounds()
        {
            bool first=true;
            // Bake once after posing arms. Imported skinned bounds include the original T-pose.
            var mesh=new Mesh();
            void IncludePose()
            {
                foreach(var renderer in character.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                    renderer.BakeMesh(mesh);
                    foreach(var vertex in mesh.vertices) {
                        var point=renderer.transform.TransformPoint(vertex);
                        if(first){bodyBounds=new Bounds(point,Vector3.zero);first=false;}else bodyBounds.Encapsulate(point);
                    }
                }
            }
            IncludePose();
            // Reserve the motion envelope once; hands stay visible without camera pumping.
            if(bodyIdle!=null&&bodyIdle.IsBound) {
                foreach(var gesture in new[]{CompanionBodyIdle.Gesture.Yawn,CompanionBodyIdle.Gesture.SideStretch})
                    foreach(float side in new[]{-1f,1f}) {
                        bodyIdle.SampleGesture(0,gesture,.5f,side,1.1f);IncludePose();
                    }
                bodyIdle.Sample(0);
            }
            Destroy(mesh);
            if(first)bodyBounds=new Bounds(character.position+Vector3.up,Vector3.one*2);
        }
        void InitializePortrait()
        {
            oldClear=portraitCamera.backgroundColor;oldFlags=portraitCamera.clearFlags;oldTarget=portraitCamera.targetTexture;
            oldCameraPosition=portraitCamera.transform.position;oldCameraRotation=portraitCamera.transform.rotation;oldAspect=portraitCamera.aspect;
            portraitCamera.clearFlags=CameraClearFlags.SolidColor;portraitCamera.backgroundColor=Color.clear;
            texture=new RenderTexture(720,960,24,RenderTextureFormat.ARGB32){antiAliasing=4,name="Companion full body"};
            texture.Create();portraitCamera.targetTexture=texture;renderWidth=720;renderHeight=960;
        }
        void RestorePortrait()
        {
            if(portraitCamera==null)return;
            portraitCamera.targetTexture=oldTarget;portraitCamera.backgroundColor=oldClear;portraitCamera.clearFlags=oldFlags;
            portraitCamera.transform.SetPositionAndRotation(oldCameraPosition,oldCameraRotation);portraitCamera.aspect=oldAspect;
        }
        void FrameBody(float width,float height)
        {
            if(float.IsNaN(width)||float.IsNaN(height)||float.IsInfinity(width)||float.IsInfinity(height)||width<1||height<1||texture==null)return;
            float aspect=width/height;
            int h=Mathf.Clamp(Mathf.RoundToInt(height*2),256,1400);
            int w=Mathf.Clamp(Mathf.RoundToInt(h*aspect),128,1080);
            if(w!=renderWidth||h!=renderHeight) {
                texture.Release();texture.width=w;texture.height=h;texture.Create();renderWidth=w;renderHeight=h;
            }
            portraitCamera.aspect=aspect;
            float tan=Mathf.Tan(portraitCamera.fieldOfView*Mathf.Deg2Rad*.5f);
            float distance=Mathf.Max(bodyBounds.extents.y/tan,bodyBounds.extents.x/(tan*aspect))*1.12f+bodyBounds.extents.z;
            portraitCamera.transform.position=bodyBounds.center+Quaternion.AngleAxis(wardrobeYaw,Vector3.up)*Vector3.forward*distance;
            portraitCamera.transform.LookAt(bodyBounds.center);
        }
        void UpdateChatLayout()
        {
            if(shell==null||root.resolvedStyle.width<1)return;
            float scale=root.resolvedStyle.width/Mathf.Max(1,Screen.width);
            bool offscreen=GetComponent<UIDocument>().panelSettings.targetTexture!=null;
            float top=offscreen?PreviewSafeInsets.x:(Screen.height-Screen.safeArea.yMax)*scale;
            float bottom=offscreen?PreviewSafeInsets.y:Screen.safeArea.yMin*scale;
            float keyboard=PreviewKeyboardInset>0?PreviewKeyboardInset:(!offscreen&&TouchScreenKeyboard.visible?TouchScreenKeyboard.area.height*scale:0);
            shell.style.top=top+8;shell.style.bottom=Mathf.Max(bottom,keyboard)+8;
            historyNavigation?.SetInsets(top+8,bottom+8);
            float height=Mathf.Max(120,root.resolvedStyle.height-top-Mathf.Max(bottom,keyboard)-16);
            bool compact=height<560;
            shell.EnableInClassList("compact",root.resolvedStyle.height-top-bottom<560);
            // The scene uses the full viewport and never depends on chat or keyboard height.
            stage.style.top=top+80;stage.style.bottom=bottom+72;
            drawer.style.top=72;drawer.style.bottom=0;
            promptChips.style.display=transcript.childCount<=1&&!compact&&!IsRecording?DisplayStyle.Flex:DisplayStyle.None;
            if(historyNavigation==null||!historyNavigation.IsOpen)FrameBody(stage.resolvedStyle.width,stage.resolvedStyle.height);
            if(IsRecording){recordingTime.text="Recording  "+microphone.Seconds.ToString("0.0")+" / 20 s";levelFill.style.width=Length.Percent(Mathf.Clamp01(microphone.Level*3)*100);}
        }
        void RefreshChatState()
        {
            if(stateLabel==null)return;RefreshPresence();
            stateLabel.text=State;bool busy=appSuspended||speaking||request!=null||IsRecording;
            send.SetEnabled(!busy&&!string.IsNullOrWhiteSpace(input.value));
            stop.style.display=busy?DisplayStyle.Flex:DisplayStyle.None;
            retry.style.display=!busy&&!replayReady&&!string.IsNullOrEmpty(lastPrompt)?DisplayStyle.Flex:DisplayStyle.None;
            replay.style.display=CanReplay?DisplayStyle.Flex:DisplayStyle.None;
            record.SetEnabled(!appSuspended&&request==null);record.tooltip=IsRecording?"Finish recording and review":"Record voice message (English, 20 seconds maximum)";
            record.EnableInClassList("recording",IsRecording);
            recordingPanel.style.display=IsRecording?DisplayStyle.Flex:DisplayStyle.None;
            transcript.style.display=IsRecording?DisplayStyle.None:DisplayStyle.Flex;
            microphoneDevice.SetEnabled(!IsRecording&&request==null);refreshMicrophones.SetEnabled(!IsRecording&&request==null);
        }
        Label AddMessage(string who,string text)
        {
            bool follow=followConversation;
            var card=new VisualElement();card.AddToClassList("bubble");card.AddToClassList(who=="You"?"outgoing":"incoming");
            var label=new Label(text);label.AddToClassList("message-body");label.selection.isSelectable=true;card.Add(label);
            var stamp=new Label((who=="You"?"You":SelectedCharacterName)+"  ·  "+DateTime.Now.ToString("HH:mm"));stamp.AddToClassList("message-meta");card.Add(stamp);
            transcript.Add(card);
            transcript.schedule.Execute(()=>{if(!transcript.Contains(card))return;if(follow&&followConversation)transcript.ScrollTo(card);else if(newMessages!=null)newMessages.style.display=DisplayStyle.Flex;});
            return label;
        }
        void FollowLatestMessage()
        {
            if(!followConversation||transcript==null||transcript.childCount==0)return;
            transcript.schedule.Execute(()=>{if(followConversation&&transcript.childCount>0)transcript.scrollOffset=new Vector2(0,transcript.verticalScroller.highValue);}).StartingIn(1);
        }
        static Label Text(string value,int size){var label=new Label(value);label.style.fontSize=size;label.style.whiteSpace=WhiteSpace.Normal;return label;}
        static Button MakeButton(string value,Action action){var b=new Button(action){text=value};b.AddToClassList("text-button");return b;}
        static Button IconButton(string kind,string label,Action action)
        {
            var button=new Button(action){tooltip=label};button.AddToClassList("icon-button");button.Add(new CompanionIcon(kind));return button;
        }
        static void StyleField(VisualElement field){field.AddToClassList("glass-field");}
        void BuildUi()
        {
            root=GetComponent<UIDocument>().rootVisualElement;root.Clear();root.AddToClassList("companion-ui");
            root.UnregisterCallback<NavigationCancelEvent>(OnNavigationCancel);
            root.RegisterCallback<NavigationCancelEvent>(OnNavigationCancel);
            root.styleSheets.Add(Resources.Load<StyleSheet>("CompanionUI/Conversation"));
            var room=new Image {image=Resources.Load<Texture2D>("CompanionUI/EveningRoom"),scaleMode=ScaleMode.ScaleAndCrop,pickingMode=PickingMode.Ignore,name="evening-room"};room.AddToClassList("full-bleed");root.Add(room);
            var shade=new VisualElement {pickingMode=PickingMode.Ignore};shade.AddToClassList("full-bleed");shade.AddToClassList("room-shade");root.Add(shade);
            shell=new VisualElement {name="conversation-shell"};shell.AddToClassList("shell");root.Add(shell);
            var header=new VisualElement {name="top-navigation"};header.AddToClassList("header");header.AddToClassList("glass");shell.Add(header);
            var identity=new VisualElement();identity.style.flexGrow=1;header.Add(identity);
            var title=Text(SelectedCharacterName,21);title.name="character-title";title.style.unityFontStyleAndWeight=FontStyle.Bold;identity.Add(title);
            presenceLabel=Text("AI · Checking availability…",11);presenceLabel.name="companion-presence";presenceLabel.AddToClassList("muted");presenceLabel.tooltip="Local AI service status; last seen is the last successful availability check in this session.";identity.Add(presenceLabel);
            var settingsButton=IconButton("settings","Open settings",OpenSettings);settingsButton.name="open-settings";header.Add(settingsButton);
            stage=new VisualElement {name="companion-stage",pickingMode=PickingMode.Ignore};stage.AddToClassList("stage");root.Insert(2,stage);
            var shadow=new VisualElement {pickingMode=PickingMode.Ignore};shadow.AddToClassList("contact-shadow");stage.Add(shadow);
            characterImage=new Image {image=texture,scaleMode=ScaleMode.StretchToFill,name="talking-character",pickingMode=PickingMode.Ignore};characterImage.AddToClassList("full-bleed");stage.Add(characterImage);
            drawer=new VisualElement {name="chat-drawer"};drawer.AddToClassList("drawer");shell.Add(drawer);
            transcript=new ScrollView {name="conversation",horizontalScrollerVisibility=ScrollerVisibility.Hidden,verticalScrollerVisibility=ScrollerVisibility.Hidden};transcript.AddToClassList("transcript");drawer.Add(transcript);
            followConversation=true;
            transcript.RegisterCallback<GeometryChangedEvent>(_=>FollowLatestMessage());
            transcript.contentContainer.RegisterCallback<GeometryChangedEvent>(_=>FollowLatestMessage());
            transcript.contentViewport.RegisterCallback<GeometryChangedEvent>(_=>FollowLatestMessage());
            transcript.RegisterCallback<WheelEvent>(e=>{if(e.delta.y<0)followConversation=false;});
            transcript.RegisterCallback<PointerDownEvent>(_=>followConversation=false);
            transcript.RegisterCallback<PointerUpEvent>(_=>transcript.schedule.Execute(()=>followConversation=transcript.verticalScroller.highValue-transcript.scrollOffset.y<40));
            transcript.verticalScroller.valueChanged+=value=>{if(transcript.verticalScroller.highValue-value<4){followConversation=true;if(newMessages!=null)newMessages.style.display=DisplayStyle.None;}};
            newMessages=MakeButton("New message ↓",()=>{followConversation=true;if(transcript.childCount>0)transcript.ScrollTo(transcript[transcript.childCount-1]);newMessages.style.display=DisplayStyle.None;});newMessages.name="new-messages";newMessages.style.display=DisplayStyle.None;drawer.Add(newMessages);
            AddMessage("Companion",Greeting);
            recordingPanel=new VisualElement {name="recording-panel"};recordingPanel.AddToClassList("recording-panel");drawer.Add(recordingPanel);
            recordingTime=Text("Recording",18);recordingPanel.Add(recordingTime);
            var meter=new VisualElement();meter.AddToClassList("level-track");levelFill=new VisualElement();levelFill.AddToClassList("level-fill");meter.Add(levelFill);recordingPanel.Add(meter);
            recordingPanel.Add(Text("English · finish to review before sending",12));
            var recordingActions=new VisualElement();recordingActions.AddToClassList("row");recordingPanel.Add(recordingActions);
            recordingActions.Add(MakeButton("Cancel",Interrupt));recordingActions.Add(MakeButton("Finish & review",ToggleRecording));
            promptChips=new VisualElement {name="prompt-chips"};promptChips.AddToClassList("row");drawer.Add(promptChips);
            promptChips.Add(MakeButton("Good news",()=>Submit("I have some good news to share.")));promptChips.Add(MakeButton("Unwind",()=>Submit("Help me unwind after a busy day.")));
            typingLabel=Text(SelectedCharacterName+" is typing…",12);typingLabel.name="typing-indicator";typingLabel.style.display=DisplayStyle.None;drawer.Add(typingLabel);
            stateLabel=Text("Ready",11);stateLabel.name="talking-status";stateLabel.AddToClassList("status");drawer.Add(stateLabel);
            var actions=new VisualElement {name="chat-actions"};actions.AddToClassList("row");drawer.Add(actions);
            stop=MakeButton("Stop",Interrupt);stop.name="stop-response";retry=MakeButton("Retry",Retry);retry.name="retry-response";replay=MakeButton("Replay",Replay);replay.name="replay-reply";actions.Add(stop);actions.Add(retry);actions.Add(replay);
            var composer=new VisualElement {name="composer"};composer.AddToClassList("composer");drawer.Add(composer);
            input=new TextField {name="message-input",maxLength=500,multiline=true};StyleField(input);composer.Add(input);
            input.RegisterValueChangedCallback(_=>RefreshChatState());input.RegisterCallback<KeyDownEvent>(e=>{if(e.keyCode==KeyCode.Return&&!e.shiftKey){Submit(input.value);e.StopPropagation();e.PreventDefault();}});
            record=IconButton("mic","Record voice",ToggleRecording);record.name="record-voice";composer.Add(record);
            send=IconButton("send","Send message",()=>Submit(input.value));send.name="send-message";send.AddToClassList("primary");composer.Add(send);
            BuildWardrobe(header);BuildSettings();BuildPermissionPanel();RefreshCharacterLabels();RefreshChatState();
        }
        // Name-bearing labels follow the selected appearance; the conversation itself is untouched.
        void RefreshCharacterLabels()
        {
            if(root==null)return;
            var title=root.Q<Label>("character-title");if(title!=null)title.text=SelectedCharacterName;
            if(input!=null){input.tooltip="Message "+SelectedCharacterName+". Enter sends; Shift+Enter adds a line.";input.textEdition.placeholder="Message "+SelectedCharacterName+"…";}
            if(typingLabel!=null)typingLabel.text=SelectedCharacterName+" is typing…";
        }
        public void OpenSettings(){settings.style.display=DisplayStyle.Flex;shell.SetEnabled(false);settings.Q<Button>()?.Focus();}
        public void CloseSettings(){settings.style.display=DisplayStyle.None;shell.SetEnabled(true);root.Q<Button>("open-settings")?.Focus();}
        void BuildSettings()
        {
            settings=new VisualElement {name="companion-settings"};settings.AddToClassList("settings");root.Add(settings);
            var card=new VisualElement();card.AddToClassList("settings-card");settings.Add(card);
            var heading=new VisualElement();heading.AddToClassList("row");card.Add(heading);
            var title=Text("Settings",23);title.style.flexGrow=1;heading.Add(title);heading.Add(MakeButton("Done",CloseSettings));
            var body=new ScrollView();body.style.flexGrow=1;card.Add(body);
            if(characters.Length>1) {
                body.Add(Text("YOUR COMPANION",11));
                var names=new System.Collections.Generic.List<string>();foreach(var option in characters)names.Add(option.name);
                characterPicker=new DropdownField("Companion",names,SelectedCharacter){name="character-picker"};StyleField(characterPicker);
                characterPicker.tooltip="Changes appearance only; this chat, draft and voice stay the same.";
                characterPicker.RegisterValueChangedCallback(e=>{
                    if(SelectCharacter(characterPicker.index)){PlayerPrefs.SetString(CharacterPreference,SelectedCharacterName);PlayerPrefs.Save();}
                    else characterPicker.SetValueWithoutNotify(SelectedCharacterName);
                });
                body.Add(characterPicker);
            }
            body.Add(Text("YOUR EXPERIENCE",11));
            var motion=new Toggle("Reduce idle motion"){name="reduce-motion"};motion.value=PlayerPrefs.GetInt("Companion.UI.ReduceMotion",0)==1;reduceMotion=motion.value;
            motion.RegisterValueChangedCallback(e=>{reduceMotion=e.newValue;PlayerPrefs.SetInt("Companion.UI.ReduceMotion",reduceMotion?1:0);});body.Add(motion);
            var larger=new Toggle("Larger messages");larger.value=PlayerPrefs.GetInt("Companion.UI.LargeText",0)==1;largeText=larger.value;
            larger.RegisterValueChangedCallback(e=>{largeText=e.newValue;root.EnableInClassList("large-text",largeText);PlayerPrefs.SetInt("Companion.UI.LargeText",largeText?1:0);});body.Add(larger);root.EnableInClassList("large-text",largeText);
            var solid=new Toggle("Reduce transparency");solid.value=PlayerPrefs.GetInt("Companion.UI.Opaque",0)==1;opaque=solid.value;
            solid.RegisterValueChangedCallback(e=>{opaque=e.newValue;root.EnableInClassList("opaque",opaque);PlayerPrefs.SetInt("Companion.UI.Opaque",opaque?1:0);});body.Add(solid);root.EnableInClassList("opaque",opaque);
            var confirm=new VisualElement();confirm.style.display=DisplayStyle.None;
            var newChat=MakeButton("New chat",()=>confirm.style.display=DisplayStyle.Flex);newChat.name="new-chat";body.Add(newChat);
            confirm.Add(Text("Clear this local conversation and draft?",14));var row=new VisualElement();row.AddToClassList("row");confirm.Add(row);
            row.Add(MakeButton("Keep chat",()=>confirm.style.display=DisplayStyle.None));row.Add(MakeButton("Clear chat",()=>{NewChat();confirm.style.display=DisplayStyle.None;CloseSettings();}));body.Add(confirm);
            body.Add(Text("LOCAL DEVELOPMENT",11));body.Add(Text("Messages run on this PC. This chat is session-only. Voice recognition is currently English only.",13));
            setup=MakeButton("Check setup",CheckSetup);setup.name="check-setup";body.Add(setup);
            microphoneSelection=new Companion.Core.MicrophoneSelection(PlayerPrefs.GetString(MicrophonePreference,""));
            var deviceRow=new VisualElement {name="microphone-row"};body.Add(deviceRow);
            microphoneDevice=new DropdownField {name="microphone-device",label="Microphone"};StyleField(microphoneDevice);deviceRow.Add(microphoneDevice);
            microphoneDevice.RegisterValueChangedCallback(e=>{if(microphoneSelection.Select(e.newValue)){PlayerPrefs.SetString(MicrophonePreference,e.newValue);PlayerPrefs.Save();}});
            refreshMicrophones=MakeButton("Refresh microphones",RefreshMicrophones);refreshMicrophones.name="refresh-microphones";deviceRow.Add(refreshMicrophones);RefreshMicrophones();
            if(Application.isEditor||Debug.isDebugBuild) {
                historyNavigation=new SyntheticHistoryNavigation(root,Interrupt);
                var lab=MakeButton("History lab",()=>{CloseSettings();historyNavigation.Open();});lab.name="history-lab";body.Add(lab);
            }
            settings.style.display=DisplayStyle.None;
        }
    }

    // Native vector strokes remain sharp at every portrait scale. No icon-font dependency.
    internal sealed class CompanionIcon:VisualElement
    {
        readonly string kind;
        public CompanionIcon(string kind){this.kind=kind;style.width=22;style.height=22;pickingMode=PickingMode.Ignore;generateVisualContent+=Draw;}
        void Draw(MeshGenerationContext context)
        {
            var p=context.painter2D;p.strokeColor=new Color(.94f,.98f,.96f);p.lineWidth=1.7f;
            void Line(float x,float y,float xx,float yy){p.BeginPath();p.MoveTo(new Vector2(x,y));p.LineTo(new Vector2(xx,yy));p.Stroke();}
            if(kind=="send") {p.BeginPath();p.MoveTo(new Vector2(2,10));p.LineTo(new Vector2(20,2));p.LineTo(new Vector2(13,20));p.LineTo(new Vector2(9,13));p.ClosePath();p.Stroke();Line(9,13,20,2);}
            else if(kind=="mic") {p.BeginPath();p.MoveTo(new Vector2(7,5));p.BezierCurveTo(new Vector2(7,0),new Vector2(15,0),new Vector2(15,5));p.LineTo(new Vector2(15,11));p.BezierCurveTo(new Vector2(15,16),new Vector2(7,16),new Vector2(7,11));p.ClosePath();p.Stroke();p.BeginPath();p.MoveTo(new Vector2(4,10));p.BezierCurveTo(new Vector2(4,20),new Vector2(18,20),new Vector2(18,10));p.Stroke();Line(11,17,11,21);Line(7,21,15,21);}
            else if(kind=="expand") {Line(4,9,4,4);Line(4,4,9,4);Line(13,18,18,18);Line(18,18,18,13);Line(4,4,9,9);Line(18,18,13,13);}
            else {for(int i=0;i<3;i++){float y=5+i*6;Line(2,y,20,y);float x=i==1?14:7;p.fillColor=new Color(.94f,.98f,.96f);p.BeginPath();p.Arc(new Vector2(x,y),2.6f,0,360);p.Fill();}}
        }
    }
}
