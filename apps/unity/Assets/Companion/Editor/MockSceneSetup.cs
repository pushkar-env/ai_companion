using System.IO;
using Companion.Presentation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Companion.Editor
{
    public static class MockSceneSetup
    {
        public const string ScenePath="Assets/Companion/Scenes/MockCompanion.unity";
        [MenuItem("Companion/Open M0 Mock Scene")]
        public static void Open()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if(File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath); else Create();
        }
        public static void Create()
        {
            if(File.Exists(ScenePath)) throw new System.InvalidOperationException("Scene already exists; open it instead.");
            Directory.CreateDirectory("Assets/Companion/Scenes"); Directory.CreateDirectory("Assets/Companion/Settings");
            var prior=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
            var camera=new GameObject("Mock Camera").AddComponent<Camera>(); camera.tag="MainCamera"; camera.transform.position=new Vector3(0,1.5f,-5); camera.transform.LookAt(new Vector3(0,1.4f,0)); camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.08f,.14f,.19f); camera.orthographic=true; camera.orthographicSize=1.8f;
            var light=new GameObject("Soft key light").AddComponent<Light>(); light.type=LightType.Directional; light.intensity=2; light.transform.rotation=Quaternion.Euler(35,-25,0);
            var avatar=new GameObject("Placeholder character - original geometric mock");
            var shader=Shader.Find("Universal Render Pipeline/Lit"); if(shader==null) throw new System.InvalidOperationException("URP Lit shader unavailable");
            var body=new Material(shader){color=new Color(.28f,.71f,.64f)}; AssetDatabase.CreateAsset(body,"Assets/Companion/Settings/PlaceholderBody.mat");
            var dark=new Material(shader){color=new Color(.04f,.10f,.15f)}; AssetDatabase.CreateAsset(dark,"Assets/Companion/Settings/PlaceholderEyes.mat");
            Part("Body",PrimitiveType.Capsule,new Vector3(0,1,0),new Vector3(.65f,.6f,.45f),body,avatar.transform);
            Part("Head",PrimitiveType.Sphere,new Vector3(0,2,0),new Vector3(.85f,.8f,.7f),body,avatar.transform);
            Part("Left eye",PrimitiveType.Sphere,new Vector3(-.17f,2.04f,-.325f),new Vector3(.085f,.12f,.05f),dark,avatar.transform);
            Part("Right eye",PrimitiveType.Sphere,new Vector3(.17f,2.04f,-.325f),new Vector3(.085f,.12f,.05f),dark,avatar.transform);
            Part("Left arm",PrimitiveType.Capsule,new Vector3(-.48f,1.15f,0),new Vector3(.2f,.4f,.2f),body,avatar.transform);
            Part("Right arm",PrimitiveType.Capsule,new Vector3(.48f,1.15f,0),new Vector3(.2f,.4f,.2f),body,avatar.transform);
            var panel=ScriptableObject.CreateInstance<PanelSettings>(); panel.scaleMode=PanelScaleMode.ScaleWithScreenSize; panel.referenceResolution=new Vector2Int(390,844); panel.match=0f;
            AssetDatabase.CreateAsset(panel,"Assets/Companion/Settings/MockPanel.asset");
            var go=new GameObject("M0 composition root - MOCK ONLY"); go.AddComponent<UIDocument>().panelSettings=panel; go.AddComponent<MockCompanionApp>().character=avatar.transform;
            EditorSceneManager.SaveScene(scene,ScenePath); AssetDatabase.SaveAssets();
            if(prior.IsValid()) SceneManager.SetActiveScene(prior);
            EditorSceneManager.CloseScene(scene,true);
            Debug.Log("M0 mock scene created; existing scene and build list preserved.");
        }
        static void Part(string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material,Transform parent)
        {
            var go=GameObject.CreatePrimitive(type); go.name=name; go.transform.SetParent(parent); go.transform.localPosition=position; go.transform.localScale=scale; go.GetComponent<Renderer>().sharedMaterial=material; Object.DestroyImmediate(go.GetComponent<Collider>());
        }
    }
    public sealed class MockReleaseGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder=>0;
        public void OnPreprocessBuild(BuildReport report)
        {
            foreach(var scene in EditorBuildSettings.scenes)
                if(scene.enabled && (scene.path==MockSceneSetup.ScenePath || scene.path==FacialLabSetup.ScenePath) && (report.summary.options & BuildOptions.Development)==0)
                    throw new BuildFailedException("M0 mock scene is development-only. Production providers and approvals are not implemented.");
        }
    }
}
