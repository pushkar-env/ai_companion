using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Companion.Presentation;

namespace Companion.Editor
{
    public static class WardrobeImport
    {
        [Serializable] public class Weight {public string[] names;public float[] values;}
        [Serializable] public class Source {public Vector3[] vertices,normals;public Vector2[] uv;public Weight[] weights;public int[] triangles;}
        public static void Run()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Import garments in stopped Editor only");
            var app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>();
            var bones=app.character.GetComponentsInChildren<Transform>();
            string folder="Assets/Companion/Resources/Wardrobe/";
            foreach(string name in new[]{"Crop_T_shirts","Denim_shorts","Sleeveless_shell","Midi_skirt"}) {
                var data=JsonUtility.FromJson<Source>(File.ReadAllText(Path.GetFullPath(Application.dataPath+"/../../../models/wardrobe/"+name+".json")));
                var mesh=new Mesh {name=name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
                mesh.vertices=data.vertices.Select(v=>new Vector3(-v.x,v.y,v.z)).ToArray();mesh.uv=data.uv;mesh.triangles=data.triangles;
                var reference=app.character.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name=="Dress");
                mesh.bindposes=bones.Select(b=>{int index=Array.FindIndex(reference.bones,t=>t.name==b.name);return index>=0?reference.sharedMesh.bindposes[index]*reference.transform.worldToLocalMatrix*app.character.localToWorldMatrix:b.worldToLocalMatrix*app.character.localToWorldMatrix;}).ToArray();
                var weights=new BoneWeight[data.vertices.Length];
                for(int i=0;i<weights.Length;i++) {
                    int[] ids=new int[4];float[] ws=new float[4];float total=0;
                    for(int j=0;j<data.weights[i].names.Length;j++) {ids[j]=Array.FindIndex(bones,b=>b.name==data.weights[i].names[j]);if(ids[j]<0)throw new Exception("Missing bone "+data.weights[i].names[j]);ws[j]=data.weights[i].values[j];total+=ws[j];}
                    if(total<=0)throw new Exception("Unweighted garment vertex");
                    weights[i]=new BoneWeight {boneIndex0=ids[0],boneIndex1=ids[1],boneIndex2=ids[2],boneIndex3=ids[3],weight0=ws[0]/total,weight1=ws[1]/total,weight2=ws[2]/total,weight3=ws[3]/total};
                }
                mesh.boneWeights=weights;mesh.normals=data.normals.Select(v=>new Vector3(-v.x,v.y,v.z)).ToArray();mesh.RecalculateTangents();mesh.RecalculateBounds();
                var existingMesh=AssetDatabase.LoadAssetAtPath<Mesh>(folder+name+".asset");if(existingMesh==null)AssetDatabase.CreateAsset(mesh,folder+name+".asset");else {EditorUtility.CopySerialized(mesh,existingMesh);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existingMesh);}
                File.WriteAllText(folder+name+".bones.txt",string.Join("\n",bones.Select(b=>b.name)));
                var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+name+"_Diffuse.jpg"));if(name=="Sleeveless_shell"||name=="Midi_skirt")mat.CopyPropertiesFromMaterial(app.character.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name=="Dress").sharedMaterial);mat.SetFloat("_Smoothness",.23f);mat.SetFloat("_Cull",0);
                var existingMat=AssetDatabase.LoadAssetAtPath<Material>(folder+name+".mat");if(existingMat==null)AssetDatabase.CreateAsset(mat,folder+name+".mat");else {EditorUtility.CopySerialized(mat,existingMat);UnityEngine.Object.DestroyImmediate(mat);EditorUtility.SetDirty(existingMat);}
            }
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        }
    }
}
