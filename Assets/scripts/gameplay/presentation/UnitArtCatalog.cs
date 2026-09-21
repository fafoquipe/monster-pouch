using System;
using UnityEngine;

namespace MonsterPouch.Gameplay.Presentation
{
    // Clockwise in screen/world space. Board Y increases toward the bottom.
    public enum UnitFacing { North, NorthEast, East, SouthEast, South, SouthWest, West, NorthWest }

    [Serializable]
    public sealed class UnitSpriteRig
    {
        public Sprite Body;
        public Sprite LeftFoot;
        public Sprite RightFoot;
        public Vector2 BodyOffset;
        public Vector2 LeftFootOffset;
        public Vector2 RightFootOffset;
    }

    [Serializable]
    public sealed class UnitDirectionalAnimation
    {
        public Sprite[] Idle = Array.Empty<Sprite>();
        public Sprite[] Move = Array.Empty<Sprite>();
        public Sprite[] Attack = Array.Empty<Sprite>();
        public Sprite[] Death = Array.Empty<Sprite>();
        public Sprite[] Revive = Array.Empty<Sprite>();
        public Sprite[] Special = Array.Empty<Sprite>();
        public bool SpecialFlipX;
        [Min(.1f)] public float SpecialDuration = .65f;
        [Min(.1f)] public float IdleFramesPerSecond = 3f;
        [Min(.1f)] public float MoveFramesPerSecond = 10f;
        [Tooltip("First attack frame shown at the simulation contact/release instant.")]
        public int AttackContactFrame = 1;
        [Tooltip("Only for symmetric characters with intentionally mirrored authored views.")]
        public bool FlipX;
    }

    [Serializable]
    public sealed class UnitArt
    {
        public string Id;
        [Tooltip("N, NE, E, SE, S, SW, W, NW. These are views, not animation frames.")]
        public Sprite[] IdleDirections = new Sprite[8];
        public Sprite[] AttackDirections = new Sprite[8];
        public UnitSpriteRig[] WalkRigs = new UnitSpriteRig[8];
        [Tooltip("Optional authored temporal clips, ordered N, NE, E, SE, S, SW, W, NW. Empty entries retain the existing rig.")]
        public UnitDirectionalAnimation[] DirectionalAnimations = new UnitDirectionalAnimation[8];
        public Sprite Portrait;
        [Tooltip("Desired visible width of the south-facing idle pose, in board world units.")]
        public float WorldWidth = 1f;
        public float ReferencePixelWidth = 150f;
        public Vector2 FootPivot;
        public bool ProvisionalDirectionalProjection;
        [TextArea] public string SourceNotes;

        public UnitDirectionalAnimation Animation(UnitFacing facing)
        {
            int index = (int)facing;
            return DirectionalAnimations != null && index >= 0 && index < DirectionalAnimations.Length
                ? DirectionalAnimations[index] : null;
        }

        public Sprite Idle(UnitFacing facing)
        {
            UnitDirectionalAnimation animation = Animation(facing);
            if (animation != null && animation.Idle != null && animation.Idle.Length > 0 && animation.Idle[0] != null)
                return animation.Idle[0];
            int index = (int)facing;
            return IdleDirections != null && index < IdleDirections.Length && IdleDirections[index] != null
                ? IdleDirections[index] : Portrait;
        }

        public Sprite Attack(UnitFacing facing)
        {
            UnitDirectionalAnimation animation = Animation(facing);
            if (animation != null && animation.Attack != null && animation.Attack.Length > 0)
            {
                Sprite contact = animation.Attack[Mathf.Clamp(animation.AttackContactFrame, 0, animation.Attack.Length - 1)];
                if (contact != null) return contact;
            }
            int index = (int)facing;
            return AttackDirections != null && index < AttackDirections.Length && AttackDirections[index] != null
                ? AttackDirections[index] : Idle(facing);
        }
    }

    [CreateAssetMenu(menuName = "Monster Pouch/Unit art catalog")]
    public sealed class UnitArtCatalog : ScriptableObject
    {
        public UnitArt[] Units = Array.Empty<UnitArt>();

        public UnitArt Get(string id)
        {
            if (Units == null) return null;
            foreach (UnitArt art in Units)
                if (art != null && string.Equals(art.Id, id, StringComparison.OrdinalIgnoreCase)) return art;
            return null;
        }
    }
}
