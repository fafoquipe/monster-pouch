using System;
using System.Collections.Generic;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Status;
using MonsterPouch.Gameplay.Units;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed class UnitStatusBadges : MonoBehaviour
    {
        // Identity is structural; presentation names are made only when a badge is bound.
        readonly struct BadgeKey : IEquatable<BadgeKey>
        {
            public readonly bool Passive;
            public readonly string Id;
            public BadgeKey(bool passive, string id) { Passive = passive; Id = id ?? ""; }
            public bool Equals(BadgeKey other) => Passive == other.Passive && string.Equals(Id, other.Id, StringComparison.Ordinal);
            public override bool Equals(object other) => other is BadgeKey value && Equals(value);
            public override int GetHashCode() => unchecked(StringComparer.Ordinal.GetHashCode(Id ?? "") * 397 ^ (Passive ? 1 : 0));
            public override string ToString() => (Passive ? "passive-" : "status-") + Id;
        }
        sealed class Badge
        {
            public RectTransform Root;
            public CombatBadgeRing Ring;
            public Image Icon;
            public Text Number;
            public float Duration, PreviousRemaining;
            public int PreviousCompleted = int.MinValue;
        }
        readonly List<ActiveStatus> states = new List<ActiveStatus>(24);
        readonly List<PassiveProgress> passives = new List<PassiveProgress>(2);
        readonly Dictionary<BadgeKey, Badge> badges = new Dictionary<BadgeKey, Badge>();
        readonly HashSet<BadgeKey> visible = new HashSet<BadgeKey>();
        readonly Stack<Badge> pool = new Stack<Badge>();
        readonly List<BadgeKey> expired = new List<BadgeKey>();
        CombatSimulation previousSimulation;
        BattleUnit previousUnit;
        int previousResetVersion = int.MinValue;
        static readonly Comparison<ActiveStatus> Compare = (a, b) =>
        { int priority = Priority(b.Id).CompareTo(Priority(a.Id)); return priority != 0 ? priority : string.CompareOrdinal(a.Id, b.Id); };
        public int VisibleCount => badges.Count;

        public void Refresh(CombatSimulation simulation, BattleUnit unit)
        {
            if (simulation == null || unit == null) { Clear(); return; }
            var frame = simulation.CaptureStatusFrame(unit);
            bool changed = simulation != previousSimulation || unit != previousUnit || previousResetVersion != frame.UnitResetVersion;
            if (changed) { Clear(); previousSimulation = simulation; previousUnit = unit; previousResetVersion = frame.UnitResetVersion; }
            if (!frame.IsActive || simulation.Finished || !unit.IsAlive || unit.IsReviving) { Clear(); return; }
            // Legacy effects keep their existing projection. New snapshots replace
            // only matching canonical IDs, including effects on legacy recipients.
            simulation.CollectStatuses(unit, states);
            simulation.CollectPassiveProgress(unit, passives);
            PresentFrame(states, passives, frame, simulation.Elapsed);
        }

        // Shared by live combat and the isolated editor review, without driving gameplay.
        public void Present(IReadOnlyList<ActiveStatus> active, IReadOnlyList<PassiveProgress> progress, float clock)
            => PresentMerged(active, progress, null, clock);

        public void PresentFrame(IReadOnlyList<ActiveStatus> active, IReadOnlyList<PassiveProgress> progress, CombatStatusFrame frame, float clock)
        {
            if (!frame.IsActive) { Clear(); return; }
            if (previousResetVersion != frame.UnitResetVersion) { Clear(); previousResetVersion = frame.UnitResetVersion; }
            PresentMerged(active, progress, frame.Statuses, clock);
        }

        static string Canonical(string id) => id == "guard" ? "damage-reduction" : id;

        void PresentMerged(IReadOnlyList<ActiveStatus> active, IReadOnlyList<PassiveProgress> progress,
            IReadOnlyList<CombatStatusSnapshot> snapshots, float clock)
        {
            if (!ReferenceEquals(active, states)) { states.Clear(); for (int i = 0; i < active.Count; i++) states.Add(active[i]); }
            states.Sort(Compare); visible.Clear();
            int index = 0;
            for (int i = 0; i < progress.Count; i++)
            {
                var state = progress[i]; var key = new BadgeKey(true, state.Id);
                var badge = Get(key, state.Id, index++);
                badge.Icon.enabled = state.Completed == 0 && badge.Icon.sprite != null;
                if (badge.PreviousCompleted != state.Completed)
                {
                    badge.PreviousCompleted = state.Completed;
                    badge.Number.text = state.Completed > 0 ? state.Completed.ToString() : "";
                }
                badge.Number.fontSize = state.Completed > 9 ? 11 : 13;
                badge.Ring.Set(new Color(.21f, .53f, .75f), 1, state.Ready, clock, false, true);
            }
            for (int i = 0; snapshots != null && i < snapshots.Count; i++)
            {
                var state = snapshots[i];
                if (string.IsNullOrEmpty(state.Id)) continue;
                var key = new BadgeKey(false, Canonical(state.Id));
                if (visible.Contains(key)) continue;
                var badge = Get(key, state.Id, index++);
                var timing = StatusVisualTiming.FromSnapshot(state);
                badge.Duration = badge.PreviousRemaining = 0;
                badge.Icon.enabled = badge.Icon.sprite != null; badge.Number.text = "";
                badge.Ring.Set(CombatHudSymbols.Tint(state.Id), timing.HasClock ? timing.Fraction : 1,
                    false, clock);
            }
            foreach (var state in states)
            {
                var key = new BadgeKey(false, Canonical(state.Id));
                if (visible.Contains(key)) continue;
                var badge = Get(key, state.Id, index++);
                if (state.Remaining > 0 && (badge.Duration <= 0 || state.Remaining > badge.PreviousRemaining + .01f)) badge.Duration = state.Remaining;
                badge.PreviousRemaining = state.Remaining;
                badge.Icon.enabled = state.Id != "invulnerable" && badge.Icon.sprite != null; badge.Number.text = "";
                badge.Ring.Set(CombatHudSymbols.Tint(state.Id), state.Remaining < 0 ? 1 : state.Remaining / Mathf.Max(.001f, badge.Duration),
                    false, clock, state.Id == "control-immunity", false, state.Id == "invulnerable");
            }
            expired.Clear();
            foreach (var pair in badges) if (!visible.Contains(pair.Key)) expired.Add(pair.Key);
            foreach (var key in expired) { var badge = badges[key]; badge.Root.gameObject.SetActive(false); pool.Push(badge); badges.Remove(key); }
        }
        static int Priority(string id)
        {
            switch (id)
            {
                case "stealth": return 110;
                case "stun": case "electric-stun": case "sleep": case "root": case "taunt": case "pull": return 100;
                case "attack-up": case "haste": case "rose-haste": case "rhythm": case "storm": return 90;
                case "anti-heal": case "weakness": case "burn": case "chocolate": return 80;
                default: return 70;
            }
        }
        Badge Get(BadgeKey key, string symbol, int index)
        {
            if (!badges.TryGetValue(key, out var badge))
            {
                badge = pool.Count > 0 ? pool.Pop() : Create(); badges.Add(key, badge);
                badge.Duration = badge.PreviousRemaining = 0;
                badge.PreviousCompleted = int.MinValue;
                badge.Root.name = key.ToString();
                badge.Icon.color = Color.white;
                badge.Root.gameObject.SetActive(true);
            }
            badge.Icon.sprite = CombatHudSymbols.Get(symbol);
            visible.Add(key);
            // Three medallions fit exactly over the 52px health gauge. Extra states
            // wrap upward; none are silently dropped, and none obscure either gauge.
            badge.Root.anchoredPosition = new Vector2(8 + index % 3 * 18, 9 + index / 3 * 19);
            return badge;
        }
        Badge Create()
        {
            var root = new GameObject("Combat badge", typeof(RectTransform), typeof(CombatBadgeRing));
            var rt = (RectTransform)root.transform; rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = Vector2.one * .5f; rt.sizeDelta = new Vector2(17, 17);
            var iconGo = new GameObject("Symbol", typeof(RectTransform), typeof(Image)); iconGo.transform.SetParent(rt, false);
            var icon = iconGo.GetComponent<Image>(); icon.raycastTarget = false; icon.rectTransform.sizeDelta = new Vector2(14, 14);
            var numberGo = new GameObject("Attack count", typeof(RectTransform), typeof(Text)); numberGo.transform.SetParent(rt, false);
            var number = numberGo.GetComponent<Text>(); number.font = Resources.Load<Font>("MonsterPouch/UI/Fonts/Lato-Bold");
            if (number.font == null) { number.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); number.fontStyle = FontStyle.Bold; }
            number.fontSize = 13; number.alignment = TextAnchor.MiddleCenter; number.raycastTarget = false; number.color = Color.white;
            number.horizontalOverflow = HorizontalWrapMode.Overflow; number.verticalOverflow = VerticalWrapMode.Overflow; number.rectTransform.sizeDelta = new Vector2(22, 18);
            number.alignByGeometry = true;
            return new Badge { Root = rt, Ring = root.GetComponent<CombatBadgeRing>(), Icon = icon, Number = number };
        }
        void Clear()
        {
            foreach (var badge in badges.Values) { badge.Root.gameObject.SetActive(false); pool.Push(badge); }
            badges.Clear(); visible.Clear();
        }
        void OnDisable() { Clear(); }
    }
}
