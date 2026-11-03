using System;
using UnityEngine;

namespace MonsterPouch.Gameplay.Presentation
{
    public enum MartioVisualState { Idle, Move, Attack, Hurt, Defeat }

    [Serializable]
    public struct MartioCelestialProp
    {
        public Sprite Sprite;
        public Vector2 OffsetInBodyHeights;
        public float SizeInBodyHeights;
        public float Opacity;
        public bool BehindBody;
    }

    // Presentation data only. This asset never schedules attacks, collisions or damage.
    public sealed class MartioVisualAssets : ScriptableObject
    {
        public UnitArt Body = new UnitArt();
        public Sprite PortraitLeft;
        public Sprite PortraitRight;
        public Sprite[] HurtDirections = new Sprite[8];
        public ProjectileArt Mars = new ProjectileArt();
        public ProjectileArt Moon = new ProjectileArt();
        public Sprite[] MarsShockwave = Array.Empty<Sprite>();
        public Sprite[] MoonShockwave = Array.Empty<Sprite>();
        public Sprite[] UpgradeIcons = new Sprite[3];
        public const float AttackDuration = .52f;
        public const float ReleaseSeconds = .24f;
        public const float PropReturnSeconds = .44f;
        public const float ShockwaveDuration = .42f;
        public const float ImpactDuration = .20f;
        public const float PreviewHopDuration = .60f;
        static readonly Vector2[] MarsOffsets = {
            new Vector2(0, .48f), new Vector2(.49f, .48f), new Vector2(.52f, .40f), new Vector2(.47f, .36f),
            new Vector2(-.46f, .38f), new Vector2(-.47f, .36f), new Vector2(-.52f, .40f), new Vector2(-.49f, .48f)
        };

        public Sprite SampleBody(UnitFacing facing, MartioVisualState state, float seconds, out bool flipX)
        {
            var clip = Body.Animation(facing);
            flipX = clip != null && clip.FlipX;
            if (clip == null) return Body.Portrait;
            float t = Mathf.Max(0, seconds);
            switch (state)
            {
                case MartioVisualState.Move:
                    var hop = TileHopProfile.For("martio");
                    float phase = t / PreviewHopDuration % 1;
                    var poses = Mathf.FloorToInt(t / PreviewHopDuration) % 2 == 0 ? clip.HopLeft : clip.HopRight;
                    return At(poses, phase < hop.Prepare ? 0 : phase < hop.Prepare + hop.Air ? 1 : 2);
                case MartioVisualState.Attack:
                    if (t >= AttackDuration) return Loop(clip.Idle, t - AttackDuration, clip.IdleFramesPerSecond);
                    return At(clip.Attack, t < .12f ? 0 : t < ReleaseSeconds ? 1 : t < .36f ? 2 : 3);
                case MartioVisualState.Hurt:
                    return t < .16f ? At(HurtDirections, (int)facing) : Loop(clip.Idle, t, clip.IdleFramesPerSecond);
                case MartioVisualState.Defeat: return At(clip.Death, t < .16f ? 0 : 1);
                default: return Loop(clip.Idle, t, clip.IdleFramesPerSecond);
            }
        }

        // Caller supplies a reused three-element buffer: one Mars and exactly two moons.
        // Props disappear at release; flight sprites are then placed at simulation Position.
        public void SampleProps(UnitFacing facing, MartioVisualState state, float stateSeconds,
            float orbitSeconds, MartioCelestialProp[] destination)
        {
            if (destination == null || destination.Length < 3)
                throw new ArgumentException("Martio requires three celestial prop slots.", nameof(destination));
            float opacity = state == MartioVisualState.Defeat ? 1 - Mathf.Clamp01(stateSeconds / .22f) : 1;
            if (state == MartioVisualState.Attack && stateSeconds >= ReleaseSeconds)
                opacity *= Mathf.Clamp01((stateSeconds - PropReturnSeconds) / (AttackDuration - PropReturnSeconds));
            int direction = Mathf.Clamp((int)facing, 0, 7);
            float pulse = state == MartioVisualState.Attack && stateSeconds < ReleaseSeconds
                ? 1 + .12f * Mathf.Clamp01(stateSeconds / ReleaseSeconds) : 1;
            destination[0] = new MartioCelestialProp {
                Sprite = Loop(Mars.Flight, orbitSeconds, 8), OffsetInBodyHeights = MarsOffsets[direction],
                SizeInBodyHeights = .48f * pulse, Opacity = opacity, BehindBody = direction == 0 || direction == 1 || direction == 7
            };
            for (int i = 0; i < 2; i++)
            {
                float angle = orbitSeconds * 1.4f + i * Mathf.PI;
                destination[i + 1] = new MartioCelestialProp {
                    Sprite = Loop(Moon.Flight, orbitSeconds + i * .125f, 8),
                    OffsetInBodyHeights = new Vector2(Mathf.Cos(angle) * .53f, .69f + Mathf.Sin(angle) * .14f),
                    SizeInBodyHeights = .32f, Opacity = opacity, BehindBody = Mathf.Sin(angle) > 0
                };
            }
        }

        // The caller must pass the actual upgrade-1 flag from the volley that impacted.
        // One visual per projectile impact, never one per splash victim.
        public bool SampleShockwave(bool upgradeOne, bool isMars, float age, out Sprite sprite,
            out float diameterInTiles, out float opacity)
        {
            sprite = null; diameterInTiles = 0; opacity = 0;
            if (!upgradeOne || age < 0 || age >= ShockwaveDuration) return false;
            float p = age / ShockwaveDuration;
            sprite = At(isMars ? MarsShockwave : MoonShockwave, Mathf.FloorToInt(p * 8));
            // Reach adjacent diagonal cells before fading; transparent atlas margins
            // are included in this presentation diameter, not in damage geometry.
            diameterInTiles = Mathf.Lerp(.8f, 4.25f, Mathf.SmoothStep(0, 1, p / .65f));
            opacity = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.52f, 1, p));
            return sprite != null;
        }

        static Sprite Loop(Sprite[] sprites, float seconds, float fps)
            => sprites == null || sprites.Length == 0 ? null : sprites[Mathf.FloorToInt(Mathf.Max(0, seconds) * fps) % sprites.Length];
        static Sprite At(Sprite[] sprites, int index)
            => sprites == null || sprites.Length == 0 ? null : sprites[Mathf.Clamp(index, 0, sprites.Length - 1)];
    }
}
