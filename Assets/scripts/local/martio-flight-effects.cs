using System.Collections.Generic;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class DocumentedBattleEffects
    {
        sealed class MartioFlightView { public RectTransform Root; public Image[] Projectiles; }
        sealed class MartioContactView { public RectTransform Root; public Image Impact, Wave; }
        readonly Dictionary<int, MartioFlightView> martioFlightViews = new Dictionary<int, MartioFlightView>();
        readonly Dictionary<int, MartioContactView> martioContactViews = new Dictionary<int, MartioContactView>();
        readonly HashSet<int> currentMartioVolleyIds = new HashSet<int>();
        readonly HashSet<int> currentMartioContactIds = new HashSet<int>();
        readonly List<int> expiredMartioIds = new List<int>();
        MartioVisualAssets martioArt;
        CombatSimulation martioSimulation;
        float martioTick = -1, martioBetweenTicks;

        void ClearMartioVisuals(bool resetClock = true)
        {
            foreach (var view in martioFlightViews.Values) if (view.Root != null) Destroy(view.Root.gameObject);
            foreach (var view in martioContactViews.Values) if (view.Root != null) Destroy(view.Root.gameObject);
            martioFlightViews.Clear(); martioContactViews.Clear();
            currentMartioVolleyIds.Clear(); currentMartioContactIds.Clear(); expiredMartioIds.Clear();
            if (resetClock) { martioTick = -1; martioBetweenTicks = 0; }
        }
        Vector3 MartioWorld(double x, double y)
        {
            float flip = match.Mapper.Flipped ? -1 : 1;
            return match.Mapper.GetWorldPosition(match.Board.GetCell(0, 0)) +
                new Vector3((float)x * match.Mapper.CellSize.x * flip, (float)y * match.Mapper.CellSize.y * flip);
        }
        void AdvanceMartioClock(CombatSimulation sim)
        {
            if (martioTick != sim.Elapsed) { martioTick = sim.Elapsed; martioBetweenTicks = 0; }
            else martioBetweenTicks = Mathf.Min(CombatSimulation.TickDuration, martioBetweenTicks + EffectDelta);
        }
        void DrawMartioFlights()
        {
            var sim = match.Simulation;
            if (sim != martioSimulation) { ClearMartioVisuals(); martioSimulation = sim; }
            if (sim == null || !Current(sim) || sim.Finished) { ClearMartioVisuals(); return; }
            // Keep the interpolation clock continuous across empty frames, but do not
            // load this character's art until its first real visual is needed.
            if (sim.MartioVolleys.Count == 0 && sim.MartioContacts.Count == 0)
            {
                AdvanceMartioClock(sim);
                ClearMartioVisuals(false);
                return;
            }
            if (martioArt == null) martioArt = Resources.Load<MartioVisualAssets>("MonsterPouch/Martio/MartioVisualAssets");
            if (martioArt == null) return;
            AdvanceMartioClock(sim);
            float cellPixels = Vector2.Distance(Point(MartioWorld(0, 0)), Point(MartioWorld(1, 0)));
            currentMartioVolleyIds.Clear();
            for (int volleyIndex = 0; volleyIndex < sim.MartioVolleys.Count; volleyIndex++) {
                var volley = sim.MartioVolleys[volleyIndex];
                currentMartioVolleyIds.Add(volley.Id);
                if (!martioFlightViews.TryGetValue(volley.Id, out var view)) {
                    view = new MartioFlightView { Root = Rect("Martio volley " + volley.Id, layer, Vector2.zero), Projectiles = new Image[3] };
                    for (int i = 0; i < 3; i++) { view.Projectiles[i] = Box("Martio projectile " + i, view.Root, Vector2.one, Color.white); view.Projectiles[i].preserveAspect = true; }
                    martioFlightViews.Add(volley.Id, view);
                }
                double progress = System.Math.Max(0, volley.Flight.Progress - volley.Speed * (CombatSimulation.TickDuration - martioBetweenTicks));
                for (int i = 0; i < 3; i++) {
                    var image = view.Projectiles[i]; var projectile = (BraidedProjectile)i;
                    image.enabled = volley.Flight.IsActive(projectile); if (!image.enabled) continue;
                    var point = volley.Flight.PositionAt(projectile, progress);
                    var frames = i == 0 ? martioArt.Mars.Flight : martioArt.Moon.Flight;
                    // i is local to this three-lane group, including both lateral groups.
                    ProjectileFlightRendering.Celestial(frames, sim.Elapsed + martioBetweenTicks, cellPixels, i != 0).Apply(image);
                    image.rectTransform.anchoredPosition = Round(Point(MartioWorld(point.X, point.Y) + Vector3.up * .4f));
                    SizeProjectile(image, volley.Source, image.rectTransform.sizeDelta, ProjectileVisualSource.Basic, i == 0 ? "mars" : "moons");
                }
            }
            expiredMartioIds.Clear();
            foreach (var id in martioFlightViews.Keys) if (!currentMartioVolleyIds.Contains(id)) expiredMartioIds.Add(id);
            foreach (var id in expiredMartioIds) {
                Destroy(martioFlightViews[id].Root.gameObject); martioFlightViews.Remove(id);
            }
            currentMartioContactIds.Clear();
            for (int contactIndex = 0; contactIndex < sim.MartioContacts.Count; contactIndex++) {
                var contact = sim.MartioContacts[contactIndex];
                if (!contact.IsCurrent) continue;
                // Match the prior cleanup predicate even for a current contact that
                // cannot be drawn because its impact cell is absent.
                currentMartioContactIds.Add(contact.Id);
                bool terminal = contact.Kind != MartioFlightEventKind.Impact;
                if (!terminal && contact.Cell == null) continue;
                if (!martioContactViews.TryGetValue(contact.Id, out var view)) {
                    var root = Rect("Martio impact " + contact.Id, layer, Vector2.zero);
                    if (terminal) root.gameObject.AddComponent<RectMask2D>();
                    view = new MartioContactView { Root = root, Wave = Box("Martio shockwave", root, Vector2.one, Color.white), Impact = Box("Martio direct impact", root, Vector2.one, Color.white) };
                    view.Wave.preserveAspect = view.Impact.preserveAspect = true; martioContactViews.Add(contact.Id, view);
                }
                float age = sim.Elapsed - contact.At + martioBetweenTicks;
                bool mars = contact.Projectile == BraidedProjectile.Center;
                var frames = mars ? martioArt.Mars.Impact : martioArt.Moon.Impact;
                Vector2 impactPosition;
                if (terminal) {
                    // Clip the terminal burst to the visible board, including flipped views.
                    var a = Point(MartioWorld(-.5, -.5));
                    var b = Point(MartioWorld(MonsterPouch.Gameplay.Board.BoardManager.Width - .5,
                        MonsterPouch.Gameplay.Board.BoardManager.Height - .5));
                    var minimum = Vector2.Min(a, b); var maximum = Vector2.Max(a, b);
                    view.Root.anchoredPosition = (minimum + maximum) * .5f;
                    view.Root.sizeDelta = maximum - minimum;
                    var point = Point(MartioWorld(contact.Position.X, contact.Position.Y) + Vector3.up * .4f);
                    point = new Vector2(Mathf.Clamp(point.x, minimum.x, maximum.x), Mathf.Clamp(point.y, minimum.y, maximum.y));
                    impactPosition = point - view.Root.anchoredPosition;
                } else impactPosition = Point(match.Mapper.GetWorldPosition(contact.Cell) + Vector3.up * .4f);
                view.Impact.enabled = age < MartioVisualAssets.ImpactDuration && frames != null && frames.Length > 0;
                if (view.Impact.enabled) {
                    view.Impact.sprite = frames[Mathf.Clamp((int)(age / MartioVisualAssets.ImpactDuration * frames.Length), 0, frames.Length - 1)];
                    view.Impact.rectTransform.anchoredPosition = Round(impactPosition);
                    view.Impact.rectTransform.sizeDelta = Vector2.one * cellPixels * (terminal ? .16f : ImpactPresentationSettings.ContactCells(false,false)*(mars?1:.64f));
                    view.Impact.color = new Color(1,1,1,terminal?.45f*(1-Mathf.Clamp01(age/MartioVisualAssets.ImpactDuration)):1);
                }
                view.Wave.enabled = martioArt.SampleShockwave(contact.Explodes, mars, age, out var wave, out float size, out float alpha);
                if (view.Wave.enabled) {
                    view.Wave.sprite = wave; view.Wave.color = new Color(1, 1, 1, alpha);
                    view.Wave.rectTransform.anchoredPosition = Round(Point(match.Mapper.GetWorldPosition(contact.Cell)));
                    view.Wave.rectTransform.sizeDelta = Vector2.one * cellPixels * size;
                }
            }
            expiredMartioIds.Clear();
            foreach (var id in martioContactViews.Keys) if (!currentMartioContactIds.Contains(id)) expiredMartioIds.Add(id);
            foreach (var id in expiredMartioIds) {
                Destroy(martioContactViews[id].Root.gameObject); martioContactViews.Remove(id);
            }
            expiredMartioIds.Clear();
        }
    }
}
