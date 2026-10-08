using System;
using System.IO;
using Companion.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
namespace Companion.Editor
{
    public sealed class SyntheticHistoryWindow:EditorWindow
    {
        SyntheticHistoryView view;
        [MenuItem("Companion/Open Synthetic Account History")]
        public static void Open(){var window=GetWindow<SyntheticHistoryWindow>();window.titleContent=new GUIContent("Synthetic history");window.minSize=new Vector2(320,600);}
        public void CreateGUI()
        {
            rootVisualElement.Clear();var load=new Button(Load){text="Load local synthetic fixture"};load.style.minHeight=44;rootVisualElement.Add(load);
            view=new SyntheticHistoryView();rootVisualElement.Add(view);
        }
        void Load(){try{var f=HistoryFixture.Read();view.Configure(f.endpoint,f.token,f.conversation);view.Connect();}catch{ShowNotification(new GUIContent("Start tools/check-database.py --api --unity first."));}}
        void Update(){view?.Tick();}
        void OnDisable(){view?.Dispose();}
    }
    [Serializable] public sealed class HistoryFixture
    {
        public string runId,endpoint,token,otherToken,conversation,runtimeConversation;
        public static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../artifacts/unity-account"));
        public static HistoryFixture Read()=>JsonUtility.FromJson<HistoryFixture>(File.ReadAllText(Path.Combine(Folder,"fixture.json")));
    }
}
