using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Companion.Editor
{
    public sealed class PortraitBuildPolicy : IPreprocessBuildWithReport
    {
        public int callbackOrder=>-1000;
        public static bool IsPortraitOnly()=>PlayerSettings.defaultInterfaceOrientation==UIOrientation.Portrait
            &&!PlayerSettings.allowedAutorotateToLandscapeLeft&&!PlayerSettings.allowedAutorotateToLandscapeRight
            &&!PlayerSettings.allowedAutorotateToPortraitUpsideDown;
        public void OnPreprocessBuild(BuildReport report)
        {
            if((report.summary.platform==BuildTarget.Android||report.summary.platform==BuildTarget.iOS)&&!IsPortraitOnly())
                throw new BuildFailedException("Mobile builds must be portrait-only. Restore portrait orientation and disable landscape/upside-down autorotation.");
        }
    }
}
