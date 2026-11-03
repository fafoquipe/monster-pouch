using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Local
{
    [DefaultExecutionOrder(30)]
    public sealed class MartioCelestialOrbit : MonoBehaviour
    {
        BattleUnit unit;
        MatchController match;
        UnitPresentation view;
        MartioVisualAssets art;
        readonly SpriteRenderer[] sprites = new SpriteRenderer[3];
        readonly MartioCelestialProp[] props = new MartioCelestialProp[3];
        float orbitTime, attackTime = -1, windup;
        int attackVersion;
        public void Configure(BattleUnit actor, MatchController owner)
        {
            unit = actor; match = owner; view = actor.GetComponent<UnitPresentation>();
            art = Resources.Load<MartioVisualAssets>("MonsterPouch/Martio/MartioVisualAssets");
            for (int i = 0; i < 3; i++) if (sprites[i] == null) {
                var item = new GameObject(i == 0 ? "martio-mars" : "martio-moon-" + i);
                item.transform.SetParent(transform, false); sprites[i] = item.AddComponent<SpriteRenderer>();
            }
        }
        public void Attack()
        { attackTime = 0; windup = CombatSimulation.GetAttackWindup(unit); attackVersion = unit.AttackResetVersion; }
        void LateUpdate()
        {
            var body = view != null ? view.Renderer : null;
            bool visible = art != null && unit != null && unit.IsAlive && !unit.IsReviving && body != null && body.enabled && body.sprite != null;
            if (match == null || !match.Paused) { orbitTime += Time.deltaTime; if (attackTime >= 0) attackTime += Time.deltaTime; }
            if (unit != null && unit.AttackResetVersion != attackVersion) attackTime = -1;
            float phase = attackTime < 0 ? 0 : attackTime < windup
                ? MartioVisualAssets.ReleaseSeconds * attackTime / Mathf.Max(.001f, windup)
                : MartioVisualAssets.ReleaseSeconds + attackTime - windup;
            if (phase > MartioVisualAssets.AttackDuration) attackTime = -1;
            if (visible) art.SampleProps(view.Facing, attackTime < 0 ? MartioVisualState.Idle : MartioVisualState.Attack, phase, orbitTime, props);
            for (int i = 0; i < 3; i++) {
                var sprite = sprites[i]; if (sprite == null) continue;
                sprite.enabled = visible && props[i].Opacity > 0;
                if (!sprite.enabled) continue;
                var prop = props[i]; sprite.sprite = prop.Sprite; if (sprite.sprite == null) { sprite.enabled = false; continue; }
                float height = (164f / 160f) * Mathf.Abs(body.transform.lossyScale.y);
                sprite.sharedMaterial = body.sharedMaterial; sprite.sortingLayerID = body.sortingLayerID;
                sprite.sortingOrder = body.sortingOrder + (prop.BehindBody ? -1 : 2);
                sprite.color = new Color(body.color.r, body.color.g, body.color.b, body.color.a * prop.Opacity);
                sprite.transform.position = view.VisualRoot.position + (Vector3)(prop.OffsetInBodyHeights * height);
                sprite.transform.localScale = Vector3.one * (height * prop.SizeInBodyHeights / Mathf.Max(.001f, sprite.sprite.bounds.size.x, sprite.sprite.bounds.size.y));
            }
        }
        void OnDisable() { foreach (var sprite in sprites) if (sprite != null) sprite.enabled = false; }
    }
}
