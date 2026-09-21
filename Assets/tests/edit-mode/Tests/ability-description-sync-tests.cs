using MonsterPouch.Gameplay.Match;
using NUnit.Framework;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public class AbilityDescriptionSyncTests
    {
        [Test]
        public void DescriptionReflectsRepeatedBalanceEditsWithoutRewritingSavedText()
        {
            var unit=DocumentedRoster.CreateAnuik();
            var ability=unit.Tricks[0];
            string saved=ability.Description;
            ability.Parameters=new[]{new AbilityParameter{Key="healthPercent",Value=37.5f}};
            Assert.That(AbilityBalance.Description(ability,unit),Does.Contain("37.5%"));
            ability.Parameters[0].Value=60;
            Assert.That(AbilityBalance.Description(ability,unit),Does.Contain("60%"));
            Assert.That(ability.Description,Is.EqualTo(saved));
        }

        [Test]
        public void AnuikDescriptionUsesCurrentRevivalStats()
        {
            var unit=DocumentedRoster.CreateAnuik();
            unit.ReviveHealthFraction=.375f;unit.RevivesPerCombat=2;
            Assert.That(AbilityBalance.Description(unit.BaseAbility,unit),Is.EqualTo("Revive 2 veces con el 37.5% de vida."));
            unit.RevivesPerCombat=0;
            Assert.That(AbilityBalance.Description(unit.BaseAbility,unit),Is.EqualTo("No revive."));
        }

        [Test]
        public void SeveralParametersAndDirectBonusesStayCurrent()
        {
            var unit=DocumentedRoster.CreateSepora();
            unit.BaseAbility.Parameters=new[]{
                new AbilityParameter{Key="duration",Value=7.5f},
                new AbilityParameter{Key="targets",Value=4},
                new AbilityParameter{Key="speedPercent",Value=80}};
            string text=AbilityBalance.Description(unit.BaseAbility,unit);
            Assert.That(text,Does.Contain("7.5 s").And.Contain("4 enemigos").And.Contain("80%"));
            var blotan=DocumentedRoster.CreateBlotan();
            blotan.Tricks[1].HealthBonus=33;
            Assert.That(AbilityBalance.Description(blotan.Tricks[1],blotan),Is.EqualTo("+33 de vida máxima."));
        }

        [Test]
        public void AllDocumentedAbilityDescriptionsResolveTheirPlaceholders()
        {
            foreach(var unit in DocumentedRoster.CreateAll())
            {
                Assert.That(AbilityBalance.Description(unit.BaseAbility,unit),Does.Not.Contain("{"),unit.Id);
                foreach(var trick in unit.Tricks)
                    Assert.That(AbilityBalance.Description(trick,unit),Does.Not.Contain("{"),trick.Id);
            }
        }
    }
}
