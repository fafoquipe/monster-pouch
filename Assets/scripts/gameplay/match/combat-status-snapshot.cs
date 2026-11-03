using System.Collections.Generic;
using MonsterPouch.Gameplay.Status;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    public sealed partial class CombatSimulation
    {
        StatusRules statusRules = new StatusRules(System.Array.Empty<StatusEffectDefinition>());
        // Read-only projection: all expiry, intensity and counters belong to combat.
        public void CollectStatuses(BattleUnit unit, List<ActiveStatus> output)
        {
            output.Clear();
            if (!begun || Finished || unit == null || !unit.IsAlive || unit.IsReviving) return;
            var actor = Clock(unit); if (actor == null) return;
            float Remaining(int until) => Mathf.Max(0, (until - tick) * TickDuration);
            void Add(string id, float remaining = -1, float intensity = 1) => output.Add(new ActiveStatus(id, remaining, intensity));
            if (unit.IsStunned) Add(unit.IsElectrocuted ? "electric-stun" : "stun", Remaining(actor.StunUntil));
            if (unit.IsRooted) Add("root", Remaining(actor.RootUntil));
            if (unit.IsSlowed) Add("slow", -1, 1 - actor.AttackSpeedMultiplier);
            if (actor.ExternalSpeedUntil > tick) Add(actor.ExternalSpeedId,
                actor.ExternalSpeedUntil == int.MaxValue ? -1 : Remaining(actor.ExternalSpeedUntil), actor.ExternalSpeedBonus);
            if (actor.SpeedBonus > 0) Add("haste", -1, actor.SpeedBonus);
            if (Id(actor) == "bugui" && actor.Attacks > 0)
                Add("rhythm", -1, Has(actor, "bugui-unlimited-stacks") ? actor.Attacks : Mathf.Min(actor.Attacks, N(actor, "bugui-rhythm", "stacks", 10)));
            if (Id(actor) == "sepora" && actor.SuperUntil > tick) Add("storm", Remaining(actor.SuperUntil));
            if (actor.DamageMultiplier > 1 || actor.DamageBonus > 0) Add("attack-up");
            else if (actor.ExternalDamageUntil > tick) Add("attack-up",
                actor.ExternalDamageUntil == int.MaxValue ? -1 : Remaining(actor.ExternalDamageUntil),
                actor.ExternalDamageBonus > 0 ? actor.ExternalDamageBonus : actor.ExternalDamageMultiplier - 1);
            if (actor.NextBasicBonus > 0) Add("empowered");
            if (ExternalReduction(actor) > 0) Add(actor.DamageReductionId,
                actor.DamageReductionUntil == int.MaxValue ? -1 : Remaining(actor.DamageReductionUntil), actor.DamageReduction);
            int armorUntil = ToTicks(V(actor, "atori-hard-head", "duration", 5));
            if (Has(actor, "atori-hard-head") && tick < armorUntil) Add("opening-guard", Remaining(armorUntil));
            if (Immune(actor)) Add("control-immunity", Remaining(ControlImmunityUntil(actor)));
            if (unit.Shield > 0) Add("shield", unit.ShieldExpiresAt == int.MaxValue ? -1 : Remaining(unit.ShieldExpiresAt), unit.Shield);
            if (actor.WeakUntil > tick) Add("weakness", Remaining(actor.WeakUntil), actor.Weakness);
            if (actor.AntiHealUntil > tick) Add("anti-heal", Remaining(actor.AntiHealUntil), actor.AntiHealMultiplier);
            if (unit.IsInvisible) Add("stealth", Remaining(actor.StealthUntil));
            if (unit.IsSleeping) Add("sleep", Remaining(actor.OpeningRecoveryUntil));
            if (unit.IsProtected) Add("protection", Remaining(actor.SuperUntil));
            if (unit.IsReflecting) Add("reflect", Remaining(actor.SuperUntil));
            if (unit.IsTaunted) Add("taunt", Remaining(actor.TauntUntil));
            if (actor.InvulnerableUntil > tick || unit.IsFlying)
                Add("invulnerable", Mathf.Max(Remaining(actor.InvulnerableUntil), unit.FlightRemaining));
            if (unit.IsBeingPulled) Add("pull", unit.PullRemaining);
            if (actor.ChocolateSource != null && actor.RootUntil > tick) Add("chocolate", Remaining(actor.RootUntil));
            // Gochan explicitly stacks marks; every mark still expires independently.
            float burnRemaining = 0;
            foreach (var burn in burns)
                if (burn.Target == unit) burnRemaining = Mathf.Max(burnRemaining, Remaining(burn.Next + (burn.Remaining - 1) * 10));
            if (burnRemaining > 0) Add("burn", burnRemaining);
        }
    }
}
