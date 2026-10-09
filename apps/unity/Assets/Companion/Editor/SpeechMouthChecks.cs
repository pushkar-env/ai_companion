using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Companion.Core;
using UnityEditor;
using UnityEngine;

namespace Companion.Editor
{
    // Deterministic checks of the speech articulation model (no Editor scene or audio needed).
    public static class SpeechMouthChecks
    {
        const int Open=0,Round=1,Wide=2,TongueUp=4,Dental=7,Explosive=9;
        static SpeechMouthMotion Make(double[] at,int[] ids,double[] durations){var m=new SpeechMouthMotion();m.SetCues(at,ids,durations);return m;}
        // Steps at fps over [0,toMs] and returns per-frame samples (ms, weights..., jaw, seal).
        static List<(double ms,float[] w,float jaw,float seal)> Run(SpeechMouthMotion m,double toMs,int fps,double playEnd=double.MaxValue)
        {
            var list=new List<(double,float[],float,float)>();
            for(int f=0;f*1000.0/fps<=toMs;f++){double ms=f*1000.0/fps;m.Step(ms,1.0/fps,ms<playEnd);list.Add((ms,Enumerable.Range(0,10).Select(m.Weight).ToArray(),m.Jaw,m.Seal));}
            return list;
        }
        static IEnumerable<(double ms,float[] w,float jaw,float seal)> Between(List<(double ms,float[] w,float jaw,float seal)> s,double a,double b)=>s.Where(x=>x.ms>=a&&x.ms<=b);

        [MenuItem("Companion/Run Speech Mouth Motion Checks")]
        public static void Run()
        {
            var lines=new List<string>();
            Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);lines.Add("PASS "+label);};
            // 1-3 continuity on repeated open vowels, bounded per-frame change, bounded jaw
            var mouth=Make(new double[]{0,100,400,500,600,700,1000},new[]{0,1,1,2,1,2,0},new double[]{100,300,100,100,100,300,300});
            var s=Run(mouth,1500,60,1300);
            float minOpen=Between(s,250,900).Min(x=>x.w[Open]);
            check(minOpen>.3f,$"repeated open vowels stay open instead of pulsing shut (min V_Open {minOpen:F2})");
            float step=0;for(int i=1;i<s.Count;i++)step=Mathf.Max(step,Mathf.Max(Mathf.Abs(s[i].w[Open]-s[i-1].w[Open]),Mathf.Abs(s[i].jaw-s[i-1].jaw)));
            check(step<.2f,$"60 FPS vowel motion changes under 0.2 per frame (max {step:F2})");
            check(s.All(x=>x.jaw>=0&&x.jaw<=1.2f),"jaw articulation stays within 0-1.2 of the rig's range");
            // 4 silence settles; 8 release is gentle, not instant
            var end=s.First(x=>x.ms>=1300);var later=s.Last();
            check(later.w.Max()<.01f&&later.jaw<.01f,"silence and playback end settle to neutral");
            var held=Make(new double[]{0},new[]{2},new double[]{400});var h=Run(held,300,60);var peak=h.Last().w[Open];
            held.Step(316,1/60.0,false);
            check(held.Weight(Open)>peak*.5f,"the mouth releases over a few frames when playback ends, not in one frame");
            // 5 neighbouring shapes overlap during a transition
            var t=Run(Make(new double[]{0,200,400},new[]{2,7,0},new double[]{200,200,300}),500,60);
            check(Between(t,170,230).Any(x=>x.w[Open]>.05f&&x.w[Round]>.05f),"neighbouring mouth shapes overlap during the transition");
            // 6 Reset is immediate
            var r=Make(new double[]{0},new[]{2},new double[]{400});Run(r,200,60);r.Reset();
            check(Enumerable.Range(0,10).All(i=>r.Weight(i)==0)&&r.Jaw==0&&r.Seal==0,"Stop clears all articulation immediately");
            // 7 frame-rate independence
            var slow=Make(new double[]{0,120,240},new[]{1,7,2},new double[]{120,120,200});var fast=Make(new double[]{0,120,240},new[]{1,7,2},new double[]{120,120,200});
            for(int i=1;i<=12;i++)slow.Step(i*1000.0/30,1.0/30,true);
            for(int i=1;i<=48;i++)fast.Step(i*1000.0/120,1.0/120,true);
            float diff=Enumerable.Range(0,10).Max(i=>Mathf.Abs(slow.Weight(i)-fast.Weight(i)));diff=Mathf.Max(diff,Mathf.Abs(slow.Jaw-fast.Jaw));
            check(diff<.01f,$"30 and 120 FPS give the same articulation (max difference {diff:F4})");
            // 9 diphthongs: Windows speech reports both halves at one timestamp
            var d=Run(Make(new double[]{0,100,100,300},new[]{0,2,6,0},new double[]{100,100,100,300}),400,120);
            float earlyOpen=Between(d,120,190).Max(x=>x.w[Open]),earlyWide=Between(d,120,150).Max(x=>x.w[Wide]),lateWide=Between(d,230,290).Max(x=>x.w[Wide]),lateOpen=Between(d,260,290).Max(x=>x.w[Open]);
            check(earlyOpen>earlyWide&&lateWide>lateOpen&&earlyOpen>.25f&&lateWide>.25f,$"both halves of a diphthong are articulated in order (aa {earlyOpen:F2} then iy {lateWide:F2})");
            // 10 p/b/m seal however short; 11 f/v lower lip reaches the teeth
            var p=Run(Make(new double[]{0,150,210,400},new[]{2,21,2,0},new double[]{150,60,190,200}),600,120);
            var mid=Between(p,175,185).ToArray();
            check(mid.All(x=>x.w[Explosive]>.9f&&x.seal>.9f&&x.jaw<.2f&&x.w[Open]<.15f),$"a 60 ms p seals the lips (explosive {mid.Min(x=>x.w[Explosive]):F2}, jaw {mid.Max(x=>x.jaw):F2})");
            var after=Between(p,265,300).Max(x=>x.w[Open]);
            check(after>.3f,$"the lips release quickly after the closure (V_Open {after:F2} within 90 ms)");
            var f=Run(Make(new double[]{0,150,220,400},new[]{2,18,2,0},new double[]{150,70,180,200}),600,120);
            check(Between(f,180,190).All(x=>x.w[Dental]>.85f),"f/v brings the lower lip to the upper teeth");
            // 12 rounding anticipation; 13 tongue phones move the tongue
            var w=Run(Make(new double[]{0,200,350},new[]{15,7,0},new double[]{200,150,300}),600,120);
            var early=w.Where(x=>x.w[Round]>.2f).Select(x=>x.ms).DefaultIfEmpty(9999).First();
            check(early<=140,$"lip rounding for 'w' starts at least 60 ms early ({early:F0} ms vs cue at 200 ms)");
            var l=Run(Make(new double[]{0,150,220,400},new[]{2,14,2,0},new double[]{150,70,180,200}),600,120);
            check(Between(l,160,230).Max(x=>x.w[TongueUp])>.4f,"l lifts the tongue tip");
            // 14 loudness: the same vowel opens wider when louder
            // loudness is relative within a sentence: a quiet syllable vs a stressed one in one clip
            var lm=Make(new double[]{0,100,300,500,700},new[]{0,2,0,2,0},new double[]{100,200,200,200,300});
            var lenv=new float[100];for(int i=0;i<lenv.Length;i++)lenv[i]=i>=10&&i<30?.35f:i>=50&&i<70?1f:.02f;
            lm.SetEnvelope(lenv,10);var ls=Run(lm,900,120);
            float quiet=Between(ls,100,320).Max(x=>x.jaw),loud=Between(ls,500,720).Max(x=>x.jaw);
            check(loud>quiet*1.15f,$"loud syllables open the jaw wider than quiet ones ({loud:F2} vs {quiet:F2})");
            // 15 emphasis peaks on loud syllables; 16 pauses
            var e=Make(new double[]{0,1200},new[]{1,0},new double[]{1200,300});
            var envelope=new float[150];for(int i=0;i<envelope.Length;i++){float tt=i*10;envelope[i]=.15f+.85f*Mathf.Max(0,Mathf.Max(Bump(tt,200),Mathf.Max(Bump(tt,650),Bump(tt,1100))));}
            e.SetEnvelope(envelope,10);
            check(e.EmphasisPeakCount==3,$"three loud syllables give three emphasis pulses ({e.EmphasisPeakCount})");
            var es=new List<float>();for(int i=0;i<=40;i++){e.Step(i*1000.0/60,1/60.0,true);es.Add(e.Emphasis);}
            check(es.Max()>.5f,"emphasis rises around a stressed syllable");
            var pause=Make(new double[]{0,200,600},new[]{2,0,2},new double[]{200,400,200});
            bool inPause=false;for(int i=0;i<=30;i++){pause.Step(i*1000.0/60,1/60.0,true);if(i*1000.0/60>=300&&i*1000.0/60<=500)inPause|=pause.InPause;}
            check(inPause,"a 400 ms silence inside a sentence is reported as a pause (blink point)");
            // 17 a new sentence clip keeps the current pose
            var c=Make(new double[]{0},new[]{2},new double[]{400});Run(c,200,60);float before=c.Weight(Open);
            c.SetCues(new double[]{0},new[]{6},new double[]{300});
            check(Mathf.Abs(c.Weight(Open)-before)<1e-6f&&before>.2f,"starting the next sentence does not snap the mouth shut");
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/evidence/m1/face-performance"));Directory.CreateDirectory(folder);
            lines.Add("PASS "+lines.Count+" speech articulation checks");File.WriteAllLines(Path.Combine(folder,"speech-motion-checks.txt"),lines);Debug.Log(lines[lines.Count-1]);
        }
        static float Bump(float t,float centre)=>Mathf.Exp(-Mathf.Pow((t-centre)/70f,2));
    }
}
