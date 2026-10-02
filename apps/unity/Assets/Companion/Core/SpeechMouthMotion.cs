using System;

namespace Companion.Core
{
    // Coarticulation on the audio clock, followed by short muscle-like easing.
    // Adjacent sounds share a pose instead of closing the mouth at every phoneme.
    public sealed class SpeechMouthMotion
    {
        public static readonly string[] Channels={"V_Open","V_Tight_O","V_Wide","V_Tight","V_Tongue_up","V_Affricate","V_Tongue_Out","V_Dental_Lip","V_Tongue_Raise","V_Explosive"};
        static readonly int[] Mapping={-1,0,0,1,2,3,2,1,1,0,1,0,3,3,4,5,5,6,7,4,8,9};
        readonly float[] weights=new float[Channels.Length],targets=new float[Channels.Length];
        double[] times=Array.Empty<double>();int[] ids=Array.Empty<int>();
        public float Jaw {get;private set;}
        public float Weight(int channel)=>weights[channel];
        public void SetCues(double[] positions,int[] visemes)
        {
            if(positions==null||visemes==null||positions.Length!=visemes.Length)throw new ArgumentException("Invalid cues");
            for(int i=0;i<positions.Length;i++)if(double.IsNaN(positions[i])||double.IsInfinity(positions[i])||positions[i]<0||(i>0&&positions[i]<positions[i-1])||visemes[i]<0||visemes[i]>21)throw new ArgumentException("Invalid cue");
            times=(double[])positions.Clone();ids=(int[])visemes.Clone();Reset();
        }
        public void Reset(){Array.Clear(weights,0,weights.Length);Jaw=0;}
        public void Step(double ms,double dt,bool playing)
        {
            Array.Clear(targets,0,targets.Length);float targetJaw=0;
            if(playing&&times.Length>0&&ms>=times[0]) {
                int index=Array.BinarySearch(times,ms);if(index<0)index=~index-1;
                int left=index,right=index;float blend=0;
                if(index>0&&ms<times[index]+Window(index)) {left=index-1;right=index;blend=Blend(ms,index);}
                else if(index+1<times.Length&&ms>times[index+1]-Window(index+1)) {right=index+1;blend=Blend(ms,index+1);}
                Add(ids[left],1-blend,ref targetJaw);if(right!=left)Add(ids[right],blend,ref targetJaw);
            }
            dt=Math.Max(0,Math.Min(.1,dt));
            for(int i=0;i<weights.Length;i++) {
                double tau=targets[i]>weights[i]?.045:.075;
                weights[i]+=(targets[i]-weights[i])*(float)(1-Math.Exp(-dt/tau));
            }
            Jaw+=(targetJaw-Jaw)*(float)(1-Math.Exp(-dt/.085));
        }
        double Window(int boundary)
        {
            double half=Math.Min(55,(times[boundary]-times[boundary-1])*.45);
            if(boundary+1<times.Length)half=Math.Min(half,(times[boundary+1]-times[boundary])*.45);
            return Math.Max(.001,half);
        }
        float Blend(double ms,int boundary)
        {
            double half=Window(boundary);double t=Math.Max(0,Math.Min(1,(ms-times[boundary]+half)/(2*half)));
            return (float)(t*t*(3-2*t));
        }
        void Add(int id,float amount,ref float targetJaw)
        {
            int channel=Mapping[id];if(channel<0)return;
            // Softer vowels and restrained tongue/closed consonants on the provisional rig.
            float strength=id==21?.48f:(id==17||id==20?.25f:.42f);
            targets[channel]+=strength*amount;
            float opening=id==21?0:(id==18?.06f:(id==1||id==2||id==9||id==11?.28f:.16f));
            targetJaw+=opening*amount;
        }
    }
}
