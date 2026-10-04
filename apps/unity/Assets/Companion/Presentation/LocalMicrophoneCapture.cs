using System;
using Companion.Core;
using UnityEngine;

namespace Companion.Presentation
{
    // Capture only after a UI gesture. No recording file or automatic background restart.
    public sealed class LocalMicrophoneCapture
    {
        AudioClip clip;string device;float started;int lastPosition;
        readonly float[] meter=new float[256];
        public bool Recording=>clip!=null;
        public float Seconds=>Recording?Mathf.Max(0,Time.realtimeSinceStartup-started):0;
        public float Level {get;private set;}
        public bool LimitReached=>Recording&&Seconds>=MicrophonePcm.MaxSeconds;
        public string Error {get;private set;}
        public bool Begin(string selected)
        {
            Cancel();Error=null;
            try {
                if(string.IsNullOrEmpty(selected)||Array.IndexOf(Microphone.devices,selected)<0){Error="No microphone available. Connect one or type instead.";return false;}
                device=selected;clip=Microphone.Start(device,false,MicrophonePcm.MaxSeconds+1,MicrophonePcm.SampleRate);started=Time.realtimeSinceStartup;
                if(clip==null){Error="Microphone unavailable. Check Windows microphone permissions.";return false;}
                return true;
            }catch{Cancel();Error="Microphone unavailable. Check Windows microphone permissions.";return false;}
        }
        public void Tick()
        {
            if(!Recording)return;
            try {
                int position=Microphone.GetPosition(device);lastPosition=Math.Max(lastPosition,position);
                if(position>=256&&clip.GetData(meter,position-256)){float peak=0;foreach(float sample in meter)peak=Mathf.Max(peak,Mathf.Abs(sample));Level=peak;}
                if(Seconds>2&&lastPosition==0){Cancel();Error="Microphone did not start. Check device and permissions.";}
                else if(Seconds>2&&!Microphone.IsRecording(device)&&!LimitReached){Cancel();Error="Microphone disconnected. Recording discarded.";}
            }catch{Cancel();Error="Microphone disconnected. Recording discarded.";}
        }
        public byte[] Finish()
        {
            if(!Recording)return null;
            try {
                int frames=Math.Min(Math.Max(lastPosition,Microphone.GetPosition(device)),clip.frequency*MicrophonePcm.MaxSeconds);
                Microphone.End(device);
                if(frames<clip.frequency/4){Error="Recording too short. Try again.";return null;}
                var data=new float[frames*clip.channels];if(!clip.GetData(data,0)){Error="Unable to read recording. Try again.";return null;}
                try{return MicrophonePcm.Encode(data,clip.channels,clip.frequency,frames);}finally{Array.Clear(data,0,data.Length);}
            }catch{Error="Unable to read recording. Try again.";return null;}
            finally{Cancel();}
        }
        public void Cancel()
        {
            if(clip!=null){try{Microphone.End(device);}catch{}UnityEngine.Object.Destroy(clip);clip=null;}
            lastPosition=0;Level=0;device=null;Array.Clear(meter,0,meter.Length);
        }
    }
}
