using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    /// <summary>Presentation only: consumes authoritative combat targets, energy and tile effects.</summary>
    public sealed class DocumentedBattleEffects : MonoBehaviour
    {
        private sealed class Badge { public RectTransform Root, Energy; public Text Status; public UnitPresentation View; }
        private MatchController match;
        private RectTransform canvas, layer;
        private Camera worldCamera;
        private Font font;
        private ProjectileArtCatalog projectiles;
        private readonly Dictionary<BattleUnit, Badge> badges = new Dictionary<BattleUnit, Badge>();
        private readonly Dictionary<string, Image> ground = new Dictionary<string, Image>();
        private readonly Dictionary<string, Sprite> pixels = new Dictionary<string, Sprite>();
        private readonly Dictionary<BattleUnit, (int frame, int count)> volleys = new Dictionary<BattleUnit, (int, int)>();
        private readonly Dictionary<BattleUnit, int> rollingFrames = new Dictionary<BattleUnit, int>();
        private float groundRefresh;
        private CombatSimulation visibleSimulation;
        private UnitArt flower;

        public void Configure(MatchController owner, RectTransform uiCanvas, Camera camera)
        {
            Unsubscribe(); match = owner; canvas = uiCanvas; worldCamera = camera;
            font = Resources.Load<Font>("MonsterPouch/PixelFont") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            projectiles = Resources.Load<ProjectileArtCatalog>("MonsterPouch/ProjectileArt");
            if (layer == null) layer = Rect("Documented battle effects", canvas, Vector2.zero);
            layer.anchorMin = Vector2.zero; layer.anchorMax = Vector2.one; layer.offsetMin = layer.offsetMax = Vector2.zero;
            match.ActorCreated += ActorCreated; match.Attacked += Attacked; match.AbilityUsed += AbilityUsed; match.Impacted += Impacted;
            match.BoulderRolled += BoulderRolled;
            foreach (var entry in match.Actors) ActorCreated(entry.Value, entry.Key);
        }

        // Existing Anuik/Bugui/Kayon artwork remains owned by LocalGameUI. All other documented ranged attacks use this helper.
        public static bool HandlesProjectile(BattleUnit actor)
        {
            if (actor == null || !actor.BaseStats.UsesDocumentedRules || actor.BaseStats.AttackRange <= 1) return false;
            string id = actor.BaseStats.DefinitionId;
            return id != "anuik" && id != "bugui" && id != "kayon";
        }

        private void ActorCreated(BattleUnit actor, OwnedUnit owned)
        {
            if (actor == null || badges.ContainsKey(actor)) return;
            var root = Rect("energy-status-" + actor.UnitId, layer, new Vector2(58, 26));
            var dark = Box("energy-outline", root, new Vector2(54, 5), new Color(.025f, .045f, .10f));
            var energy = Box("energy", dark.rectTransform, new Vector2(50, 2), new Color(.74f, .49f, 1f));
            energy.rectTransform.anchorMin = energy.rectTransform.anchorMax = new Vector2(0, .5f);
            energy.rectTransform.pivot = new Vector2(0, .5f); energy.rectTransform.anchoredPosition = new Vector2(2, 0);
            var status = Label(root, "", new Vector2(68, 14), 8); status.rectTransform.anchoredPosition = new Vector2(0, 22);
            var view = actor.GetComponent<UnitPresentation>();
            if (owned.Definition.Id == "flower" && view != null)
            {
                if (flower == null)
                {
                    Sprite bloom = PixelSprite("flower");
                    flower = new UnitArt { Id = "flower", Portrait = bloom, WorldWidth = .4f, ReferencePixelWidth = 15,
                        IdleDirections = Enumerable.Repeat(bloom, 8).ToArray(), AttackDirections = Enumerable.Repeat(bloom, 8).ToArray() };
                }
                view.Configure(actor, match.Mapper, flower);
            }
            badges[actor] = new Badge { Root = root, Energy = energy.rectTransform, Status = status, View = view };
        }

        private void Attacked(BattleUnit actor, BattleUnit target, bool projectile)
        {
            if (!projectile || !HandlesProjectile(actor) || target == null) return;
            if (CombatSimulation.IsInstantRay(actor))
            {
                // Both ends are captured when damage is applied; the ray must survive a lethal hit's round transition.
                StartCoroutine(InstantRay(actor.transform.position + Vector3.up * .5f, target.transform.position + Vector3.up * .5f));
                return;
            }
            if (actor.BaseStats.DefinitionId == "trimol" && rollingFrames.TryGetValue(actor, out int rolledFrame) && rolledFrame == Time.frameCount) return;
            volleys.TryGetValue(actor, out var previous);
            int order = previous.frame == Time.frameCount ? previous.count : 0;
            volleys[actor] = (Time.frameCount, order + 1);
            StartCoroutine(Flight(actor, target, order, CombatSimulation.GetImpactDelay(actor, target)));
        }

        private IEnumerator InstantRay(Vector3 source, Vector3 target)
        {
            var simulation = match.Simulation;
            var root = Rect("instant-ray-sepora", layer, Vector2.zero);
            Vector2 from = Point(source), to = Point(target), direction = to - from;
            Vector2 normal = direction.sqrMagnitude > .01f ? new Vector2(-direction.y, direction.x).normalized : Vector2.right;
            const int segments = 9;
            Vector2 last = from;
            for (int i = 1; i <= segments; i++)
            {
                Vector2 next = Vector2.Lerp(from, to, i / (float)segments);
                if (i < segments) next += normal * (i % 2 == 0 ? -5 : 5);
                next = Round(next);
                Vector2 delta = next - last;
                foreach (float thickness in new[] { 7f, 2f })
                {
                    var segment = Box(thickness > 2 ? "ray-glow" : "ray-core", root, new Vector2(delta.magnitude + 1, thickness),
                        thickness > 2 ? new Color(.89f, .2f, 1f, .85f) : new Color(1, .96f, 1));
                    segment.rectTransform.anchoredPosition = Round((last + next) * .5f);
                    segment.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                }
                last = next;
            }
            var contact = Box("ray-contact", root, new Vector2(9, 9), Color.white);
            contact.rectTransform.anchoredPosition = Round(to);
            // Create the complete visible ray synchronously before yielding, without any travel/windup animation.
            float elapsed = 0;
            while (elapsed < .18f && match.Simulation == simulation && match.Phase != MatchPhase.Menu)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (root != null) Destroy(root.gameObject);
        }

        private void BoulderRolled(BattleUnit actor, BoardCell from, BoardCell to, float travel)
        {
            if (actor == null || from == null || to == null) return;
            rollingFrames[actor] = Time.frameCount;
            StartCoroutine(RollingRock(match.Mapper.GetWorldPosition(from), match.Mapper.GetWorldPosition(to), Mathf.Max(.1f, travel), true));
        }

        private IEnumerator RollingRock(Vector3 from, Vector3 to, float duration, bool large)
        {
            var simulation = match.Simulation;
            var root = Rect("rolling-rock-trimol", layer, Vector2.zero);
            var shadow = Box("rock-shadow", root, new Vector2(large ? 35 : 24, 10), new Color(0, 0, 0, .35f));
            shadow.sprite = PixelSprite("rock-shadow");
            var rock = Box("rolling-rock", root, Vector2.one * (large ? 38 : 27), Color.white);
            rock.sprite = PixelSprite("trimol");
            var dust = new Image[3];
            for (int i = 0; i < dust.Length; i++) dust[i] = Box("rock-dust", root, Vector2.one * (5 - i), new Color(.76f, .68f, .49f, .65f));
            Vector2 start = Point(from), end = Point(to), direction = (end - start).normalized;
            float elapsed = 0;
            while (elapsed < duration && Current(simulation))
            {
                elapsed += Time.deltaTime; float t = Mathf.Clamp01(elapsed / duration);
                root.anchoredPosition = Round(Vector2.Lerp(start, end, t));
                rock.rectTransform.anchoredPosition = new Vector2(0, (large ? 14 : 10) + Mathf.Round(Mathf.Abs(Mathf.Sin(t * Mathf.PI * 5)) * 2));
                rock.rectTransform.localRotation = Quaternion.Euler(0, 0, -t * Vector2.Distance(start, end) * (direction.x < 0 ? -1 : 1) * 6);
                for (int i = 0; i < dust.Length; i++)
                {
                    float offset = 8 + Mathf.Repeat(elapsed * 35 + i * 8, 24);
                    dust[i].rectTransform.anchoredPosition = Round(-direction * offset + Vector2.up * (i % 2 == 0 ? 2 : -2));
                    dust[i].color = new Color(.76f, .68f, .49f, (1 - offset / 36) * .65f);
                }
                yield return null;
            }
            if (root != null) Destroy(root.gameObject);
        }

        private IEnumerator Flight(BattleUnit actor, BattleUnit target, int order, float impactDelay)
        {
            CombatSimulation simulation = match.Simulation;
            string id = actor.BaseStats.DefinitionId;
            float windup = CombatSimulation.GetAttackWindup(actor), elapsed = 0;
            int attackVersion=actor.AttackResetVersion;
            while (elapsed < windup && Current(simulation) && actor!=null && actor.AttackResetVersion==attackVersion) { elapsed += Time.deltaTime; yield return null; }
            if (!Current(simulation) || actor == null || target == null || actor.AttackResetVersion!=attackVersion) yield break;
            if (id == "trimol")
            {
                yield return RollingRock(actor.transform.position, target.transform.position, Mathf.Max(.1f, impactDelay - windup), false);
                yield break;
            }
            var style = projectiles?.Get(id);
            var image = Box("documented-projectile-" + id, layer, new Vector2(24, 24), Color.white);
            Vector3 start = actor.transform.position + Vector3.up * .5f;
            float travel = Mathf.Max(.1f, impactDelay - windup); elapsed = 0;
            while (elapsed < travel && Current(simulation) && target != null)
            {
                elapsed += Time.deltaTime; float t = Mathf.Clamp01(elapsed / travel);
                Vector3 end = target.transform.position + Vector3.up * .5f;
                Vector2 p = Point(Vector3.Lerp(start, end, t));
                // Separate twin/triple trajectories even if they strike the same captured target.
                float spread = order % 2 == 0 ? -1 : 1;
                p += new Vector2(spread * Mathf.Sin(t * Mathf.PI) * (6 + order * 3), Mathf.Sin(t * Mathf.PI) * 3);
                image.rectTransform.anchoredPosition = Round(p);
                if (style != null && style.Flight != null && style.Flight.Length > 0)
                {
                    image.sprite = style.Flight[Mathf.FloorToInt(elapsed * 16) % style.Flight.Length];
                    image.rectTransform.sizeDelta = image.sprite.rect.size * (style.DisplaySize / Mathf.Max(1, style.ReferencePixels));
                }
                else
                {
                    image.sprite = PixelSprite(id);
                    if (id == "hymay")
                    {
                        Vector2 origin = Point(start), direction = p - origin;
                        image.rectTransform.anchoredPosition = Round((origin + p) * .5f);
                        image.rectTransform.sizeDelta = new Vector2(Mathf.Max(8, direction.magnitude), 9);
                        image.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                    }
                    else image.rectTransform.localRotation = Quaternion.Euler(0, 0, id == "trimol" ? elapsed * 300 : 0);
                }
                yield return null;
            }
            if (image != null) Destroy(image.gameObject);
        }

        private void AbilityUsed(BattleUnit actor, string kind)
        {
            if (actor == null || layer == null) return;
            string word = kind == "bugaloo-super" ? "PROVOCA" : kind == "sepora-stealth" ? "OCULTO" :
                kind == "tokoro-sleep" ? "ZZZ" : kind == "blotan-income" ? "+1" : kind == "tsu-root" ? "RAIZ" :
                kind == "trimol-boulder" ? "ROCA" : "SUPER";
            Color color = kind.Contains("flo") ? new Color(.5f, 1, .65f) : kind.Contains("sepora") ? new Color(1, .4f, .85f) : new Color(1, .82f, .25f);
            StartCoroutine(Pulse(actor.transform.position + Vector3.up * .65f, word, color));
        }

        private void Impacted(BattleUnit actor, BattleUnit target, int damage)
        {
            if (actor == null || target == null || damage <= 0 || !HandlesProjectile(actor)) return;
            StartCoroutine(Pulse(target.transform.position + Vector3.up * .55f, "", actor.BaseStats.DefinitionId == "jazar" ? new Color(.65f, 1, .15f) : new Color(.7f, .9f, 1)));
        }

        private IEnumerator Pulse(Vector3 position, string word, Color color)
        {
            var simulation = match.Simulation;
            var root = Rect("ability-feedback", layer, new Vector2(32, 32));
            var sparks = new Image[4];
            for (int i = 0; i < 4; i++) sparks[i] = Box("spark", root, new Vector2(4, 4), color);
            Text label = word.Length > 0 ? Label(root, word, new Vector2(100, 18), 11) : null;
            if (label != null) label.color = color;
            float elapsed = 0;
            while (elapsed < .55f && match.Simulation == simulation && match.Phase != MatchPhase.Menu)
            {
                elapsed += Time.deltaTime; float t = Mathf.Clamp01(elapsed / .55f);
                root.anchoredPosition = Round(Point(position) + Vector2.up * t * 12);
                for (int i = 0; i < 4; i++)
                {
                    float angle = i * Mathf.PI * .5f;
                    sparks[i].rectTransform.anchoredPosition = Round(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (8 + t * 21));
                    sparks[i].color = new Color(color.r, color.g, color.b, 1 - t);
                }
                if (label != null) { label.rectTransform.anchoredPosition = new Vector2(0, 22); label.color = new Color(color.r, color.g, color.b, 1 - t * .7f); }
                yield return null;
            }
            if (root != null) Destroy(root.gameObject);
        }

        private void LateUpdate()
        {
            if (match == null || layer == null) return;
            layer.gameObject.SetActive(match.Phase != MatchPhase.Menu);
            foreach (var pair in badges.ToArray())
            {
                BattleUnit unit = pair.Key; Badge badge = pair.Value;
                if (unit == null) { if (badge.Root != null) Destroy(badge.Root.gameObject); badges.Remove(pair.Key); continue; }
                bool visible = unit.IsAlive && unit.gameObject.activeInHierarchy && match.Phase != MatchPhase.Menu;
                badge.Root.gameObject.SetActive(visible); if (!visible) continue;
                badge.Root.anchoredPosition = Round(Point(unit.transform.position + Vector3.up * 1.38f) + Vector2.down * 2);
                badge.Energy.parent.gameObject.SetActive(unit.BaseStats.EnergyMax > 0);
                badge.Energy.sizeDelta = new Vector2(50 * Mathf.Clamp01(unit.Energy / Mathf.Max(1, unit.BaseStats.EnergyMax)), 2);
                badge.Status.text = unit.IsSleeping ? "ZZZ" : unit.IsStunned ? "ATURD." : unit.IsRooted ? "RAIZ" : unit.IsInvisible ? "OCULTO" : unit.Shield > 0 ? "ESC " + unit.Shield : "";
                badge.Status.color = unit.IsInvisible ? new Color(.85f, .55f, 1) : unit.IsRooted ? new Color(1, .55f, .8f) : new Color(1, .88f, .4f);
                if (badge.View != null && badge.View.Renderer != null && !unit.IsReviving)
                {
                    Color tint = badge.View.Renderer.color; tint.a = unit.IsInvisible ? .4f : 1;
                    badge.View.Renderer.color = tint;
                }
            }
            if (match.Simulation != visibleSimulation) { visibleSimulation = match.Simulation; groundRefresh = 0; volleys.Clear(); }
            groundRefresh -= Time.unscaledDeltaTime;
            if (groundRefresh <= 0) { groundRefresh = .1f; RefreshGround(); }
        }

        private void RefreshGround()
        {
            var wanted = new HashSet<string>();
            if (match.Phase == MatchPhase.Combat && match.Simulation != null)
                foreach (var effect in match.Simulation.GroundEffects)
                {
                    string key = effect.Kind + ":" + effect.Side + ":" + effect.Cell.X + ":" + effect.Cell.Y;
                    wanted.Add(key);
                    if (!ground.TryGetValue(key, out var icon))
                    {
                        icon = Box("ground-" + key, layer, new Vector2(26, 26), new Color(1, 1, 1, .8f));
                        icon.sprite = PixelSprite(effect.Kind); icon.transform.SetAsFirstSibling(); ground[key] = icon;
                    }
                    icon.rectTransform.anchoredPosition = Round(Point(match.Mapper.GetWorldPosition(effect.Cell)));
                }
            foreach (var pair in ground.ToArray()) if (!wanted.Contains(pair.Key)) { if (pair.Value != null) Destroy(pair.Value.gameObject); ground.Remove(pair.Key); }
        }

        private bool Current(CombatSimulation simulation) => match != null && match.Simulation == simulation && match.Phase == MatchPhase.Combat;
        private Vector2 Point(Vector3 world)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, worldCamera.WorldToScreenPoint(world), null, out Vector2 point);
            return point;
        }
        private static Vector2 Round(Vector2 p) => new Vector2(Mathf.Round(p.x), Mathf.Round(p.y));
        private static RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = size; return rect;
        }
        private static Image Box(string name, Transform parent, Vector2 size, Color color)
        {
            var image = Rect(name, parent, size).gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
        }
        private Text Label(Transform parent, string text, Vector2 size, int fontSize)
        {
            var label = Rect("status-text", parent, size).gameObject.AddComponent<Text>(); label.font = font; label.fontSize = fontSize;
            label.text = text; label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow; label.verticalOverflow = VerticalWrapMode.Overflow; return label;
        }

        // Small code-native pixel symbols: no character image is modified or redrawn here.
        private Sprite PixelSprite(string kind)
        {
            if (pixels.TryGetValue(kind, out var sprite)) return sprite;
            const int size = 20; var data = new Color32[size * size];
            Color32 purple = new Color32(241, 94, 235, 255), green = new Color32(169, 241, 61, 255), gold = new Color32(255, 208, 79, 255);
            void Fill(int x, int y, int w, int h, Color32 color)
            { for (int py = Mathf.Max(0, y); py < Mathf.Min(size, y + h); py++) for (int px = Mathf.Max(0, x); px < Mathf.Min(size, x + w); px++) data[py * size + px] = color; }
            if (kind == "flower")
            {
                Fill(8, 2, 4, 9, new Color32(57, 159, 87, 255)); Fill(3, 6, 5, 3, green); Fill(12, 8, 5, 3, green);
                Fill(5, 0, 10, 4, new Color32(185, 99, 53, 255));
                Fill(7, 9, 6, 10, Color.white); Fill(2, 12, 16, 4, Color.white); Fill(7, 12, 6, 5, gold);
            }
            else if (kind == "jazar" || kind == "radiation")
            { Fill(8, 8, 4, 4, gold); Fill(3, 12, 5, 5, green); Fill(12, 12, 5, 5, green); Fill(7, 2, 6, 5, green); }
            else if (kind == "tsu" || kind == "cone")
            { Fill(9, 1, 2, 10, new Color32(255, 239, 188, 255)); Fill(4, 10, 12, 6, purple); Fill(6, 16, 8, 3, new Color32(255, 161, 209, 255)); Fill(7, 12, 3, 4, Color.white); }
            else if (kind == "trap")
            { Fill(2, 4, 16, 4, purple); Fill(4, 2, 12, 8, purple); Fill(5, 5, 10, 2, new Color32(138, 56, 150, 255)); }
            else if (kind == "gochan")
            { Fill(3, 3, 14, 14, new Color32(160, 66, 30, 255)); Fill(5, 5, 10, 10, gold); Fill(8, 5, 4, 7, new Color32(194, 113, 28, 255)); Fill(8, 10, 7, 3, new Color32(194, 113, 28, 255)); }
            else if (kind == "trimol")
            {
                // Asymmetric lit facets make rotation visible; the stepped circular silhouette reads as a solid boulder.
                for (int y = 1; y < 19; y++) for (int x = 1; x < 19; x++)
                {
                    float dx = x - 9.5f, dy = y - 9.5f, radius = dx * dx + dy * dy;
                    if (radius > 82) continue;
                    Color32 color = radius > 59 ? new Color32(42, 48, 65, 255) :
                        x + y > 21 ? new Color32(166, 179, 196, 255) :
                        x < 8 ? new Color32(75, 83, 108, 255) : new Color32(115, 126, 149, 255);
                    data[y * size + x] = color;
                }
                Fill(6, 12, 4, 3, new Color32(205, 213, 216, 255));
                Fill(11, 6, 3, 5, new Color32(59, 69, 89, 255));
                Fill(8, 8, 5, 2, new Color32(74, 87, 112, 255));
            }
            else if (kind == "rock-shadow")
            { for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) if ((x - 9.5f) * (x - 9.5f) / 90 + (y - 9.5f) * (y - 9.5f) / 52 <= 1) data[y * size + x] = Color.white; }
            else if (kind == "hymay")
            { Fill(0, 8, 20, 4, new Color32(115, 47, 24, 255)); Fill(0, 11, 20, 2, new Color32(239, 158, 67, 255)); }
            else
            { Fill(8, 1, 5, 7, purple); Fill(6, 7, 8, 5, Color.white); Fill(5, 11, 5, 8, purple); }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "fx-pixel-" + kind, filterMode = FilterMode.Point };
            texture.SetPixels32(data); texture.Apply(false, true);
            sprite = Sprite.Create(texture, new Rect(0, 0, size, size), kind == "flower" ? new Vector2(.5f, .05f) : new Vector2(.5f, .5f), 20);
            sprite.name = "fx-pixel-" + kind; pixels[kind] = sprite; return sprite;
        }

        private void Unsubscribe()
        {
            if (match == null) return;
            match.ActorCreated -= ActorCreated; match.Attacked -= Attacked; match.AbilityUsed -= AbilityUsed; match.Impacted -= Impacted;
            match.BoulderRolled -= BoulderRolled;
        }
        private void OnDestroy()
        {
            Unsubscribe();
            foreach (Sprite sprite in pixels.Values) if (sprite != null) { Destroy(sprite.texture); Destroy(sprite); }
            if (layer != null) Destroy(layer.gameObject);
        }
    }
}
