using System;
using System.Collections.Generic;
using Companion.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Companion.Presentation
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class MockCompanionApp : MonoBehaviour
    {
        public ChatSession Session { get; private set; }
        public Transform character;
        UIDocument document;
        VisualElement root, shell, avatarPanel, chatPanel, settingsPanel;
        Label avatarDescription;
        ScrollView transcript;
        TextField input;
        DropdownField scenario;
        Label state, assistant, empty;
        Button send, cancel, retry, replay;
        Toggle reducedMotion, largeText;
        bool isLargeText;
        float lastWidth, lastHeight;
        Rect lastSafeArea;
        float lastKeyboard;
#if UNITY_EDITOR
        Vector4 simulatedInsets;
        float simulatedKeyboard;
        public void SimulateInsets(Vector4 insets, float keyboard) { simulatedInsets=insets; simulatedKeyboard=keyboard; Layout(); }
#endif
        float nextTick;
        int entries;
        RenderTexture portrait;
        bool previousRunInBackground;
        public string StatusText => state == null ? "" : state.text;
        public string ResponseText => assistant == null ? "" : assistant.text;
        public bool UiReady => input != null && send != null;
        void OnEnable()
        {
            previousRunInBackground=Application.runInBackground; Application.runInBackground=true;
            Session = new ChatSession(new MockTextProvider());
            document = GetComponent<UIDocument>(); root = document.rootVisualElement;
            Screen.orientation=ScreenOrientation.Portrait;
            Build(); Render();
        }
        void OnDisable() { Session?.Cancel(); root?.Clear(); Application.runInBackground=previousRunInBackground; if(Camera.main!=null) Camera.main.targetTexture=null; if(portrait!=null) { portrait.Release(); Destroy(portrait); } }
        void OnApplicationPause(bool paused) { if (paused) { Session?.Cancel(); Render(); } }
        void Build()
        {
            root.Clear(); root.style.flexGrow = 1; root.style.color = Color.white;
            root.style.backgroundColor = new Color(0.035f, 0.055f, 0.085f, 1);
            root.style.alignItems=Align.Center;
            shell=new VisualElement {name="portrait-shell"}; shell.style.width=Length.Percent(100); shell.style.maxWidth=480;
            shell.style.flexGrow=1; shell.style.minHeight=0; root.Add(shell);
            var header = new VisualElement(); header.style.flexDirection = FlexDirection.Row; header.style.justifyContent = Justify.SpaceBetween;
            header.style.flexShrink=0;header.style.height=30;
            header.Add(Text("COMPANION", 17, new Color(.67f,.9f,.82f)));
            header.Add(Text("M0 • MOCK", 12, new Color(1,.79f,.4f))); shell.Add(header);
            avatarPanel = new VisualElement {name="avatar-card"}; avatarPanel.style.flexDirection=FlexDirection.Row;
            avatarPanel.style.flexShrink=0; avatarPanel.style.marginBottom=10;avatarPanel.style.alignItems=Align.Center;
            avatarPanel.style.backgroundColor = new Color(.08f,.14f,.19f,1); avatarPanel.style.paddingRight = 14;
            Round(avatarPanel,16); shell.Add(avatarPanel);
            portrait=new RenderTexture(420,520,24); portrait.Create();
            if(Camera.main!=null) Camera.main.targetTexture=portrait;
            var portraitView=new Image {name="character-portrait",image=portrait, scaleMode=ScaleMode.ScaleToFit};
            portraitView.style.width=Length.Percent(40);portraitView.style.height=Length.Percent(100);portraitView.style.flexShrink=0;avatarPanel.Add(portraitView);
            var intro = new VisualElement(); intro.style.flexGrow=1;intro.style.minWidth=0;
            intro.Add(Text("A little space\nto connect.", 23, Color.white));
            avatarDescription=Text("Placeholder character\nScripted replies • No live AI",12,new Color(.7f,.8f,.83f));avatarDescription.style.marginTop=8;intro.Add(avatarDescription);avatarPanel.Add(intro);
            chatPanel = new VisualElement(); chatPanel.style.flexGrow = 1; chatPanel.style.minWidth = 0;chatPanel.style.minHeight=0;shell.Add(chatPanel);
            transcript = new ScrollView(); transcript.name = "transcript"; transcript.style.flexGrow = 1;transcript.style.flexBasis=0;transcript.style.minHeight=0; transcript.style.marginBottom = 8; chatPanel.Add(transcript);
            empty = Text("What would you like to try?\n\nSend a message to see a scripted response.",17,new Color(.75f,.83f,.88f)); empty.style.marginTop = 10; transcript.Add(empty);
            state = Text("Ready",12,new Color(.67f,.9f,.82f)); state.name="chat-status"; state.style.marginBottom=6;state.style.flexShrink=0;chatPanel.Add(state);
            input = new TextField(); input.name="message-input"; input.multiline=true; input.maxLength=8000; input.style.height=58; input.style.flexShrink=0;input.style.fontSize=17;
            input.tooltip="Message for the local scripted demo. Maximum 8,000 characters."; chatPanel.Add(input);
            var field=input.Q<VisualElement>(className:"unity-text-field__input"); if(field!=null) {field.style.backgroundColor=new Color(.12f,.18f,.24f);field.style.color=Color.white;}
            input.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return && !e.shiftKey) { if (!Session.Busy) SendDraft(); e.StopPropagation(); } });
            var actions = new VisualElement {name="chat-actions"}; actions.style.flexDirection=FlexDirection.Row; actions.style.marginTop=6;actions.style.flexShrink=0;chatPanel.Add(actions);
            send = ActionButton("Send",SendDraft); send.name="send"; actions.Add(send);
            cancel = ActionButton("Cancel",CancelResponse); cancel.name="cancel"; actions.Add(cancel);
            retry = ActionButton("Retry",RetryResponse); retry.name="retry"; actions.Add(retry);
            replay = ActionButton("Replay",ReplayResponse); replay.name="replay";actions.Add(replay);
            var settingsButton=ActionButton("Demo controls",()=>SetSettingsVisible(settingsPanel.style.display.value!=DisplayStyle.Flex));settingsButton.name="demo-controls";settingsButton.style.flexGrow=0;settingsButton.style.marginTop=6;shell.Add(settingsButton);
            shell.Add(Text("English mock • No data saved",11,new Color(.65f,.72f,.79f)));
            // Overlay settings: expanding test controls must not push the composer below the viewport.
            settingsPanel=new VisualElement {name="demo-settings"};settingsPanel.style.position=Position.Absolute;settingsPanel.style.left=0;settingsPanel.style.right=0;settingsPanel.style.bottom=68;
            settingsPanel.style.backgroundColor=new Color(.1f,.17f,.23f);settingsPanel.style.paddingLeft=14;settingsPanel.style.paddingRight=14;settingsPanel.style.paddingTop=10;settingsPanel.style.paddingBottom=10;Round(settingsPanel,12);shell.Add(settingsPanel);
            settingsPanel.Add(Text("Local demo controls",18,Color.white));
            scenario = new DropdownField(new List<string>{"Normal","Slow loading","Error before response","Disconnect midway"},0);scenario.name="simulation";scenario.style.height=48;scenario.style.marginTop=8;settingsPanel.Add(scenario);
            reducedMotion=new Toggle("Reduce character motion"); reducedMotion.style.minHeight=36;settingsPanel.Add(reducedMotion);
            largeText=new Toggle("Larger chat text") {name="larger-text"}; largeText.style.minHeight=36;settingsPanel.Add(largeText);
            largeText.RegisterValueChangedCallback(e=> { isLargeText=e.newValue; transcript.Query<Label>().ForEach(l=>l.style.fontSize=isLargeText?22:17); input.style.fontSize=isLargeText?22:17; });
            settingsPanel.Add(ActionButton("Clear session",()=>{ClearSession();SetSettingsVisible(false);}));
            settingsPanel.Add(ActionButton("Close controls",()=>SetSettingsVisible(false)));SetSettingsVisible(false);
            root.RegisterCallback<GeometryChangedEvent>(_=>Layout()); Layout();
        }
        public void SetSettingsVisible(bool visible) { settingsPanel.style.display=visible?DisplayStyle.Flex:DisplayStyle.None; }
        void Layout()
        {
            if(root==null || Screen.width<=0 || float.IsNaN(root.resolvedStyle.width)) return;
            float scale=root.resolvedStyle.width/Screen.width;
            var safe=Screen.safeArea;
            float left=safe.xMin*scale,right=(Screen.width-safe.xMax)*scale,top=(Screen.height-safe.yMax)*scale,bottom=safe.yMin*scale;
            float keyboard=TouchScreenKeyboard.visible?TouchScreenKeyboard.area.height*scale:0;
#if UNITY_EDITOR
            left=Mathf.Max(left,simulatedInsets.x);top=Mathf.Max(top,simulatedInsets.y);right=Mathf.Max(right,simulatedInsets.z);bottom=Mathf.Max(bottom,simulatedInsets.w);keyboard=Mathf.Max(keyboard,simulatedKeyboard);
#endif
            root.style.paddingLeft=12+left;root.style.paddingRight=12+right;root.style.paddingTop=10+top;root.style.paddingBottom=8+Mathf.Max(bottom,keyboard);
            float usable=root.resolvedStyle.height-top-Mathf.Max(bottom,keyboard)-18;
            avatarPanel.style.height=keyboard>0?64:Mathf.Clamp(usable*.25f,100,210);
            avatarDescription.style.display=keyboard>0?DisplayStyle.None:DisplayStyle.Flex;
            if(keyboard>0)SetSettingsVisible(false);
            if(Camera.main!=null) Camera.main.rect=new Rect(0,0,1,1);
            lastWidth=Screen.width;lastHeight=Screen.height;lastSafeArea=safe;lastKeyboard=TouchScreenKeyboard.visible?TouchScreenKeyboard.area.height:0;
        }
        static Label Text(string text,int size,Color color)
        {
            var l=new Label(text); l.style.fontSize=size; l.style.color=color; l.style.whiteSpace=WhiteSpace.Normal;
            l.enableRichText=false; l.selection.isSelectable=true; return l;
        }
        static void Round(VisualElement v,int radius) { v.style.borderTopLeftRadius=radius; v.style.borderTopRightRadius=radius; v.style.borderBottomLeftRadius=radius; v.style.borderBottomRightRadius=radius; }
        static Button ActionButton(string title,Action action)
        {
            var b=new Button(action){text=title}; b.style.height=48;b.style.minHeight=48;b.style.flexShrink=0;b.style.flexGrow=1;b.style.flexBasis=0;b.style.minWidth=0;b.style.marginLeft=0;b.style.marginRight=4;b.style.fontSize=14; b.style.backgroundColor=new Color(.16f,.27f,.33f);b.style.color=Color.white;Round(b,8);return b;
        }
        void AddTurn()
        {
            if(empty!=null) { empty.RemoveFromHierarchy(); empty=null; }
            // Bound memory in this local demo; no retention policy is implied.
            if(entries>=20) { transcript.Clear(); entries=0; }
            var user=Text("YOU\n"+Session.UserText,isLargeText?22:17,new Color(.9f,.95f,.97f)); user.style.backgroundColor=new Color(.13f,.21f,.29f); user.style.paddingTop=10; user.style.paddingBottom=10; user.style.paddingLeft=12; user.style.paddingRight=12; Round(user,12); transcript.Add(user);
            assistant=Text("",isLargeText?22:17,new Color(.8f,.94f,.89f)); assistant.style.marginTop=12; assistant.style.marginBottom=16; transcript.Add(assistant); entries++;
        }
        void SendDraft() { SendMessage(input.value,(MockScenario)scenario.index); }
        public void SendMessage(string message,MockScenario mode)
        {
            try { Session.Send(message,mode); AddTurn(); input.value=""; nextTick=Time.unscaledTime+(mode==MockScenario.Slow?3f:.65f); Render(); }
            catch(Exception e) when(e is ArgumentException || e is InvalidOperationException) { state.text=e.Message; }
        }
        public void CancelResponse() { Session.Cancel(); Render(); }
        public void RetryResponse()
        {
            try { Session.Retry(); nextTick=Time.unscaledTime+.65f; Render(); }
            catch(InvalidOperationException e) { state.text=e.Message; }
        }
        public void ReplayResponse() { if(!Session.Busy && !string.IsNullOrEmpty(Session.UserText)) SendMessage(Session.UserText,MockScenario.Normal); }
        public void ClearSession() { Session.Cancel(); Session=new ChatSession(new MockTextProvider()); transcript.Clear(); assistant=null; entries=0; input.value=""; Render(); }
        void Update()
        {
            if(lastWidth!=Screen.width||lastHeight!=Screen.height||lastSafeArea!=Screen.safeArea||lastKeyboard!=(TouchScreenKeyboard.visible?TouchScreenKeyboard.area.height:0))Layout();
            if(Session!=null && Session.Busy && Time.unscaledTime>=nextTick) { Session.Tick(); nextTick=Time.unscaledTime+.095f; Render(); }
            if(character!=null && reducedMotion!=null) character.localRotation=Quaternion.Euler(0,reducedMotion.value?0:Mathf.Sin(Time.unscaledTime*.65f)*6,0);
        }
        void Render()
        {
            if(state==null || Session==null) return;
            state.text=Session.Status==ChatStatus.Draft?"Ready • local scripted demo":Session.Status==ChatStatus.Queued?"Loading mock response…":Session.Status==ChatStatus.Accepted?"Accepted • preparing scripted response…":Session.Status==ChatStatus.Streaming?"Streaming • simulated":Session.Status==ChatStatus.Failed?"Error: "+Session.Error+" • Retry is available.":Session.Status==ChatStatus.Cancelled?"Cancelled • partial response kept. Retry starts a new attempt.":"Completed • scripted response";
            if(assistant!=null) { assistant.text="MOCK COMPANION\n"+(Session.Text.Length==0?"…":Session.Text)+(Session.Status==ChatStatus.Failed?"\n[Incomplete — simulated error]":Session.Status==ChatStatus.Cancelled?"\n[Cancelled]":""); transcript.ScrollTo(assistant); }
            send.SetEnabled(!Session.Busy); cancel.SetEnabled(Session.Busy); retry.SetEnabled(Session.Retryable&&!Session.Busy&&Session.Attempt<2); replay.SetEnabled(!Session.Busy&&Session.UserText.Length>0); scenario.SetEnabled(!Session.Busy);
        }
    }
}
