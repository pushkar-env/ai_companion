using System.Collections;
using UnityEngine;

namespace Companion.Presentation
{
    public interface IMicrophonePermission
    {
        bool Granted {get;}
        bool CanOpenSettings {get;}
        IEnumerator Request();
        bool OpenSettings();
    }

    public sealed class MicrophonePermission : IMicrophonePermission
    {
        public bool Granted {
            get {
#if UNITY_ANDROID && !UNITY_EDITOR
                return UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone);
#elif UNITY_IOS && !UNITY_EDITOR
                return Application.HasUserAuthorization(UserAuthorization.Microphone);
#else
                return true; // Desktop capture reports OS/device failures itself.
#endif
            }
        }
        public bool CanOpenSettings=>Application.isMobilePlatform&&!Application.isEditor;
        public IEnumerator Request()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            bool done=false;
            var callbacks=new UnityEngine.Android.PermissionCallbacks();
            callbacks.PermissionGranted+=_=>done=true;
            callbacks.PermissionDenied+=_=>done=true;
            callbacks.PermissionDeniedAndDontAskAgain+=_=>done=true;
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone,callbacks);
            float until=Time.realtimeSinceStartup+60;
            while(!done&&Time.realtimeSinceStartup<until)yield return null;
#elif UNITY_IOS && !UNITY_EDITOR
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
#else
            yield return null;
#endif
        }
        public bool OpenSettings()
        {
            try {
#if UNITY_ANDROID && !UNITY_EDITOR
                using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
                using(var uriClass=new AndroidJavaClass("android.net.Uri"))
                using(var uri=uriClass.CallStatic<AndroidJavaObject>("parse","package:"+Application.identifier))
                using(var intent=new AndroidJavaObject("android.content.Intent","android.settings.APPLICATION_DETAILS_SETTINGS",uri))
                    activity.Call("startActivity",intent);
                return true;
#elif UNITY_IOS && !UNITY_EDITOR
                Application.OpenURL("app-settings:");return true;
#else
                return false;
#endif
            }catch{return false;}
        }
    }
}
