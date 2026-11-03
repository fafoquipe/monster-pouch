using System;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class CharacterMenuScaleTests
    {
        [Test] public void DefaultsAndMissingFieldsPreserveInheritedContextSizes()
        {
            var art = new UnitArt();
            Assert.AreEqual(1.4f, art.GetLobbyScale(1.4f));
            Assert.AreEqual(1.02f, art.GetBriefScale(1.02f));
            art.LobbySizePercent = art.BriefSizePercent = 0;
            Assert.AreEqual(1.4f, art.GetLobbyScale(1.4f));
            Assert.AreEqual(1.02f, art.GetBriefScale(1.02f));
        }
        [Test] public void ContextEditsStayIndependentAndSupportFractions()
        {
            var art = new UnitArt { LobbySizePercent = 125.5f, BriefSizePercent = 72.25f };
            Assert.That(art.GetLobbyScale(1.4f), Is.EqualTo(1.757f).Within(.00001f));
            Assert.That(art.GetBriefScale(1.02f), Is.EqualTo(.73695f).Within(.00001f));
            art.LobbySizePercent = 350;
            Assert.AreEqual(3.5f, art.GetLobbyScale());
            Assert.That(art.GetBriefScale(), Is.EqualTo(.7225f).Within(.00001f));
        }
        [Test] public void MenuChangesDoNotReadOrRewriteCombatSize()
        {
            var art = new UnitArt { WorldWidth = 1.1f, DisplayScale = 1.2f, InGameSizePercent = 57.7f };
            float combatScale = art.GetInGameScale(1.15f), authoredWidth = art.AuthoredWorldWidth;
            art.LobbySizePercent = 165; art.BriefSizePercent = 65;
            Assert.AreEqual(combatScale, art.GetInGameScale(1.15f));
            Assert.AreEqual(authoredWidth, art.AuthoredWorldWidth);
            Assert.AreEqual(57.7f, art.InGameSizePercent);
            float lobby = art.GetLobbyScale(), brief = art.GetBriefScale();
            art.InGameSizePercent = 2;
            Assert.AreEqual(lobby, art.GetLobbyScale()); Assert.AreEqual(brief, art.GetBriefScale());
        }
        [Test] public void InvalidSerializedValuesRetainTheLegacyScale()
        {
            var art = new UnitArt();
            foreach (float percent in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                art.LobbySizePercent = art.BriefSizePercent = percent;
                Assert.AreEqual(1.1f, art.GetLobbyScale(1.1f));
                Assert.AreEqual(.9f, art.GetBriefScale(.9f));
            }
        }
        [Test] public void InvalidGeometryCannotReturnANonfiniteScale()
        {
            var art = new UnitArt { LobbySizePercent = 125 };
            foreach (float inherited in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
                Assert.AreEqual(1.25f, art.GetLobbyScale(inherited));
            art.LobbySizePercent = float.MaxValue;
            Assert.AreEqual(float.MaxValue, art.GetLobbyScale(float.MaxValue));
        }
    }

    // Requires Unity. Never loads or modifies a human character asset.
    public sealed class CharacterMenuScaleSerializationTests
    {
        [Test] public void OldJsonWithoutMenuFieldsPreservesItsExistingAppearance()
        {
            var art = JsonUtility.FromJson<UnitArt>("{\"WorldWidth\":1.25,\"DisplayScale\":1.1,\"InGameSizePercent\":52}");
            Assert.AreEqual(1.4f, art.GetLobbyScale(1.4f));
            Assert.AreEqual(1.02f, art.GetBriefScale(1.02f));
            Assert.AreEqual(52f, art.InGameSizePercent);
        }
        [Test] public void SavedVisualPropertiesReachRuntimeCatalogWithoutChangingCombat()
        {
            string path = "Assets/__menu-size-fixture-" + Guid.NewGuid().ToString("N") + ".asset";
            CharacterDefinition character = null;
            UnitArtCatalog catalog = null, runtime = null;
            try
            {
                character = ScriptableObject.CreateInstance<CharacterDefinition>();
                character.InitializeIdentity("menu-size-fixture");
                character.Visuals = new UnitArt { InGameSizePercent = 57.7f };
                AssetDatabase.CreateAsset(character, path);
                var serialized = new SerializedObject(character);
                serialized.FindProperty("Visuals.LobbySizePercent").floatValue = 127.5f;
                serialized.FindProperty("Visuals.BriefSizePercent").floatValue = 73.25f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(character);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var loaded = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path);
                Assert.AreEqual(127.5f, loaded.Visuals.LobbySizePercent);
                Assert.AreEqual(73.25f, loaded.Visuals.BriefSizePercent);
                Assert.AreEqual(57.7f, loaded.Visuals.InGameSizePercent);
                catalog = ScriptableObject.CreateInstance<UnitArtCatalog>();
                runtime = catalog.ForCharacters(new[] { loaded });
                var drawing = runtime.Get(loaded.Id);
                Assert.AreEqual(127.5f, drawing.LobbySizePercent);
                Assert.AreEqual(73.25f, drawing.BriefSizePercent);
                Assert.AreEqual(57.7f, drawing.InGameSizePercent);
                drawing.LobbySizePercent = 200;
                Assert.AreEqual(127.5f, loaded.Visuals.LobbySizePercent);
            }
            finally
            {
                if (runtime != null) Object.DestroyImmediate(runtime);
                if (catalog != null) Object.DestroyImmediate(catalog);
                AssetDatabase.DeleteAsset(path);
                if (character != null && !AssetDatabase.Contains(character)) Object.DestroyImmediate(character);
            }
        }
    }
}
