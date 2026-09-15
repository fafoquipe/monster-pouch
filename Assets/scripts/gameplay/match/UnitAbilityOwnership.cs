using System;
using System.Collections.Generic;

namespace MonsterPouch.Gameplay.Match
{
    /// <summary>Documented abilities are assigned by character and slot, never by editable effect IDs.</summary>
    public static class UnitAbilityOwnership
    {
        static readonly Dictionary<string, UnitDefinition> Owners = BuildOwners();

        static Dictionary<string, UnitDefinition> BuildOwners()
        {
            var result = new Dictionary<string, UnitDefinition>(StringComparer.Ordinal);
            foreach (var unit in DocumentedRoster.CreateAll()) result.Add(unit.Id, unit);
            return result;
        }

        /// <summary>Builds a detached combat snapshot without changing the edited asset.</summary>
        public static UnitDefinition ForCombat(UnitDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            // Legacy fixtures retain their original rules. The shipped PDF roster always opts in.
            if (!definition.UsesDocumentedRules) return definition;
            Owners.TryGetValue(definition.Id ?? "", out var owner);
            bool hasEnergy = owner != null && owner.EnergyMax > 0;
            bool canRevive = owner != null && owner.RevivesPerCombat > 0;
            bool dummySummoner = owner != null && owner.Id == "kayon";
            var result = new UnitDefinition
            {
                Id = definition.Id, DisplayName = definition.DisplayName,
                UsesDocumentedRules = true, IsMonster = owner?.IsMonster ?? false,
                AttackKind = owner?.AttackKind ?? definition.AttackKind,
                BasicAttackName = owner?.BasicAttackName ?? definition.BasicAttackName,
                BasicAttackDescription = owner?.BasicAttackDescription ?? definition.BasicAttackDescription,
                SourceDocument = owner?.SourceDocument, SourcePages = owner?.SourcePages,
                HasProvisionalBalance = definition.HasProvisionalBalance,
                MaxHealth = definition.MaxHealth, Damage = definition.Damage,
                AttackRange = definition.AttackRange, AttackInterval = definition.AttackInterval,
                AttackWindup = definition.AttackWindup, MoveInterval = definition.MoveInterval,
                IQSpeed = definition.IQSpeed, BaseCost = definition.BaseCost,
                Formation = definition.Formation, TargetPolicy = definition.TargetPolicy,
                EnergyMax = hasEnergy ? definition.EnergyMax : 0,
                EnergyPerAttack = hasEnergy ? definition.EnergyPerAttack : 0,
                EnergyOnDamage = hasEnergy ? definition.EnergyOnDamage : 0,
                EnergyPerSecond = hasEnergy ? definition.EnergyPerSecond : 0,
                RevivesPerCombat = canRevive ? definition.RevivesPerCombat : 0,
                ReviveHealthFraction = canRevive ? definition.ReviveHealthFraction : .5f,
                ReviveDelay = canRevive ? definition.ReviveDelay : .8f,
                // The PDFs use each unit's own energy/trigger logic, not legacy auto-execute/spawn timers.
                LethalEveryHits = 0, SummonInterval = 0, MaxLivingSummons = 0,
                SummonHealth = dummySummoner ? definition.SummonHealth : 14,
                SummonDamage = dummySummoner ? definition.SummonDamage : 2,
                SummonAttackInterval = dummySummoner ? definition.SummonAttackInterval : 1.4f,
                SummonMoveInterval = dummySummoner ? definition.SummonMoveInterval : .7f,
                BaseAbility = OwnEffect(owner?.BaseAbility, definition.BaseAbility),
                Tricks = new TrickDefinition[3], MonsterUpgrade = null
            };
            for (int i = 0; i < result.Tricks.Length; i++)
                result.Tricks[i] = OwnEffect(owner?.Tricks != null && i < owner.Tricks.Length ? owner.Tricks[i] : null,
                    definition.Tricks != null && i < definition.Tricks.Length ? definition.Tricks[i] : null);
            return result;
        }

        static TrickDefinition OwnEffect(TrickDefinition canonical, TrickDefinition edited)
        {
            if (canonical == null) return null;
            // Both identifiers must match the exact slot. A prefix or another slot is insufficient.
            bool same = edited != null && edited.Id == canonical.Id && edited.EffectId == canonical.EffectId;
            var balance = same ? edited : canonical;
            return new TrickDefinition
            {
                Id = canonical.Id, EffectId = canonical.EffectId, Trigger = canonical.Trigger,
                Name = canonical.Name, Description = canonical.Description, Cost = balance.Cost,
                EveryAttacks = canonical.EveryAttacks > 0 ? balance.EveryAttacks : 0,
                HealthBonus = canonical.HealthBonus != 0 ? balance.HealthBonus : 0,
                DamageBonus = canonical.DamageBonus != 0 ? balance.DamageBonus : 0,
                RangeBonus = canonical.RangeBonus != 0 ? balance.RangeBonus : 0,
                AttackIntervalMultiplier = canonical.AttackIntervalMultiplier != 1 ? balance.AttackIntervalMultiplier : 1,
                Armor = canonical.Armor != 0 ? balance.Armor : 0,
                HealOnHit = canonical.HealOnHit != 0 ? balance.HealOnHit : 0,
                BonusEveryHits = canonical.BonusEveryHits != 0 ? balance.BonusEveryHits : 0,
                BonusDamage = canonical.BonusDamage != 0 ? balance.BonusDamage : 0
            };
        }
    }
}
