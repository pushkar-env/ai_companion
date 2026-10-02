using System;
using System.IO;
using System.Diagnostics;
using Companion.Presentation;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Companion.Editor
{
    [InitializeOnLoad]
    public static class TalkingCharacterSetup
    {
        public const string ScenePath="Assets/Companion/Scenes/TalkingCompanion.unity";
        [Serializable] class Session {public string url,token;public int pid;}
        static TalkingCharacterSetup(){EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.ExitingEditMode&&SceneManager.GetActiveScene().path==ScenePath)StartService();};}
        [MenuItem("Companion/Start Local Talking Service")]
        public static void StartService()
        {
            string repo=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
            string config=Path.Combine(repo,"artifacts/talking-character/session.json");
            if(File.Exists(config))try {var s=JsonUtility.FromJson<Session>(File.ReadAllText(config));var p=Process.GetProcessById(s.pid);if(!p.HasExited&&p.ProcessName=="node")return;}catch{}
            Process.Start(new ProcessStartInfo {FileName="node",Arguments="\""+Path.Combine(repo,"services/voice-agent/talking-character.mjs")+"\"",WorkingDirectory=repo,UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});
        }
        [MenuItem("Companion/Open Talking Companion")]
        public static void Open()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            if(!File.Exists(ScenePath)) {
                if(!AssetDatabase.CopyAsset(CCCharacterLabSetup.ScenePath,ScenePath))throw new InvalidOperationException("Cannot copy existing character scene");
                var scene=EditorSceneManager.OpenScene(ScenePath);
                var lab=UnityEngine.Object.FindFirstObjectByType<CCCharacterLab>();
                var go=lab.gameObject;var character=lab.character;var camera=lab.portraitCamera;
                UnityEngine.Object.DestroyImmediate(lab);
                var app=go.AddComponent<TalkingCharacter>();app.character=character;app.portraitCamera=camera;go.name="Talking companion - local Editor prototype";
                EditorSceneManager.SaveScene(scene);
            } else EditorSceneManager.OpenScene(ScenePath);
            StartService();
        }
    }
    public sealed class TalkingCharacterBuildGuard:IProcessSceneWithReport
    {
        public int callbackOrder=>0;
        public void OnProcessScene(Scene scene,BuildReport report){if(report!=null&&scene.path==TalkingCharacterSetup.ScenePath)throw new BuildFailedException("TalkingCompanion is a Windows Editor prototype. Mobile transport and voice are not implemented.");}
    }
}
