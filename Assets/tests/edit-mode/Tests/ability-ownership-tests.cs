using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class AbilityOwnershipTests
    {
        [Test]
        public void EveryDocumentedCharacter_RejectsEveryOtherCharactersAbilities()
        {
            var roster = DocumentedRoster.CreateAll();
            Assert.AreEqual(19, roster.Length);
            foreach (var expected in roster)
            foreach (var foreign in roster.Where(unit => unit.Id != expected.Id))
            {
                var altered = DocumentedRoster.CreateAll().Single(unit => unit.Id == expected.Id);
                altered.BaseAbility = foreign.BaseAbility;
                altered.Tricks = foreign.Tricks;
                altered.MonsterUpgrade = foreign.BaseAbility;
                altered.RevivesPerCombat = 99;
                altered.LethalEveryHits = 1;
                altered.SummonInterval = .1f;
                altered.MaxLivingSummons = 99;
                var owned = new OwnedUnit(altered);
                for (int i = 0; i < 3; i++) owned.Tricks[i] = true;
                var stats = UnitStats.FromDefinition(altered, owned);
                var effects = new[] { expected.BaseAbility.EffectId }.Concat(expected.Tricks.Select(t => t.EffectId));
                CollectionAssert.AreEquivalent(effects, stats.EffectIds, expected.Id + " <- " + foreign.Id);
                Assert.AreEqual(0, stats.LethalEveryHits);
                Assert.AreEqual(0, stats.SummonInterval);
                if (expected.Id != "anuik") Assert.AreEqual(0, stats.RevivesPerCombat);
                Assert.AreSame(foreign.BaseAbility, altered.BaseAbility, "Do not mutate the shared edited definition.");
            }
        }

        [Test]
        public void OwnStatsRemainEditable_AndBaseSlotCannotGrantAnUnboughtUpgrade()
        {
            foreach (var expected in DocumentedRoster.CreateAll())
            {
                var unit = DocumentedRoster.CreateAll().Single(d => d.Id == expected.Id);
                unit.MaxHealth = 157; unit.Damage = 23; unit.AttackRange = 6;
                unit.BaseAbility = unit.Tricks[2];
                var stats = UnitStats.FromDefinition(unit);
                Assert.AreEqual(157, stats.MaxHealth);
                Assert.AreEqual(23, stats.Attack);
                Assert.AreEqual(6, stats.AttackRange);
                CollectionAssert.AreEquivalent(new[] { expected.BaseAbility.EffectId }, stats.EffectIds);
                Assert.IsFalse(stats.HasEffect(unit.Tricks[2].EffectId));
            }
        }

        [Test]
        public void UnusedGenericEffectsCannotBeInjectedIntoAValidOwnAbility()
        {
            var unit = DocumentedRoster.CreateAnuik();
            unit.BaseAbility.HealOnHit = 50; unit.BaseAbility.Armor = 50;
            unit.BaseAbility.BonusEveryHits = 1; unit.BaseAbility.BonusDamage = 500;
            unit.BaseAbility.DamageBonus = 999;
            var stats = UnitStats.FromDefinition(unit);
            Assert.AreEqual(unit.Damage, stats.Attack);
            Assert.AreEqual(0, stats.HealOnHit); Assert.AreEqual(0, stats.Armor);
            Assert.AreEqual(0, stats.BonusEveryHits); Assert.AreEqual(0, stats.BonusDamage);
        }
    }
}
