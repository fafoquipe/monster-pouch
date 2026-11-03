using MonsterPouch.Gameplay.Presentation;
using UnityEngine;

namespace MonsterPouch.Local
{
    // One sampled renderer for combat and inspector playback. Only the common sizing helper applies overrides.
    public static class AireolaFlightRendering
    {
        public const float CalibrationCellPixels = 64;
        public const string Bolt = "aireola-crossbow", Note = "aireola-note", Sphere = "aireola-sphere";
        public static bool Owns(string renderer) => renderer == Bolt || renderer == Note || renderer == Sphere;
        // All three Aireola producers bind to the same imported catalog, including basic flights.
        // This is the real runtime and editor route; do not substitute a QA-only fixture.
        public static ProjectileArt Resolve(string renderer)
        {
            string clip = renderer == Bolt ? "zephalontropo/basic-flight"
                : renderer == Note ? "alboguera/basic-flight"
                : renderer == Sphere ? "zephalontropo/sphere-flight" : null;
            return clip == null ? null : Resources.Load<AireolaVisualCatalog>("MonsterPouch/Aireola/AireolaVisuals")?.Get(clip)?.FlightArt();
        }
        public static ProjectileVisualPose Sample(ProjectileArt art, float elapsed, float cellPixels, float angle = 0)
        {
            var pose = ProjectileFlightRendering.Catalog(art, elapsed, cellPixels / CalibrationCellPixels);
            if (art != null && art.OrientToTarget) pose.Rotation = angle;
            return pose;
        }
    }
}
