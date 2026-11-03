using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class DocumentedBattleEffects
    {
        // Three readable pixel bursts per damage tick; presentation never deals damage.
        IEnumerator ChocolateMiniExplosions(Vector3 position)
        {
            var simulation = match.Simulation;
            var root = EffectRoot("tsu-gum-fireworks");
            const int bursts = 3, rays = 8;
            const float rise = .15f, stagger = .095f, lifetime = .64f;
            var cores = new Image[bursts];
            var sparks = new Image[bursts * rays];
            var trails = new Image[bursts * rays];
            var colors = new[] { new Color(1, .26f, .65f), new Color(.22f, .95f, 1), new Color(1, .81f, .2f) };
            for (int burst = 0; burst < bursts; burst++)
            {
                cores[burst] = Box("firework-core", root, Vector2.one * 4, Color.white);
                for (int ray = 0; ray < rays; ray++)
                {
                    int index = burst * rays + ray;
                    trails[index] = Box("firework-trail", root, Vector2.one * 2, colors[burst]);
                    sparks[index] = Box("firework-spark", root, Vector2.one * 3, Color.white);
                }
            }
            float elapsed = 0;
            while (root != null && elapsed < rise + stagger * (bursts - 1) + lifetime && Current(simulation))
            {
                elapsed += EffectDelta;
                root.anchoredPosition = Round(Point(position));
                for (int burst = 0; burst < bursts; burst++)
                {
                    float age = elapsed - burst * stagger;
                    float t = (age - rise) / lifetime;
                    Vector2 center = new Vector2((burst - 1) * 20, burst == 1 ? 30 : 14);
                    bool launching = age >= 0 && age < rise;
                    bool blooming = t >= 0 && t < 1;
                    cores[burst].enabled = launching || (blooming && t < .22f);
                    if (launching)
                    {
                        cores[burst].rectTransform.anchoredPosition = Round(center + Vector2.down * (1 - age / rise) * 25);
                        cores[burst].rectTransform.sizeDelta = new Vector2(5, 11);
                        cores[burst].color = Color.Lerp(colors[burst], Color.white, .65f);
                    }
                    else if (blooming)
                    {
                        cores[burst].rectTransform.anchoredPosition = center;
                        cores[burst].rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(18, 3, Mathf.Clamp01(t / .22f));
                        cores[burst].color = Alpha(Color.white, Mathf.Clamp01(1 - t / .22f));
                    }
                    for (int ray = 0; ray < rays; ray++)
                    {
                        int index = burst * rays + ray;
                        var spark = sparks[index]; var trail = trails[index];
                        spark.enabled = trail.enabled = blooming;
                        if (!blooming) continue;
                        float angle = (ray * 360f / rays + burst * 18) * Mathf.Deg2Rad;
                        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                        float radius = (ray % 2 == 0 ? 43 : 33) * (1 - (1 - t) * (1 - t));
                        Vector2 head = center + direction * (4 + radius) + Vector2.down * (19 * t * t);
                        float fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.48f, 1, t));
                        spark.rectTransform.anchoredPosition = Round(head);
                        spark.rectTransform.sizeDelta = Vector2.one * (t < .62f ? 6 : 4);
                        spark.color = Alpha(Color.Lerp(Color.white, colors[burst], Mathf.Clamp01(t * 2)), fade);
                        Vector2 tail = head - direction * Mathf.Lerp(7, 16, t) + Vector2.up * (4 * t);
                        DrawSegment(trail, Round(tail), Round(head), 3);
                        float twinkle = Mathf.FloorToInt(age * 26 + ray) % 3 == 0 ? .72f : 1;
                        trail.color = Alpha(colors[burst], fade * twinkle);
                    }
                }
                yield return null;
            }
            ReleaseEffect(root);
        }
    }
}
