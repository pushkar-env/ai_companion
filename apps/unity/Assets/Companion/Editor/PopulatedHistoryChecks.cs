using System;
using System.Collections.Generic;
using System.IO;
using Companion.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
namespace Companion.Editor
{
    public static class PopulatedHistoryChecks
    {
        static GameObject host;static PanelSettings panel;static RenderTexture texture;static Scene scene;
        static VisualElement root,original;static SyntheticHistoryNavigation nav;static SyntheticHistoryView view;static HistoryFixture fixture;
        static int phase;static double ready,deadline;static readonly List<string> lines=new List<string>();
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/evidence/m2/populated-history"));
        [MenuItem("Companion/Run Populated History Layout Checks")]
        public static void Run()
        {
            if(host!=null || !EditorApplication.isPlaying)throw new InvalidOperationException("Run once in Play mode");
            fixture=HistoryFixture.Read();lines.Clear();phase=0;Directory.CreateDirectory(Folder);
            try {
                scene=SceneManager.CreateScene("Populated History Offscreen Check");host=new GameObject("Populated History Check"){hideFlags=HideFlags.HideAndDontSave};SceneManager.MoveGameObjectToScene(host,scene);
                panel=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Companion/Settings/MockPanel.asset"));panel.hideFlags=HideFlags.HideAndDontSave;panel.scaleMode=PanelScaleMode.ConstantPixelSize;panel.scale=1;
                texture=new RenderTexture(360,640,0);texture.Create();panel.targetTexture=texture;
                var doc=host.AddComponent<UIDocument>();doc.panelSettings=panel;root=doc.rootVisualElement;root.style.width=360;root.style.height=640;
                original=new Label("Original chat");root.Add(original);nav=new SyntheticHistoryNavigation(root,()=>{});nav.Open();nav.SetInsets(24,24);
                view=root.Q<SyntheticHistoryView>();deadline=EditorApplication.timeSinceStartup+60;ready=EditorApplication.timeSinceStartup+2;EditorApplication.update+=Tick;
            }catch(Exception e){Finish(e.Message);}
        }
        static void Check(bool ok,string label){if(!ok)throw new Exception(label);lines.Add("PASS "+label);}
        static void Bounds()
        {
            foreach(string name in new[]{"history-back","history-connect","history-stop"}) {
                var b=root.Q(name).worldBound;Check(b.width>0 && b.height>=43.5f && b.xMin>=-.5f && b.xMax<=360.5f && b.yMin>=23.5f && b.yMax<=616.5f,"populated phase "+phase+" contains "+name);
            }
        }
        static void Tick()
        {
            try {
                nav.Tick();if(view.HasError)throw new Exception(view.DiagnosticCode);
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Runtime history timed out");
                if(EditorApplication.timeSinceStartup<ready)return;
                var scroll=view.Q<ScrollView>("history-transcript");
                if(phase==0) {
                    if(view.Cursor!=12 || !view.StatusText.StartsWith("Live"))return;
                    Check(view.RenderedTurns==6,"real API hydrates six completed turns through runtime navigation");
                    Check(view.RenderedText.Contains("[SYNTHETIC] A walk"),"long canonical reply reaches runtime screen");
                    Check(scroll.contentViewport.worldBound.height>=80,"normal text retains readable transcript viewport");
                    Check(scroll.contentContainer.worldBound.height>scroll.contentViewport.worldBound.height,"long history overflows into scroll container");Bounds();Capture("normal-360x640");
                    view.Q<Toggle>("history-larger-messages").value=true;phase=1;ready=EditorApplication.timeSinceStartup+2;
                } else if(phase==1) {
                    Check(scroll.Q<Label>("history-card").resolvedStyle.fontSize==24,"larger messages render at 150 percent body size");
                    Check(scroll.contentViewport.worldBound.height>=80,"larger messages retain readable transcript viewport");
                    Check(scroll.verticalScroller.highValue>0,"large long history remains scrollable");Bounds();Capture("large-360x640");
                    view.Q<ListView>("history-list").ScrollToItem(5);phase=2;ready=EditorApplication.timeSinceStartup+2;
                } else {
                    var card=scroll.Query<Label>("history-card").ToList().Find(x=>x.userData is int && (int)x.userData==5);
                    if(card==null)throw new Exception("Final virtual row missing");var last=card.worldBound;
                    Check(last.yMax<=scroll.contentViewport.worldBound.yMax+2 && last.yMax>=scroll.contentViewport.worldBound.yMin,"scroll reaches final reply without hiding fixed actions");Capture("large-bottom-360x640");
                    nav.Pause();Check(view.StatusText.Contains("stopped") && view.RenderedTurns==6,"pause stops listening while retaining populated history");
                    nav.Close();Check(root.childCount==1 && root[0]==original,"Back removes populated account view and restores chat");Finish(null);
                }
            }catch(Exception e){Finish(e.Message);}
        }
        static void Capture(string name)
        {
            var previous=RenderTexture.active;RenderTexture.active=texture;var image=new Texture2D(texture.width,texture.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Folder,name+".png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=previous;
        }
        static void Finish(string error)
        {
            EditorApplication.update-=Tick;nav?.Dispose();nav=null;view=null;if(host!=null)UnityEngine.Object.DestroyImmediate(host);host=null;
            if(scene.IsValid())SceneManager.UnloadSceneAsync(scene);if(panel!=null)UnityEngine.Object.DestroyImmediate(panel);if(texture!=null){texture.Release();UnityEngine.Object.DestroyImmediate(texture);}
            if(error!=null)lines.Add("FAIL "+error);File.WriteAllLines(Path.Combine(Folder,"checks.txt"),lines);
            File.WriteAllText(Path.Combine(HistoryFixture.Folder,"runtime-result.json"),JsonUtility.ToJson(new Result {runId=fixture.runId,passed=error==null,checks=lines.Count}));
        }
        [Serializable] sealed class Result {public string runId;public bool passed;public int checks;}
    }
}
