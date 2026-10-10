using System;
using UnityEngine;

namespace Companion.Presentation
{
    // Wardrobe category of a body and its garments. A garment is only ever offered to, or worn by, a
    // character whose profile has the same category and fitted body (WARD-01 body compatibility).
    public enum WardrobeCategory { Female, Male }
    public enum GarmentSlot { Top, Bottom, Shoes, Accessory }

    // One fitted, swappable garment under a character root: a skinned renderer bound to the character's
    // skeleton, plus any garment-only spring bones grafted under it at import. CompanionWardrobe turns the
    // object on and off; inactive garments keep their bones but their chains are paused.
    [DisallowMultipleComponent]
    public sealed class CompanionGarment : MonoBehaviour
    {
        public string id;
        public string displayName;
        public WardrobeCategory category;
        [Tooltip("Body the mesh is fitted to; garments are fitted per body today.")] public string bodyFamily;
        public GarmentSlot slot;
        [Tooltip("Spring chains (CompanionSecondaryMotion names) that only move while this garment is worn.")]
        public string[] chains=Array.Empty<string>();
        [Tooltip("Body renderers hidden while this garment is worn (occlusion masks).")]
        public string[] hides=Array.Empty<string>();
    }
}
