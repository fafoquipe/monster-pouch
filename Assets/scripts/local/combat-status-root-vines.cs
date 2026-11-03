using System.Collections.Generic;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Local
{
    public sealed partial class CombatStatusEffects
    {
        sealed class RootVineBinding { public RootVinesView View; public int ResetVersion; }
        readonly Dictionary<BattleUnit,RootVineBinding> rootViews = new Dictionary<BattleUnit,RootVineBinding>();
        readonly HashSet<BattleUnit> currentRootViews = new HashSet<BattleUnit>();
        readonly List<BattleUnit> retiredRootViews = new List<BattleUnit>();
        Transform rootWorldLayer;
        Sprite[] rootBackFrames, rootFrontFrames;

        bool LoadRootFrames()
        {
            if (rootBackFrames == null)
            {
                rootBackFrames = new Sprite[4]; rootFrontFrames = new Sprite[4];
                for (int i = 0; i < 4; i++)
                {
                    string suffix = i.ToString("00",System.Globalization.CultureInfo.InvariantCulture);
                    rootBackFrames[i] = Resources.Load<Sprite>("MonsterPouch/StatusPresentation/RootVines/root-vines-back-" + suffix);
                    rootFrontFrames[i] = Resources.Load<Sprite>("MonsterPouch/StatusPresentation/RootVines/root-vines-front-" + suffix);
                }
            }
            for (int i = 0; i < rootBackFrames.Length; i++)
                if (rootBackFrames[i] == null || rootFrontFrames[i] == null) return false;
            return true;
        }
        void ReconcileRootVines()
        {
            if (match == null || match.Phase != MatchPhase.Combat || simulation == null) { ClearRootVines(); return; }
            currentRootViews.Clear();
            foreach (var pair in match.Actors)
            {
                var actor = pair.Value;
                if (actor == null) continue;
                var frame = simulation.CaptureStatusFrame(actor);
                if (!frame.IsActive) continue;
                CombatStatusSnapshot rootState = default; bool hasRoot = false;
                foreach (var state in frame.Statuses)
                    if (state.Id == "root") { rootState = state; hasRoot = true; break; }
                if (!hasRoot) continue;
                var body = actor.GetComponent<UnitPresentation>();
                if (body == null || !body.TryGetPresentationAnchors(out var anchors) ||
                    anchors.Unit != actor || anchors.UnitResetVersion != frame.UnitResetVersion ||
                    !RootVinesLayout.TryFitCell(
                    anchors.OccupiedCellWorldBounds.size.x, anchors.OccupiedCellWorldBounds.size.y,
                    out var layout) || !LoadRootFrames()) continue;
                if (rootViews.TryGetValue(actor,out var previous) && previous.ResetVersion != frame.UnitResetVersion)
                    RetireRootView(actor);
                if (!rootViews.TryGetValue(actor,out var binding))
                {
                    // A separate identity transform keeps canvas/body squash out of world geometry.
                    if (rootWorldLayer == null) rootWorldLayer = new GameObject("Root vines world layer").transform;
                    var go = new GameObject("Root vines - " + actor.name); go.transform.SetParent(rootWorldLayer,false);
                    var view = go.AddComponent<RootVinesView>(); view.Configure(rootBackFrames,rootFrontFrames);
                    binding = new RootVineBinding { View = view, ResetVersion = frame.UnitResetVersion };
                    rootViews.Add(actor,binding);
                }
                var vines = binding.View;
                if (!vines.AssetsReady || !ClaimAuthoredRoot(actor,binding.ResetVersion,vines,vines.Hide))
                { vines.Hide(); continue; }
                var timing = StatusVisualTiming.FromSnapshot(rootState);
                vines.Present(true,binding.ResetVersion,timing.HasClock ? timing.AgeSeconds : .24f,
                    anchors.OccupiedCellWorldBounds.center+new Vector3(layout.AnchorOffsetX,layout.AnchorOffsetY,0),
                    new Vector2(layout.WorldWidth,layout.WorldHeight),
                    new Vector2(RootVinesLayout.NormalizedVisibleWidth,RootVinesLayout.NormalizedVisibleHeight),
                    anchors.BodySortingLayerId,anchors.BodySortingOrder);
                if (vines.IsShown) currentRootViews.Add(actor);
                else { vines.Hide(); ReleaseAuthoredRoot(actor,binding.ResetVersion,vines); }
            }
            retiredRootViews.Clear();
            foreach (var pair in rootViews) if (!currentRootViews.Contains(pair.Key)) retiredRootViews.Add(pair.Key);
            foreach (var actor in retiredRootViews) RetireRootView(actor);
        }
        void RetireRootView(BattleUnit actor)
        {
            if (!rootViews.TryGetValue(actor,out var binding)) return;
            if (binding.View != null)
            {
                binding.View.Hide(); ReleaseAuthoredRoot(actor,binding.ResetVersion,binding.View);
                Destroy(binding.View.gameObject);
            }
            rootViews.Remove(actor);
        }
        void ClearRootVines()
        {
            retiredRootViews.Clear(); retiredRootViews.AddRange(rootViews.Keys);
            foreach (var actor in retiredRootViews) RetireRootView(actor);
            currentRootViews.Clear(); retiredRootViews.Clear();
        }
    }
}
