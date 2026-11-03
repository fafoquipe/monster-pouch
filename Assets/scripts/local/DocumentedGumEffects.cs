using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class DocumentedBattleEffects
    {
        readonly Dictionary<BattleUnit, SpriteRenderer> gumTiles = new Dictionary<BattleUnit, SpriteRenderer>();
        readonly List<BattleUnit> gumRemoval = new List<BattleUnit>();
        readonly HashSet<BattleUnit> wantedGum = new HashSet<BattleUnit>();

        static Color GumColor(GumBuff? buff) => buff == GumBuff.Speed ? new Color(.84f, 1, .22f) :
            buff == GumBuff.Shield ? new Color(.25f, .78f, 1) :
            buff == GumBuff.Damage ? new Color(1, .32f, .2f) : new Color(1, .36f, .71f);

        void ColoredGumLaunched(BattleUnit source, BattleUnit target, GumBuff buff, float duration)
        {
            if (isActiveAndEnabled && layer != null && source != null && target != null)
                StartCoroutine(GumFlight(source, target, buff, duration));
        }

        IEnumerator GumFlight(BattleUnit source, BattleUnit target, GumBuff? buff, float duration)
        {
            var simulation = match.Simulation;
            var root = EffectRoot(buff.HasValue ? "tsu-colored-gum-" + buff : "tsu-sticky-gum-projectile");
            var color = GumColor(buff);
            var trail = new Image[3];
            for (int i = 0; i < trail.Length; i++)
            {
                trail[i] = Box("gum-trail", root, Vector2.one * (7 - i * 2), Alpha(color, .65f - i * .15f));
                trail[i].sprite = GumSprite(buff, false);
            }
            var ball = Box("gum-ball", root, Vector2.one * (buff.HasValue ? 23 : 29), Color.white);
            ball.sprite = GumSprite(buff, false);
            Vector3 start = source.transform.position + Vector3.up * .5f;
            float elapsed = 0;
            duration = Mathf.Max(.1f, duration);
            while (root != null && elapsed < duration && Current(simulation) && target != null &&
                target.IsAlive && !target.IsReviving)
            {
                elapsed += EffectDelta;
                float t = Mathf.Clamp01(elapsed / duration);
                Vector2 from = Point(start), to = Point(target.transform.position + Vector3.up * .5f);
                Vector2 arc = Vector2.up * Mathf.Sin(t * Mathf.PI) * (buff.HasValue ? 25 : 11);
                Vector2 head = Vector2.Lerp(from, to, t) + arc;
                root.anchoredPosition = Round(head);
                var pose=ProjectileFlightRendering.Gum(ball.sprite,buff.HasValue,t);pose.Apply(ball);
                float sizeRatio = SizeProjectile(ball, source, pose.Size, buff.HasValue ? ProjectileVisualSource.Upgrade3 : ProjectileVisualSource.Ability, buff.HasValue ? buff.Value.ToString().ToLowerInvariant() : "flight");
                for (int i = 0; i < trail.Length; i++)
                {
                    ProjectileFlightRendering.GumTrail(trail[i].sprite,buff,i,sizeRatio).Apply(trail[i]);
                    float tail = Mathf.Max(0, t - (i + 1) * .055f);
                    Vector2 p = Vector2.Lerp(from, to, tail) + Vector2.up * Mathf.Sin(tail * Mathf.PI) * (buff.HasValue ? 25 : 11);
                    trail[i].rectTransform.anchoredPosition = Round(p - head);
                }
                yield return null;
            }
            if (root != null && target != null && target.IsAlive && !target.IsReviving && Current(simulation))
            {
                ball.rectTransform.sizeDelta = Vector2.one * (buff.HasValue ? 23 : 29); // Restore the independent impact visual.
                for (int i = 0; i < trail.Length; i++) trail[i].enabled = false;
                Vector3 contact = target.transform.position + Vector3.up * (buff.HasValue ? .5f : .08f);
                elapsed = 0;
                while (root != null && elapsed < .18f && Current(simulation))
                {
                    elapsed += EffectDelta;
                    float t = Mathf.Clamp01(elapsed / .18f);
                    root.anchoredPosition = Round(Point(contact));
                    ball.rectTransform.localScale = buff.HasValue ? Vector3.one * (1 + t * .55f) : new Vector3(1 + t * 1.4f, 1 - t * .6f, 1);
                    ball.color = Alpha(Color.white, 1 - t);
                    yield return null;
                }
            }
            ReleaseEffect(root);
        }

        void RefreshGumTiles()
        {
            wantedGum.Clear();
            if (match.Phase == MatchPhase.Combat && match.Simulation != null)
                foreach (var unit in match.Simulation.GumRootedUnits)
                {
                    wantedGum.Add(unit);
                    if (!gumTiles.TryGetValue(unit, out var tile) || tile == null)
                    {
                        tile = new GameObject("tsu-sticky-pink-cell").AddComponent<SpriteRenderer>();
                        tile.transform.SetParent(transform, false);
                        tile.sharedMaterial = Resources.Load<Material>("MonsterPouch/UnitSpriteMaterial");
                        tile.sprite = GumSprite(null, true); tile.sortingOrder = 21;
                        gumTiles[unit] = tile; areaFloors.Add(tile.gameObject);
                    }
                    tile.transform.position = match.Mapper.GetWorldPosition(unit.CurrentCell);
                    tile.transform.localScale = new Vector3(Mathf.Abs(match.Mapper.CellSize.x), Mathf.Abs(match.Mapper.CellSize.y), 1);
                    tile.color = new Color(1, 1, 1, .83f);
                }
            gumRemoval.Clear();
            foreach (var pair in gumTiles) if (!wantedGum.Contains(pair.Key)) gumRemoval.Add(pair.Key);
            foreach (var unit in gumRemoval)
            {
                var tile = gumTiles[unit];
                if (tile != null) { areaFloors.Remove(tile.gameObject); Destroy(tile.gameObject); }
                gumTiles.Remove(unit);
            }
        }
        void ClearGumTiles()
        {
            foreach (var tile in gumTiles.Values) if (tile != null) { areaFloors.Remove(tile.gameObject); Destroy(tile.gameObject); }
            gumTiles.Clear(); wantedGum.Clear(); gumRemoval.Clear();
        }

        Sprite GumSprite(GumBuff? buff, bool floor) => ProjectileProceduralArt.Gum(buff, floor, pixels);
    }
}
