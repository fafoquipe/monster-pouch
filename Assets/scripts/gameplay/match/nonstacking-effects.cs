using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    public sealed partial class CombatSimulation
    {
        // Compare complete applications: never borrow the duration of a weaker effect.
        // Losing applications are discarded, so they cannot return after the winner expires.
        bool EffectWins(float value, int until, float current, int currentUntil) =>
            value > 0 && until > tick && (currentUntil <= tick || value > current ||
                Mathf.Approximately(value, current) && until > currentUntil);

        void ApplyAttackSpeed(ActorClock actor, float bonus, int until, string status)
        {
            if (!EffectWins(bonus, until, actor.ExternalSpeedBonus, actor.ExternalSpeedUntil)) return;
            if(!EronyaAcceptLegacyReceived(actor,EronyaStatusKind.AttackSpeed,bonus,until))return;
            actor.ExternalSpeedBonus = bonus;
            actor.ExternalSpeedUntil = until;
            actor.ExternalSpeedId = status;
            RetimeRoseAttack(actor);
        }

        float AttackSpeed(ActorClock actor, bool includeStorm = true)
        {
            // Rhythm, hunt rewards and Storm are the recipient's own abilities.
            // They can coexist with exactly one received attack-speed bonus.
            float rhythm = Id(actor) == "bugui" ? (Has(actor, "bugui-unlimited-stacks") ?
                actor.Attacks * V(actor, "bugui-unlimited-stacks", "speedPercent", 5) / 100 :
                Mathf.Min(actor.Attacks, N(actor, "bugui-rhythm", "stacks", 10)) * V(actor, "bugui-rhythm", "speedPercent", 5) / 100) : 0;
            float external = actor.ExternalSpeedUntil > tick ? actor.ExternalSpeedBonus : 0;
            external=Mathf.Max(external,(float)EronyaMagnitude(actor,EronyaStatusKind.AttackSpeed));
            float storm = includeStorm && Id(actor) == "sepora" && actor.SuperUntil > tick ?
                1 + V(actor, "sepora-storm", "speedPercent", 50) / 100 : 1;
            return Mathf.Max(.05f, (1 + actor.SpeedBonus + rhythm + external) * Mathf.Min(actor.AttackSpeedMultiplier,1f-(float)EronyaMagnitude(actor,EronyaStatusKind.Slow)) * storm);
        }

        bool ApplyReduction(ActorClock actor, float reduction, int until, string status)
        {
            reduction = Mathf.Clamp01(reduction);
            if (!EffectWins(reduction, until, actor.DamageReduction, actor.DamageReductionUntil)) return false;
            if(!EronyaAcceptLegacyReceived(actor,EronyaStatusKind.DamageReduction,reduction,until))return false;
            actor.DamageReduction = reduction;
            actor.DamageReductionUntil = until;
            actor.DamageReductionId = status;
            pokrakBubbles.RemoveAll(b => b.Recipient == actor.Unit);
            return true;
        }

        float ExternalReduction(ActorClock actor) =>
            actor.DamageReductionUntil > tick ? actor.DamageReduction : 0;

        void ApplyWeakness(ActorClock actor, float multiplier, int until)
        {
            if (!EffectWins(multiplier - 1, until, actor.Weakness - 1, actor.WeakUntil)) return;
            actor.Weakness = multiplier; actor.WeakUntil = until;
        }

        void ApplyAntiHeal(ActorClock actor, float reduction, int until)
        {
            reduction = Mathf.Clamp01(reduction);
            if (!EffectWins(reduction, until, 1 - actor.AntiHealMultiplier, actor.AntiHealUntil)) return;
            if(!EronyaAcceptLegacyReceived(actor,EronyaStatusKind.AntiHeal,reduction,until))return;
            actor.AntiHealMultiplier = 1 - reduction; actor.AntiHealUntil = until;
        }

        void ResetReceivedEffects(ActorClock actor)
        {
            actor.ExternalSpeedBonus = actor.ExternalMoveBonus = actor.DamageReduction = 0;
            actor.ExternalDamageMultiplier = 1;
            actor.ExternalDamageBonus = actor.ExternalDamageUntil = 0;
            actor.KazuruHaloUntil = actor.KazuruHealNext = actor.KazuruHealTicks = 0;
            actor.ExternalSpeedUntil = actor.DamageReductionUntil = 0;
            actor.ExternalSpeedId = actor.DamageReductionId = null;
            actor.WeakUntil = actor.AntiHealUntil = 0;
            actor.ChocolateSource = null; actor.ChocolateNext = actor.ChocolateStacks = 0;
            burns.RemoveAll(b => b.Target == actor.Unit);
            pokrakBubbles.RemoveAll(b => b.Recipient == actor.Unit);
        }
    }
}
