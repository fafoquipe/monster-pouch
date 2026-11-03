using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Local;
using NUnit.Framework;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class EronyaUpgradeDescriptionTests
    {
        static UnitDefinition Unit(string id) => EronyaRoster.CreateAll().Single(u => u.Id == id);
        static TrickDefinition Effect(UnitDefinition u, string id) => new[] {u.BaseAbility,u.PassiveAbility}.Concat(u.Tricks).Single(t => t?.EffectId == id);
        static void Set(TrickDefinition t, string key, float value)
        {
            t.Parameters=AbilityBalance.Resolve(t).Select(p=>new AbilityParameter {Key=p.Key,Value=p.Key==key?value:p.Value}).ToArray();
        }

        [TestCase(true)] [TestCase(false)]
        public void EditedDamageUsesCurrentBaseAndKeepsZeroAndReductions(bool spanish)
        {
            var u=Unit("canonero-del-orvek");var t=u.Tricks[0];
            Set(u.BaseAbility,"damage",20);Set(t,"damage",25);t.Description="STALE 999";
            Assert.That(AbilitySummaryDescriptions.Describe(t,u,spanish),Does.Contain("+5").And.Not.Contain("25").And.Not.Contain("STALE"));
            Assert.That(AbilitySummaryDescriptions.Describe(u.BaseAbility,u,spanish),Does.Contain("20").And.Not.Contain("+20"));
            Set(u.BaseAbility,"damage",25);
            Assert.That(AbilitySummaryDescriptions.Describe(t,u,spanish),Does.Contain(" 0.").And.Not.Contain("+0"));
            Set(t,"damage",20);
            Assert.That(AbilitySummaryDescriptions.Describe(t,u,spanish),Does.Contain("−5"));
        }

        [Test]
        public void PercentagesArePointsAndRespectTheLiveReductionCap()
        {
            Assert.AreEqual("+10",AbilityBalance.DescriptionDifference(.5f,.6f,true));
            var u=Unit("alveyr");var t=u.Tricks[1];
            Set(u.BaseAbility,"reduction",.4f);Set(t,"reduction",.5f);
            Assert.That(AbilitySummaryDescriptions.Describe(t,u,true),Does.Contain("+10 %"));
            Set(u.BaseAbility,"reduction",.5f);Set(t,"reduction",.6f);
            Assert.That(AbilitySummaryDescriptions.Describe(t,u,true),Does.Contain(" 0 %"));
        }

        [TestCase(true)] [TestCase(false)]
        public void CompatibleTrickHasItsOwnComparatorAndSpeedIsABonusNotAnInterval(bool spanish)
        {
            var u=Unit("alboguera");var duration=u.Tricks[2];
            Set(u.BaseAbility,"duration",5);Set(u.Tricks[0],"duration",7);Set(duration,"duration",6);
            string text=AbilitySummaryDescriptions.Describe(duration,u,spanish);
            Assert.That(text,Does.Contain("+1 s").And.Contain("−1 s"));
            Set(u.BaseAbility,"attackSpeed",.25f);
            Set(u.Tricks[1],"primaryAttackSpeed",.3f);Set(u.Tricks[1],"secondaryAttackSpeed",.2f);
            text=AbilitySummaryDescriptions.Describe(u.Tricks[1],u,spanish);
            Assert.That(text,Does.Contain("+5 %").And.Contain("−5 %"));
            u.AttackInterval=20;
            Assert.AreEqual(text,AbilitySummaryDescriptions.Describe(u.Tricks[1],u,spanish));
        }

        [Test]
        public void PassiveReferencesAndIndependentEffectsDoNotBecomeFalseDeltas()
        {
            var u=Unit("jabalinero");Set(u.PassiveAbility,"damagePerSecond",5);Set(u.Tricks[0],"damagePerSecond",3);
            Assert.That(AbilitySummaryDescriptions.Describe(u.Tricks[0],u,true),Does.Contain("−2/s"));
            var dagata=Unit("dagata");Set(dagata.BaseAbility,"damagePerHit",40);Set(dagata.Tricks[2],"bonusDamage",9);
            Assert.That(AbilitySummaryDescriptions.Describe(dagata.Tricks[2],dagata,true),Does.Contain("9").And.Not.Contain("−31"));
            var golem=Unit("golem-tectonico");golem.MaxHealth=50;
            Set(golem.BaseAbility,"shield",22);Set(golem.Tricks[0],"shield",30);
            Assert.That(AbilitySummaryDescriptions.Describe(golem.Tricks[0],golem,true),Does.Contain("escudo 0."));
        }
    }
}
