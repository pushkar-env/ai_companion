using System;
using UnityEngine;

namespace Companion.Presentation
{
    // Per-character wardrobe data on the model root: the body's category and fitted body (which garments it
    // may wear) and the preset outfits. A model without a profile never wears modular garments.
    [DisallowMultipleComponent]
    public sealed class CompanionWardrobeProfile : MonoBehaviour
    {
        [Serializable] public sealed class Outfit
        {
            public string id;
            public string displayName;
            public string[] garments=Array.Empty<string>();
        }
        public WardrobeCategory category;
        public string bodyFamily;
        [Tooltip("Preset looks; the first one is the signature look restored by default.")]
        public Outfit[] outfits=Array.Empty<Outfit>();
    }
}
