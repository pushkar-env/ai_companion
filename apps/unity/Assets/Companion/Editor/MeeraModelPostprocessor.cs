using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Companion.Editor
{
    // Import-time blendshape preparation for Blender-rigged characters (Meera, Tara, ...): any
    // Imported/<Name>/<Name>.fbx that has a <Name>.rig.json beside it. Results live in the Library.
    // Blender in-between shapes named "<shape>__fNN" become frames of <shape>, and normal/tangent
    // deltas are kept only where the shape actually moves vertices, so frames stay sparse.
    // The class name and version are kept so existing model imports are not invalidated.
    public sealed class MeeraModelPostprocessor : AssetPostprocessor
    {
        public override uint GetVersion()=>1;
        void OnPostprocessModel(GameObject root)
        {
            if(!IsRiggedCharacter(assetPath))return;
            foreach(var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if(renderer.sharedMesh!=null&&renderer.sharedMesh.blendShapeCount>0)FoldFrames(renderer.sharedMesh);
        }
        public static bool IsRiggedCharacter(string path)
        {
            path=path.Replace('\\','/');
            if(!path.Contains("/Imported/")||!path.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase))return false;
            string name=Path.GetFileNameWithoutExtension(path),folder=Path.GetDirectoryName(path)?.Replace('\\','/');
            return folder!=null&&Path.GetFileName(folder)==name&&File.Exists(folder+"/"+name+".rig.json");
        }
        public static void FoldFrames(Mesh mesh)
        {
            int n=mesh.vertexCount;
            var shapes=new List<(string name,List<(float weight,Vector3[] v,Vector3[] nn,Vector3[] t)> frames)>();
            var helpers=new Dictionary<string,List<(float,Vector3[],Vector3[],Vector3[])>>();
            for(int i=0;i<mesh.blendShapeCount;i++) {
                string name=mesh.GetBlendShapeName(i);var frames=new List<(float,Vector3[],Vector3[],Vector3[])>();
                for(int f=0;f<mesh.GetBlendShapeFrameCount(i);f++) {
                    var v=new Vector3[n];var nn=new Vector3[n];var t=new Vector3[n];mesh.GetBlendShapeFrameVertices(i,f,v,nn,t);
                    for(int k=0;k<n;k++)if(v[k].sqrMagnitude<1e-14f){v[k]=Vector3.zero;nn[k]=Vector3.zero;t[k]=Vector3.zero;}
                    frames.Add((mesh.GetBlendShapeFrameWeight(i,f),v,nn,t));
                }
                int marker=name.IndexOf("__f",StringComparison.Ordinal);
                if(marker>0&&float.TryParse(name.Substring(marker+3),out float weight)) {
                    string target=name.Substring(0,marker);
                    if(!helpers.TryGetValue(target,out var list))helpers[target]=list=new List<(float,Vector3[],Vector3[],Vector3[])>();
                    list.Add((weight,frames[0].Item2,frames[0].Item3,frames[0].Item4));
                }
                else shapes.Add((name,frames));
            }
            mesh.ClearBlendShapes();
            foreach(var shape in shapes) {
                var frames=shape.frames.ToList();
                if(helpers.TryGetValue(shape.name,out var extra))frames.AddRange(extra);
                foreach(var frame in frames.OrderBy(f=>f.weight))mesh.AddBlendShapeFrame(shape.name,frame.weight,frame.v,frame.nn,frame.t);
            }
        }
    }
}
