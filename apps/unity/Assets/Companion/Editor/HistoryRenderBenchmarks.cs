using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using Companion.Core;
using Companion.Presentation;
using UnityEditor;
using UnityEngine;
namespace Companion.Editor
{
    public static class HistoryRenderBenchmarks
    {
        [MenuItem("Companion/Measure History Render Current")]
        public static void Current()=>Run("after");
        static void Run(string name)
        {
            var view=new SyntheticHistoryView();view.Configure("http://127.0.0.1:12345/",new string('a',64),Guid.NewGuid().ToString());
            var client=(SyntheticHistoryTransport)typeof(SyntheticHistoryView).GetField("client",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view);
            for(int i=0;i<1000;i++) {
                string turn=Guid.NewGuid().ToString();
                client.History.Apply(new AccountEvent {event_id=Guid.NewGuid().ToString(),turn_id=turn,sequence=i*2+1,version=1,type="turn.accepted",text="[SYNTHETIC] A short user message."});
                client.History.Apply(new AccountEvent {event_id=Guid.NewGuid().ToString(),turn_id=turn,sequence=i*2+2,version=2,type="turn.completed",text="[SYNTHETIC] This is a deterministic reply for measuring history rendering without provider calls."});
            }
            var render=typeof(SyntheticHistoryView).GetMethod("Render",BindingFlags.Instance|BindingFlags.NonPublic);render.Invoke(view,null);
            var lines=new string[4];lines[0]="1000 synthetic completed turns, warm render x3, detached UI construction; not frame time or RSS.";
            for(int i=0;i<3;i++) {
                long before=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.StartNew();render.Invoke(view,null);watch.Stop();
                lines[i+1]="sample="+i+" ms="+watch.Elapsed.TotalMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" runtime_allocation_counter_unverified="+(GC.GetAllocatedBytesForCurrentThread()-before);
            }
            view.Dispose();var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/evidence/m2/history-performance"));Directory.CreateDirectory(folder);File.WriteAllLines(Path.Combine(folder,name+".txt"),lines);
        }
    }
}
