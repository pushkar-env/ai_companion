using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Companion.Core;
using UnityEngine;
using UnityEngine.UIElements;
namespace Companion.Presentation
{
    // Opt-in synthetic screen, never mounted on TalkingCompanion automatically.
    public sealed class SyntheticHistoryView:VisualElement,IDisposable
    {
        SyntheticHistoryTransport client;IEnumerator operation;int phase;long rendered=-1;
        readonly Label status;readonly ScrollView transcript;readonly Button connect,stop;
        readonly Toggle largerMessages;
        readonly List<AccountTurn> items=new List<AccountTurn>();
        readonly ListView virtualList;
        readonly Label empty;
        int bodySize=16;
        public string StatusText=>status.text;
        public int RenderedTurns {get;private set;}
        public string RenderedText {get {var result=new StringBuilder();foreach(var turn in items)result.AppendLine(TurnText(turn));return result.ToString();} }
        public long Cursor=>client==null?0:client.History.Cursor;
        public string DiagnosticCode=>client==null?"none":client.Error??client.State;
        public bool HasError=>client!=null && client.Error!=null;
        public SyntheticHistoryView()
        {
            name="synthetic-history";style.flexGrow=1;style.width=Length.Percent(100);style.maxWidth=480;style.minHeight=0;
            style.alignSelf=Align.Center;style.backgroundColor=new Color(.035f,.055f,.085f);style.paddingLeft=16;style.paddingRight=16;style.paddingTop=16;style.paddingBottom=16;
            Add(Text("Conversation history",24));Add(Text("SYNTHETIC TEST • Local accounts only",13));
            Add(Text("Test messages only. This screen does not save your talking-character chats.",14));
            status=Text("Start the local fixture to connect.",15);status.name="history-status";Add(status);
            largerMessages=new Toggle("Larger messages"){name="history-larger-messages"};largerMessages.style.minHeight=44;largerMessages.style.flexShrink=0;largerMessages.style.color=Color.white;
            largerMessages.RegisterValueChangedCallback(e=>{if(client!=null)Render();});Add(largerMessages);
            virtualList=new ListView {name="history-list",virtualizationMethod=CollectionVirtualizationMethod.DynamicHeight,selectionType=SelectionType.None,fixedItemHeight=100};
            virtualList.style.flexGrow=1;virtualList.style.flexBasis=0;virtualList.style.minHeight=0;
            virtualList.makeItem=MakeCard;virtualList.bindItem=BindCard;virtualList.unbindItem=(element,index)=>{((Label)element).text="";element.userData=null;};virtualList.itemsSource=items;
            transcript=virtualList.Q<ScrollView>();transcript.name="history-transcript";virtualList.focusable=true;virtualList.tabIndex=0;
            transcript.tooltip="Conversation messages. Use Page Up, Page Down, Home or End to scroll.";
            transcript.style.borderLeftWidth=2;transcript.style.borderRightWidth=2;transcript.style.borderTopWidth=2;transcript.style.borderBottomWidth=2;
            FocusOutline(false);virtualList.RegisterCallback<FocusInEvent>(e=>FocusOutline(true));virtualList.RegisterCallback<FocusOutEvent>(e=>FocusOutline(false));
            virtualList.RegisterCallback<KeyDownEvent>(ScrollKey,TrickleDown.TrickleDown);empty=Text("No messages in this synthetic conversation yet.",16);empty.style.display=DisplayStyle.None;Add(empty);Add(virtualList);
            connect=new Button(Connect){name="history-connect",text="Connect / Retry"};stop=new Button(Stop){name="history-stop",text="Stop listening"};
            foreach(var b in new[]{connect,stop}){b.style.minHeight=44;b.style.flexShrink=0;b.style.marginTop=8;Add(b);}
            connect.SetEnabled(false);stop.SetEnabled(false);
        }
        static Label Text(string text,int size){var label=new Label(text);label.style.flexShrink=0;label.style.whiteSpace=WhiteSpace.Normal;label.style.fontSize=size;label.style.color=new Color(.90f,.95f,.96f);label.style.marginBottom=10;return label;}
        public void ShowSetupRequired(){status.text="Start a local synthetic test session, then reopen History lab.";}
        public void Configure(string endpoint,string token,string conversation)
        {
            Dispose();client=new SyntheticHistoryTransport(endpoint,token,conversation,true);rendered=-1;RenderedTurns=0;items.Clear();virtualList.Rebuild();empty.style.display=DisplayStyle.None;
            status.text="Ready to load synthetic history.";connect.SetEnabled(true);
        }
        public void Connect()
        {
            if(client==null || !client.CanRetry)return;StopOperation();operation=client.Load();phase=0;status.text="Loading history…";connect.SetEnabled(false);stop.SetEnabled(true);
        }
        // Driven by the owning main-thread host (Editor update or runtime Update).
        public void Tick()
        {
            if(operation==null || client==null)return;
            try {
                if(!operation.MoveNext()) {
                    (operation as IDisposable)?.Dispose();operation=null;
                    if(phase==0 && client.Error==null){phase=1;operation=client.Follow();}
                    else {connect.SetEnabled(client.CanRetry);stop.SetEnabled(false);}
                }
                if(client.History.Cursor!=rendered)Render();
                status.text=client.Error!=null?RecoveryMessage():Describe(client.State);
            } catch {StopOperation();status.text="Unable to load synthetic history. Retry with a fresh fixture.";connect.SetEnabled(true);stop.SetEnabled(false);}
        }
        void FocusOutline(bool focused)
        {
            var color=focused?new Color(.35f,.8f,1f):Color.clear;
            transcript.style.borderLeftColor=color;transcript.style.borderRightColor=color;transcript.style.borderTopColor=color;transcript.style.borderBottomColor=color;
        }
        void ScrollKey(KeyDownEvent e)
        {
            float current=transcript.scrollOffset.y,step=transcript.contentViewport.layout.height*.9f;
            if(e.keyCode==KeyCode.Home){virtualList.ScrollToItem(0);e.StopImmediatePropagation();return;}
            else if(e.keyCode==KeyCode.End){virtualList.ScrollToItem(items.Count-1);e.StopImmediatePropagation();return;}
            else if(e.keyCode==KeyCode.PageUp)current-=step;
            else if(e.keyCode==KeyCode.PageDown)current+=step;
            else return;
            transcript.scrollOffset=new Vector2(0,Mathf.Clamp(current,0,transcript.verticalScroller.highValue));e.StopImmediatePropagation();
        }
        static string TurnText(AccountTurn turn)
        {
            string text="You\n"+turn.UserText+"\n"+turn.State;
            if(turn.AssistantText!=null)text+="\nCompanion\n"+turn.AssistantText;
            return text;
        }
        VisualElement MakeCard()
        {
            var card=Text("",bodySize);card.name="history-card";card.style.backgroundColor=new Color(.10f,.17f,.22f);
            card.style.paddingLeft=12;card.style.paddingRight=12;card.style.paddingTop=12;card.style.paddingBottom=12;
            return card;
        }
        void BindCard(VisualElement element,int index)
        {
            var card=(Label)element;card.text=TurnText(items[index]);card.userData=index;card.style.fontSize=bodySize;
        }
        void Render()
        {
            var turns=client.History.Snapshot;RenderedTurns=turns.Length;empty.style.display=turns.Length==0?DisplayStyle.Flex:DisplayStyle.None;int desired=largerMessages.value?24:16;
            bool refresh=items.Count!=turns.Length || bodySize!=desired;bodySize=desired;
            for(int i=0;i<turns.Length;i++) {
                if(i>=items.Count)items.Add(turns[i]);
                else if(items[i].Version!=turns[i].Version){items[i]=turns[i];if(!refresh)virtualList.RefreshItem(i);}
            }
            if(refresh)virtualList.RefreshItems();
            rendered=client.History.Cursor;
        }
        string RecoveryMessage()
        {
            if(client.RequiresSessionReload)return "Session expired or access changed. Reload the local test session to continue.";
            if(!client.CanRetry)return "History unavailable. Reopen with a valid local test session.";
            return "Connection unavailable. Retry when the local fixture is ready.";
        }
        static string Describe(string state)
        {
            switch(state){case "loading":return "Loading history…";case "ready":return "History loaded. Connecting…";case "connected":return "Live • synthetic history";case "connecting":return "Connecting…";case "reconnecting":return "Connection interrupted. Reconnecting…";case "stopped":return "Listening stopped. History stays on this screen.";default:return "Ready";}
        }
        void StopOperation(){client?.Stop();(operation as IDisposable)?.Dispose();operation=null;}
        public void Stop(){StopOperation();status.text=client!=null && client.Error!=null?RecoveryMessage():"Listening stopped. History stays on this screen.";connect.SetEnabled(client!=null && client.CanRetry);stop.SetEnabled(false);}
        public void Dispose(){StopOperation();client?.Dispose();client=null;connect.SetEnabled(false);stop.SetEnabled(false);}
    }
}
