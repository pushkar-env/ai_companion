using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Companion.Editor
{
    public static class CCDiagnosticMeshChecks
    {
        [MenuItem("Companion/Check CC Build Mesh Subset")]
        public static void Run()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Companion/Imported/Alita/alita.Fbx");
            if (prefab == null) throw new InvalidOperationException("CC inspection FBX missing");
            var required = CCDiagnosticMeshPolicy.RequiredNames();
            var available=new HashSet<string>(prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r=>Enumerable.Range(0,r.sharedMesh.blendShapeCount).Select(i=>r.sharedMesh.GetBlendShapeName(i))));
            required.IntersectWith(available); // Older CC correctives are not authored on Alita.
            if(!Companion.Presentation.TalkingCharacter.CanAnimate(prefab.transform))throw new InvalidOperationException("Required speech channels missing");
            var found = new HashSet<string>();
            var lines = new List<string>();
            int frames = 0, removed = 0;
            foreach (var renderer in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var source = renderer.sharedMesh;
                if (source == null || source.blendShapeCount == 0) continue;
                int originalCount = source.blendShapeCount;
                var reduced = CCDiagnosticMeshPolicy.CreateReducedMesh(source);
                try
                {
                    Equal(source.vertices, reduced.vertices, "base vertices");
                    Equal(source.normals, reduced.normals, "base normals");
                    Equal(source.tangents, reduced.tangents, "base tangents");
                    Equal(source.boneWeights, reduced.boneWeights, "bone weights");
                    Equal(source.bindposes, reduced.bindposes, "bind poses");
                    Check(source.bounds == reduced.bounds, "bounds");
                    Check(source.subMeshCount == reduced.subMeshCount, "submesh count");
                    for (int s = 0; s < source.subMeshCount; s++) Equal(source.GetIndices(s), reduced.GetIndices(s), "topology");
                    for (int uv = 0; uv < 8; uv++)
                    {
                        var a = new List<Vector4>(); var b = new List<Vector4>();
                        source.GetUVs(uv, a); reduced.GetUVs(uv, b); Equal(a.ToArray(), b.ToArray(), "UV" + uv);
                    }
                    for (int shape = 0; shape < source.blendShapeCount; shape++)
                    {
                        string name = source.GetBlendShapeName(shape);
                        int index = reduced.GetBlendShapeIndex(name);
                        if (!required.Contains(name)) { Check(index == -1, "unused shape removed"); removed++; continue; }
                        found.Add(name); Check(index >= 0, "required shape " + name);
                        Check(source.GetBlendShapeFrameCount(shape) == reduced.GetBlendShapeFrameCount(index), "frame count");
                        var av = new Vector3[source.vertexCount]; var an = new Vector3[source.vertexCount]; var at = new Vector3[source.vertexCount];
                        var bv = new Vector3[source.vertexCount]; var bn = new Vector3[source.vertexCount]; var bt = new Vector3[source.vertexCount];
                        for (int frame = 0; frame < source.GetBlendShapeFrameCount(shape); frame++)
                        {
                            Check(source.GetBlendShapeFrameWeight(shape, frame) == reduced.GetBlendShapeFrameWeight(index, frame), "frame weight");
                            source.GetBlendShapeFrameVertices(shape, frame, av, an, at);
                            reduced.GetBlendShapeFrameVertices(index, frame, bv, bn, bt);
                            Equal(av, bv, name + " position deltas"); Equal(an, bn, name + " normal deltas"); Equal(at, bt, name + " tangent deltas"); frames++;
                        }
                    }
                    Check(source.blendShapeCount == originalCount, "source unchanged");
                    lines.Add("PASS " + renderer.name + ": " + originalCount + " -> " + reduced.blendShapeCount + " shapes; base geometry/UV/skin/topology and retained frames exact");
                }
                finally { UnityEngine.Object.DestroyImmediate(reduced); }
            }
            Check(required.SetEquals(found), "all runtime and cue channels retained");
            lines.Insert(0, DateTime.UtcNow.ToString("O") + " Unity " + Application.unityVersion);
            lines.Add("PASS " + required.Count + " required names; " + frames + " retained frames compared exactly; " + removed + " unused mesh shapes omitted");
            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../docs/evidence/m1/mesh-subset"));
            Directory.CreateDirectory(dir); File.WriteAllLines(Path.Combine(dir, "checks.txt"), lines); Debug.Log(string.Join("\n", lines));
        }
        static void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException("CC subset check failed: " + name); }
        static void Equal<T>(T[] a, T[] b, string name)
        {
            Check(a.Length == b.Length, name + " length");
            for (int i = 0; i < a.Length; i++)
                if (!EqualityComparer<T>.Default.Equals(a[i], b[i])) throw new InvalidOperationException(name + " differs at " + i);
        }
    }
}
