using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using Companion.Core;
using Companion.Presentation;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
namespace Companion.Editor
{
    public static class LocalFailureChecks
    {
        [MenuItem("Companion/Run Local Failure Checks")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying)throw new Exception("Enter Play first");
            var app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>();var lines=new List<string>();
            Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception(name);lines.Add("PASS "+name);};
            app.NewChat();
            string[] codes={"busy_retry","unauthorized","cancelled_or_timeout","local_model_unavailable","speech_unavailable","invalid_model_reply","untrusted raw request value"};
            foreach(string code in codes) {
                using(var stream=new LocalSpeechStream()) {
                    var bytes=Encoding.UTF8.GetBytes(JsonUtility.ToJson(new TalkingCharacter.Reply{type="error",error=code})+"\n");
                    typeof(LocalSpeechStream).GetMethod("ReceiveData",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(stream,new object[]{bytes,bytes.Length});
                    bool accepted=(bool)typeof(TalkingCharacter).GetMethod("ReadSpeechFrames",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(app,new object[]{stream});
                    check(!accepted,"error frame stops turn: "+(code.StartsWith("untrusted")?"unknown":code));
                    typeof(TalkingCharacter).GetMethod("FailStream",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(app,null);
                    check(app.State==LocalServiceFailure.Explain(code,false)&&!app.State.Contains("untrusted raw")&&!app.IsSpeaking,"safe actionable status and no playback: "+(code.StartsWith("untrusted")?"unknown":code));
                }
            }
            check(LocalServiceFailure.FromHttp(429,false,false).Contains("busy"),"HTTP busy fallback gives retry guidance");
            check(LocalServiceFailure.FromHttp(0,true,true).Contains("Record again"),"transcription timeout uses recording recovery, not chat Retry");
            var root=app.GetComponent<UIDocument>().rootVisualElement;
            check(root.Q("chat-actions").worldBound.yMax<=root.worldBound.yMax,"failure controls remain inside portrait");
            var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/evidence/m1/local-failures"));Directory.CreateDirectory(folder);File.WriteAllLines(Path.Combine(folder,"editor-checks.txt"),lines);app.NewChat();Debug.Log("PASS "+lines.Count+" local failure checks");
        }
    }
}
