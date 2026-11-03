#if UNITY_EDITOR
using System;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Local;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Explicitly invoked by the integrator after import, never at editor startup. No scene/profile writes.
public static class AireolaVisualQA
{
    static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Aireola visual QA: " + message); }

    public static string Run()
    {
        var catalog = Resources.Load<AireolaVisualCatalog>("MonsterPouch/Aireola/AireolaVisuals");
        Require(catalog != null && catalog.Clips.Length == 12, "Expected 12 referenced clips.");
        int spriteCount = 0, samples = 0;
        foreach (var clip in catalog.Clips)
        {
            Require(clip.Frames.Length == 4, clip.Id + " frame count");
            foreach (var sprite in clip.Frames) { Require(sprite != null, clip.Id + " missing Sprite"); spriteCount++; }
        }
        foreach (var id in new[] { "zephalontropo", "alboguera" })
        {
            var character = AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/resources/MonsterPouch/Characters/" + id + "/Character.asset");
            Require(character != null, id + " Character asset");
            foreach (bool ability in id == "zephalontropo" ? new[] { false, true } : new[] { false })
            {
                var source = ability ? ProjectileVisualSource.Ability : ProjectileVisualSource.Basic;
                var original = ProjectileVisualSizing.Resolve(character, source, "flight");
                string renderer = ability ? AireolaFlightRendering.Sphere : id == "zephalontropo" ? AireolaFlightRendering.Bolt : AireolaFlightRendering.Note;
                Require(original != null && original.RendererId == renderer && original.Frames.Length == 4, renderer + " real slot");
                // Exercise the exact binding called by battle and inspector playback.
                var art = AireolaFlightRendering.Resolve(renderer);
                Require(art?.Flight != null && art.Flight.Length == 4, renderer + " flight art");
                Require(art.DisplaySize > 0 && art.ReferencePixels == 256, renderer + " calibrated catalog");
                for (int frame = 0; frame < 4; frame++)
                    Require(art.Flight[frame] != null && art.Flight[frame] == original.Frames[frame].Sprite,
                        renderer + " actual slot/catalog frame " + frame);
                var slot = new ProjectileVisualSlot { RendererId = original.RendererId, Frames = original.Frames, OverrideSize = true };
                UnityEngine.Object owner = ability ? (UnityEngine.Object)character.Ability : character.Attack;
                using (var preview = new ProjectileVisualPlayback(character, owner, slot))
                {
                    Require(Mathf.Approximately(preview.Duration, MonsterPouch.Gameplay.Match.CombatSimulation.GetProjectileTravelTime(3, character.Attack.ProjectileSpeed)), renderer + " shared speed-based duration");
                    var go = new GameObject("Aireola QA flight", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)) { hideFlags = HideFlags.HideAndDontSave };
                    try
                    {
                        var runtimeImage = go.GetComponent<Image>();
                        foreach (float cell in new[] { 32f, 64f, 128f })
                        foreach (float percent in new[] { 0f, 50f, 100f })
                        for (int frame = 0; frame < 4; frame++)
                        {
                            float elapsed = (frame + .1f) / 16;
                            slot.SizePercent = percent;
                            var pose = AireolaFlightRendering.Sample(art, elapsed, cell);
                            pose.Apply(runtimeImage);
                            ProjectileVisualSizing.Apply(runtimeImage, slot, cell, pose.Size);
                            preview.Sample(slot, elapsed, elapsed / preview.Duration, cell);
                            var shown = preview.Layers[preview.Head];
                            Require(shown.sprite == art.Flight[frame] && shown.sprite == runtimeImage.sprite, renderer + " frame parity");
                            Require((shown.rectTransform.sizeDelta - runtimeImage.rectTransform.sizeDelta).sqrMagnitude < .0001f, renderer + " size parity");
                            var bounds = slot.ClipRect(shown.sprite);
                            var rect = shown.rectTransform.sizeDelta;
                            float visible = Mathf.Max(rect.x * bounds.width, rect.y * bounds.height);
                            Require(Mathf.Abs(visible - cell * 2 * percent / 100) < .001f, renderer + " 0/50/100 envelope");
                            samples++;
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(go); }
                }
            }
        }
        return "PASS: " + spriteCount + " loaded sprites; " + samples + " preview/runtime samples; scene and asset values untouched. Playback lifecycle/contact QA remains separate.";
    }
}
#endif
