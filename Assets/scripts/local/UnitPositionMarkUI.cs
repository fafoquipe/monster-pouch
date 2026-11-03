using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    // Persistent match information, separate from temporary combat status timers.
    // The existing first-upgrade artwork stays beside HP in combat and preparation.
    public sealed class UnitPositionMarkUI : MonoBehaviour
    {
        Image icon;
        public bool Visible => icon != null && gameObject.activeSelf;
        public Sprite Icon => icon != null ? icon.sprite : null;

        public void Refresh(BattleUnit unit, OwnedUnit owned)
        {
            bool visible = unit != null && unit.isActiveAndEnabled && unit.IsAlive && !unit.IsReviving &&
                owned != null && owned.MarkedPositionRound > 0;
            gameObject.SetActive(visible);
            if (!visible || icon != null) return;
            var root = (RectTransform)transform;
            root.anchorMin = root.anchorMax = new Vector2(1, 0);
            root.pivot = Vector2.one * .5f;
            root.anchoredPosition = new Vector2(11, 20);
            root.sizeDelta = new Vector2(17, 17);
            var ring = gameObject.AddComponent<CombatBadgeRing>();
            ring.raycastTarget = false;
            ring.Set(new Color(.21f, .53f, .75f), 1, false, 0, false, false);
            var child = new GameObject("Presa Marcada", typeof(RectTransform), typeof(Image));
            child.transform.SetParent(root, false);
            icon = child.GetComponent<Image>();
            icon.sprite = AbilityArtwork.Get("tauris-mark");
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            icon.rectTransform.sizeDelta = new Vector2(14, 14);
        }
    }
}
