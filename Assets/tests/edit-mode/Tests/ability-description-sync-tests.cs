using MonsterPouch.Gameplay.Match;
using NUnit.Framework;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public class AbilityDescriptionSyncTests
    {
        [TestCase("stein",2,true)] [TestCase("stein",2,false)]
        [TestCase("hymay",0,true)] [TestCase("kayon",-1,false)]
        [TestCase("tauris",2,false)] [TestCase("pokrak",-1,true)]
        [TestCase("suki",-1,false)] [TestCase("rosie",-1,true)]
        public void DescriptionsFollowTheCurrentDisplayNameWithoutRenamingTheIdentity(string id,int upgrade,bool spanish)
        {
            var source=UnityEngine.Resources.Load<MonsterPouch.Gameplay.Characters.CharacterDefinition>(
                "MonsterPouch/Characters/"+id+"/Character");
            Assert.IsNotNull(source);
            var unit=source.CreateRuntimeDefinition();
            var ability=upgrade<0?unit.BaseAbility:unit.Tricks[upgrade];
            string stored=ability.Description;
            Assert.That(MonsterPouch.Local.GameText.AbilityDescription(ability,unit,spanish),Does.Contain(source.General.DisplayName));
            unit.DisplayName="Nombre QA 123";
            Assert.That(MonsterPouch.Local.GameText.AbilityDescription(ability,unit,spanish),Does.Contain(unit.DisplayName));
            Assert.AreEqual(id,unit.Id);Assert.AreEqual(id,source.Id);
            Assert.AreEqual(stored,ability.Description);
        }

        [Test] public void CurrentAnikuNamePreservesAnuikIdentity()
        {
            var source=UnityEngine.Resources.Load<MonsterPouch.Gameplay.Characters.CharacterDefinition>(
                "MonsterPouch/Characters/anuik/Character");
            Assert.IsNotNull(source);
            var unit=source.CreateRuntimeDefinition();
            Assert.AreEqual("anuik",unit.Id);Assert.AreEqual("Aniku",unit.DisplayName);
        }

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
            Assert.That(text,Does.Contain("7.5 s").And.Contain("4 objetivos").And.Contain("+80%"));
            var blotan=DocumentedRoster.CreateBlotan();
            blotan.Tricks[1].HealthBonus=33;
            Assert.That(AbilityBalance.Description(blotan.Tricks[1],blotan),Does.Contain("0.5 s").And.Contain("teletransporta").And.Not.Contain("33"));
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

        [Test]
        public void AttackScaledDamageKeepsTheFormulaWhenBasicDamageChanges()
        {
            foreach(var unit in new[]{DocumentedRoster.CreateTrimol(),DocumentedRoster.CreateAtong()})
            {
                string description=AbilityBalance.Description(unit.BaseAbility,unit);
                Assert.That(description,Does.Contain("×").And.Contain("daño de ataque"));
                unit.Damage=137;
                Assert.AreEqual(description,AbilityBalance.Description(unit.BaseAbility,unit));
            }
        }

        [TestCase(27)] [TestCase(0)]
        public void FixedDamageIsExplicitAndDoesNotAdvertiseAnUnusedMultiplier(int damage)
        {
            var unit=DocumentedRoster.CreateTokoro();
            unit.BaseAbility.Parameters=new[]{new AbilityParameter{Key="damageOverride",Value=damage}};
            string text=AbilityBalance.Description(unit.BaseAbility,unit);
            Assert.That(text,Does.Contain(damage+" de daño fijo").And.Not.Contain("×2"));
        }

        [Test]
        public void UpgradeDeltaTracksTheEditedBaseAndBothCanBeEditedWithoutChangingStoredText()
        {
            var unit=DocumentedRoster.CreateFlo();var upgrade=unit.Tricks[0];
            string original=upgrade.Description;
            unit.BaseAbility.Parameters=new[]{new AbilityParameter{Key="heal",Value=25}};
            upgrade.Parameters=new[]{new AbilityParameter{Key="heal",Value=40}};
            Assert.That(AbilityBalance.Description(upgrade,unit),Does.Contain("+15"));
            float display=AbilityBalance.ParameterEditorValue(upgrade.EffectId,"heal",40,unit);
            Assert.AreEqual(15,display);
            upgrade.Parameters[0].Value=AbilityBalance.ParameterStoredValue(upgrade.EffectId,"heal",7,unit);
            Assert.AreEqual(32,upgrade.Parameters[0].Value);
            Assert.That(AbilityBalance.Description(upgrade,unit),Does.Contain("+7"));
            unit.BaseAbility.Parameters[0].Value=35;
            Assert.That(AbilityBalance.Description(upgrade,unit),Does.Contain("-3"));
            Assert.AreEqual(original,upgrade.Description);
        }

        [Test]
        public void EditorConversionsRoundTripEveryRosterParameterWithoutRebalancing()
        {
            foreach(var unit in DocumentedRoster.CreateAll())
            foreach(var ability in new[]{unit.BaseAbility,unit.Tricks[0],unit.Tricks[1],unit.Tricks[2]})
            foreach(var parameter in AbilityBalance.Resolve(ability))
            {
                float displayed=AbilityBalance.ParameterEditorValue(ability.EffectId,parameter.Key,parameter.Value,unit);
                Assert.That(AbilityBalance.ParameterStoredValue(ability.EffectId,parameter.Key,displayed,unit),
                    Is.EqualTo(parameter.Value).Within(.0001f),ability.Id+":"+parameter.Key);
            }
            Assert.AreEqual(2,AbilityBalance.ParameterEditorValue("atong-critical-damage","damagePercent",200,null));
            Assert.AreEqual(250,AbilityBalance.ParameterStoredValue("atong-critical-damage","damagePercent",2.5f,null));
            Assert.AreEqual(-1,AbilityBalance.ParameterStoredValue("tokoro-headbutt","damageOverride",-1,null));
        }

        [Test]
        public void AdditionalStunDoesNotRepeatTheObsoleteTotal()
        {
            var unit=DocumentedRoster.CreateStein();
            unit.Tricks[0].Parameters=new[]{new AbilityParameter{Key="extraDuration",Value=2.75f},new AbilityParameter{Key="duration",Value=999}};
            string text=AbilityBalance.Description(unit.Tricks[0],unit);
            Assert.That(text,Does.Contain("+2.75 s").And.Not.Contain("999"));
        }

        [Test]
        public void InspectorPreviewUsesPendingBaseEditsWithoutSavingTheAsset()
        {
            var config=UnityEngine.ScriptableObject.CreateInstance<MatchConfig>();
            try
            {
                var flo=DocumentedRoster.CreateFlo();config.Units=new[]{flo};
                flo.BaseAbility.Parameters=new[]{new AbilityParameter{Key="heal",Value=20}};
                using(var serialized=new UnityEditor.SerializedObject(config))
                {
                    var unit=serialized.FindProperty("Units").GetArrayElementAtIndex(0);
                    unit.FindPropertyRelative("BaseAbility").FindPropertyRelative("Parameters").GetArrayElementAtIndex(0).FindPropertyRelative("Value").floatValue=30;
                    var inspector=System.Type.GetType("MonsterPouchBalanceInspector, Assembly-CSharp-Editor");
                    Assert.IsNotNull(inspector);
                    var upgrade=unit.FindPropertyRelative("Tricks").GetArrayElementAtIndex(0);
                    string preview=(string)inspector.GetMethod("DescriptionPreview").Invoke(null,new object[]{upgrade,unit});
                    Assert.That(preview,Does.Contain("+10"));
                    Assert.AreEqual(20,flo.BaseAbility.Parameters[0].Value);
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(config);}
        }
    }
}
