using UnityEngine;

namespace Companion.Presentation
{
    public static class MobilePortraitLock
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
            Screen.autorotateToLandscapeLeft=false;
            Screen.autorotateToLandscapeRight=false;
            Screen.autorotateToPortraitUpsideDown=false;
            Screen.autorotateToPortrait=true;
            Screen.orientation=ScreenOrientation.Portrait;
#endif
        }
    }
}
