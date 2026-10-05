using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Companion.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Companion.Editor
{
    // Measures a disposable imported copy. Never modifies the open scene or source FBX.
    public static class CCDeformationAudit
    {
        const string ModelPath = "Assets/Companion/Imported/Alita/alita.Fbx";
        const float Epsilon = 0.000001f;

        [MenuItem("Companion/Audit CC Mesh Deformation")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before auditing the imported rig.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (prefab == null) throw new InvalidOperationException("CC inspection model missing.");
            var scene = EditorSceneManager.NewPreviewScene();
            var baked = new Mesh();
            try
            {
                var character = UnityEngine.Object.Instantiate(prefab);
                SceneManager.MoveGameObjectToScene(character, scene);
                foreach (var animator in character.GetComponentsInChildren<Animator>()) animator.enabled = false;
                var meshes = character.GetComponentsInChildren<SkinnedMeshRenderer>();
                var baseline = new Vector3[meshes.Length][];
                for (int r = 0; r < meshes.Length; r++)
                {
                    SetAllZero(meshes[r]);
                    baseline[r] = BakeWorld(meshes[r], baked);
                }

                var names = new List<string>(CCCharacterLab.Channels);
                // The tone's kk cue uses this native control in addition to the manual list.
                if (!names.Contains("V_Tongue_Raise")) names.Add("V_Tongue_Raise");
                var csv = new StringBuilder("channel,weight,bound_meshes,moved_meshes,moved_vertices,max_displacement_m,reset_max_m\n");
                var meshCsv = new StringBuilder("channel,renderer,vertices,moved_vertices,max_displacement_m\n");
                var notes = new StringBuilder();
                int probes = 0, noMotion = 0;
                foreach (string requested in names)
                {
                    string name=requested;
                    bool native=false;foreach(var renderer in meshes)native|=renderer.sharedMesh.GetBlendShapeIndex(name)>=0;
                    if(!native)foreach(var alias in TalkingCharacter.ExpressionAliases)if(alias.Value==requested){name=alias.Key;break;}
                    int bound = 0;
                    foreach (var mesh in meshes) if (mesh.sharedMesh.GetBlendShapeIndex(name) >= 0) bound++;
                    if (bound == 0) throw new InvalidOperationException("Missing native channel: " + name);
                    foreach (float weight in new[] { .25f, .5f, 1f })
                    {
                        int movedMeshes = 0, movedVertices = 0;
                        float maximum = 0, resetMaximum = 0;
                        for (int r = 0; r < meshes.Length; r++)
                        {
                            int index = meshes[r].sharedMesh.GetBlendShapeIndex(name);
                            if (index < 0) continue;
                            meshes[r].SetBlendShapeWeight(index, weight * 100);
                            var vertices = BakeWorld(meshes[r], baked);
                            int moved = 0;
                            float meshMaximum = 0;
                            for (int v = 0; v < vertices.Length; v++)
                            {
                                float delta = Vector3.Distance(vertices[v], baseline[r][v]);
                                RequireFinite(delta);
                                maximum = Mathf.Max(maximum, delta);
                                meshMaximum = Mathf.Max(meshMaximum, delta);
                                if (delta > Epsilon) moved++;
                            }
                            if (moved > 0) movedMeshes++;
                            movedVertices += moved;
                            if (weight == 1) meshCsv.AppendLine(string.Join(",", name, Csv(meshes[r].name), vertices.Length, moved, Number(meshMaximum)));
                            meshes[r].SetBlendShapeWeight(index, 0);
                            vertices = BakeWorld(meshes[r], baked);
                            for (int v = 0; v < vertices.Length; v++)
                            {
                                float delta = Vector3.Distance(vertices[v], baseline[r][v]);
                                RequireFinite(delta);
                                resetMaximum = Mathf.Max(resetMaximum, delta);
                            }
                        }
                        if (resetMaximum > Epsilon) throw new InvalidOperationException("Reset drift: " + name);
                        if (movedVertices == 0) { noMotion++; notes.AppendLine("NO MEASURABLE MORPH MOTION: " + name + " at " + Number(weight)); }
                        csv.AppendLine(string.Join(",", name, Number(weight), bound, movedMeshes, movedVertices, Number(maximum), Number(resetMaximum)));
                        probes++;
                    }
                }
                string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../docs/evidence/m1/cc-calibration"));
                Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output, "deformation.csv"), csv.ToString());
                File.WriteAllText(Path.Combine(output, "mesh-coverage.csv"), meshCsv.ToString());
                File.WriteAllText(Path.Combine(output, "audit.txt"), DateTime.UtcNow.ToString("O") + "\nUnity " + Application.unityVersion
                    + "\nModel: " + ModelPath + "\n" + names.Count + " native controls; " + probes + " probes; " + noMotion + " without measurable morph motion.\n"
                    + "PASS all bindings present, finite geometry and neutral reset within 1 micrometre.\n"
                    + "World-space displacement in metres, >1 micrometre movement threshold. Morph-only; no jaw-bone assist.\n"
                    + "A moving mesh does not prove perceptual quality, anatomical side, speech calibration or mobile performance.\n" + notes);
                Debug.Log("CC deformation audit: " + probes + " probes, " + noMotion + " without measurable morph motion. Report: " + output);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                UnityEngine.Object.DestroyImmediate(baked);
            }
        }

        static void SetAllZero(SkinnedMeshRenderer mesh)
        {
            for (int i = 0; i < mesh.sharedMesh.blendShapeCount; i++) mesh.SetBlendShapeWeight(i, 0);
        }
        static Vector3[] BakeWorld(SkinnedMeshRenderer renderer, Mesh baked)
        {
            renderer.BakeMesh(baked);
            var vertices = baked.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = renderer.transform.TransformPoint(vertices[i]);
            return vertices;
        }
        static string Number(float value) => value.ToString("G9", CultureInfo.InvariantCulture);
        static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
        static void RequireFinite(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidOperationException("Non-finite baked vertex displacement.");
        }
    }
}
