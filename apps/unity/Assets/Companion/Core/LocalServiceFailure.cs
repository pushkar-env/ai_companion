namespace Companion.Core
{
    // Display only known local error codes, never raw server bodies or request data.
    public static class LocalServiceFailure
    {
        public static string Explain(string code,bool transcription)
        {
            string retry=transcription?"Record again or type instead.":"Then Retry.";
            switch(code) {
                case "busy_retry":return "Local service is busy. Wait a moment. "+retry;
                case "unauthorized":return "Local connection expired. Check setup to reconnect. "+retry;
                case "cancelled_or_timeout":return "Local request timed out. Try a shorter message or recording. "+retry;
                case "local_model_unavailable":return "Local AI unavailable. Start Ollama and check setup. "+retry;
                case "speech_unavailable":return transcription?"Recognition unavailable. Check local speech setup; record again or type instead.":"Speech unavailable. Check Windows voice setup, then Retry.";
                case "invalid_model_reply":return "The local AI returned an unusable reply. Retry or rephrase your message.";
                case "invalid_audio":return "Recording format unavailable. Record again or type instead.";
                default:return "Local service unavailable or response interrupted. Check setup. "+retry;
            }
        }
        public static string FromHttp(long status,bool timedOut,bool transcription)
            =>Explain(timedOut?"cancelled_or_timeout":status==429?"busy_retry":status==401?"unauthorized":null,transcription);
    }
}
