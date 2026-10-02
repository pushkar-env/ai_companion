using Companion.Core;

static class FacialChecks
{
    public static void Run(Action<string,Action> check)
    {
        void Require(bool value){if(!value)throw new Exception("Facial assertion failed");}
        check("FACE-01 52 bindings: missing, duplicate and unknown rejected",()=>{
            Require(FacialProfile.Channels.Count==52&&FacialProfile.Channels.Distinct().Count()==52);
            Require(FacialProfile.ValidateBindings(FacialProfile.Channels).Count==0);
            var invalid=FacialProfile.Channels.Skip(1).Concat(new[]{"jawOpen","bogus"});
            var errors=FacialProfile.ValidateBindings(invalid);Require(errors.Count==3);
            Require(!FacialProfile.ValidWeight(float.NaN)&&!FacialProfile.ValidWeight(float.PositiveInfinity)&&!FacialProfile.ValidWeight(1.01f));
        });
        check("FACE-02 sample clock, silence and every diagnostic viseme",()=>{
            var playback=new VisemePlayback();long epoch=playback.Begin("test");var frames=new SyntheticVoiceAgent().DiagnosticFrames();
            Require(frames.Count==15);foreach(var f in frames)Require(playback.Enqueue(epoch,"test",f));
            var weights=new float[52];playback.Sample(100*24,24000,weights);Require(weights.All(w=>w==0));
            for(int i=1;i<15;i++){playback.Sample((i*250+100)*24,24000,weights);Require(weights.Any(w=>w>0)&&weights.All(w=>w>=0&&w<=1));playback.Sample((i*250+240)*24,24000,weights);Require(weights.All(w=>w==0));}
        });
        check("VOICE-03 interruption flushes, rejects stale epoch and old utterance",()=>{
            var p=new VisemePlayback();var frames=new SyntheticVoiceAgent().DiagnosticFrames();long old=p.Begin("a");Require(p.Enqueue(old,"a",frames[1]));p.Cancel();
            Require(!p.Enqueue(old,"a",frames[2])&&p.BufferedFrames==0);var w=new float[52];p.Sample(9000,24000,w);Require(w.All(x=>x==0));
            long fresh=p.Begin("b");Require(!p.Enqueue(fresh,"a",frames[0]));Require(p.Enqueue(fresh,"b",frames[0]));p.Sample(100,24000,w);p.Sample(0,24000,w);Require(!p.Active&&w.All(x=>x==0));
        });
        check("FACE-03 rejects invalid, reordered, overlapping and oversized frames",()=>{
            var p=new VisemePlayback();long e=p.Begin("a");var w=new float[52];w[0]=float.NaN;Require(!p.Enqueue(e,"a",new FacialFrame(0,100,w)));w[0]=0;
            Require(!p.Enqueue(e,"a",new FacialFrame(-1,100,w))&&!p.Enqueue(e,"a",new FacialFrame(0,501,w)));
            for(int i=0;i<200;i++)Require(p.Enqueue(e,"a",new FacialFrame(i*100,100,w)));
            Require(!p.Enqueue(e,"a",new FacialFrame(20000,100,w)));p.Cancel();e=p.Begin("b");Require(p.Enqueue(e,"b",new FacialFrame(100,100,w)));Require(!p.Enqueue(e,"b",new FacialFrame(150,100,w)));
        });
        check("FACE-04 mouth priority, expression channels and mute",()=>{
            var speech=new float[52];var expression=new float[52];var result=new float[52];speech[FacialProfile.Index("jawOpen")]=.8f;expression[FacialProfile.Index("jawOpen")]=1;expression[FacialProfile.Index("eyeBlinkLeft")]=.6f;
            FacialProfile.Mix(speech,expression,result,false);Require(result[FacialProfile.Index("jawOpen")]==.8f&&result[FacialProfile.Index("eyeBlinkLeft")]==.6f);
            FacialProfile.Mix(speech,expression,result,true);Require(result.All(x=>x==0));
        });
        check("VOICE-01 deterministic audio and 20 transport cycles never capture",()=>{
            var provider=new SyntheticVoiceAgent();var a=provider.Tone();Require(a.Length==90000&&a.SequenceEqual(provider.Tone())&&a.All(x=>Math.Abs(x)<=.061f));
            var t=new MockRealtimeTransport();t.Connect(false,false);Require(t.State==TransportState.Failed&&!t.CaptureActive);
            for(int i=0;i<20;i++){t.Connect(true,false);Require(t.State==TransportState.Listening&&!t.CaptureActive);t.End();Require(t.State==TransportState.Ended);}t.Dispose();
        });
    }
}
