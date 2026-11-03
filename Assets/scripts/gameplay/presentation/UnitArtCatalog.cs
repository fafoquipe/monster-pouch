using System;
using UnityEngine;

namespace MonsterPouch.Gameplay.Presentation
{
    // Clockwise in screen/world space. Board Y increases toward the bottom.
    public enum UnitFacing { North, NorthEast, East, SouthEast, South, SouthWest, West, NorthWest }

    // WholeBodyHop retains the directional full-body pose through the shared launch, arc and landing.
    // Explicit values preserve existing serialized characters.
    public enum UnitTravelMotion { Legacy = 0, AlternatingHop = 1, Flying = 2, WholeBodyHop = 3 }

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
        [Tooltip("Planted launch, airborne, opposite-foot landing; authored left-foot hop.")]
        public Sprite[] HopLeft = Array.Empty<Sprite>();
        [Tooltip("Planted launch, airborne, opposite-foot landing; authored right-foot hop.")]
        public Sprite[] HopRight = Array.Empty<Sprite>();
        [Tooltip("Optional opening relocation: takeoff, air, landing. No baked translation. HopLeft is the fallback.")]
        public Sprite[] OpeningJump = Array.Empty<Sprite>();
        [Tooltip("Opening tunnel: dive, underground travel, emergence. No baked translation or gameplay invisibility.")]
        public Sprite[] Tunnel = Array.Empty<Sprite>();
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
        [Tooltip("Presentation-only travel toward the target during an authored attack. Board occupancy never moves.")]
        [Min(0f)] public float AttackLungeDistance = .09f;
        [Tooltip("Presentation-only squash/stretch at attack contact.")]
        [Range(0f, .3f)] public float AttackImpactSquash = .07f;
        [Tooltip("Presentation-only travel during a Super animation.")]
        [Min(0f)] public float SpecialLungeDistance = .06f;
        [Tooltip("Vertical lift used while presenting the Super animation.")]
        [Min(0f)] public float SpecialLift = .04f;
        [Tooltip("Scale punch at the Super release pose.")]
        [Range(0f, .3f)] public float SpecialPunchScale = .08f;
        [Tooltip("Normalized point where the Super reaches its release/contact pose.")]
        [Range(.15f, .85f)] public float SpecialReleasePoint = .55f;
        [Tooltip("Only for symmetric characters with intentionally mirrored authored views.")]
        public bool FlipX;
        [Tooltip("Mirror only the walking sequence, relative to FlipX. Idle, attack and Super keep their authored facing.")]
        public bool MoveFlipX;
    }

    [Serializable]
    public sealed class UnitArt
    {
        public string Id;
        [Tooltip("Visual travel only. Flying does not grant combat flight or invulnerability.")]
        public UnitTravelMotion TravelMotion;
        [Tooltip("WholeBodyHop only: use HopLeft's planted, airborne and landing full-body poses. Off preserves existing whole-body artwork, including Kazuru.")]
        public bool WholeBodyHopUsesPoses;
        [Tooltip("Use authored full-body attack frames instead of legacy body-part slicing.")]
        public bool AuthoredBodyMotion;
        [Tooltip("N, NE, E, SE, S, SW, W, NW. These are views, not animation frames.")]
        public Sprite[] IdleDirections = new Sprite[8];
        public Sprite[] AttackDirections = new Sprite[8];
        public UnitSpriteRig[] WalkRigs = new UnitSpriteRig[8];
        [Tooltip("Optional authored temporal clips, ordered N, NE, E, SE, S, SW, W, NW. Empty entries retain the existing rig.")]
        public UnitDirectionalAnimation[] DirectionalAnimations = new UnitDirectionalAnimation[8];
        public Sprite Portrait;
        [Tooltip("Desired visible width of the south-facing idle pose, in board world units.")]
        public float WorldWidth = 1f;
        [Tooltip("Escala visual según la complexión del personaje. Se comparte entre combate y figuras de lobby; conserva las proporciones y el anclaje de los pies.")]
        [Range(.75f,1.35f)] public float DisplayScale = 1f;
        public const float AuthoredScaleMultiplier = 1.2f;
        public const float MinimumWidthInTiles = .4f;
        public const float MaximumWidthInTiles = 2f;
        // -1 retains existing art sizes until the designer moves the main character slider.
        [HideInInspector] public float InGameSizePercent = -1f;
        // Independent menu sizes. 100 retains the inherited context size; missing
        // fields also retain it when an older serialized asset supplies zero.
        [HideInInspector] public float LobbySizePercent = 100f;
        [HideInInspector] public float BriefSizePercent = 100f;
        public float GetLobbyScale(float legacyScale = 1f) => ResolveFigureScale(LobbySizePercent, legacyScale);
        public float GetBriefScale(float legacyScale = 1f) => ResolveFigureScale(BriefSizePercent, legacyScale);
        private static float ResolveFigureScale(float percent, float legacyScale)
        {
            if (float.IsNaN(legacyScale) || float.IsInfinity(legacyScale) || legacyScale <= 0) legacyScale = 1f;
            if (float.IsNaN(percent) || float.IsInfinity(percent) || percent <= 0) return legacyScale;
            double result = (double)legacyScale * percent / 100d;
            return result > float.MaxValue || result < float.Epsilon ? legacyScale : (float)result;
        }
        public float AuthoredWorldWidth => AuthoredScaleMultiplier * DisplayScale * WorldWidth;
        public float GetInGameSizePercent(float tileWidth)
        {
            if (InGameSizePercent < 0 || float.IsNaN(InGameSizePercent) || float.IsInfinity(InGameSizePercent))
                return Mathf.InverseLerp(MinimumWidthInTiles * tileWidth, MaximumWidthInTiles * tileWidth, AuthoredWorldWidth) * 100f;
            return Mathf.Clamp(InGameSizePercent, 0, 100);
        }
        public float GetInGameScale(float tileWidth) =>
            Mathf.Lerp(MinimumWidthInTiles, MaximumWidthInTiles, GetInGameSizePercent(tileWidth) / 100f)
            * tileWidth / Mathf.Max(.001f, AuthoredWorldWidth);
        public float ReferencePixelWidth = 150f;
        public Vector2 FootPivot;
        [Tooltip("Punto superior de la cabeza en coordenadas locales del dibujo, desde el pivote de los pies. La barra sigue este punto al cambiar el tamaño. Cero conserva el anclaje de respaldo para arte sin calibrar.")]
        public Vector2 VitalsHeadAnchor;
        public bool ProvisionalDirectionalProjection;
        [Header("Presentation tuning")]
        [Tooltip("Duración visual de la habilidad en todas las direcciones. 0 conserva la duración de cada clip. Los desplazamientos, invocaciones y resurrecciones usan sus tiempos de combate.")]
        [Min(0f)] public float AbilityAnimationDuration;
        [Min(.05f)] public float HitFlashDuration = .16f;
        [Min(.05f)] public float AbilityPulseDuration = .42f;
        [Range(0f, .25f)] public float AbilityPulseScale = .09f;
        [Range(0f, 1f)] public float MoveEase = .35f;
        [Range(0f, .15f)] public float MoveArcHeight = .025f;
        [Tooltip("Subtle body weight transfer added to authored walk frames. Does not alter movement or attack timing.")]
        [Range(0f, 1f)] public float WalkWeight = 1f;
        [Range(0f, .08f)] public float SuperReadyPulseScale = .025f;
        [TextArea] public string SourceNotes;

        public float GetAbilityAnimationDuration(UnitFacing facing)
        {
            float duration = AbilityAnimationDuration;
            if (float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0)
                duration = Animation(facing)?.SpecialDuration ?? .65f;
            return float.IsNaN(duration) || float.IsInfinity(duration) ? .65f : Mathf.Max(.1f, duration);
        }

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

        public UnitArtCatalog ForCharacters(MonsterPouch.Gameplay.Characters.CharacterDefinition[] characters)
        {
            var runtime=Instantiate(this);var units=new System.Collections.Generic.List<UnitArt>(Units??Array.Empty<UnitArt>());
            foreach(var character in characters)
            {
                if(character==null||character.Visuals==null)continue;
                var drawing=JsonUtility.FromJson<UnitArt>(JsonUtility.ToJson(character.Visuals));drawing.Id=character.Id;
                if(character.Portrait!=null)drawing.Portrait=character.Portrait;
                int index=units.FindIndex(u=>u!=null&&u.Id==character.Id);if(index>=0)units[index]=drawing;else units.Add(drawing);
            }
            runtime.Units=units.ToArray();return runtime;
        }
        public UnitArt Get(string id)
        {
            if (Units == null) return null;
            foreach (UnitArt art in Units)
                if (art != null && string.Equals(art.Id, id, StringComparison.OrdinalIgnoreCase)) return art;
            return null;
        }
    }
}
