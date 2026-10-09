using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Companion.Presentation;
using UnityEditor;
using UnityEngine;

namespace Companion.Editor
{
    // Play-mode evidence: one real local-AI voice turn per roster character, with face crops taken from
    // the app's own portrait render every 0.25 s while she speaks, plus first-audio/complete timings.
    // Restores the original selection afterwards (never writes the character preference).
    public static class LiveFaceCapture
    {
        static TalkingCharacter app;static Queue<int> pending;static string folder,prompt;static int index,shot,original;
        static double next,deadline;static bool spoke;static List<string> report;

        public static void Run(string outFolder,string message,params int[] characters)
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play first");
            app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>()??throw new InvalidOperationException("TalkingCharacter missing");
            folder=outFolder;prompt=message;Directory.CreateDirectory(folder);report=new List<string>();
            original=app.SelectedCharacter;pending=new Queue<int>(characters.Length>0?characters:Enumerable.Range(0,app.characters.Length));
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;Begin();
        }
        static void Begin()
        {
            index=pending.Dequeue();app.SelectCharacter(index);app.NewChat();shot=0;spoke=false;
            deadline=EditorApplication.timeSinceStartup+150;next=0;app.Submit(prompt);
        }
        static void Tick()
        {
            try {
                if(!EditorApplication.isPlaying||app==null){Finish("Play stopped");return;}
                double now=EditorApplication.timeSinceStartup;string name=app.characters[index].name;
                if(now>deadline){report.Add($"{name}: timed out ({app.State})");Next();return;}
                if(app.IsSpeaking){spoke=true;if(now>=next&&shot<16){Capture(name);shot++;next=now+.25;}}
                else if(spoke&&app.State.Contains("finished")) {
                    report.Add($"{name}: \"{app.LastResponse}\" [{app.Expression}] first text {app.FirstTextSeconds:F2} s, first audio {app.FirstAudioSeconds:F2} s, complete {app.ReplyCompletedSeconds:F2} s, {shot} face captures");
                    Next();
                }
            }catch(Exception e){Finish("FAIL "+e.Message);}
        }
        static void Next(){if(pending.Count>0)Begin();else Finish(null);}
        static void Capture(string name)
        {
            var rt=app.portraitCamera.targetTexture;if(rt==null)return;
            var head=app.character.GetComponentsInChildren<Transform>().First(t=>t.name=="CC_Base_Head");
            var eyes=app.character.GetComponentsInChildren<Transform>().Where(t=>t.name=="CC_Base_L_Eye"||t.name=="CC_Base_R_Eye").Select(t=>t.position).ToArray();
            Vector3 centre=eyes.Length==2?(eyes[0]+eyes[1])/2-app.character.up*.04f:head.position;
            var vp=app.portraitCamera.WorldToViewportPoint(centre);
            float size=Mathf.Abs(app.portraitCamera.WorldToViewportPoint(centre+app.character.up*.14f).y-vp.y)*rt.height;
            int s=Mathf.Clamp(Mathf.RoundToInt(size),64,rt.height);int x=Mathf.Clamp(Mathf.RoundToInt(vp.x*rt.width-s/2f),0,rt.width-s),y=Mathf.Clamp(Mathf.RoundToInt(vp.y*rt.height-s/2f),0,rt.height-s);
            var old=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(s,s,TextureFormat.RGBA32,false);
            image.ReadPixels(new Rect(x,y,s,s),0,0);image.Apply();RenderTexture.active=old;
            File.WriteAllBytes(Path.Combine(folder,$"{name.ToLowerInvariant()}-{shot:00}.png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
        }
        static void Finish(string error)
        {
            EditorApplication.update-=Tick;
            if(error!=null)report.Add(error);
            if(app!=null){app.Interrupt();app.SelectCharacter(original);app.NewChat();}
            File.WriteAllLines(Path.Combine(folder,"live-turns.txt"),report);Debug.Log("Live face capture: "+string.Join(" | ",report));
        }
    }
}
