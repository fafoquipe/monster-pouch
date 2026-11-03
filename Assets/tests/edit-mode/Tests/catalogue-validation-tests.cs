using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class CatalogueValidationTests
    {
        readonly List<Object> owned = new List<Object>();
        T New<T>() where T : ScriptableObject
        {
            var value = ScriptableObject.CreateInstance<T>();
            owned.Add(value);
            return value;
        }
        static string[] Validate(MatchConfig config)
        {
            var type = Type.GetType("MonsterPouchCatalogueValidation, Assembly-CSharp-Editor");
            Assert.NotNull(type, "The validator must remain an Editor-only tool.");
            return (string[])type.GetMethod("Validate").Invoke(null, new object[] { config });
        }
        MatchConfig Config(params CharacterDefinition[] characters)
        {
            var config = New<MatchConfig>();
            config.Characters = characters;
            return config;
        }
        CharacterDefinition Character(string id = "anuik", string name = "Aniku")
        {
            var definition = DocumentedRoster.CreateAll().Single(u => u.Id == id);
            var character = New<CharacterDefinition>();
            character.InitializeIdentity(id);
            character.General = definition;
            character.General.DisplayName = name;
            character.Attack = New<CharacterAttack>();
            character.Attack.Import(definition);
            character.Ability = New<CharacterAbility>();
            character.Ability.Definition = definition.BaseAbility;
            character.Upgrades = definition.Tricks.Select(trick =>
            {
                var ability = New<CharacterAbility>();
                ability.Definition = trick;
                return ability;
            }).ToArray();
            return character;
        }
        [TearDown] public void Cleanup()
        {
            foreach (var item in owned) if (item != null) Object.DestroyImmediate(item);
            owned.Clear();
        }

        [Test] public void CurrentCataloguePassesWithoutChangingAssetsNamesOrIdentity()
        {
            var config = Resources.Load<MatchConfig>("MonsterPouch/MatchConfig");
            Assert.NotNull(config);
            var assets = new List<Object> { config };
            foreach (var character in config.Characters)
            {
                assets.Add(character);
                assets.Add(character.Attack);
                assets.Add(character.Ability);
                assets.AddRange(character.Upgrades);
            }
            var before = assets.Select(asset => EditorJsonUtility.ToJson(asset)).ToArray();
            var dirty = assets.Select(asset => EditorUtility.IsDirty(asset)).ToArray();
            CollectionAssert.IsEmpty(Validate(config));
            CollectionAssert.AreEqual(before, assets.Select(asset => EditorJsonUtility.ToJson(asset)).ToArray(),
                "Validation must never repair, rename or normalize the shared assets.");
            CollectionAssert.AreEqual(dirty, assets.Select(asset => EditorUtility.IsDirty(asset)).ToArray());
        }

        [Test] public void DuplicateStableIdsReportBothCurrentNamesWithoutRenamingEither()
        {
            var first = Character(name: "Aniku actual");
            var second = Character(name: "Nombre editado");
            var problems = Validate(Config(first, second));
            Assert.That(problems.Any(p => p.Contains("ID repetido") && p.Contains("Aniku actual") && p.Contains("Nombre editado")));
            Assert.AreEqual("anuik", first.Id);
            Assert.AreEqual("anuik", second.Id);
            Assert.AreEqual("Nombre editado", second.General.DisplayName);
        }

        [Test] public void MissingCharacterAttackAndUpgradeReferencesAreReportedTogether()
        {
            var character = Character(name: "Aniku");
            Object.DestroyImmediate(character.Attack);
            character.Upgrades[1] = null;
            var problems = Validate(Config(character, null));
            Assert.That(problems.Any(p => p.Contains("Aniku") && p.Contains("Ataque inválido")));
            Assert.That(problems.Any(p => p.Contains("Aniku") && p.Contains("mejora 2") && p.Contains("falta")));
            Assert.That(problems.Any(p => p.Contains("Characters[1]") && p.Contains("referencia")));
        }

        [Test] public void ForeignAndSwappedAbilitiesUseTheExistingCombatOwnershipRules()
        {
            var character = Character();
            var other = Character("popow", "Pombú");
            character.Ability = other.Ability;
            var first = character.Upgrades[0];
            character.Upgrades[0] = character.Upgrades[1];
            character.Upgrades[1] = first;
            var before = EditorJsonUtility.ToJson(character);
            var problems = Validate(Config(character));
            Assert.That(problems.Any(p => p.Contains("principal") && p.Contains("referencia ajena")));
            Assert.That(problems.Any(p => p.Contains("mejora 1") && p.Contains("referencia ajena")));
            Assert.That(problems.Any(p => p.Contains("mejora 2") && p.Contains("referencia ajena")));
            Assert.AreSame(other.Ability, character.Ability);
            Assert.AreEqual(before, EditorJsonUtility.ToJson(character));
        }

        [Test] public void MissingAbilityDefinitionAndGeneralMetadataDoNotCrashTheReport()
        {
            var character = Character();
            character.General = null;
            character.Ability.Definition = null;
            var problems = Validate(Config(character));
            Assert.That(problems.Any(p => p.Contains("nombre visible")));
            Assert.That(problems.Any(p => p.Contains("principal") && p.Contains("definición")));
        }

        [Test] public void DuplicateAbilityIdsAndInvalidParameterEntriesCannotHideBehindTheFirstValue()
        {
            var character = Character();
            character.Upgrades[2].Definition.Id = character.Upgrades[0].Definition.Id;
            character.Upgrades[0].Definition.Parameters = new[] {
                new AbilityParameter { Key = "healthPercent", Value = 25 },
                new AbilityParameter { Key = "healthPercent", Value = float.NaN },
                null
            };
            var problems = Validate(Config(character));
            Assert.That(problems.Any(p => p.Contains("mejora 3") && p.Contains("ID repetido")));
            Assert.That(problems.Any(p => p.Contains("parámetro repetido")));
            Assert.That(problems.Any(p => p.Contains("valor no finito")));
            Assert.That(problems.Any(p => p.Contains("parámetro vacío")));
        }

        [Test] public void MissingConfigAndEmptyCatalogueAreActionableDiagnostics()
        {
            Assert.That(Validate(null).Single(), Does.Contain("Falta MatchConfig"));
            var config = Config();
            config.Units = Array.Empty<UnitDefinition>();
            Assert.That(Validate(config).Single(), Does.Contain("catálogo está vacío"));
        }

        [Test] public void LegacyConfigIsValidatedWithoutReplacingItsExistingRules()
        {
            var config = New<MatchConfig>();
            string before = EditorJsonUtility.ToJson(config);
            CollectionAssert.IsEmpty(Validate(config));
            Assert.AreEqual(before, EditorJsonUtility.ToJson(config));
            Assert.IsEmpty(config.Characters);
        }

        [Test] public void PartialLegacyUpgradeWithRealDataStillRequiresItsIdentity()
        {
            var config = New<MatchConfig>();
            config.Units[0].Tricks[0] = new TrickDefinition { DamageBonus = 2 };
            var problems = Validate(config);
            Assert.That(problems.Any(p => p.Contains("mejora 1") && p.Contains("falta ID estable")));
            Assert.AreEqual(2, config.Units[0].Tricks[0].DamageBonus);
        }
    }
}
