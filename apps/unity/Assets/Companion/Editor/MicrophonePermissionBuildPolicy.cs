using System.IO;
using System.Xml;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Companion.Editor
{
    public sealed class MicrophonePermissionBuildPolicy : IPreprocessBuildWithReport
#if UNITY_ANDROID
        , UnityEditor.Android.IPostGenerateGradleAndroidProject
#endif
    {
        public const string Usage="Record a voice message so you can review its words before sending. You can also type instead.";
        public int callbackOrder=>100;
        public void OnPreprocessBuild(BuildReport report)
        {
            if(report.summary.platform==BuildTarget.iOS&&string.IsNullOrWhiteSpace(PlayerSettings.iOS.microphoneUsageDescription))
                throw new BuildFailedException("Set the iOS microphone usage description before building.");
        }
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifest=Path.Combine(path,"src/main/AndroidManifest.xml");
            File.WriteAllText(manifest,ConfigureAndroidManifest(File.ReadAllText(manifest)));
        }
        public static string ConfigureAndroidManifest(string source)
        {
            const string android="http://schemas.android.com/apk/res/android";
            var doc=new XmlDocument();doc.LoadXml(source);
            var application=doc.DocumentElement.SelectSingleNode("application") as XmlElement;
            if(application==null)throw new BuildFailedException("Generated Android manifest has no application element.");
            XmlElement entry=null;
            foreach(XmlElement child in application.SelectNodes("meta-data"))
                if(child.GetAttribute("name",android)=="unityplayer.SkipPermissionsDialog"){entry=child;break;}
            if(entry==null){entry=doc.CreateElement("meta-data");application.AppendChild(entry);entry.SetAttribute("name",android,"unityplayer.SkipPermissionsDialog");}
            entry.SetAttribute("value",android,"true");
            bool hasRecord=false;
            foreach(XmlElement child in doc.DocumentElement.SelectNodes("uses-permission"))hasRecord|=child.GetAttribute("name",android)=="android.permission.RECORD_AUDIO";
            if(!hasRecord){var permission=doc.CreateElement("uses-permission");permission.SetAttribute("name",android,"android.permission.RECORD_AUDIO");doc.DocumentElement.AppendChild(permission);}
            return doc.OuterXml;
        }
    }
}
