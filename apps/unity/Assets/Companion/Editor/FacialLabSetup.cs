using System;
using System.IO;
using System.Collections.Generic;
using Companion.Core;
using Companion.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Companion.Editor
{
    public static class FacialLabSetup
    {
        public const string ScenePath="Assets/Companion/Scenes/FacialDiagnostics.unity";
        [MenuItem("Companion/Open M1 Synthetic Diagnostics")]
        public static void Open()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before switching scenes.");
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            if(!File.Exists(ScenePath))Create();EditorSceneManager.OpenScene(ScenePath);
        }
        public static void Create()
        {
            if(File.Exists(ScenePath))throw new InvalidOperationException("Existing lab scene preserved; open it instead");
            string dir="Assets/Companion/Diagnostics";Directory.CreateDirectory(dir);
            var prior=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            var mesh=SyntheticRigFactory.Create();AssetDatabase.CreateAsset(mesh,dir+"/Synthetic52.asset");
            var material=new Material(Shader.Find("Universal Render Pipeline/Unlit")){color=new Color(.3f,.8f,.7f)};AssetDatabase.CreateAsset(material,dir+"/SyntheticPads.mat");
            var rig=new GameObject("Synthetic 52-channel board - NOT an avatar").AddComponent<SkinnedMeshRenderer>();rig.sharedMesh=mesh;rig.sharedMaterial=material;rig.updateWhenOffscreen=true;
            var camera=new GameObject("Diagnostic camera").AddComponent<Camera>();camera.transform.position=new Vector3(0,0,-5);camera.orthographic=true;camera.orthographicSize=1.2f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.07f,.12f,.17f);camera.gameObject.AddComponent<AudioListener>();
            var go=new GameObject("M1 synthetic lab - no network or capture");go.AddComponent<UIDocument>().panelSettings=AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Companion/Settings/MockPanel.asset");
            var source=go.AddComponent<AudioSource>();source.playOnAwake=false;var lab=go.AddComponent<FacialLab>();lab.rig=rig;lab.rigCamera=camera;
            EditorSceneManager.SaveScene(scene,ScenePath);if(prior.IsValid())SceneManager.SetActiveScene(prior);EditorSceneManager.CloseScene(scene,true);
            AssetDatabase.SaveAssetIfDirty(mesh);AssetDatabase.SaveAssetIfDirty(material);AssetDatabase.Refresh();
        }
        public static List<string> Validate(SkinnedMeshRenderer rig)
        {
            var errors=new List<string>();if(rig==null||rig.sharedMesh==null){errors.Add("Select a renderer with a mesh");return errors;}
            var mesh=rig.sharedMesh;var names=new List<string>();for(int i=0;i<mesh.blendShapeCount;i++)names.Add(mesh.GetBlendShapeName(i));errors.AddRange(FacialProfile.ValidateBindings(names));
            for(int i=0;i<mesh.blendShapeCount;i++) {
                if(!FacialProfile.ValidWeight(rig.GetBlendShapeWeight(i)/100))errors.Add("Invalid current weight: "+names[i]);
                if(mesh.GetBlendShapeFrameCount(i)==0)errors.Add("No frame: "+names[i]);
                for(int f=0;f<mesh.GetBlendShapeFrameCount(i);f++)if(!FacialProfile.ValidWeight(mesh.GetBlendShapeFrameWeight(i,f)/100))errors.Add("Invalid frame weight: "+names[i]);
            }
            return errors;
        }
        [MenuItem("Companion/Validate Selected Facial Bindings")]
        public static void ValidateSelection()
        {
            var errors=Validate(Selection.activeGameObject==null?null:Selection.activeGameObject.GetComponentInChildren<SkinnedMeshRenderer>());
            string report="Channel binding validator only; not humanoid, rights, topology or perceptual approval.\n"+(errors.Count==0?"PASS: all 52 channels present with valid frame/current weights":string.Join("\n",errors));
            string dir=Path.GetFullPath("../../docs/evidence/m1");Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,"selected-rig-validation.txt"),report);Debug.Log(report);
        }
    }
}
