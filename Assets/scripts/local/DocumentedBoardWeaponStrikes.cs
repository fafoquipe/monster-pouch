using System.Collections;
using System.Collections.Generic;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Local
{
    public sealed partial class DocumentedBattleEffects
    {
        sealed class BoardStrike
        {
            public CombatAttackPresentationSnapshot Emission;
            public CombatSimulation Simulation;
            public BoardWeaponStrikeVisual Art;
            public BoardWeaponStrikePhase Phase;
            public Transform Plane;
            public SpriteRenderer Renderer;
            public Vector3 Source, Target;
            public int AttackReset;
            public float EmittedAt, ReleaseAt;
            public bool Cancelled;
        }
        BoardWeaponStrikeVisuals boardStrikeArt;
        readonly List<BoardStrike> boardStrikes = new List<BoardStrike>();

        bool TryBoardWeaponStrike(BattleUnit actor, BattleUnit target)
        {
            string id = actor?.BaseStats.DefinitionId;
            if ((id != "palista" && id != "cael") || target == null || match == null ||
                match.Mapper == null || match.Simulation == null || !Current(match.Simulation)) return false;
            var emission = match.CurrentAttackPresentation;
            if (emission == null || emission.IsAbility || emission.Source != actor || emission.Target != target) return false;
            if (boardStrikeArt == null) boardStrikeArt = Resources.Load<BoardWeaponStrikeVisuals>("MonsterPouch/BoardWeaponStrikes/BoardWeaponStrikeVisuals");
            var art = boardStrikeArt != null ? boardStrikeArt.Get(id) : null;
            if (art?.Sprite == null || actor.CurrentCell == null || target.CurrentCell == null) return false;
            var simulation = match.Simulation;
            var tail = BasicAttackTiming.Tail(actor);
            var cast = new BoardStrike {
                Emission = emission, Simulation = simulation, Art = art,
                Phase = new BoardWeaponStrikePhase(emission.PreparationSeconds + emission.TravelSeconds,
                    art.ForwardSeconds, Mathf.Min(art.ReturnSeconds, Mathf.Max(0, tail.x + tail.y))),
                Source = match.Mapper.GetWorldPosition(actor.CurrentCell),
                Target = match.Mapper.GetWorldPosition(target.CurrentCell),
                EmittedAt = simulation.Elapsed, ReleaseAt = simulation.Elapsed + emission.PreparationSeconds,
                AttackReset = actor.AttackResetVersion
            };
            boardStrikes.Add(cast); StartCoroutine(BoardWeaponStrike(cast));
            // This is a separate ground-plane accent. Existing projectile renderers,
            // their editor sizing controls and settled contact VFX retain ownership.
            return true;
        }

        void ContactBoardWeaponStrike(BattleUnit actor, BattleUnit target)
        {
            foreach (var cast in boardStrikes)
            {
                if (cast.Cancelled || cast.Phase.Contact || cast.Emission.Source != actor || cast.Emission.Target != target ||
                    cast.Simulation != match.Simulation || cast.Simulation.Elapsed + .00001f < cast.EmittedAt + cast.Phase.Due) continue;
                if (target.CurrentCell != null) cast.Target = match.Mapper.GetWorldPosition(target.CurrentCell);
                cast.Phase.ConfirmContact(); DrawBoardWeaponStrike(cast); return;
            }
        }

        bool BoardStrikeValid(BoardStrike cast)
        {
            var e = cast.Emission;
            if (cast.Cancelled || !Current(cast.Simulation) || e.Source == null || e.Target == null ||
                e.Source.CombatResetVersion != e.SourceResetVersion || e.Target.CombatResetVersion != e.TargetResetVersion) return false;
            if (cast.Phase.Contact) return !cast.Phase.Finished;
            if (e.TravelSeconds <= 0 && (!e.Source.IsAlive || e.Source.AttackResetVersion != cast.AttackReset)) return false;
            // Before release, interruption invalidates the queued visual. Afterwards
            // a ranged strike follows the same lifetime as its already released hit.
            if (cast.Simulation.Elapsed + .00001f < cast.ReleaseAt &&
                (!e.Source.IsAlive || e.Source.IsStunned || !e.Source.isActiveAndEnabled || e.Source.AttackResetVersion != cast.AttackReset)) return false;
            if (!e.Target.IsAlive) return false;
            return cast.Simulation.Elapsed <= cast.EmittedAt + cast.Phase.Due + CombatSimulation.TickDuration;
        }

        IEnumerator BoardWeaponStrike(BoardStrike cast)
        {
            try
            {
                while (BoardStrikeValid(cast))
                {
                    if (!cast.Phase.Contact && cast.Emission.Target.CurrentCell != null)
                        cast.Target = match.Mapper.GetWorldPosition(cast.Emission.Target.CurrentCell);
                    if (cast.Phase.Visible) DrawBoardWeaponStrike(cast);
                    yield return null;
                    cast.Phase.Advance(EffectDelta, cast.Simulation.Elapsed - cast.EmittedAt, CombatSimulation.TickDuration);
                }
            }
            finally { ReleaseBoardWeaponStrike(cast); }
        }

        void DrawBoardWeaponStrike(BoardStrike cast)
        {
            if (cast.Plane == null)
            {
                cast.Plane = new GameObject("board-weapon-strike-" + cast.Emission.EmissionId).transform;
                cast.Renderer = UnitPresentation.CreateSpriteRenderer("flat-weapon", cast.Plane);
                cast.Plane.gameObject.layer = cast.Renderer.gameObject.layer = cast.Emission.Source.gameObject.layer;
            }
            // No VisualRoot, body sprite, hand anchor, height offset, or body scaling.
            BoardWeaponStrikeRendering.Apply(cast.Plane, cast.Renderer, cast.Art, cast.Source, cast.Target,
                match.Mapper.CellSize, cast.Phase.Progress, cast.Phase.Alpha);
        }

        void ReleaseBoardWeaponStrike(BoardStrike cast)
        {
            cast.Cancelled = true;
            if (cast.Renderer != null) cast.Renderer.enabled = false;
            if (cast.Plane != null) Destroy(cast.Plane.gameObject);
            boardStrikes.Remove(cast);
        }

        void ClearBoardWeaponStrikes()
        {
            for (int i = boardStrikes.Count - 1; i >= 0; i--) ReleaseBoardWeaponStrike(boardStrikes[i]);
        }
    }
}
