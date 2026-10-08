using System;
using System.Collections.Generic;
using System.IO;
using Companion.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
namespace Companion.Editor
{
    public static class HistoryNavigationChecks
    {
        static GameObject host;static PanelSettings panel;static RenderTexture texture;static Scene scene;
        static VisualElement root,original;static SyntheticHistoryNavigation nav;static int phase,interrupts;static double ready;
        static readonly List<string> lines=new List<string>();
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/evidence/m2/history-navigation"));
        [MenuItem("Companion/Run History Navigation Layout Checks")]
        public static void Run()
        {
            if(host!=null)throw new InvalidOperationException("Already running");
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play mode first");
            lines.Clear();phase=0;interrupts=0;Directory.CreateDirectory(Folder);
            try {
                scene=SceneManager.CreateScene("Synthetic History Offscreen Check");host=new GameObject("History UI offscreen check"){hideFlags=HideFlags.HideAndDontSave};SceneManager.MoveGameObjectToScene(host,scene);
                panel=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Companion/Settings/MockPanel.asset"));panel.hideFlags=HideFlags.HideAndDontSave;
                panel.scaleMode=PanelScaleMode.ConstantPixelSize;panel.scale=1;
                texture=new RenderTexture(360,640,0);texture.Create();panel.targetTexture=texture;
                var doc=host.AddComponent<UIDocument>();doc.panelSettings=panel;root=doc.rootVisualElement;
                root.style.width=360;root.style.height=640;root.style.backgroundColor=Color.black;
                original=new Label("Original chat preserved");root.Add(original);
                nav=new SyntheticHistoryNavigation(root,()=>interrupts++);nav.Open();nav.SetInsets(24,24);
                ready=EditorApplication.timeSinceStartup+2;EditorApplication.update+=Tick;EditorApplication.QueuePlayerLoopUpdate();
            }catch(Exception e){Finish(e.Message);}
        }
        static void Check(bool ok,string label){if(!ok)throw new Exception(label);lines.Add("PASS "+label);}
        static void Tick()
        {
            EditorApplication.QueuePlayerLoopUpdate();if(EditorApplication.timeSinceStartup<ready)return;
            try {
                var overlay=root.Q("history-route");
                Check(overlay!=null && overlay.worldBound.width>0,"offscreen panel has resolved layout phase "+phase);
                foreach(string name in new[]{"history-back","history-connect","history-stop"}) {
                    var element=root.Q(name);var b=element.worldBound;
                    Check(b.width>0 && b.height>=43.5f && b.xMin>=-.5f && b.xMax<=root.worldBound.width+.5f && b.yMin>=23.5f && b.yMax<=root.worldBound.height-23.5f,"portrait phase "+phase+" contains "+name);
                }
                Check(original.resolvedStyle.display==DisplayStyle.None,"underlying chat hidden from interaction phase "+phase);
                if(phase==0) {
                    Capture("360x640");root.style.width=390;root.style.height=844;
                    texture.Release();texture.width=390;texture.height=844;texture.Create();phase++;ready=EditorApplication.timeSinceStartup+2;return;
                }
                Capture("390x844");nav.Pause();nav.Close();
                Check(root.Q("history-route")==null && original.style.display.value!=DisplayStyle.None,"Back restores original chat without replacing its elements");
                nav.Open();nav.Close();Check(interrupts==2,"each navigation interrupts current speech exactly once");
                nav.Dispose();Check(root.childCount==1,"route cleanup leaves only original content");Finish(null);
            }catch(Exception e){Finish(e.Message);}
        }
        static void Capture(string name)
        {
            var previous=RenderTexture.active;RenderTexture.active=texture;var image=new Texture2D(texture.width,texture.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Folder,name+".png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=previous;
        }
        static void Finish(string error)
        {
            EditorApplication.update-=Tick;nav?.Dispose();nav=null;if(host!=null)UnityEngine.Object.DestroyImmediate(host);host=null;
            if(scene.IsValid())SceneManager.UnloadSceneAsync(scene);if(panel!=null)UnityEngine.Object.DestroyImmediate(panel);if(texture!=null){texture.Release();UnityEngine.Object.DestroyImmediate(texture);}
            if(error!=null)lines.Add("FAIL "+error);File.WriteAllLines(Path.Combine(Folder,"checks.txt"),lines);
        }
    }
}
