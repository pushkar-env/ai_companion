using System;
using System.Linq;
using Companion.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Companion.Editor
{
    // Facial calibration lives in code (CharacterSpec.Face, FaceTunings) and is copied onto the
    // TalkingCompanion roster, so a re-run always restores the reviewed values.
    public static class FaceTuningSetup
    {
        public static FaceTuning For(string name)
        {
            if(name=="Alita")return FaceTunings.Alita();
            if(name==MeeraCharacterSetup.Spec.Name)return MeeraCharacterSetup.Spec.Face.Clone();
            if(name==TaraCharacterSetup.Spec.Name)return TaraCharacterSetup.Spec.Face.Clone();
            return null;
        }

        [MenuItem("Companion/Characters/Apply Face Tuning")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first");
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=TalkingCharacterSetup.ScenePath)throw new InvalidOperationException("Open TalkingCompanion");
            var app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>(UnityEngine.FindObjectsInactive.Include)??throw new InvalidOperationException("TalkingCharacter missing");
            Undo.RecordObject(app,"Apply face tuning");
            foreach(var option in app.characters){var tuning=For(option.name);if(tuning!=null)option.face=tuning;}
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            UnityEngine.Debug.Log("Face tuning applied: "+string.Join(", ",app.characters.Select(c=>c.name+" jaw "+c.face.jawDegrees+"°")));
        }
    }
}
