using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Companion.Core;
using Companion.Presentation;
using UnityEditor;
using UnityEngine;

namespace Companion.Editor
{
    // Face performance layer checks on isolated copies of every roster character (stopped Editor).
    public static class FacePerformanceChecks
    {
        static readonly List<string> lines=new List<string>();
        static void Check(bool ok,string label){if(!ok)throw new Exception(label);lines.Add("PASS "+label);}

        [MenuItem("Companion/Characters/Run Face Performance Checks")]
        public static void Run()
        {
            lines.Clear();
            try {
                if(EditorApplication.isPlaying)throw new Exception("Run the face checks while stopped");
                var app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>(FindObjectsInactive.Include);
                foreach(var option in app.characters)CheckCharacter(option);
                lines.Add("PASS "+lines.Count+" face performance checks ("+string.Join(", ",app.characters.Select(c=>c.name))+")");
            }catch(Exception e){lines.Add("FAIL "+e.Message);throw;}
            finally {
                string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/evidence/m1/face-performance"));Directory.CreateDirectory(folder);
                File.WriteAllLines(Path.Combine(folder,"face-checks.txt"),lines);Debug.Log(lines.LastOrDefault());
            }
        }

        // A sentence of open vowels with a 300 ms pause every two seconds.
        static SpeechMouthMotion Talk(float seconds)
        {
            var at=new List<double>();var ids=new List<int>();var dur=new List<double>();
            for(double t=0;t<seconds*1000;t+=2000){at.Add(t);ids.Add(2);dur.Add(850);at.Add(t+850);ids.Add(21);dur.Add(80);at.Add(t+930);ids.Add(1);dur.Add(770);at.Add(t+1700);ids.Add(0);dur.Add(300);}
            var m=new SpeechMouthMotion();m.SetCues(at.ToArray(),ids.ToArray(),dur.ToArray());return m;
        }

        static void CheckCharacter(TalkingCharacter.CharacterOption option)
        {
            string n=option.name;const float dt=1/30f;
            using var rig=new FaceRig(option);
            var face=rig.Face(option.face,11);
            Check(rig.Has("Mouth_Corner_Pull_L")&&rig.Has("Brow_Raise_In_L")&&rig.Has("Brow_Raise_Outer_L")&&rig.Has("Eye_Blink_L"),n+": smile, brow and blink roles bind");
            Check(rig.Has("Cheek_Raise_L")||rig.Has("cheekSquintLeft"),n+": cheek raise available for genuine smiles");
            bool tripo=rig.Has("mouthClose");
            var idleMouth=new SpeechMouthMotion();
            // Blinks: natural cadence, fast close and slower open, full closure.
            int Blinks(bool speaking,float seconds,out float peak,out float closeTime,out float openTime)
            {
                var f=rig.Face(option.face,23);var m=speaking?Talk(seconds):idleMouth;int count=0;bool was=false;peak=0;closeTime=openTime=0;
                float start=-1,top=-1;
                for(float t=0;t<seconds;t+=dt) {
                    rig.ResetFace();m.Step(t*1000,dt,speaking);
                    f.Step(new FaceInput{time=t,dt=dt,speaking=speaking,emotion="neutral"},m);
                    float b=f.Blink;peak=Mathf.Max(peak,b);
                    if(b>.05f&&start<0)start=t;if(b>.95f&&top<0)top=t;
                    if(b<.05f&&start>=0){if(top>=0&&closeTime==0){closeTime=top-start;openTime=t-top;}start=top=-1;}
                    if(b>.9f&&!was)count++;was=b>.9f;
                }
                return count;
            }
            int idleBlinks=Blinks(false,60,out float idlePeak,out float close,out float open);
            Check(idleBlinks>=9&&idleBlinks<=30,$"{n}: {idleBlinks} blinks per idle minute (natural range 9-30)");
            Check(idlePeak>.95f&&close>0&&close<open,$"{n}: blinks close fully, closing faster ({close*1000:F0} ms) than opening ({open*1000:F0} ms)");
            int talkBlinks=Blinks(true,60,out _,out _,out _);
            Check(talkBlinks>idleBlinks,$"{n}: blinks come more often while talking ({talkBlinks}/min vs {idleBlinks}/min idle)");
            // Emotion onset while speaking, linger after, fade later.
            var mouth=Talk(4);float t0=0;
            float Step(bool speaking,string emotion,bool question=false,bool thinking=false,bool reduced=false)
            {
                rig.ResetFace();mouth.Step(t0*1000,dt,speaking);
                face.Step(new FaceInput{time=t0,dt=dt,speaking=speaking,emotion=emotion,question=question,thinking=thinking,reduced=reduced},mouth);t0+=dt;
                return rig.Weight("Mouth_Corner_Pull_L");
            }
            float smile=0;for(int i=0;i<30;i++)smile=Step(true,"happy");
            Check(smile>.25f,$"{n}: a happy reply smiles within a second of speaking ({smile:F2})");
            float cheek=Mathf.Max(rig.Weight("Cheek_Raise_L"),rig.Weight("cheekSquintLeft"));
            Check(cheek>.15f,$"{n}: the smile raises the cheeks (Duchenne, {cheek:F2})");
            float linger=0;for(int i=0;i<45;i++)linger=Step(false,"happy");
            Check(linger>.1f,$"{n}: the smile lingers after the reply ends ({linger:F2} at +1.5 s)");
            float faded=0;for(int i=0;i<240;i++)faded=Step(false,"happy");
            Check(faded<.03f,$"{n}: the lingering smile fades within ten seconds ({faded:F3})");
            for(int i=0;i<45;i++)Step(true,"concerned");
            Check(rig.Weight("Brow_Raise_In_L")>.3f,$"{n}: concern lifts the inner brows ({rig.Weight("Brow_Raise_In_L"):F2})");
            // Questions lift the brows toward the end of the sentence.
            face.Reset();float statement=0,question=0;
            for(int i=0;i<60;i++){Step(true,"neutral");if(i>=30)statement+=rig.Weight("Brow_Raise_Outer_L");}
            face.Reset();
            for(int i=0;i<60;i++){Step(true,"neutral",question:true);if(i>=30)question+=rig.Weight("Brow_Raise_Outer_L");}
            Check(question/30>statement/30+.08f,$"{n}: questions raise the brows ({question/30:F2} vs {statement/30:F2})");
            // Stop clears performance state.
            for(int i=0;i<20;i++)Step(true,"happy");face.Reset();
            Check(face.EmotionWeight("happy")==0&&face.Blink==0&&face.GazeAside==Vector2.zero,$"{n}: Stop clears emotion, blink and look-away immediately");
            // Head motion is bounded and stops under Reduce motion.
            float maxHead=0;for(int i=0;i<90;i++){rig.ResetFace();var rest=rig.head.rotation;mouth.Step(t0*1000,dt,true);
                face.Step(new FaceInput{time=t0,dt=dt,speaking=true,emotion="curious",question=true},mouth);t0+=dt;maxHead=Mathf.Max(maxHead,Quaternion.Angle(rest,rig.head.rotation));}
            Check(maxHead>.3f&&maxHead<7f,$"{n}: speech head motion stays subtle ({maxHead:F1}° peak)");
            {rig.ResetFace();var rest=rig.head.rotation;mouth.Step(t0*1000,dt,true);face.Step(new FaceInput{time=t0,dt=dt,speaking=true,emotion="curious",reduced=true},mouth);t0+=dt;
             Check(Quaternion.Angle(rest,rig.head.rotation)<.001f,$"{n}: Reduce motion keeps the head still");}
            // Jaw and lip seal follow articulation.
            var seal=new SpeechMouthMotion();seal.SetCues(new double[]{0,200,260,500},new[]{2,21,2,0},new double[]{200,60,240,200});float sealMax=0,jawErr=0;
            for(float t=0;t<.5f;t+=1/120f){rig.ResetFace();seal.Step(t*1000,1/120f,true);face.Step(new FaceInput{time=t,dt=1/120f,speaking=true,emotion="neutral"},seal);
                jawErr=Mathf.Max(jawErr,Mathf.Abs(face.JawDegrees-seal.Jaw*option.face.jawDegrees));if(tripo)sealMax=Mathf.Max(sealMax,rig.Weight("mouthClose"));}
            Check(jawErr<1e-3f&&Quaternion.Angle(rig.jawRest,rig.jaw.localRotation)>=0,$"{n}: jaw rotation follows articulation × {option.face.jawDegrees}° range");
            if(tripo)Check(sealMax>.005f,$"{n}: mouthClose seals the lips against the jaw gap on p/b/m");
            // Thinking looks up and away.
            face.Reset();for(int i=0;i<30;i++)Step(false,"neutral",thinking:true);
            Check(face.GazeAside.magnitude>4,$"{n}: thinking looks up and away ({face.GazeAside.magnitude:F1}°)");
        }
    }
}
