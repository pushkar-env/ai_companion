using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using Companion.Presentation;
using UnityEngine;
using UnityEngine.Networking;
using UnityEditor;

namespace Companion.Editor
{
    public static class ConversationContextChecks
    {
        [Serializable] class Turn {public string message;public TalkingCharacter.Message[] history;}
        static TalkingCharacter app;static int phase;static double deadline;static string completedReply;
        static readonly List<string> lines=new List<string>();
        const string First="Please say exactly: A peaceful morning begins.";
        const string Cancelled="Please say exactly these two sentences: This is an unfinished exchange. The second sentence should not enter the next conversation.";
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/evidence/m1/conversation-context"));
        static object Field(string name)=>typeof(TalkingCharacter).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(app);
        static List<TalkingCharacter.Message> History=>(List<TalkingCharacter.Message>)Field("history");
        static void Check(bool ok,string text){if(!ok)throw new Exception(text);lines.Add("PASS "+text);}
        static Turn Request()=>JsonUtility.FromJson<Turn>(System.Text.Encoding.UTF8.GetString(((UnityWebRequest)Field("request")).uploadHandler.data));
        [MenuItem("Companion/Run Conversation Context Checks")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying)throw new Exception("Enter Play first");
            app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>();if(app==null)throw new Exception("Open TalkingCompanion");
            EditorApplication.update-=Tick;lines.Clear();Directory.CreateDirectory(Folder);app.NewChat();app.Submit(First);
            Check(Request().history.Length==0,"first actual request starts with empty context");
            phase=0;deadline=EditorApplication.timeSinceStartup+100;EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            try {
                if(app==null||!EditorApplication.isPlaying)throw new Exception("Play stopped");
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Timeout: "+app.State);
                if(app.State.Contains("unavailable"))throw new Exception(app.State);
                if(phase==0&&app.State.Contains("finished")) {
                    completedReply=app.LastResponse;
                    Check(History.Count==2&&History[0].role=="user"&&History[0].content==First&&History[1].role=="assistant"&&History[1].content==completedReply,"completed speech commits one ordered user/assistant pair");
                    app.Submit(Cancelled);phase=1;
                } else if(phase==1&&app.IsSpeaking) {
                    Check(History.Count==2&&History[0].content==First,"partial streamed playback does not commit unfinished prompt");
                    app.Interrupt();phase=2;deadline=EditorApplication.timeSinceStartup+100;
                    // Wait briefly for the server's cancellation to release its single turn slot.
                    resumeAt=EditorApplication.timeSinceStartup+2;
                } else if(phase==2&&EditorApplication.timeSinceStartup>resumeAt) {
                    app.Retry();var payload=Request();
                    Check(payload.message==Cancelled&&payload.history.Length==2&&payload.history[0].content==First&&payload.history[1].content==completedReply,"Retry sends last prompt once with only completed prior context");
                    phase=3;
                } else if(phase==3&&app.State.Contains("finished")) {
                    Check(History.Count==4&&History[2].content==Cancelled&&History[3].content==app.LastResponse,"completed Retry adds exactly one exchange");
                    // Seed older complete pairs to exercise the rolling development bound
                    // through real playback rather than duplicating the trimming code.
                    History.Clear();for(int i=0;i<4;i++){History.Add(new TalkingCharacter.Message{role="user",content="seed user "+i});History.Add(new TalkingCharacter.Message{role="assistant",content="seed reply "+i});}
                    app.Submit(First);phase=4;deadline=EditorApplication.timeSinceStartup+100;
                } else if(phase==4&&app.State.Contains("finished")) {
                    Check(History.Count==8&&History[0].content=="seed user 1"&&History[6].content==First&&History[7].content==app.LastResponse,"rolling context drops oldest whole pair and retains newest exchange");
                    app.NewChat();Check(History.Count==0,"New chat clears completed context");Finish();
                }
            }catch(Exception e){lines.Add("FAIL "+e.Message);Finish();Debug.LogError(e.Message);}
        }
        static double resumeAt;
        static void Finish(){EditorApplication.update-=Tick;File.WriteAllLines(Path.Combine(Folder,"editor-checks.txt"),lines);Debug.Log("Conversation context checks saved");}
    }
}
