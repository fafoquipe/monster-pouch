using System;
using System.IO;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class CharacterSizeTests
    {
        [Test]
        public void ExistingCharactersKeepTheirAuthoredSizeWhenTheFieldIsAbsent()
        {
            var config = Resources.Load<MatchConfig>("MonsterPouch/MatchConfig");
            Assert.IsNotEmpty(config.Characters);
            foreach (var character in config.Characters)
            {
                Assert.IsNotNull(character);
                if (File.ReadAllText(AssetDatabase.GetAssetPath(character)).Contains("InGameSizePercent:")) continue;
                Assert.That(character.Visuals.GetInGameScale(UnitPresentation.ReferenceTileWidth), Is.EqualTo(1).Within(.0001f), character.Id);
            }
        }

        [TestCase(-20f, 50f)]
        [TestCase(140f, 100f)]
        [TestCase(float.NaN, 50f)]
        [TestCase(float.PositiveInfinity, 50f)]
        public void InvalidValuesCannotHideOrInvertTheCharacter(float percent, float expected)
        {
            var art = new UnitArt { InGameSizePercent = percent };
            Assert.That(art.GetInGameSizePercent(1), Is.EqualTo(expected).Within(.0001f));
            Assert.That(art.AuthoredWorldWidth * art.GetInGameScale(1), Is.InRange(.4f, 2f));
        }

        [TestCase(0f)]
        [TestCase(37.5f)]
        [TestCase(100f)]
        public void SliderEditSurvivesSaveReloadAndRuntimeCopy(float percent)
        {
            string path = "Assets/__character-size-test-" + Guid.NewGuid().ToString("N") + ".asset";
            var source = Resources.Load<CharacterDefinition>("MonsterPouch/Characters/atong/Character");
            string original = JsonUtility.ToJson(source);
            var copy = Object.Instantiate(source);
            UnitArtCatalog catalog = null, runtime = null;
            try
            {
                AssetDatabase.CreateAsset(copy, path);
                var serialized = new SerializedObject(copy);
                serialized.FindProperty("Visuals.InGameSizePercent").floatValue = percent;
                Assert.IsTrue(serialized.ApplyModifiedProperties());
                AssetDatabase.SaveAssetIfDirty(copy);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var loaded = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path);
                Assert.That(loaded.Visuals.InGameSizePercent, Is.EqualTo(percent));
                Assert.That(File.ReadAllText(path), Does.Contain("InGameSizePercent:"));
                catalog = ScriptableObject.CreateInstance<UnitArtCatalog>();
                runtime = catalog.ForCharacters(new[] { loaded });
                Assert.That(runtime.Get(source.Id).InGameSizePercent, Is.EqualTo(percent));
                Assert.AreNotSame(loaded.Visuals, runtime.Get(source.Id));
                runtime.Get(source.Id).InGameSizePercent = 19;
                Assert.That(loaded.Visuals.InGameSizePercent, Is.EqualTo(percent), "Runtime copies cannot rewrite the saved choice.");
                Assert.AreEqual(original, JsonUtility.ToJson(source), "Editing one character cannot change another asset.");
            }
            finally
            {
                if (runtime != null) Object.DestroyImmediate(runtime);
                if (catalog != null) Object.DestroyImmediate(catalog);
                AssetDatabase.DeleteAsset(path);
                if (copy != null && !EditorUtility.IsPersistent(copy)) Object.DestroyImmediate(copy);
            }
        }

        [TestCase("atong", 0f, .4f, UnitPresentation.ReferenceTileWidth)]
        [TestCase("atong", 50f, 1.2f, UnitPresentation.ReferenceTileWidth)]
        [TestCase("atong", 100f, 2f, UnitPresentation.ReferenceTileWidth)]
        [TestCase("tsu", 0f, .4f, UnitPresentation.ReferenceTileWidth)]
        [TestCase("tsu", 100f, 2f, UnitPresentation.ReferenceTileWidth)]
        [TestCase("atong", 100f, 2f, 2f)]
        public void PreparationAndCombatScaleTogetherWithoutMovingFeetOrChangingStats(string id, float percent, float expectedTiles, float tileWidth)
        {
            var character = Resources.Load<CharacterDefinition>("MonsterPouch/Characters/" + id + "/Character");
            var art = JsonUtility.FromJson<UnitArt>(JsonUtility.ToJson(character.Visuals));
            art.InGameSizePercent = -1;
            float originalScale = UnitPresentation.PresentationScale(art, tileWidth);
            var originalShadow = UnitPresentation.ContactShadowScale(art, tileWidth);
            float factor = expectedTiles * tileWidth / art.AuthoredWorldWidth;
            art.InGameSizePercent = percent;
            var actorObject = new GameObject("character-size-combat");
            var echoObject = new GameObject("character-size-preparation");
            try
            {
                var definition = character.CreateRuntimeDefinition();
                var actor = actorObject.AddComponent<MonsterUnit>();
                actor.Initialize(id, BoardSide.Blue, UnitStats.FromDefinition(definition, new OwnedUnit(definition)));
                actor.transform.position = new Vector3(2, 3, 0);
                string stats = JsonUtility.ToJson(actor.BaseStats);
                var view = actorObject.AddComponent<UnitPresentation>();
                var mapper = actorObject.AddComponent<BoardWorldMapper>();
                mapper.Configure(null, null, new Vector2(tileWidth, -1.1f), Vector2.zero);
                view.Configure(actor, mapper, art);
                var echo = echoObject.AddComponent<OpponentEchoView>();
                echo.Configure(id, new Vector2Int(2, 3), actor.transform.position, art, false, tileWidth);
                Assert.That(view.VisualRoot.localScale.x, Is.EqualTo(originalScale * factor).Within(.0001f));
                float visibleWidth = view.VisualRoot.localScale.x * art.ReferencePixelWidth / art.Portrait.pixelsPerUnit;
                Assert.That(visibleWidth / tileWidth, Is.EqualTo(expectedTiles).Within(.0001f));
                Assert.AreEqual(view.VisualRoot.localScale, echo.Renderer.transform.localScale);
                Assert.AreEqual(Vector3.zero, view.VisualRoot.localPosition);
                Assert.AreEqual(new Vector3(2, 3, 0), actor.transform.position);
                Assert.AreEqual(actor.transform.position, echo.FootPosition);
                Assert.Greater(view.Renderer.bounds.size.magnitude, .1f, "The minimum size must still be visible.");
                var shadow = actor.transform.Find("ground-shadow");
                Assert.That(shadow.localScale.x, Is.EqualTo(originalShadow.x * factor).Within(.0001f));
                Assert.That(shadow.localScale.y, Is.EqualTo(originalShadow.y * factor).Within(.0001f));
                Assert.AreEqual(shadow.localScale, echo.transform.Find("revealed-ground-shadow").localScale);
                float windup = CombatSimulation.GetAttackWindup(actor);
                view.Attack(Vector3.right * 4, definition.AttackKind == AttackKind.Ranged, windup);
                Assert.That(view.AttackContactDelay, Is.EqualTo(windup));
                Assert.AreEqual(stats, JsonUtility.ToJson(actor.BaseStats));
            }
            finally
            {
                Object.DestroyImmediate(actorObject);
                Object.DestroyImmediate(echoObject);
            }
        }
    }
}
