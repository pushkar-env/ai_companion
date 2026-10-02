using System;
using System.IO;
using System.Collections.Generic;
using Companion.Core;
using UnityEditor;
using UnityEngine;

namespace Companion.Editor
{
    public static class SpeechMouthChecks
    {
        [MenuItem("Companion/Run Speech Mouth Motion Checks")]
        public static void Run()
        {
            var lines=new List<string>();
            Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);lines.Add("PASS "+label);};
            var mouth=new SpeechMouthMotion();
            mouth.SetCues(new double[]{0,400,500,600,700,1000},new int[]{1,1,2,1,2,0});
            float min=1,maxStep=0,last=0;
            for(int frame=0;frame<=60;frame++){
                mouth.Step(frame*1000.0/60,1.0/60,true);float value=mouth.Weight(0);
                if(frame>=24&&frame<=48)min=Math.Min(min,value);
                maxStep=Math.Max(maxStep,Math.Abs(value-last));last=value;
            }
            check(min>.40f,"repeated equivalent vowels no longer pulse closed at each cue");
            check(maxStep<.14f,"60 FPS opening step stays below 14 blendshape percentage points");
            check(mouth.Jaw<.29f,"jaw opening remains below 4.64 degrees");
            for(int i=0;i<40;i++)mouth.Step(1200+i*1000.0/60,1.0/60,true);
            check(mouth.Weight(0)<.002f&&mouth.Jaw<.002f,"silence cues settle to neutral");
            mouth.SetCues(new double[]{0,400,800},new int[]{1,7,0});
            for(int frame=0;frame<=26;frame++)mouth.Step(frame*1000.0/60,1.0/60,true);
            check(mouth.Weight(0)>.01f&&mouth.Weight(1)>.01f,"neighboring mouth shapes overlap during transition");
            mouth.Reset();check(mouth.Weight(0)==0&&mouth.Weight(1)==0&&mouth.Jaw==0,"interrupt clears smoothed motion immediately");
            var slow=new SpeechMouthMotion();var fast=new SpeechMouthMotion();
            slow.SetCues(new double[]{0},new int[]{1});fast.SetCues(new double[]{0},new int[]{1});
            for(int i=1;i<=6;i++)slow.Step(i*1000.0/30,1.0/30,true);
            for(int i=1;i<=24;i++)fast.Step(i*1000.0/120,1.0/120,true);
            check(Math.Abs(slow.Weight(0)-fast.Weight(0))<.001f&&Math.Abs(slow.Jaw-fast.Jaw)<.001f,"easing is consistent at 30 and 120 FPS");
            for(int i=0;i<60;i++)fast.Step(0,1.0/60,false);
            check(fast.Weight(0)<.001f&&fast.Jaw<.001f,"natural speech end releases mouth gently");
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/evidence/m1/lip-smoothing"));Directory.CreateDirectory(folder);
            lines.Add("PASS 8 speech mouth motion checks");File.WriteAllLines(Path.Combine(folder,"motion-checks.txt"),lines);Debug.Log(lines[lines.Count-1]);
        }
    }
}
