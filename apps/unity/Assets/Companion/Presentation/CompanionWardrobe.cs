using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Companion.Presentation
{
    public sealed class CompanionWardrobe : IDisposable
    {
        [Serializable] public sealed class Look
        {
            public int outfit,top,bottom,topColor,bottomColor,hairColor,shoeColor,skinTone;
            public Look Copy()=>JsonUtility.FromJson<Look>(JsonUtility.ToJson(this));
        }
        public const string Preference="Companion.Appearance.v1";
        public static readonly string[] PaletteNames={"Original","Emerald","Rose","Midnight","Ivory"};
        public static readonly Color[] Palette={Color.white,new Color(.23f,.55f,.43f),new Color(.74f,.40f,.43f),new Color(.22f,.28f,.38f),new Color(.94f,.87f,.70f)};
        public static readonly string[] SkinToneNames={"Original","Light","Medium","Tan","Brown","Deep"};
        public static readonly Color[] SkinSwatches={new Color(.80f,.63f,.53f),new Color(.94f,.77f,.65f),new Color(.72f,.52f,.39f),new Color(.59f,.39f,.26f),new Color(.43f,.27f,.18f),new Color(.28f,.17f,.12f)};
        // Multipliers preserve the authored skin texture, normals and facial details.
        static readonly Color[] SkinTints={Color.white,new Color(1.12f,1.09f,1.04f),new Color(.92f,.81f,.71f),new Color(.79f,.65f,.53f),new Color(.62f,.46f,.35f),new Color(.44f,.30f,.23f)};
        // Tintable roles: whole renderers in the CC export; in single-mesh Blender rigs, material slots named
        // <Name>_<Role> (Top/Kurti/Dress, Bottom/Palazzo/Skirt/Pants, Hair, Shoes). Other slots keep their colour.
        enum Role {None,Top,Bottom,Hair,Shoes}
        static readonly string[] TopSlots={"_Top","_Kurti","_Dress"},BottomSlots={"_Bottom","_Palazzo","_Skirt","_Pants"};
        static bool Slot(Material material,params string[] suffixes)=>suffixes.Any(s=>material.name.EndsWith(s,StringComparison.Ordinal));
        static Role RoleOf(string renderer,Material material)
        {
            if(material==null)return Role.None;
            if(renderer=="Dress"||Slot(material,TopSlots))return Role.Top;
            if(Slot(material,BottomSlots))return Role.Bottom;
            if(renderer=="Shoulder_length_hair"||Slot(material,"_Hair"))return Role.Hair;
            if(renderer=="Canvas_shoes"||Slot(material,"_Shoes"))return Role.Shoes;
            return Role.None;
        }
        readonly List<(Material material,Color original)> skinMaterials=new List<(Material,Color)>();
        readonly Dictionary<Role,List<Material>> roles=new Dictionary<Role,List<Material>>{{Role.Top,new List<Material>()},{Role.Bottom,new List<Material>()},{Role.Hair,new List<Material>()},{Role.Shoes,new List<Material>()}};
        readonly Transform model;
        readonly string preference;
        readonly List<GameObject> created=new List<GameObject>();
        readonly List<Material> ownedMaterials=new List<Material>();
        readonly Dictionary<Renderer,Material[]> originals=new Dictionary<Renderer,Material[]>();
        readonly Renderer dress;
        readonly SkinnedMeshRenderer tee,shorts,shell,skirt;
        readonly bool originalDress;
        public Look Current {get;private set;}=new Look();
        public bool HasSeparates=>tee!=null&&shorts!=null&&shell!=null&&skirt!=null;
        public bool HasTop=>roles[Role.Top].Count>0||HasSeparates;
        public bool HasBottom=>roles[Role.Bottom].Count>0||HasSeparates;
        public bool HasHair=>roles[Role.Hair].Count>0;
        public bool HasShoes=>roles[Role.Shoes].Count>0;
        public bool HasSkin=>skinMaterials.Count>0;
        public CompanionWardrobe(Transform model,bool loadSaved=true,string preference=Preference)
        {
            this.model=model;this.preference=string.IsNullOrEmpty(preference)?Preference:preference;
            foreach(var r in model.GetComponentsInChildren<SkinnedMeshRenderer>())if(r.name=="Dress")dress=r;
            originalDress=dress!=null&&dress.enabled;
            foreach(var r in model.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                var materials=r.sharedMaterials;bool changed=false;
                for(int i=0;i<materials.Length;i++) {
                    bool skin=IsSkinMaterial(materials[i]);var role=skin?Role.None:RoleOf(r.name,materials[i]);
                    if(!skin&&role==Role.None)continue;
                    if(!changed)originals[r]=r.sharedMaterials;
                    materials[i]=Clone(materials[i]);changed=true;
                    if(skin)skinMaterials.Add((materials[i],materials[i].GetColor("_BaseColor")));else roles[role].Add(materials[i]);
                }
                if(changed)r.sharedMaterials=materials;
            }
            // Mix-and-match garments are fitted to the CC body that wears the signature dress.
            if(dress!=null){tee=Create("Crop_T_shirts");shorts=Create("Denim_shorts");shell=Create("Sleeveless_shell");skirt=Create("Midi_skirt");}
            if(loadSaved)try{Current=JsonUtility.FromJson<Look>(PlayerPrefs.GetString(this.preference,"{}"))??new Look();}catch(ArgumentException){Current=new Look();}
            Apply(Current);
        }
        Material Clone(Material source){var m=new Material(source);ownedMaterials.Add(m);return m;}
        SkinnedMeshRenderer Create(string id)
        {
            var mesh=Resources.Load<Mesh>("Wardrobe/"+id);var material=Resources.Load<Material>("Wardrobe/"+id);var names=Resources.Load<TextAsset>("Wardrobe/"+id+".bones");
            if(mesh==null||material==null||names==null)return null;
            var map=model.GetComponentsInChildren<Transform>().GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
            var boneNames=names.text.Split('\n');map[boneNames[0].Trim()]=model;if(boneNames.Any(n=>!map.ContainsKey(n.Trim())))return null;
            var go=new GameObject(id+" wardrobe");go.transform.SetParent(model,false);created.Add(go);
            var r=go.AddComponent<SkinnedMeshRenderer>();r.sharedMesh=mesh;r.bones=boneNames.Select(n=>map[n.Trim()]).ToArray();r.rootBone=map["CC_Base_Hip"];r.sharedMaterial=Clone(material);r.localBounds=new Bounds(Vector3.up*.9f,new Vector3(2,2,1));r.updateWhenOffscreen=true;return r;
        }
        public void Apply(Look value)
        {
            Current=value.Copy();Current.outfit=HasSeparates?Mathf.Clamp(Current.outfit,0,1):0;
            Current.skinTone=Current.skinTone>=0&&Current.skinTone<SkinTints.Length?Current.skinTone:0;
            foreach(var skin in skinMaterials) {var tint=skin.original*SkinTints[Current.skinTone];tint.a=skin.original.a;skin.material.SetColor("_BaseColor",tint);}
            Current.topColor=Mathf.Clamp(Current.topColor,0,4);Current.bottomColor=Mathf.Clamp(Current.bottomColor,0,4);Current.hairColor=Mathf.Clamp(Current.hairColor,0,4);Current.shoeColor=Mathf.Clamp(Current.shoeColor,0,4);
            if(dress!=null)dress.enabled=Current.outfit==0;
            Current.top=Mathf.Clamp(Current.top,0,1);Current.bottom=Mathf.Clamp(Current.bottom,0,1);
            if(tee!=null)tee.enabled=Current.outfit==1&&Current.top==0;if(shell!=null)shell.enabled=Current.outfit==1&&Current.top==1;
            if(shorts!=null)shorts.enabled=Current.outfit==1&&Current.bottom==0;if(skirt!=null)skirt.enabled=Current.outfit==1&&Current.bottom==1;
            Tint(shell,Current.topColor);Tint(skirt,Current.bottomColor);Tint(tee,Current.topColor);Tint(shorts,Current.bottomColor);
            Tint(roles[Role.Top],Current.topColor);Tint(roles[Role.Bottom],Current.bottomColor);Tint(roles[Role.Hair],Current.hairColor);Tint(roles[Role.Shoes],Current.shoeColor);
        }
        static void Tint(Renderer r,int color){if(r==null)return;Tint(r.sharedMaterials,color);}
        static void Tint(IEnumerable<Material> materials,int color){foreach(var m in materials)if(m!=null&&m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",Palette[color]);}
        public static bool IsSkinMaterial(Material material)=>material!=null&&material.HasProperty("_BaseColor")&&
            (material.name=="Std_Skin_Head"||material.name=="Std_Skin_Body"||material.name=="Std_Skin_Arm"||material.name=="Std_Skin_Leg"||Slot(material,"_Skin"));
        public void Save(){PlayerPrefs.SetString(preference,JsonUtility.ToJson(Current));PlayerPrefs.Save();}
        public void Dispose()
        {
            if(dress!=null)dress.enabled=originalDress;foreach(var pair in originals)if(pair.Key!=null)pair.Key.sharedMaterials=pair.Value;
            foreach(var go in created)Release(go);foreach(var m in ownedMaterials)Release(m);
        }
        static void Release(UnityEngine.Object o){if(Application.isPlaying)UnityEngine.Object.Destroy(o);else UnityEngine.Object.DestroyImmediate(o);}
    }
}
