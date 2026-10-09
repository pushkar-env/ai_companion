using UnityEditor;

namespace Companion.Editor
{
    // Meera's rig (stopped Editor) and in-app (Play) checks; shared implementation in CharacterRigChecks.
    public static class MeeraRigChecks
    {
        [MenuItem("Companion/Characters/Check Meera Rig")]
        public static void Rig()=>CharacterRigChecks.Rig(MeeraCharacterSetup.Spec);
        [MenuItem("Companion/Characters/Check Meera In App")]
        public static void InApp()=>CharacterRigChecks.InApp(MeeraCharacterSetup.Spec);
    }
}
