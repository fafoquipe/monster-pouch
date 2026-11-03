using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using UnityEngine;

namespace MonsterPouch.Gameplay.Presentation
{
    public sealed partial class UnitPresentation
    {
        private bool displacementActive, displacementTimedTunnel;
        private CombatDisplacementKind displacementKind;
        private BoardCell displacementDestination;
        private Vector3 displacementFrom, displacementTo, displacementGroundWorldPosition;
        private float displacementElapsed, displacementDuration, displacementGroundY;
        private int displacementResetVersion, latestDisplacementTick = -1;
        private readonly System.Collections.Generic.HashSet<long> displacementEvents =
            new System.Collections.Generic.HashSet<long>();

        public bool IsPresentingDisplacement => displacementActive;

        private void ResetDisplacementTracking()
        {
            displacementActive = displacementTimedTunnel = displacementTimedDagata = false;
            displacementDestination = null;
            displacementEvents.Clear();
            latestDisplacementTick = -1;
            displacementResetVersion = unit?.CombatResetVersion ?? 0;
        }

        private void ClearDisplacementPresentation()
        {
            if (!displacementActive) return;
            displacementActive = displacementTimedTunnel = displacementTimedDagata = false;
            displacementDestination = null;
            displacementElapsed = displacementDuration = 0;
            if (visualRoot != null && art != null) ResetPose();
        }

        /// <summary>Consumes an already committed relocation. Never moves a cell or changes combat flight.</summary>
        public void PresentDisplacement(CombatDisplacementSnapshot motion, float openingWindowRemaining = 0)
        {
            if (art?.Id == "dagata" && dagataJumpSource != null && motion.Kind == CombatDisplacementKind.OpeningJump) return;
            if (unit == null || motion.Unit != unit || !unit.IsAlive || !unit.isActiveAndEnabled ||
                dead || reviving || frozen || visualRoot == null || art == null || mapper == null ||
                motion.UnitResetVersion != unit.CombatResetVersion ||
                !ReferenceEquals(unit.CurrentCell, motion.To) || !ReferenceEquals(motion.To?.OccupiedBy, unit) ||
                !mapper.TryGetWorldPosition(motion.From, out var from) ||
                !mapper.TryGetWorldPosition(motion.To, out var to)) return;
            if (displacementResetVersion != motion.UnitResetVersion) ResetDisplacementTracking();
            if (motion.StartTick < latestDisplacementTick || !displacementEvents.Add(motion.EventId)) return;
            latestDisplacementTick = motion.StartTick;
            SnapToCell();
            ResetPose();
            // The collider and gameplay target transform remain at the committed destination.
            transform.position = to;
            float window = float.IsNaN(openingWindowRemaining) || float.IsInfinity(openingWindowRemaining)
                ? 0 : Mathf.Max(0, openingWindowRemaining);
            float authoredDuration = motion.IsAuthoritativeInstant ? 0 :
                Mathf.Max(0, motion.EndTick - motion.StartTick) * CombatSimulation.TickDuration;
            bool timedTunnel = motion.Kind == CombatDisplacementKind.Tunnel && authoredDuration > 0;
            float duration = motion.Kind == CombatDisplacementKind.OpeningJump ? window : authoredDuration;
            if (motion.Kind == CombatDisplacementKind.Teleport || duration <= 0 || attacking || specialActive ||
                unit.IsStunned || unit.IsRooted || (timedTunnel ? !unit.IsFlying : unit.IsFlying) || unit.IsBeingPulled) return;
            // Core reuses the legacy travel lifecycle; Tunnel still uses its own authored drawings.
            displacementTimedTunnel = timedTunnel;
            displacementKind = motion.Kind;
            displacementFrom = from;
            displacementTo = to;
            displacementDestination = motion.To;
            displacementElapsed = 0;
            displacementDuration = duration;
            displacementActive = true;
            FaceWorld(to - from);
            AnimateDisplacement(0);
            UpdateSorting();
        }

        private void ValidateDisplacementPresentation()
        {
            if (!displacementActive || displacementTimedDagata) return;
            if (unit == null || !unit.IsAlive || !unit.isActiveAndEnabled ||
                unit.CombatResetVersion != displacementResetVersion ||
                !ReferenceEquals(unit.CurrentCell, displacementDestination) ||
                !ReferenceEquals(displacementDestination.OccupiedBy, unit) ||
                dead || reviving || frozen || unit.IsStunned || unit.IsRooted ||
                (displacementTimedTunnel ? !unit.IsFlying : unit.IsFlying) ||
                unit.IsBeingPulled || moving || attacking || specialActive)
            {
                SnapToCell();
                ResetPose();
            }
        }

        private void AnimateDisplacement(float dt)
        {
            if (displacementTimedDagata) { AnimateDagataJump(); return; }
            // Do not create a second travel clock or advance it from rendering while paused.
            displacementElapsed = displacementTimedTunnel
                ? Mathf.Clamp(displacementDuration - unit.FlightRemaining, 0, displacementDuration)
                : displacementElapsed + dt;
            float phase = Mathf.Clamp01(displacementElapsed / displacementDuration);
            if (phase >= 1)
            {
                ClearDisplacementPresentation();
                AnimateIdle();
                return;
            }
            var clip = art.Animation(facing);
            var profile = TileHopProfile.For(art.Id);
            float flight = profile.Flight(phase);
            float travel = displacementKind == CombatDisplacementKind.ForcedPush
                ? phase : Mathf.SmoothStep(0, 1, flight);
            Vector3 ground = Vector3.Lerp(displacementFrom, displacementTo, travel);
            displacementGroundWorldPosition = ground;
            displacementGroundY = ground.y;
            Vector3 offset = transform.InverseTransformVector(ground - displacementTo);
            float arc = Mathf.Sin(flight * Mathf.PI) * Mathf.Abs(mapper.CellSize.y) * profile.Height;
            Sprite[] poses = displacementKind == CombatDisplacementKind.Tunnel ? clip?.Tunnel : clip?.OpeningJump;
            if (displacementKind == CombatDisplacementKind.OpeningJump && (poses == null || poses.Length < 3))
                poses = clip?.HopLeft;
            if (poses != null && poses.Length >= 3 && displacementKind != CombatDisplacementKind.ForcedPush)
            {
                int index = phase < profile.Prepare ? 0 : phase < profile.Prepare + profile.Air ? 1 : 2;
                ShowFrame(poses[index], clip);
            }
            float lift = displacementKind == CombatDisplacementKind.OpeningJump ? arc :
                displacementKind == CombatDisplacementKind.Tunnel ? -arc : 0;
            visualRoot.localPosition = offset + transform.InverseTransformVector(Vector3.up * lift);
            // These dive/traverse/emerge drawings do not decide Core targetability or travel duration.
            shadow.transform.localPosition = offset + new Vector3(0, -.035f, .02f);
            if (displacementKind == CombatDisplacementKind.OpeningJump)
            {
                float air = Mathf.Sin(flight * Mathf.PI);
                shadow.transform.localScale *= 1 - air * .09f;
                shadow.color = new Color(.015f, .025f, .035f, ShadowOpacity - air * .05f);
            }
        }
    }
}
