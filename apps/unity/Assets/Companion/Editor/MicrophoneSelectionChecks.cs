using System;
using System.IO;
using System.Collections.Generic;
using Companion.Core;
using Companion.Presentation;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace Companion.Editor
{
    public static class MicrophoneSelectionChecks
    {
        [MenuItem("Companion/Run Microphone Selection Checks")]
        public static void Run()
        {
            var lines=new List<string>();
            Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);lines.Add("PASS "+label);};
            var selection=new MicrophoneSelection("USB");selection.Refresh(new[]{"Virtual","USB"});
            check(selection.Selected=="USB"&&selection.IsAvailable,"remembered input wins over device enumeration order");
            selection.Refresh(new[]{"USB","Virtual"});check(selection.Selected=="USB","refresh/reorder preserves input");
            selection.Refresh(new[]{"Virtual"});check(!selection.IsAvailable&&selection.Selected=="USB"&&selection.Display==MicrophoneSelection.Missing,"disconnected selected input never silently switches");
            check(!selection.Select(MicrophoneSelection.Missing),"placeholder cannot become a recording device");
            selection.Refresh(new[]{"USB","Virtual"});check(selection.IsAvailable&&selection.Selected=="USB","reconnected selected input is restored");
            check(selection.Select("Virtual")&&selection.Selected=="Virtual","explicit replacement selection works");
            selection.Refresh(Array.Empty<string>());check(selection.Display==MicrophoneSelection.None&&!selection.IsAvailable,"empty input list remains explicit");
            var fresh=new MicrophoneSelection("");fresh.Refresh(Array.Empty<string>());fresh.Refresh(new[]{"USB"});check(!fresh.IsAvailable,"new input after empty startup requires explicit selection");
            fresh.Select("USB");var recreated=new MicrophoneSelection(fresh.Selected);recreated.Refresh(new[]{"Virtual","USB"});check(recreated.Selected=="USB","selection restores in a fresh selection model");
            if(!EditorApplication.isPlaying)throw new Exception("Enter Play for UI checks");
            var app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>();var root=app.GetComponent<UIDocument>().rootVisualElement;
            string selected=app.SelectedMicrophone,draft=app.Draft;app.RefreshMicrophones();
            check(app.SelectedMicrophone==selected&&app.Draft==draft&&!app.IsRecording,"actual refresh preserves draft/selection and never opens microphone");
            check(root.Q("refresh-microphones").worldBound.height>=40&&root.Q("chat-actions").worldBound.yMax<=root.worldBound.yMax,"refresh is touch-sized and controls fit portrait");
            check(root.Q("microphone-row").worldBound.xMax<=root.worldBound.xMax,"device row stays inside portrait width");
            var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/evidence/m1/microphone-selection"));Directory.CreateDirectory(folder);File.WriteAllLines(Path.Combine(folder,"editor-checks.txt"),lines);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,"picker.png"));Debug.Log("PASS "+lines.Count+" microphone selection checks");
        }
    }
}
