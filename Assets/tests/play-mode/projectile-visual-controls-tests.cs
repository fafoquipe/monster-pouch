using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class ProjectileVisualControlsTests
{
    readonly List<Object> objects = new List<Object>();
    T Track<T>(T value) where T : Object { objects.Add(value); return value; }
    [TearDown] public void Cleanup() { foreach (var o in objects.AsEnumerable().Reverse()) if (o != null) Object.DestroyImmediate(o); objects.Clear(); }

    Sprite Sprite()
    {
        var texture = Track(new Texture2D(16, 8, TextureFormat.RGBA32, false));
        var pixels = new Color32[128];
        for (int y = 2; y < 6; y++) for (int x = 4; x < 12; x++) pixels[y * 16 + x] = Color.white;
        texture.SetPixels32(pixels); texture.Apply(); texture.filterMode = FilterMode.Point;
        return Track(UnityEngine.Sprite.Create(texture, new Rect(0, 0, 16, 8), Vector2.one * .5f, 100));
    }
    Image Image(Sprite sprite)
    {
        var go = Track(new GameObject("isolated projectile", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)));
        var image = go.GetComponent<Image>(); image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false;
        return image;
    }
    ProjectileVisualSlot Slot(Sprite sprite, float percent, bool active = true) => new ProjectileVisualSlot {
        OverrideSize = active, SizePercent = percent, Frames = new[] { new ProjectileVisualFrame { Sprite = sprite, VisibleRect = new Rect(.25f, .25f, .5f, .5f) } }
    };

    [TestCase(0)] [TestCase(50)] [TestCase(100)]
    public void ImageSizeUsesVisibleBoundsAndLeavesParentColliderAndPositionIntact(float percent)
    {
        var image = Image(Sprite()); var parent = Track(new GameObject("unchanged parent"));
        image.transform.SetParent(parent.transform, false);
        var collider = parent.AddComponent<BoxCollider2D>(); collider.size = new Vector2(2, 3);
        image.rectTransform.anchoredPosition = new Vector2(17, 29); image.rectTransform.localRotation = Quaternion.Euler(0, 0, 27);
        ProjectileVisualSizing.Apply(image, Slot(image.sprite, percent), 80, new Vector2(64, 64));
        Vector2 fit = ProjectileVisualGeometry.Fit(image.rectTransform.sizeDelta, image.sprite.rect.size, true);
        Assert.AreEqual(160 * percent / 100, fit.x * .5f, .001f);
        Assert.AreEqual(new Vector2(17, 29), image.rectTransform.anchoredPosition);
        Assert.AreEqual(Vector3.one, parent.transform.localScale); Assert.AreEqual(new Vector2(2, 3), collider.size);
        Assert.AreEqual(27, image.rectTransform.localEulerAngles.z, .001f); Assert.IsFalse(image.raycastTarget);
    }

    [Test] public void AbsentOverrideAndExplicitZeroAreDifferentAndVariantsStayIndependent()
    {
        var image = Image(Sprite()); var mars = Slot(image.sprite, 0, false); var moons = Slot(image.sprite, 50);
        ProjectileVisualSizing.Apply(image, mars, 80, new Vector2(123, 79));
        Assert.AreEqual(new Vector2(123, 79), image.rectTransform.sizeDelta);
        mars.OverrideSize = true; ProjectileVisualSizing.Apply(image, mars, 80, new Vector2(123, 79));
        Assert.AreEqual(Vector2.zero, image.rectTransform.sizeDelta);
        ProjectileVisualSizing.Apply(image, moons, 80, new Vector2(123, 79)); Assert.Greater(image.rectTransform.sizeDelta.x, 0);
        Assert.AreEqual(50, moons.SizePercent); Assert.AreEqual(0, mars.SizePercent);
    }

    [Test] public void GochanCapturesBasicAndSuperSlotsBeforeLaterEmissionsAndOutsideTheCallback()
    {
        var config = Track(ScriptableObject.CreateInstance<MatchConfig>()); config.Units = DocumentedRoster.CreateAll(); config.RoundIncome = new[] { 50 };
        var go = Track(new GameObject("Gochan emission fixture")); var board = go.AddComponent<BoardManager>(); board.BuildBoard();
        var mapper = go.AddComponent<BoardWorldMapper>(); mapper.Configure(board, go.transform, new Vector2(1, -1), Vector2.zero);
        var match = go.AddComponent<MatchController>(); match.enabled = false; match.Configure(config, board, mapper);
        Assert.IsTrue(match.StartMatch("bugaloo", new[] { "gochan" }, 409));
        var offer = match.Player.Offers[0]; Assert.NotNull(offer); Assert.IsTrue(match.BuyAndPlace(0, offer.Token, board.GetCell(1, 6))); match.Ready();
        var actor = match.Actors.Values.Single(a => a.BaseStats.DefinitionId == "gochan" && a.Side == match.Player.Side);
        var enemy = match.Actors.Values.First(a => a.Side != actor.Side);
        var c = Track(ScriptableObject.CreateInstance<CharacterDefinition>()); c.InitializeIdentity("gochan");
        c.Attack = Track(ScriptableObject.CreateInstance<CharacterAttack>()); c.Ability = Track(ScriptableObject.CreateInstance<CharacterAbility>());
        c.Ability.Definition.EffectId = "gochan-seal-burst";
        var basic = new ProjectileVisualSlot { OverrideSize = true, SizePercent = 0 };
        var super = new ProjectileVisualSlot { OverrideSize = true, SizePercent = 100 };
        c.Attack.ProjectileVisuals = new[] { basic }; c.Ability.ProjectileVisuals = new[] { super };
        var registry = new ProjectileVisualRegistry(new[] { c });
        var emissions = new List<CombatAttackPresentationSnapshot>(); var slots = new List<ProjectileVisualSlot>();
        match.Attacked += (a, t, p) => {
            var emission = match.CurrentAttackPresentation; Assert.NotNull(emission);
            emissions.Add(emission); slots.Add(ProjectileVisualSizing.ResolveEmission(registry, emission, a, t));
        };
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        var clock = ((IEnumerable)typeof(CombatSimulation).GetField("actors", hidden).GetValue(match.Simulation)).Cast<object>()
            .Single(a => a.GetType().GetField("Unit").GetValue(a) == (object)actor);
        var queue = typeof(CombatSimulation).GetMethod("QueueImpact", hidden);
        queue.Invoke(match.Simulation, new object[] { clock, enemy, 1, null, false, -1, false, false });
        Assert.IsNull(match.CurrentAttackPresentation);
        queue.Invoke(match.Simulation, new object[] { clock, enemy, 1, "gochan-super", true, -1, false, false });
        Assert.IsNull(match.CurrentAttackPresentation);
        Assert.AreEqual(2, emissions.Count); Assert.AreSame(basic, slots[0]); Assert.AreSame(super, slots[1]);
        Assert.IsFalse(emissions[0].IsAbility); Assert.IsTrue(emissions[1].IsAbility); Assert.AreEqual("gochan-seal-burst", emissions[1].EffectId);
        Assert.Greater(emissions[1].EmissionId, emissions[0].EmissionId);
        Assert.IsNull(ProjectileVisualSizing.ResolveEmission(registry, null, actor, enemy));
        Assert.IsNull(ProjectileVisualSizing.ResolveEmission(registry, emissions[1], enemy, actor));
        match.Menu();
    }

    [TestCase("mars", 154)] [TestCase("moons", 151)]
    public void RegisteredMartioUsesApprovedFixedClipEnvelopeAndIndependentControls(string variant, float expectedPixels)
    {
        var character = Resources.Load<CharacterDefinition>("MonsterPouch/Characters/martio/Character"); Assert.NotNull(character);
        var registered = ProjectileVisualSizing.Resolve(character, ProjectileVisualSource.Basic, variant); Assert.NotNull(registered, "Merge the serialized slot fragments first.");
        bool originalOverride = registered.OverrideSize;
        Assert.AreEqual(4, registered.Frames.Length);
        var slot = new ProjectileVisualSlot { OverrideSize = true, Frames = registered.Frames };
        var image = Image(registered.Frames[0].Sprite);
        foreach (float percent in new[] { 0f, 50f, 100f })
        {
            slot.SizePercent = percent; float maximum = 0; float rectSize = -1;
            foreach (var frame in slot.Frames)
            {
                image.sprite = frame.Sprite;
                Rect clip = slot.ClipRect(frame.Sprite);
                Assert.AreEqual(expectedPixels, Mathf.Max(clip.width * frame.Sprite.rect.width, clip.height * frame.Sprite.rect.height), .001f);
                ProjectileVisualSizing.Apply(image, slot, 63, Vector2.one * 100);
                if (rectSize < 0) rectSize = image.rectTransform.sizeDelta.x;
                else Assert.AreEqual(rectSize, image.rectTransform.sizeDelta.x, .001f, "Keep scaling fixed across the whole clip.");
                Vector2 fit = ProjectileVisualGeometry.Fit(image.rectTransform.sizeDelta, frame.Sprite.rect.size, true);
                maximum = Mathf.Max(maximum, fit.x * frame.VisibleRect.width, fit.y * frame.VisibleRect.height);
            }
            Assert.AreEqual(126 * percent / 100, maximum, .001f);
        }
        Assert.AreEqual(originalOverride, registered.OverrideSize); // Fixture never edits the source asset.
    }

    [TestCase(256, 0)] [TestCase(256, 50)] [TestCase(256, 100)]
    [TestCase(512, 0)] [TestCase(512, 50)] [TestCase(512, 100)]
    [TestCase(1024, 0)] [TestCase(1024, 50)] [TestCase(1024, 100)]
    public void NativeRenderMatchesTwoWorldTilesAtMultipleResolutions(int resolution, float percent)
    {
        var camera = Track(new GameObject("fixture camera")).AddComponent<Camera>();
        camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true; camera.orthographicSize = 2;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.cullingMask = 1 << 30;
        var canvasObject = Track(new GameObject("isolated fixture canvas", typeof(RectTransform), typeof(Canvas)));
        canvasObject.layer = 30; var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = camera; canvasObject.transform.localScale = Vector3.one * .01f;
        var image = Image(Sprite()); image.gameObject.layer = 30; image.transform.SetParent(canvas.transform, false);
        ProjectileVisualSizing.Apply(image, Slot(image.sprite, percent), 100, new Vector2(100, 100)); // 100 canvas units = 1 world tile.
        var target = Track(new RenderTexture(resolution, resolution, 24)); var readback = Track(new Texture2D(resolution, resolution, TextureFormat.RGB24, false));
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0); readback.Apply();
            var pixels = readback.GetPixels32(); int minX = resolution, maxX = -1, minY = resolution, maxY = -1;
            for (int y = 0; y < resolution; y++) for (int x = 0; x < resolution; x++)
                if (pixels[y * resolution + x].r > 5) { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); }
            int width = maxX < minX ? 0 : maxX - minX + 1, height = maxY < minY ? 0 : maxY - minY + 1;
            Assert.AreEqual(resolution / 2f * percent / 100, width, 2);
            Assert.AreEqual(width / 2f, height, 2);
            string folder = Path.GetFullPath("Logs/local-validation/projectile-visual-editor"); Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, "fixture-" + resolution + "-" + percent + ".png"), readback.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; camera.targetTexture = null; }
    }

    [Test] public void AllRuntimeDefinitionsAndCombatEventsDigestStayIdenticalAtZeroFiftyHundred()
    {
        string original = null, digest = null, events = null;
        foreach (float percent in new[] { 0f, 50f, 100f })
        {
            var source = Track(ScriptableObject.CreateInstance<MatchConfig>()); source.RoundIncome = new[] { 50 };
            source.Characters = DocumentedRoster.CreateAll().Select(d => {
                var c = Track(ScriptableObject.CreateInstance<CharacterDefinition>()); c.InitializeIdentity(d.Id); c.General = d;
                c.Attack = Track(ScriptableObject.CreateInstance<CharacterAttack>()); c.Attack.Import(d);
                c.Ability = Track(ScriptableObject.CreateInstance<CharacterAbility>()); c.Ability.Definition = d.BaseAbility;
                c.Attack.ProjectileVisuals = new[] { new ProjectileVisualSlot { OverrideSize = true, SizePercent = percent } };
                c.Ability.ProjectileVisuals = new[] { new ProjectileVisualSlot { OverrideSize = true, SizePercent = percent } };
                return c;
            }).ToArray();
            var snapshot = Track(source.CreateRuntimeConfig()); string json = JsonUtility.ToJson(snapshot);
            if (original == null) original = json; else Assert.AreEqual(original, json, "Visual slots must not enter network/combat values.");
            var go = Track(new GameObject("deterministic fixture")); var board = go.AddComponent<BoardManager>(); board.BuildBoard();
            var mapper = go.AddComponent<BoardWorldMapper>(); mapper.Configure(board, go.transform, new Vector2(1, -1), Vector2.zero);
            var match = go.AddComponent<MatchController>(); match.enabled = false; match.Configure(snapshot, board, mapper);
            var log = new List<string>(); match.Attacked += (a, t, p) => log.Add("attack:" + a.UnitId + ":" + t.UnitId + ":" + p);
            match.Impacted += (a, t, damage) => log.Add("hit:" + a.UnitId + ":" + t.UnitId + ":" + damage);
            Assert.IsTrue(match.StartMatch("anuik", new[] { "martio" }, 73021));
            var offer = match.Player.Offers[0]; Assert.NotNull(offer);
            Assert.IsTrue(match.BuyAndPlace(0, offer.Token, board.GetCell(1, 6))); match.Ready();
            for (int i = 0; i < 150; i++) match.Advance(.1f);
            Assert.Greater(log.Count, 0); string currentEvents = string.Join("\n", log), currentDigest = match.OnlineDigest();
            if (digest == null) { digest = currentDigest; events = currentEvents; }
            else { Assert.AreEqual(digest, currentDigest); Assert.AreEqual(events, currentEvents); }
            match.Menu(); Object.DestroyImmediate(go);
        }
    }

#if UNITY_EDITOR
    [TestCase(false)] [TestCase(true)]
    public void SerializedEditUndoRedoSaveAndReloadPreserveExplicitZeroAndLocalizedLabels(bool isAbility)
    {
        string folder = "Assets/__ProjectileVisualFixture_" + Guid.NewGuid().ToString("N");
        UnityEditor.AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
        try
        {
            ScriptableObject attack = isAbility ? (ScriptableObject)ScriptableObject.CreateInstance<CharacterAbility>() : ScriptableObject.CreateInstance<CharacterAttack>();
            var initialSlots = new[] { new ProjectileVisualSlot { NameEnglish = "Moon", NameSpanish = "Luna", SizePercent = 50 } };
            if (attack is CharacterAttack basic) basic.ProjectileVisuals = initialSlots;
            else ((CharacterAbility)attack).ProjectileVisuals = initialSlots;
            ProjectileVisualSlot Current() => attack is CharacterAttack a ? a.ProjectileVisuals[0] : ((CharacterAbility)attack).ProjectileVisuals[0];
            string path = folder + "/Attack.asset"; UnityEditor.AssetDatabase.CreateAsset(attack, path);
            var serialized = new UnityEditor.SerializedObject(attack); serialized.Update();
            var slot = serialized.FindProperty("ProjectileVisuals").GetArrayElementAtIndex(0);
            UnityEditor.Undo.IncrementCurrentGroup();
            slot.FindPropertyRelative("OverrideSize").boolValue = true; slot.FindPropertyRelative("SizePercent").floatValue = 0;
            Assert.IsTrue(serialized.ApplyModifiedProperties()); UnityEditor.Undo.FlushUndoRecordObjects();
            Assert.IsTrue(UnityEditor.EditorUtility.IsDirty(attack));
            UnityEditor.Undo.PerformUndo(); Assert.IsFalse(Current().OverrideSize); Assert.AreEqual(50, Current().SizePercent);
            UnityEditor.Undo.PerformRedo(); Assert.IsTrue(Current().OverrideSize); Assert.AreEqual(0, Current().SizePercent);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(attack); UnityEditor.AssetDatabase.ImportAsset(path, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
            attack = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(path); var loaded = Current();
            Assert.IsTrue(loaded.OverrideSize); Assert.AreEqual(0, loaded.SizePercent); Assert.AreEqual("Moon", loaded.Name(false)); Assert.AreEqual("Luna", loaded.Name(true));
        }
        finally { UnityEditor.AssetDatabase.DeleteAsset(folder); }
    }
#endif
}
