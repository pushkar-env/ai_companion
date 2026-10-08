using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Companion.Core;
using Companion.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
namespace Companion.Editor
{
    public static class HistoryScaleChecks
    {
        static GameObject host;static PanelSettings panel;static RenderTexture texture;static Scene scene;
        static SyntheticHistoryView view;static SyntheticHistoryTransport client;static ScrollView scroll;static MethodInfo render;
        static VisualElement first,last;static string lastTurn;static int phase;static double ready;static float atEnd;
        static readonly List<string> lines=new List<string>();
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/evidence/m2/history-virtualization"));
        [MenuItem("Companion/Run History Scale and Keyboard Checks")]
        public static void Run()
        {
            if(host!=null || !EditorApplication.isPlaying)throw new InvalidOperationException("Run once in Play mode");lines.Clear();phase=0;Directory.CreateDirectory(Folder);
            try {
                scene=SceneManager.CreateScene("History scale offscreen");host=new GameObject("History scale"){hideFlags=HideFlags.HideAndDontSave};SceneManager.MoveGameObjectToScene(host,scene);
                panel=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Companion/Settings/MockPanel.asset"));panel.hideFlags=HideFlags.HideAndDontSave;panel.scaleMode=PanelScaleMode.ConstantPixelSize;panel.scale=1;
                texture=new RenderTexture(360,640,0);texture.Create();panel.targetTexture=texture;var doc=host.AddComponent<UIDocument>();doc.panelSettings=panel;doc.rootVisualElement.style.width=360;doc.rootVisualElement.style.height=640;
                view=new SyntheticHistoryView();doc.rootVisualElement.Add(view);view.Configure("http://127.0.0.1:12345/",new string('a',64),Guid.NewGuid().ToString());
                client=(SyntheticHistoryTransport)typeof(SyntheticHistoryView).GetField("client",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(view);
                for(int i=0;i<1000;i++) {
                    lastTurn=Guid.NewGuid().ToString();client.History.Apply(new AccountEvent {event_id=Guid.NewGuid().ToString(),turn_id=lastTurn,sequence=i*2+1,type="turn.accepted",version=1,text="[SYNTHETIC] Message "+(i+1)});
                    if(i<999)client.History.Apply(new AccountEvent {event_id=Guid.NewGuid().ToString(),turn_id=lastTurn,sequence=i*2+2,type="turn.completed",version=2,text="[SYNTHETIC] A short retained reply."});
                }
                render=typeof(SyntheticHistoryView).GetMethod("Render",BindingFlags.NonPublic|BindingFlags.Instance);render.Invoke(view,null);
                scroll=view.Q<ScrollView>("history-transcript");ready=EditorApplication.timeSinceStartup+3;EditorApplication.update+=Tick;
            }catch(Exception e){Finish(e.Message);}
        }
        static void Check(bool ok,string label){if(!ok)throw new Exception(label);lines.Add("PASS "+label);}
        static void Key(KeyCode key){using(var e=KeyDownEvent.GetPooled(new Event {type=EventType.KeyDown,keyCode=key})){e.target=view.Q<ListView>("history-list");view.Q<ListView>("history-list").SendEvent(e);}}
        static List<Label> Cards()=>scroll.Query<Label>("history-card").ToList().FindAll(x=>x.userData is int);
        static Label Card(int index)=>Cards().Find(x=>(int)x.userData==index);
        static void Tick()
        {
            if(EditorApplication.timeSinceStartup<ready)return;
            try {
                if(phase==0) {
                    Check(view.RenderedTurns==1000 && view.Q<ListView>("history-list").itemsSource.Count==1000,"1000 canonical turns remain available to virtual list");
                    Check(Cards().Count>0 && Cards().Count<=32,"initial layout creates at most 32 bound cards");first=Card(0);
                    render.Invoke(view,null);
                    Check(first!=null && ReferenceEquals(first,Card(0)),"unchanged render retains visible card");scroll.scrollOffset=new Vector2(0,300);
                    client.History.Apply(new AccountEvent {event_id=Guid.NewGuid().ToString(),turn_id=lastTurn,sequence=2000,type="turn.completed",version=2,text="[SYNTHETIC] Terminal update applied once."});render.Invoke(view,null);
                    Check(Card(999)==null && Cards().Count<=32 && view.RenderedText.Contains("Terminal update"),"offscreen terminal update changes data without building a hidden row");
                    Check(Mathf.Abs(scroll.scrollOffset.y-300)<1,"offscreen update preserves current scroll offset");
                    view.Q<Toggle>("history-larger-messages").value=true;phase=1;
                } else if(phase==1) {
                    Check(Cards().Count>0 && Cards().TrueForAll(x=>x.resolvedStyle.fontSize==24),"larger text rebinds visible virtual cards at 24px");
                    view.Q<ListView>("history-list").Focus();Key(KeyCode.Home);Check(scroll.scrollOffset.y==0,"Home scrolls to beginning");
                    Key(KeyCode.PageDown);Check(scroll.scrollOffset.y>0,"Page Down advances keyboard scroll");
                    Key(KeyCode.End);atEnd=scroll.verticalScroller.highValue;phase=2;
                } else {
                    Check(Card(999)!=null && Card(999).text.Contains("Terminal update"),"End binds updated final turn");
                    Check(Cards().Count<=32,"scroll to final turn keeps bound card count bounded");atEnd=scroll.scrollOffset.y;
                    var focused=scroll.panel.focusController.focusedElement as VisualElement;var list=view.Q<ListView>("history-list");
                    Check(focused!=null && (focused==list || list.Contains(focused)),"transcript accepts keyboard focus");
                    Check(scroll.resolvedStyle.borderLeftColor.a>.5f,"keyboard focus has visible outline");Capture();
                    Key(KeyCode.PageUp);Check(scroll.scrollOffset.y<atEnd,"Page Up moves away from final position");
                    bool denied=false;try{client.History.Apply(new AccountEvent {event_id=Guid.NewGuid().ToString(),turn_id=Guid.NewGuid().ToString(),sequence=2001,type="turn.accepted",version=1,text="over cap"});}catch(InvalidDataException){denied=true;}
                    Check(denied && client.History.Cursor==2000 && view.RenderedTurns==1000,"capacity rejection preserves existing history and checkpoint");
                    view.Configure("http://127.0.0.1:12345/",new string('b',64),Guid.NewGuid().ToString());Check(view.RenderedTurns==0 && view.Q<ListView>("history-list").itemsSource.Count==0 && Cards().Count==0,"account reconfiguration clears virtual data and bindings");Finish(null);return;
                }
                ready=EditorApplication.timeSinceStartup+2;
            }catch(Exception e){Finish(e.Message);}
        }
        static void Capture(){var prior=RenderTexture.active;RenderTexture.active=texture;var image=new Texture2D(360,640,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,360,640),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Folder,"keyboard-focus.png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=prior;}
        static void Finish(string error)
        {
            EditorApplication.update-=Tick;view?.Dispose();view=null;if(host!=null)UnityEngine.Object.DestroyImmediate(host);host=null;if(scene.IsValid())SceneManager.UnloadSceneAsync(scene);
            if(panel!=null)UnityEngine.Object.DestroyImmediate(panel);if(texture!=null){texture.Release();UnityEngine.Object.DestroyImmediate(texture);}
            if(error!=null)lines.Add("FAIL "+error);File.WriteAllLines(Path.Combine(Folder,"checks.txt"),lines);
        }
    }
}
