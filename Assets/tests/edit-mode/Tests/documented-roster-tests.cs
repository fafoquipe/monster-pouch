using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests
{
    public sealed class DocumentedRosterTests
    {
        [Test]
        public void DocumentedCatalogMakesEverySourceCharacterSelectableAndKeepsSummonOutOfDeck()
        {
            MatchConfig config = MatchConfig.CreateDocumented();
            try
            {
                Assert.That(config.Units.Count(unit => unit.IsMonster), Is.EqualTo(5));
                Assert.That(config.Units.Count(unit => !unit.IsMonster), Is.EqualTo(14));
                Assert.That(config.Get("dummy"), Is.Null, "Dummy belongs to Kayon's summon pool.");
                Assert.That(config.Get("bugui"), Is.Not.Null, "PDF name takes priority over mockup label Bugmy.");
                foreach (UnitDefinition unit in config.Units.Where(unit => !unit.IsMonster))
                {
                    Assert.That(config.TryResolveWhelpSelection(new[] { unit.Id }, out string[] ids, out string reason),
                        Is.True, reason);
                    Assert.That(ids, Is.EqualTo(new[] { unit.Id }));
                }
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void DocumentedDefinitionsHaveTraceableUniqueBehaviorIdsAndThreeUpgrades()
        {
            var ids = new HashSet<string>();
            var effects = new HashSet<string>();
            foreach (UnitDefinition definition in DocumentedRoster.CreateAll())
            {
                Assert.That(ids.Add(definition.Id), Is.True, definition.Id);
                Assert.That(definition.UsesDocumentedRules, Is.True);
                Assert.That(definition.SourceDocument, Is.EqualTo(definition.IsMonster ? "Monsters.pdf" : "whelps.pdf"));
                Assert.That(definition.SourcePages, Is.Not.Empty);
                Assert.That(definition.HasProvisionalBalance, Is.True, "PDF omits base balance numbers.");
                Assert.That(definition.Tricks.Length, Is.EqualTo(3));
                foreach (TrickDefinition effect in new[] { definition.BaseAbility }.Concat(definition.Tricks))
                {
                    Assert.That(effect, Is.Not.Null);
                    Assert.That(effects.Add(effect.EffectId), Is.True, effect.Id);
                    Assert.That(effect.Name, Is.Not.Empty);
                    Assert.That(effect.Description, Is.Not.Empty);
                }
            }
        }

        [Test]
        public void StatSnapshotIncludesOnlySelectedEffectsAndDoesNotFollowLaterRosterMutation()
        {
            UnitDefinition definition = DocumentedRoster.CreateStein();
            var owned = new OwnedUnit(definition);
            owned.Tricks[0] = true;
            UnitStats snapshot = UnitStats.FromDefinition(definition, owned);
            Assert.That(snapshot.DefinitionId, Is.EqualTo("stein"));
            Assert.That(snapshot.UsesDocumentedRules, Is.True);
            Assert.That(snapshot.HasEffect("stein-stun"), Is.True);
            Assert.That(snapshot.HasEffect("stein-long-stun"), Is.True);
            Assert.That(snapshot.HasEffect("stein-triple"), Is.False);
            owned.Tricks[2] = true;
            definition.Tricks[0].EffectId = "changed";
            Assert.That(snapshot.HasEffect("stein-long-stun"), Is.True);
            Assert.That(snapshot.HasEffect("stein-triple"), Is.False);
            Assert.That(snapshot.HasEffect(null), Is.False);
        }

        [Test]
        public void MonsterSnapshotIgnoresAdditionalTricksFromInvalidLoadout()
        {
            UnitDefinition definition = DocumentedRoster.CreateBugaloo();
            var owned = new OwnedUnit(definition);
            owned.Tricks[0] = true;
            owned.Tricks[2] = true;
            UnitStats stats = UnitStats.FromDefinition(definition, owned);
            Assert.That(stats.HasEffect("bugaloo-reflect"), Is.True);
            Assert.That(stats.HasEffect("bugaloo-protection"), Is.True);
            Assert.That(stats.HasEffect("bugaloo-healing"), Is.False);
            Assert.That(stats.HasEffect("bugaloo-revenge"), Is.False);
            Assert.That(stats.EnergyMax, Is.EqualTo(100));
            Assert.That(stats.EnergyPerAttack, Is.EqualTo(25));
        }

        [Test]
        public void PermanentKillPowerIsCopiedIntoStatsWithoutChangingSharedDefinition()
        {
            UnitDefinition definition = DocumentedRoster.CreateSepora();
            var owned = new OwnedUnit(definition);
            typeof(OwnedUnit).GetProperty("PersistentDamageBonus").SetValue(owned, 3);
            UnitStats first = UnitStats.FromDefinition(definition, owned);
            UnitStats independent = UnitStats.FromDefinition(definition, new OwnedUnit(definition));
            Assert.That(first.Attack, Is.EqualTo(definition.Damage + 3));
            Assert.That(independent.Attack, Is.EqualTo(definition.Damage));
            Assert.That(definition.Damage, Is.EqualTo(6));
        }

        [Test]
        public void BlotanHealthBonusIsAppliedOnceToSnapshot()
        {
            UnitDefinition definition = DocumentedRoster.CreateBlotan();
            var owned = new OwnedUnit(definition);
            owned.Tricks[1] = true;
            Assert.That(UnitStats.FromDefinition(definition, owned).MaxHealth, Is.EqualTo(definition.MaxHealth + 20));
            Assert.That(UnitStats.FromDefinition(definition, owned).MaxHealth, Is.EqualTo(definition.MaxHealth + 20));
        }

        [Test]
        public void DocumentedTaurisAndKayonDoNotAlsoRunLegacyPeriodicSpecials()
        {
            UnitStats tauris = UnitStats.FromDefinition(DocumentedRoster.CreateTauris());
            UnitStats kayon = UnitStats.FromDefinition(DocumentedRoster.CreateKayon());
            Assert.That(tauris.LethalEveryHits, Is.Zero);
            Assert.That(kayon.SummonInterval, Is.Zero);
            Assert.That(tauris.EnergyMax, Is.GreaterThan(0));
            Assert.That(kayon.EnergyMax, Is.GreaterThan(0));
            Assert.That(tauris.AttackRange, Is.EqualTo(1));
            Assert.That(kayon.AttackRange, Is.EqualTo(4));
        }

        [Test]
        public void ExplicitEveryAttackAbilitiesAreNotChangedIntoEnergyTimers()
        {
            UnitDefinition tsu = DocumentedRoster.CreateTsu();
            UnitDefinition trimol = DocumentedRoster.CreateTrimol();
            Assert.That(tsu.BaseAbility.Trigger, Is.EqualTo(AbilityTrigger.EveryAttacks));
            Assert.That(tsu.BaseAbility.EveryAttacks, Is.EqualTo(4));
            Assert.That(trimol.BaseAbility.EveryAttacks, Is.EqualTo(4));
            Assert.That(tsu.EnergyMax, Is.Zero);
        }

        [Test]
        public void BuguiUnlimitedRangeReachesOppositeBoardCornerOnlyAfterUpgrade()
        {
            UnitDefinition definition = DocumentedRoster.CreateBugui();
            var owned = new OwnedUnit(definition);
            Assert.That(UnitStats.FromDefinition(definition, owned).AttackRange, Is.EqualTo(3));
            owned.Tricks[0] = true;
            Assert.That(UnitStats.FromDefinition(definition, owned).AttackRange, Is.GreaterThanOrEqualTo(14));
        }

        [Test]
        public void LegacyDefaultsStayOnLegacyBehaviorAndFactoriesAreDetached()
        {
            MatchConfig legacy = MatchConfig.CreateDefault();
            try
            {
                Assert.That(legacy.Units.All(definition => !definition.UsesDocumentedRules), Is.True);
                UnitDefinition first = DocumentedRoster.CreateKayon();
                UnitDefinition second = DocumentedRoster.CreateKayon();
                first.Tricks[0].Cost = 999;
                Assert.That(second.Tricks[0].Cost, Is.EqualTo(3));
                Assert.That(UnitStats.FromDefinition(legacy.Get("bugaloo")).HasEffect("bugaloo-reflect"), Is.False);
            }
            finally { Object.DestroyImmediate(legacy); }
        }
    }
}
