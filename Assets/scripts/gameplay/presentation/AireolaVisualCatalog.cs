using System;
using UnityEngine;

namespace MonsterPouch.Gameplay.Presentation
{
    [Serializable]
    public sealed class AireolaVisualClip
    {
        public string Id;
        public Sprite[] Frames = Array.Empty<Sprite>();
        public float VisibleEnvelopePixels;
        public float WidthCells = 1;
        public float FramesPerSecond = 12;
        public float DisplaySize;
        public bool OrientToTarget;
        public ProjectileArt FlightArt() => new ProjectileArt {
            UnitId = Id, Flight = Frames, ReferencePixels = 256,
            DisplaySize = DisplaySize, OrientToTarget = OrientToTarget
        };
    }

    // Explicit references keep all 48 sprites available in player builds, including art outside Resources.
    public sealed class AireolaVisualCatalog : ScriptableObject
    {
        public AireolaVisualClip[] Clips = Array.Empty<AireolaVisualClip>();
        public AireolaVisualClip Get(string id)
        {
            foreach (var clip in Clips) if (clip != null && clip.Id == id) return clip;
            return null;
        }
    }
}
