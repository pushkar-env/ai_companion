using System;
namespace Companion.Core
{
    public static class MicrophonePcm
    {
        public const int SampleRate=16000,MaxSeconds=20;
        public static byte[] Encode(float[] interleaved,int channels,int sourceRate,int frames)
        {
            if(interleaved==null||channels<1||channels>8||sourceRate<8000||sourceRate>192000||frames<sourceRate/4||frames>sourceRate*MaxSeconds||(long)frames*channels>interleaved.Length)throw new ArgumentException("Invalid microphone samples");
            int count=(int)((long)frames*SampleRate/sourceRate);var pcm=new byte[count*2];
            for(int i=0;i<count;i++) {
                double position=i*(double)sourceRate/SampleRate;int a=(int)position,b=Math.Min(frames-1,a+1);double sum=0;
                for(int c=0;c<channels;c++) {float x=interleaved[a*channels+c],y=interleaved[b*channels+c];if(float.IsNaN(x)||float.IsInfinity(x)||float.IsNaN(y)||float.IsInfinity(y))throw new ArgumentException("Invalid sample");sum+=x+(y-x)*(position-a);}
                short sample=(short)Math.Round(Math.Max(-1,Math.Min(1,sum/channels))*32767);
                pcm[2*i]=(byte)(sample&255);pcm[2*i+1]=(byte)((sample>>8)&255);
            }
            return pcm;
        }
    }
}
