using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Companion.Presentation
{
    public sealed partial class TalkingCharacter
    {
        VisualElement wardrobePanel;
        CompanionWardrobe.Look savedLook;
        float wardrobeYaw;
        public CompanionWardrobe.Look CurrentLook=>wardrobe?.Current.Copy();
        public bool WardrobeOpen=>wardrobePanel!=null&&wardrobePanel.style.display==DisplayStyle.Flex;
        public void OpenWardrobe()
        {
            if(wardrobe==null||WardrobeOpen)return;
            savedLook=wardrobe.Current.Copy();wardrobePanel.Clear();wardrobePanel.style.display=DisplayStyle.Flex;
            drawer.style.display=DisplayStyle.None;
            var heading=new VisualElement();heading.AddToClassList("row");wardrobePanel.Add(heading);
            var title=Text(SelectedCharacterName+"’s wardrobe",20);title.name="wardrobe-title";title.style.flexGrow=1;heading.Add(title);
            heading.Add(MakeButton("Cancel",()=>CloseWardrobe(false)));
            var save=MakeButton("Save look",()=>CloseWardrobe(true));save.AddToClassList("primary");save.name="save-look";heading.Add(save);
            var scroll=new ScrollView {name="wardrobe-options"};scroll.style.flexGrow=1;wardrobePanel.Add(scroll);
            var skinHeading=Text("Skin tone · "+CompanionWardrobe.SkinToneNames[wardrobe.Current.skinTone],13);skinHeading.name="skin-tone-label";scroll.Add(skinHeading);
            var swatches=new List<Button>();
            void RefreshSkinSelection(){skinHeading.text="Skin tone · "+CompanionWardrobe.SkinToneNames[wardrobe.Current.skinTone];for(int n=0;n<swatches.Count;n++){bool selected=n==wardrobe.Current.skinTone;swatches[n].EnableInClassList("selected",selected);swatches[n].tooltip=CompanionWardrobe.SkinToneNames[n]+(selected?" — selected":" skin tone");}}
            for(int rowIndex=0;rowIndex<2;rowIndex++) {
                var row=new VisualElement();row.AddToClassList("row");scroll.Add(row);
                for(int column=0;column<3;column++) {
                    int tone=rowIndex*3+column;
                    var button=new Button(()=>{wardrobe.Current.skinTone=tone;wardrobe.Apply(wardrobe.Current);RefreshSkinSelection();}){name="skin-tone-"+tone};button.AddToClassList("skin-swatch");
                    var chip=new VisualElement {pickingMode=PickingMode.Ignore};chip.AddToClassList("skin-chip");chip.style.backgroundColor=CompanionWardrobe.SkinSwatches[tone];button.Add(chip);
                    button.Add(new Label(CompanionWardrobe.SkinToneNames[tone]){pickingMode=PickingMode.Ignore});row.Add(button);swatches.Add(button);
                }
            }
            RefreshSkinSelection();
            void Pick(string label,string id,string[] values,int selected,System.Action<int> set)
            {
                var field=new DropdownField(label,new List<string>(values),selected){name=id};StyleField(field);field.AddToClassList("wardrobe-choice");
                field.RegisterValueChangedCallback(e=>{set(field.index);wardrobe.Apply(wardrobe.Current);});scroll.Add(field);
            }
            // Only offer choices the selected rig can wear; single-outfit rigs keep colour and skin options.
            if(wardrobe.HasSeparates) {
                Pick("Outfit","outfit",new[]{"Signature dress","Mix & match"},wardrobe.Current.outfit,v=>wardrobe.Current.outfit=v);
                Pick("Top","top",new[]{"Relaxed tee","Sleeveless shell"},wardrobe.Current.top,v=>{wardrobe.Current.top=v;wardrobe.Current.outfit=1;scroll.Q<DropdownField>("outfit").SetValueWithoutNotify("Mix & match");});
                Pick("Bottom","bottom",new[]{"Denim shorts","Midi skirt"},wardrobe.Current.bottom,v=>{wardrobe.Current.bottom=v;wardrobe.Current.outfit=1;scroll.Q<DropdownField>("outfit").SetValueWithoutNotify("Mix & match");});
            }
            // Modular wardrobes list only garments fitted to this body and category (never the other category's).
            if(wardrobe.HasModularWardrobe)AddGarmentPickers(scroll);
            if(wardrobe.HasTop)Pick(wardrobe.HasSeparates?"Top / dress color":"Top color","top-color",CompanionWardrobe.PaletteNames,wardrobe.Current.topColor,v=>wardrobe.Current.topColor=v);
            if(wardrobe.HasBottom)Pick("Bottom color","bottom-color",CompanionWardrobe.PaletteNames,wardrobe.Current.bottomColor,v=>wardrobe.Current.bottomColor=v);
            if(wardrobe.HasHair)Pick("Hair tint","hair-color",CompanionWardrobe.PaletteNames,wardrobe.Current.hairColor,v=>wardrobe.Current.hairColor=v);
            if(wardrobe.HasShoes)Pick("Sneaker color","shoe-color",CompanionWardrobe.PaletteNames,wardrobe.Current.shoeColor,v=>wardrobe.Current.shoeColor=v);
            var reset=MakeButton("Restore signature look",()=>{wardrobe.Apply(new CompanionWardrobe.Look());var original=savedLook;wardrobePanel.style.display=DisplayStyle.None;OpenWardrobe();savedLook=original;});reset.name="reset-look";scroll.Add(reset);
            var turn=new Slider("Turn preview",-180,180){name="wardrobe-turn"};turn.AddToClassList("wardrobe-choice");turn.RegisterValueChangedCallback(e=>wardrobeYaw=e.newValue);scroll.Add(turn);
            scroll.Add(Text("Preview freely. Save keeps this look on this device.",11));
        }
        void AddGarmentPickers(VisualElement scroll)
        {
            var outfits=new List<CompanionWardrobeProfile.Outfit>(wardrobe.Outfits);
            var outfitNames=new List<string>();foreach(var o in outfits)outfitNames.Add(o.displayName);outfitNames.Add("Mix & match");
            int OutfitIndex(){int i=outfits.FindIndex(o=>o.id==wardrobe.Current.outfitId);return i>=0?i:outfits.Count;}
            var outfitField=new DropdownField("Outfit",outfitNames,OutfitIndex()){name="outfit"};StyleField(outfitField);outfitField.AddToClassList("wardrobe-choice");scroll.Add(outfitField);
            var slotFields=new List<(GarmentSlot slot,DropdownField field,List<CompanionGarment> options)>();
            void Refresh()
            {
                outfitField.SetValueWithoutNotify(outfitNames[OutfitIndex()]);
                foreach(var (slot,field,options) in slotFields) {
                    int i=options.IndexOf(wardrobe.Worn(slot));
                    field.SetValueWithoutNotify(field.choices[slot==GarmentSlot.Accessory?i+1:Mathf.Max(i,0)]);
                }
            }
            outfitField.RegisterValueChangedCallback(e=>{
                int i=outfitField.index;
                if(i>=0&&i<outfits.Count)wardrobe.WearOutfit(outfits[i].id);else wardrobe.Current.outfitId=CompanionWardrobe.CustomOutfit;
                Refresh();
            });
            foreach(var slot in new[]{GarmentSlot.Top,GarmentSlot.Bottom,GarmentSlot.Shoes,GarmentSlot.Accessory}) {
                var options=new List<CompanionGarment>(wardrobe.Options(slot));
                bool optional=slot==GarmentSlot.Accessory;
                if(options.Count==0||(!optional&&options.Count<2))continue;
                var choices=new List<string>();if(optional)choices.Add("None");foreach(var g in options)choices.Add(g.displayName);
                var field=new DropdownField(slot==GarmentSlot.Accessory?"Accessory":slot.ToString(),choices,0){name=slot.ToString().ToLowerInvariant()};
                StyleField(field);field.AddToClassList("wardrobe-choice");scroll.Add(field);
                field.RegisterValueChangedCallback(e=>{
                    int i=field.index-(optional?1:0);
                    if(i<0)wardrobe.ClearAccessory();else wardrobe.Equip(options[i].id);
                    Refresh();
                });
                slotFields.Add((slot,field,options));
            }
            Refresh();
        }
        public void CloseWardrobe(bool save)
        {
            if(save)wardrobe.Save();else if(savedLook!=null)wardrobe.Apply(savedLook);
            wardrobeYaw=0;wardrobePanel.style.display=DisplayStyle.None;drawer.style.display=DisplayStyle.Flex;root.Q<Button>("open-wardrobe")?.Focus();
        }
        void BuildWardrobe(VisualElement header)
        {
            var button=MakeButton("Style",OpenWardrobe);button.name="open-wardrobe";button.style.flexGrow=0;button.style.minWidth=52;header.Add(button);
            wardrobePanel=new VisualElement {name="wardrobe-panel"};wardrobePanel.AddToClassList("wardrobe-panel");wardrobePanel.AddToClassList("glass");wardrobePanel.style.display=DisplayStyle.None;shell.Add(wardrobePanel);
        }
    }
}
