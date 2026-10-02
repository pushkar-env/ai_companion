using System;
using System.Threading;

namespace Companion.Core
{
    public interface IIdentityProvider { string Subject { get; } bool IsMock { get; } }
    public interface ICommerceProvider { string Purchase(string productId); bool HasEntitlement(string entitlement); }
    public interface IPushProvider { string Schedule(string fixtureId); }
    public interface IVoiceProvider { float[] SyntheticAudio(int sampleRate, int milliseconds, CancellationToken cancel); }
    public sealed class LocalMockServices : IIdentityProvider, ICommerceProvider, IPushProvider, IVoiceProvider
    {
        public bool IsMock => true;
        public string Subject => "00000000-0000-4000-8000-000000000001";
        public string Purchase(string productId) => "mock_checkout_disabled";
        public bool HasEntitlement(string entitlement) => false;
        public string Schedule(string fixtureId) => "mock_not_delivered:" + fixtureId;
        public float[] SyntheticAudio(int sampleRate,int milliseconds,CancellationToken cancel)
        {
            if(sampleRate<8000||sampleRate>48000||milliseconds<1||milliseconds>2000) throw new ArgumentOutOfRangeException();
            var pcm=new float[sampleRate*milliseconds/1000];
            for(int i=0;i<pcm.Length;i++) {cancel.ThrowIfCancellationRequested();pcm[i]=(float)(.1*Math.Sin(2*Math.PI*220*i/sampleRate));}
            return pcm; // Diagnostic tone, not synthesized speech. Never auto-played.
        }
    }
}
