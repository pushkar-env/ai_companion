using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
namespace Companion.Presentation
{
    // Development-only route; never reads/writes the talking-character transcript.
    public sealed class SyntheticHistoryNavigation:IDisposable
    {
        readonly VisualElement root;readonly Action interrupt;
        readonly Dictionary<VisualElement,StyleEnum<DisplayStyle>> hidden=new Dictionary<VisualElement,StyleEnum<DisplayStyle>>();
        VisualElement overlay;SyntheticHistoryView view;
        public bool IsOpen=>overlay!=null;
        public SyntheticHistoryNavigation(VisualElement root,Action interrupt){this.root=root;this.interrupt=interrupt;}
        public void Open()
        {
            if(IsOpen || (!Application.isEditor && !Debug.isDebugBuild))return;
            interrupt?.Invoke();
            foreach(var child in root.Children()){hidden.Add(child,child.style.display);child.style.display=DisplayStyle.None;}
            overlay=new VisualElement {name="history-route"};overlay.style.position=Position.Absolute;
            overlay.style.left=0;overlay.style.right=0;overlay.style.top=0;overlay.style.bottom=0;
            overlay.style.backgroundColor=new Color(.035f,.055f,.085f);overlay.style.paddingLeft=8;overlay.style.paddingRight=8;
            var back=new Button(Close){name="history-back",text="Back to companion"};back.style.minHeight=44;back.style.flexShrink=0;overlay.Add(back);
            view=new SyntheticHistoryView();overlay.Add(view);root.Add(overlay);SetInsets(8,8);
#if UNITY_EDITOR
            try {
                var path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../artifacts/unity-account/fixture.json"));
                var fixture=JsonUtility.FromJson<Fixture>(File.ReadAllText(path));
                view.Configure(fixture.endpoint,fixture.token,string.IsNullOrEmpty(fixture.runtimeConversation)?fixture.conversation:fixture.runtimeConversation);view.Connect();
            } catch {view.ShowSetupRequired();}
#else
            view.ShowSetupRequired();
#endif
        }
        public void SetInsets(float top,float bottom){if(overlay==null)return;overlay.style.paddingTop=Mathf.Max(0,top);overlay.style.paddingBottom=Mathf.Max(0,bottom);}
        public void Tick(){view?.Tick();}
        public void Pause(){view?.Stop();}
        public void Close()
        {
            view?.Dispose();view=null;overlay?.RemoveFromHierarchy();overlay=null;
            foreach(var entry in hidden)if(entry.Key.parent==root)entry.Key.style.display=entry.Value;
            hidden.Clear();
        }
        public void Dispose()=>Close();
        [Serializable] sealed class Fixture {public string endpoint,token,conversation,runtimeConversation;}
    }
}
