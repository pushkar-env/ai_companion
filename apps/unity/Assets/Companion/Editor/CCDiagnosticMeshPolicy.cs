using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Companion.Presentation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Companion.Editor
{
    // Only the development lab's build copy is reduced. Source meshes are never modified.
    public sealed class CCDiagnosticMeshPolicy : IProcessSceneWithReport
    {
        public int callbackOrder => 10;
        public static HashSet<string> RequiredNames()
        {
            var names = new HashSet<string>(CCCharacterLab.Channels, StringComparer.Ordinal);
            names.Add("V_Tongue_Raise"); // Synthetic kk cue, not in the manual dropdown.
            names.Add("C_BlinkL");
            names.Add("C_BlinkR");
            return names;
        }

        public static Mesh CreateReducedMesh(Mesh source)
        {
            var keep = RequiredNames();
            var copy = UnityEngine.Object.Instantiate(source);
            copy.name = source.name + "_CCDiagnosticSubset";
            try
            {
                copy.ClearBlendShapes();
                var vertices = new Vector3[source.vertexCount];
                var normals = new Vector3[source.vertexCount];
                var tangents = new Vector3[source.vertexCount];
                for (int shape = 0; shape < source.blendShapeCount; shape++)
                {
                    string name = source.GetBlendShapeName(shape);
                    if (!keep.Contains(name)) continue;
                    for (int frame = 0; frame < source.GetBlendShapeFrameCount(shape); frame++)
                    {
                        source.GetBlendShapeFrameVertices(shape, frame, vertices, normals, tangents);
                        copy.AddBlendShapeFrame(name, source.GetBlendShapeFrameWeight(shape, frame), vertices, normals, tangents);
                    }
                }
                return copy;
            }
            catch { UnityEngine.Object.DestroyImmediate(copy); throw; }
        }

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null || scene.path != CCCharacterLabSetup.ScenePath) return;
            if (report.summary.platform != BuildTarget.Android && report.summary.platform != BuildTarget.iOS) return;
            if ((report.summary.options & BuildOptions.Development) == 0)
                throw new BuildFailedException("CC diagnostic mesh subset is development-only.");
            var cache = new Dictionary<Mesh, Mesh>();
            var lines = new StringBuilder("renderer,source_shapes,retained_shapes,vertices\n");
            foreach (var root in scene.GetRootGameObjects())
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var source = renderer.sharedMesh;
                if (source == null || source.blendShapeCount == 0) continue;
                // Nonzero authored weights outside the subset must not silently disappear.
                var keep = RequiredNames();
                var weights = new Dictionary<string, float>();
                for (int i = 0; i < source.blendShapeCount; i++)
                {
                    string name = source.GetBlendShapeName(i);
                    float value = renderer.GetBlendShapeWeight(i);
                    if (!keep.Contains(name) && value != 0)
                        throw new BuildFailedException("Cannot discard authored facial pose: " + name);
                    weights[name] = value;
                }
                if (!cache.TryGetValue(source, out var reduced))
                    cache[source] = reduced = CreateReducedMesh(source);
                renderer.sharedMesh = reduced;
                for (int i = 0; i < reduced.blendShapeCount; i++)
                    renderer.SetBlendShapeWeight(i, weights[reduced.GetBlendShapeName(i)]);
                lines.AppendLine(renderer.name + "," + source.blendShapeCount + "," + reduced.blendShapeCount + "," + reduced.vertexCount);
            }
            string output = Environment.GetEnvironmentVariable("COMPANION_ANDROID_OUTPUT");
            if (!string.IsNullOrEmpty(output) && Path.IsPathRooted(output))
                File.WriteAllText(Path.Combine(output, "mesh-subset.txt"), lines.ToString());
            Debug.Log("CC diagnostic build mesh subset:\n" + lines);
        }
    }
}
