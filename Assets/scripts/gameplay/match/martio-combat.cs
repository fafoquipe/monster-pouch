using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    public sealed partial class CombatSimulation
    {
        public sealed class MartioVolley
        {
            public int Id { get; internal set; }
            public BattleUnit Source { get; internal set; }
            public MartioProjectileSimulation Flight { get; internal set; }
            public float Speed { get; internal set; }
            public bool Explodes { get; internal set; }
            public bool Active { get; internal set; }
            public int FanSide { get; internal set; }
            internal int SourceVersion;
        }
        public sealed class MartioContact
        {
            public int Id { get; internal set; }
            public BoardCell Cell { get; internal set; }
            public BraidedProjectile Projectile { get; internal set; }
            public bool Explodes { get; internal set; }
            public float At { get; internal set; }
            public MartioFlightPoint Position { get; internal set; }
            public MartioFlightEventKind Kind { get; internal set; }
            public bool StopsProjectile { get; internal set; }
            public int VolleyId { get; internal set; }
            public double Progress { get; internal set; }
            public BattleUnit Source { get; internal set; }
            internal int SourceVersion;
            public bool IsCurrent => Source != null && Source.CombatResetVersion == SourceVersion;
        }
        readonly List<MartioVolley> martioVolleys = new List<MartioVolley>();
        readonly List<MartioContact> martioContacts = new List<MartioContact>();
        int martioSequence;
        public IReadOnlyList<MartioVolley> MartioVolleys => martioVolleys;
        public IReadOnlyList<MartioContact> MartioContacts => martioContacts;

        void QueueMartioAttack(ActorClock actor, BattleUnit target, int damage)
        {
            if (target == null) return;
            int release = tick + ToTicks(GetAttackWindup(actor.Unit));
            // Use the ordinary windup reservation: target loss, stun and taunt already
            // cancel or redirect it. The captured victim is discarded at release.
            pending.Add(new PendingImpact { Source = actor, Target = target,
                Effect = "martio-release", Damage = damage, DueTick = release, ReleaseTick = release,
                Projectile = true, RequiresRange = true });
            Attacked?.Invoke(actor.Unit, target, true);
        }

        void LaunchMartio(PendingImpact impact)
        {
            var actor = impact.Source;
            var unit = actor.Unit;
            if (unit == null || !unit.IsAlive || unit.CurrentCell == null || !unit.isActiveAndEnabled || !CanReach(actor, impact.Target)) return;
            var from = unit.CurrentCell; var to = impact.Target.CurrentCell;
            int Damage(string effect, string key, int fallback) => (int)MartioBalanceValues.Normalize(effect,key,V(actor,effect,key,fallback));
            var damage = new MartioPrototypeConfig {
                MarsImpactDamage = impact.Damage,
                FirstMoonImpactDamage = Damage("martio-orbits", "firstMoonDamage", 3),
                SecondMoonImpactDamage = Damage("martio-orbits", "secondMoonDamage", 3),
                MarsExplosionDamage = Damage("martio-explosion", "marsDamage", 6),
                FirstMoonExplosionDamage = Damage("martio-explosion", "firstMoonDamage", 3),
                SecondMoonExplosionDamage = Damage("martio-explosion", "secondMoonDamage", 3)
            };
            int range = (int)MartioBalanceValues.Normalize("martio-orbits","travelRange",V(actor,"martio-orbits","travelRange",7));
            bool explosion = Has(actor, "martio-explosion");
            int lateralRange = Damage("martio-moons", "lateralTravelRange", 3);
            int fanAngle = Damage("martio-moons", "fanAngleDegrees", 30);
            var heading = new ProjectileGridPoint(to.X - from.X, to.Y - from.Y);
            foreach (var group in MartioFanPattern.Groups(heading, range, Has(actor, "martio-moons"), lateralRange, fanAngle)) {
                var flight = new MartioProjectileSimulation(damage, new ProjectileGridPoint(from.X, from.Y),
                    group.Heading, group.Range, unit.BaseStats.ProjectileSpeed,
                    explosion, Has(actor, "martio-piercing"), false,
                    MartioRepeatHitPolicy.OncePerEnemy, MartioCornerPolicy.InteriorOnly,
                    new MartioFlightBounds(BoardManager.Width, BoardManager.Height),
                    HasEditedArea(actor,"martio-explosion") ? (Func<ProjectileGridPoint,IEnumerable<ProjectileGridPoint>>)(center=>
                        ResolveAbilityArea(actor,"martio-explosion",board.GetCell(center.X,center.Y),Array.Empty<BoardCell>(),board.GetCell(center.X,center.Y))
                            .Select(c=>new ProjectileGridPoint(c.X,c.Y))) : null);
                martioVolleys.Add(new MartioVolley { Id = ++martioSequence, Source = unit, FanSide = group.Side,
                    SourceVersion = unit.CombatResetVersion, Flight = flight,
                    Speed = unit.BaseStats.ProjectileSpeed, Explodes = explosion && !HasEditedArea(actor,"martio-explosion"), Active = true });
            }
        }

        void TickMartioFlights()
        {
            martioContacts.RemoveAll(c => !c.IsCurrent || Elapsed - c.At > .65f);
            foreach (var volley in martioVolleys.ToArray())
            {
                if (volley.Source == null || volley.Source.CombatResetVersion != volley.SourceVersion) { volley.Active = false; continue; }
                var source = Clock(volley.Source);
                if (source == null) { volley.Active = false; continue; }
                var occupancy = new MartioBoardOccupancy(board, volley.Source.Side, enemy =>
                    enemy.IsAlive && !enemy.IsReviving && enemy.isActiveAndEnabled && !IsInvisible(enemy));
                volley.Flight.Advance(ExactTickDuration, occupancy, hit => {
                    var victim = actors.FirstOrDefault(a => a.Unit != null && a.Unit.UnitId == hit.EnemyId)?.Unit;
                    if (victim == null) return;
                    // The existing resolver owns armor, shields, invulnerability, reflection,
                    // simultaneous damage, energy, death and kill rewards.
                    pending.Add(new PendingImpact { Source = source, Target = victim, Damage = hit.Damage,
                        Effect = hit.Kind == MartioDamageKind.Impact ? "martio-impact" : "martio-splash",
                        DueTick = tick, ReleaseTick = tick, Projectile = true,
                        Secondary = hit.Kind != MartioDamageKind.Impact });
                }, visual => {
                    // Exactly one event for a non-piercing impact or a terminal lane.
                    // Range/edge expiry is visual only, even with the explosion upgrade.
                    martioContacts.Add(new MartioContact { Id = ++martioSequence, VolleyId = volley.Id,
                        Projectile = visual.Projectile, Position = visual.Position, Kind = visual.Kind, Progress = visual.Progress,
                        Source = volley.Source, SourceVersion = volley.SourceVersion,
                        StopsProjectile = visual.StopsProjectile,
                        Cell = board.GetCell(visual.Cell.X, visual.Cell.Y),
                        Explodes = volley.Explodes && visual.Kind == MartioFlightEventKind.Impact, At = Elapsed });
                });
                if (volley.Flight.Complete) volley.Active = false;
            }
            martioVolleys.RemoveAll(v => !v.Active);
        }

        void ClearMartioFlights()
        {
            foreach (var volley in martioVolleys) volley.Active = false;
            martioVolleys.Clear(); martioContacts.Clear(); martioSequence = 0;
        }
    }
}
