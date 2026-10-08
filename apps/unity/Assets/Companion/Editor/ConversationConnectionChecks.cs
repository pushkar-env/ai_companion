using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Companion.Core;
using Companion.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Companion.Editor
{
    public static class ConversationConnectionChecks
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        [MenuItem("Companion/Run Conversation Connection Checks")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play first.");
            var app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>();
            var type=typeof(TalkingCharacter);
            object Get(string name)=>type.GetField(name,Flags).GetValue(app);
            void Set(string name,object value)=>type.GetField(name,Flags).SetValue(app,value);
            if(app==null||Get("request")!=null||Get("setupRequest")!=null||app.IsSpeaking||app.IsRecording)
                throw new InvalidOperationException("An idle talking scene is required.");
            var checks=new List<string>();
            void Check(bool value,string name){if(!value)throw new Exception(name);checks.Add("PASS "+name);}
            var source=Get("connectionSource");var session=Get("session");
            var field=(TextField)Get("input");var draft=field.value;string state=app.State;
            var transcript=(ScrollView)Get("transcript");int count=transcript.childCount;
            try {
                Set("connectionSource",new UnconfiguredConversationSource());
                field.SetValueWithoutNotify("Keep this unsent draft");
                app.Submit(field.value);
                Check(app.Draft=="Keep this unsent draft","Unavailable send preserves draft");
                Check(transcript.childCount==count,"Unavailable send creates no phantom message");
                Check(Get("request")==null&&Get("routine")==null,"Unavailable send creates no request/coroutine");
                Check(Get("session")==null,"Failed refresh discards stale credential");
                Check(app.State.Contains("not configured")&&!app.State.Contains("Companion >"),"Player guidance contains no Editor menu instructions");
                app.ToggleRecording();
                Check(!app.IsRecording&&!app.PermissionPanelOpen,"Unconfigured service does not capture audio or request permission");
                Check(app.Draft=="Keep this unsent draft","Unavailable microphone preserves draft");
                type.GetMethod("RefreshPresence",Flags).Invoke(app,null);
                Check(app.PresenceText.StartsWith("AI ·"),"Unconfigured player uses neutral AI label");
                ConversationConnection.TryLocalEditor(true,"http://127.0.0.1:43210",new string('a',64),out var valid);
                foreach(ConversationOperation operation in Enum.GetValues(typeof(ConversationOperation))) {
                    using(var request=ConversationRequests.Create(valid,operation)) {
                        Check(request.redirectLimit==0,"No credential redirect: "+operation);
                        Check(request.timeout>0&&request.GetRequestHeader("Authorization")==valid.Authorization,"Bounded authenticated request: "+operation);
                    }
                }
            } finally {
                Set("connectionSource",source);Set("session",session);field.SetValueWithoutNotify(draft);
                type.GetMethod("SetState",Flags).Invoke(app,new object[]{state});
                type.GetMethod("RefreshPresence",Flags).Invoke(app,null);
            }
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/evidence/production/p01"));
            Directory.CreateDirectory(folder);File.WriteAllLines(Path.Combine(folder,"editor-checks.txt"),checks);
            Debug.Log("Conversation connection checks: "+checks.Count+" passed");
        }
    }
}
