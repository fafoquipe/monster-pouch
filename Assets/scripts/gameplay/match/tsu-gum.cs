using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    public enum GumBuff { Speed, Shield, Damage }

    public sealed partial class CombatSimulation
    {
        sealed class GumGift
        {
            public ActorClock Source, Target;
            public int Release, Due, AttackVersion, SourceVersion, TargetVersion;
            public bool Released;
            public GumBuff Buff;
            public float Bonus, DamageReduction;
        }
        readonly List<GumGift> gumGifts = new List<GumGift>();
        readonly Dictionary<BattleUnit, (int Version, int Until)> gumRootVersions = new Dictionary<BattleUnit, (int, int)>();
        public event Action<BattleUnit, BattleUnit, GumBuff, float> ColoredGumLaunched;

        public bool IsStickyGumAttack(BattleUnit unit)
        {
            var actor = Clock(unit);
            return actor != null && Id(actor) == "tsu" && actor.Attacks > 0 && actor.Attacks % N(actor, "tsu-root", "every", 4) == 0;
        }
        public IEnumerable<BattleUnit> GumRootedUnits => actors.Where(a => a.Unit != null && a.Unit.IsAlive &&
            a.RootUntil > tick && a.Unit.CurrentCell != null && gumRootVersions.TryGetValue(a.Unit, out var gum) &&
            gum.Until > tick && gum.Version == a.Unit.CombatResetVersion).Select(a => a.Unit);

        void MarkGumRoot(ActorClock target, int until)
        {
            if (gumRootVersions.TryGetValue(target.Unit, out var previous) && previous.Version == target.Unit.CombatResetVersion)
                until = Mathf.Max(until, previous.Until);
            gumRootVersions[target.Unit] = (target.Unit.CombatResetVersion, until);
        }
        void ClearTsuGum() { gumGifts.Clear(); gumRootVersions.Clear(); }
        public static float ColoredGumSpeed(BattleUnit actor)
        {
            float edited = actor.BaseStats.AbilityValue("tsu-colored-gum", "projectileSpeed", 0);
            return Mathf.Max(.1f, edited > 0 ? edited : actor.BaseStats.ProjectileSpeed * .5f);
        }
        void ColoredGum(ActorClock actor)
        {
            var ally = actors.Where(a => a != actor && a.Unit != null && a.Unit.IsAlive && a.Unit.CurrentCell != null && a.Unit.Side == actor.Unit.Side)
                .OrderBy(a => Distance(actor.Unit.CurrentCell, a.Unit.CurrentCell))
                .ThenBy(a => a.Unit.UnitId, StringComparer.Ordinal).FirstOrDefault() ?? actor;
            gumGifts.Add(new GumGift
            {
                Source = actor, Target = ally, Release = tick + AbilityTiming.Ticks(GetAttackWindup(actor.Unit)),
                AttackVersion = actor.Unit.AttackResetVersion, SourceVersion = actor.Unit.CombatResetVersion,
                TargetVersion = ally.Unit.CombatResetVersion,
                Bonus = V(actor, "tsu-colored-gum", "bonusPercent", 50) / 100f,
                DamageReduction = V(actor, "tsu-colored-gum", "shieldPercent", 30) / 100f
            });
        }
        void TickGumGifts()
        {
            foreach (var gift in gumGifts.ToArray())
            {
                var source = gift.Source.Unit; var target = gift.Target.Unit;
                if (target == null || !target.IsAlive || target.CombatResetVersion != gift.TargetVersion || target.CurrentCell == null)
                { gumGifts.Remove(gift); continue; }
                if (!gift.Released)
                {
                    if (source == null || !source.IsAlive || source.CombatResetVersion != gift.SourceVersion ||
                        source.AttackResetVersion != gift.AttackVersion || source.CurrentCell == null)
                    { gumGifts.Remove(gift); continue; }
                    if (tick < gift.Release) continue;
                    int distance = Mathf.Max(1, Distance(source.CurrentCell, target.CurrentCell));
                    int travel = Mathf.Max(1, AbilityTiming.Ticks(distance / ColoredGumSpeed(source)));
                    gift.Due = tick + travel; gift.Released = true;
                    gift.Buff = (GumBuff)(gift.Source.GumCycle++ % 3);
                    ColoredGumLaunched?.Invoke(source, target, gift.Buff, travel * TickDuration);
                }
                if (tick < gift.Due) continue;
                switch (gift.Buff)
                {
                    case GumBuff.Speed:
                        ApplyAttackSpeed(gift.Target, gift.Bonus, int.MaxValue, "haste");
                        gift.Target.ExternalMoveBonus = Mathf.Max(gift.Target.ExternalMoveBonus, gift.Bonus);
                        break;
                    case GumBuff.Shield: ApplyReduction(gift.Target, gift.DamageReduction, int.MaxValue, "guard"); break;
                    case GumBuff.Damage: ApplyDamageBoost(gift.Target, 0, 1 + gift.Bonus, int.MaxValue); break;
                }
                gumGifts.Remove(gift);
            }
        }
    }
}
