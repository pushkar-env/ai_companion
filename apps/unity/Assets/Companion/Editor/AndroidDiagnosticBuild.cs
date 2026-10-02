using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Companion.Editor
{
    public static class AndroidDiagnosticBuild
    {
        // Batch-only entry point: the runner creates an isolated source snapshot.
        public static void Run()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Use tools/build-android-test.ps1; preserves the interactive Editor.");
            string output=Environment.GetEnvironmentVariable("COMPANION_ANDROID_OUTPUT");
            if(string.IsNullOrWhiteSpace(output)||!Path.IsPathRooted(output))throw new InvalidOperationException("Absolute build output directory required");
            Directory.CreateDirectory(output);
            try {
                if(!PortraitBuildPolicy.IsPortraitOnly())throw new BuildFailedException("Portrait configuration invalid");
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
                PlayerSettings.Android.useCustomKeystore=false;
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"com.local.aicompanion.cctest");
                EditorUserBuildSettings.buildAppBundle=false;
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes=new[]{CCCharacterLabSetup.ScenePath},target=BuildTarget.Android,
                    locationPathName=Path.Combine(output,"Companion-CC-Test.apk"),
                    options=BuildOptions.Development|BuildOptions.CompressWithLz4
                });
                var s=report.summary;
                File.WriteAllText(Path.Combine(output,"build-result.txt"),"Unity "+Application.unityVersion+"\nResult: "+s.result+"\nBytes: "+s.totalSize+"\nErrors: "+s.totalErrors+"\nWarnings: "+s.totalWarnings+"\nDuration: "+s.totalTime+"\nPortrait / Android ARM64 IL2CPP / Development / local debug signing\n");
                if(s.result!=BuildResult.Succeeded)throw new BuildFailedException("Android diagnostic build failed: "+s.result);
            } catch(Exception e){File.AppendAllText(Path.Combine(output,"build-result.txt"),"FAILED: "+e+"\n");throw;}
        }
    }
}
