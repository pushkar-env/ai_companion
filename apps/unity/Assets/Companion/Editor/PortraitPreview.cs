using System;
using System.Reflection;
using UnityEditor;

namespace Companion.Editor
{
    // Game view sizing is Editor-only; no reflection or internal API enters player builds.
    public static class PortraitPreview
    {
        [MenuItem("Companion/Portrait Preview (390 x 844)")]
        public static void Phone() { SetSize(390,844); }
        public static void SetSize(int width,int height)
        {
            var asm=typeof(UnityEditor.Editor).Assembly;
            var sizes=asm.GetType("UnityEditor.GameViewSizes");
            var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizes);
            var instance=singleton.GetProperty("instance").GetValue(null);
            var groupType=asm.GetType("UnityEditor.GameViewSizeGroupType");
            // Current target group ensures this also works after selecting a mobile target.
            var current=sizes.GetProperty("currentGroupType",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(instance);
            var group=sizes.GetMethod("GetGroup").Invoke(instance,new[]{current});
            var type=group.GetType();
            var texts=(string[])type.GetMethod("GetDisplayTexts").Invoke(group,null);
            string label="Companion Portrait "+width+"x"+height;
            int index=Array.FindIndex(texts,s=>s.Contains(label));
            if(index<0)
            {
                var sizeType=asm.GetType("UnityEditor.GameViewSize");
                var kind=asm.GetType("UnityEditor.GameViewSizeType");
                var size=Activator.CreateInstance(sizeType,new[]{Enum.Parse(kind,"FixedResolution"),(object)width,height,label});
                type.GetMethod("AddCustomSize").Invoke(group,new[]{size});
                index=((string[])type.GetMethod("GetDisplayTexts").Invoke(group,null)).Length-1;
            }
            var viewType=asm.GetType("UnityEditor.GameView");
            var view=EditorWindow.GetWindow(viewType);
            viewType.GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(view,index);
            view.Focus();view.Repaint();
        }
    }
}
