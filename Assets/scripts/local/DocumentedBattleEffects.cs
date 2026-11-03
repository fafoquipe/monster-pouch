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
    public sealed partial class DocumentedBattleEffects : MonoBehaviour
    {
        private sealed class Badge { public RectTransform Root; public UnitPresentation View; }
        private MatchController match;
        private RectTransform canvas, layer;
        private Camera worldCamera;
        private Font font;
        private ProjectileArtCatalog projectiles;
        private ProjectileVisualRegistry projectileVisuals;
        private readonly Dictionary<BattleUnit, Badge> badges = new Dictionary<BattleUnit, Badge>();
        private readonly Dictionary<string, Image> ground = new Dictionary<string, Image>();
        private readonly Dictionary<string, Sprite> pixels = new Dictionary<string, Sprite>();
        private readonly Dictionary<BattleUnit, (int frame, int count)> volleys = new Dictionary<BattleUnit, (int, int)>();
        private readonly Dictionary<BattleUnit, int> rollingFrames = new Dictionary<BattleUnit, int>();
        // Reuse removal buffers instead of copying both dictionaries every update.
        private readonly List<BattleUnit> expiredBadges = new List<BattleUnit>(16);
        private readonly List<string> expiredGround = new List<string>(32);
        private readonly HashSet<string> wantedGround = new HashSet<string>();
        private float groundRefresh;
        private CombatSimulation visibleSimulation;
        private UnitArt flower;
        private Sprite authoredFlower;

        public void Configure(MatchController owner, RectTransform uiCanvas, Camera camera, ProjectileVisualRegistry visualRegistry = null)
        {
            Unsubscribe(); ClearGumTiles(); ClearImpactVisuals(); match = owner; canvas = uiCanvas; worldCamera = camera;
            naturalisClips.Clear();
            projectileVisuals = visualRegistry;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            projectiles = Resources.Load<ProjectileArtCatalog>("MonsterPouch/ProjectileArt");
            if (layer == null) layer = Rect("Documented battle effects", canvas, Vector2.zero);
            layer.anchorMin = Vector2.zero; layer.anchorMax = Vector2.one; layer.offsetMin = layer.offsetMax = Vector2.zero;
            match.ActorCreated += ActorCreated; match.Attacked += Attacked; match.AbilityUsed += AbilityUsed; match.Impacted += Impacted;
            match.ProjectileBounced += ContactProjectileBounced;
            match.FishShieldLaunched += FishShieldLaunched;
            match.HealingRoseLaunched += HealingRoseLaunched;
            match.BoulderRolled += BoulderRolled;
            match.ReflectedMoonLaunched += ReflectedMoonLaunched;
            match.KazuruRingLaunched += KazuruRingLaunched;
            match.DashStarted += DashStarted;
            match.FlowerCastStarted += FlowerCastStarted;
            match.ConesLaunched += ConesLaunched;
            match.ColoredGumLaunched += ColoredGumLaunched;
            match.AreaEffect += AreaEffect;
            match.Displaced += GeotharDisplaced;
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
            var view = actor.GetComponent<UnitPresentation>();
            if (owned.Definition.Id == "flower" && view != null)
            {
                if (flower == null)
                {
                    Sprite bloom = FlowerSprite();
                    flower = new UnitArt { Id = "flower", Portrait = bloom, WorldWidth = .4f,
                        ReferencePixelWidth = authoredFlower != null ? bloom.rect.width : 15,
                        IdleDirections = Enumerable.Repeat(bloom, 8).ToArray(), AttackDirections = Enumerable.Repeat(bloom, 8).ToArray() };
                }
                view.Configure(actor, match.Mapper, flower);
            }
            if (owned.Definition.Id == "kazuru")
            {
                var orbit = actor.GetComponent<KazuruRingOrbit>() ?? actor.gameObject.AddComponent<KazuruRingOrbit>();
                orbit.Configure(actor, match);
            }
            if (owned.Definition.Id == "martio")
                (actor.GetComponent<MartioCelestialOrbit>() ?? actor.gameObject.AddComponent<MartioCelestialOrbit>()).Configure(actor, match);
            badges[actor] = new Badge { Root = root, View = view };
        }

        private void Attacked(BattleUnit actor, BattleUnit target, bool projectile)
        {
            if (TryMaestroWave(match.CurrentAttackPresentation)) return;
            TryBoardWeaponStrike(actor, target);
            ContactEffects?.TrackContactFlight(match.CurrentAttackPresentation, projectile, projectileVisuals);
            if (TryVolkanarAttack(actor, target)) return;
            if (TryNaturalisBasic(actor, target)) return;
            if (actor != null && actor.BaseStats.DefinitionId == "martio")
            { actor.GetComponent<MartioCelestialOrbit>()?.Attack(); return; }
            if (actor != null && target != null && actor.BaseStats.DefinitionId == "hymay")
            {
                StartWhip(actor, target);
                return;
            }
            if (actor != null && target != null && actor.BaseStats.DefinitionId == "kazuru" && !projectile)
                actor.GetComponent<KazuruRingOrbit>()?.Strike(target);
            if (!projectile || !HandlesProjectile(actor) || target == null) return;
            if (TryAireolaFlight(actor, target)) return;
            if (TryAquarionBasicFlight(actor, target)) return;
            if (CombatSimulation.IsInstantRay(actor))
            {
                // Impacted draws the ray on the authoritative hit tick, including the first preparation after a Super.
                return;
            }
            if (actor.BaseStats.DefinitionId == "trimol" && rollingFrames.TryGetValue(actor, out int rolledFrame) && rolledFrame == Time.frameCount) return;
            volleys.TryGetValue(actor, out var previous);
            int order = previous.frame == Time.frameCount ? previous.count : 0;
            volleys[actor] = (Time.frameCount, order + 1);
            var emission = match.CurrentAttackPresentation; // Immutable context captured inside the synchronous callback.
            StartCoroutine(Flight(actor, target, order, CombatSimulation.GetImpactDelay(actor, target), -1, emission));
        }

        static float ContactCells(BattleUnit source,bool ability)
            => ImpactPresentationSettings.ContactCells(source!=null&&source.BaseStats.EnergyMax>0,ability);

        private IEnumerator InstantRay(Vector3 source, Vector3 target, bool electric=false, float contactCells=.35f)
        {
            var simulation = match.Simulation;
            var root = Rect(electric?"super-ray-stein":"instant-ray-sepora", layer, Vector2.zero);
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
                for (int pass = 0; pass < 2; pass++)
                {
                    float thickness = pass == 0 ? 7f : 2f;
                    var segment = Box(thickness > 2 ? "ray-glow" : "ray-core", root, new Vector2(delta.magnitude + 1, thickness),
                        thickness > 2 ? (electric?new Color(.15f,.72f,1,.9f):new Color(.89f,.2f,1f,.85f)) : new Color(1,.98f,1));
                    segment.rectTransform.anchoredPosition = Round((last + next) * .5f);
                    segment.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                }
                last = next;
            }
            var contact = Box("ray-contact", root, Vector2.one*ContactCellPixels(target)*contactCells, Color.white);
            contact.rectTransform.anchoredPosition = Round(to);
            // Create the complete visible ray synchronously before yielding, without any travel/windup animation.
            float elapsed = 0;
            while (elapsed < (electric?.32f:.18f) && match.Simulation == simulation && match.Phase != MatchPhase.Menu)
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
            StartCoroutine(RollingRock(actor, match.Mapper.GetWorldPosition(from), match.Mapper.GetWorldPosition(to), Mathf.Max(.1f, travel), true));
        }

        private IEnumerator RollingRock(BattleUnit actor, Vector3 from, Vector3 to, float duration, bool large)
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
                var pose=ProjectileFlightRendering.Rock(rock.sprite,large,t,Vector2.Distance(start,end),direction.x);
                pose.Apply(rock);rock.rectTransform.anchoredPosition=pose.Offset;
                float sizeRatio = SizeProjectile(rock, actor, pose.Size, large ? ProjectileVisualSource.Ability : ProjectileVisualSource.Basic);
                ProjectileFlightRendering.RockShadow(shadow.sprite,large,sizeRatio).Apply(shadow);
                for (int i = 0; i < dust.Length; i++)
                {
                    var dustPose=ProjectileFlightRendering.RockDust(i,elapsed,direction,sizeRatio);
                    dustPose.Apply(dust[i]);dust[i].rectTransform.anchoredPosition=dustPose.Offset;
                }
                yield return null;
            }
            if (root != null) Destroy(root.gameObject);
        }

        private void KazuruRingLaunched(BattleUnit actor, BattleUnit target, float travel)
        {
            ContactEffects?.TrackReleasedContactFlight(actor,target,travel);
            // The follow-up starts on the simulation release tick, without a new basic attack animation.
            if (actor != null && target != null) StartCoroutine(Flight(actor, target, 1, travel, 0, null, ProjectileVisualSizing.Resolve(projectileVisuals, actor)));
        }

        private IEnumerator Flight(BattleUnit actor, BattleUnit target, int order, float impactDelay, float releaseWindup = -1, CombatAttackPresentationSnapshot emission = null, ProjectileVisualSlot releasedSlot = null)
        {
            CombatSimulation simulation = match.Simulation;
            var visualSlot = releasedSlot ?? ProjectileVisualSizing.ResolveEmission(projectileVisuals, emission, actor, target);
            string id = actor.BaseStats.DefinitionId;
            bool stickyGum = id == "tsu" && simulation.IsStickyGumAttack(actor);
            float windup = releaseWindup >= 0 ? releaseWindup : CombatSimulation.GetAttackWindup(actor), elapsed = 0;
            int attackVersion=actor.AttackResetVersion;
            while (elapsed < windup && Current(simulation) && actor!=null && actor.AttackResetVersion==attackVersion) { elapsed += EffectDelta; yield return null; }
            if (!Current(simulation) || actor == null || target == null || actor.AttackResetVersion!=attackVersion) yield break;
            if (id == "trimol")
            {
                yield return RollingRock(actor, actor.transform.position, target.transform.position, Mathf.Max(.1f, impactDelay - windup), false);
                yield break;
            }
            if(id=="jazar")
            {
                yield return ToxicFlight(actor,target,Mathf.Max(.1f,impactDelay-windup));
                yield break;
            }
            if (stickyGum)
            {
                yield return GumFlight(actor, target, null, Mathf.Max(.1f, impactDelay - windup));
                yield break;
            }
            var style = id == "gochan" || id == "tsu" ? null : projectiles?.Get(id);
            var image = Box("documented-projectile-" + id, layer, new Vector2(24, 24), Color.white);
            Vector3 start = actor.transform.position + Vector3.up * .5f;
            float travel = Mathf.Max(.1f, impactDelay - windup); elapsed = 0;
            while (elapsed < travel && Current(simulation) && target != null)
            {
                elapsed += EffectDelta; float t = Mathf.Clamp01(elapsed / travel);
                Vector3 end = target.transform.position + Vector3.up * .5f;
                Vector2 p = Point(Vector3.Lerp(start, end, t));
                // Separate twin/triple trajectories even if they strike the same captured target.
                float spread = order % 2 == 0 ? -1 : 1;
                p += new Vector2(spread * Mathf.Sin(t * Mathf.PI) * (6 + order * 3), Mathf.Sin(t * Mathf.PI) * 3);
                image.rectTransform.anchoredPosition = Round(p);
                if (style != null && style.Flight != null && style.Flight.Length > 0)
                {
                    ProjectileFlightRendering.Catalog(style, elapsed).Apply(image);
                    if(id=="protectora-del-mausoleo" && style.OrientToTarget)
                        image.rectTransform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(Point(end).y-Point(start).y,Point(end).x-Point(start).x)*Mathf.Rad2Deg);
                    if(id=="kazuru") image.rectTransform.localRotation=Quaternion.Euler(0,0,elapsed*540);
                    if(id=="rosie")image.rectTransform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(Point(end).y-Point(start).y,Point(end).x-Point(start).x)*Mathf.Rad2Deg+Mathf.Sin(t*Mathf.PI*4)*9);

                }
                else
                {
                    image.rectTransform.sizeDelta = new Vector2(24, 24);
                    image.sprite = PixelSprite(id);
                    image.rectTransform.localRotation = Quaternion.Euler(0, 0, id == "trimol" ? elapsed * 300 : 0);
                    if(id=="gochan")
                    {
                        ProjectileFlightRendering.FireSeal(image.sprite,t).Apply(image);
                    }
                }
                SizeProjectile(image, visualSlot, image.rectTransform.sizeDelta);
                yield return null;
            }
            if (image != null) Destroy(image.gameObject);
        }

        private void AbilityUsed(BattleUnit actor, string kind)
        {
            if (actor == null || layer == null) return;
            if (IsVolkanarEffect(kind) && VolkanarArt != null) return;
            if (TryNaturalisActive(actor, kind)) return;
            if (IsAireolaAbility(kind)) return;
            if (IsAquarionAbility(kind)) return;
            if (GeotharOwns(actor) && GeotharHasArea(kind)) return;
            // The persistent lunar shields own Bugaloo's cast visual, without a generic golden burst.
            if (kind == "bugaloo-super") return;
            if (kind == "hymay-super") MarkWhipSuper(actor);
            // Income uses the actual Moonken artwork and the currency HUD flight.
            if (kind == "blotan-income") return;
            string word = kind == "tokoro-sleep" ? "ZZZ" : "";
            Color color = kind.Contains("flo") ? new Color(.5f, 1, .65f) : kind.Contains("sepora") ? new Color(1, .4f, .85f) : new Color(1, .82f, .25f);
            float duration = actor.GetComponent<UnitPresentation>()?.GetAbilityDuration(kind) ?? .55f;
            StartCoroutine(Pulse(actor.transform.position + Vector3.up * .65f, word, color, duration));
        }

        private void Impacted(BattleUnit actor, BattleUnit target, int damage)
        {
            ContactBoardWeaponStrike(actor, target);
            var contact=match.Simulation?.CurrentImpactPresentation;
            if(ContactEffects==null||!ContactEffects.ObserveContact(actor,target,damage,contact))return;
            if(actor.BaseStats.DefinitionId=="martio")return; // Authoritative MartioContacts owns each lane impact/terminal.
            if(contact?.EffectId=="stein-super")return; // Its authoritative lightning includes a contact core.
            // Preserve authored area contacts; adding a second common flash would double the impact.
            if(contact!=null&&contact.IsAbility&&
                (IsVolkanarEffect(contact.EffectId)||(IsAquarionAbility(contact.EffectId)&&contact.EffectId!=MaestroWaveFlight.EffectId)||
                 GeotharHasArea(contact.EffectId)||contact.EffectId=="korran-bastion-imperial:rupture"))return;
            // Aireola contact clips come from settled, origin-aware module events.
            if (AireolaOwns(actor)&&(contact==null||contact.IsBasic||contact.IsAbility&&IsAireolaAbility(contact.EffectId))) return;
            if (GeotharImpact(actor, target, damage)) return;
            if (actor != null && target != null)
            {
                if(CombatSimulation.IsInstantRay(actor))
                { StartCoroutine(InstantRay(actor.transform.position+Vector3.up*.5f,ContactPosition(target)+Vector3.up*.5f,false,ContactCells(actor,contact!=null&&contact.IsAbility))); return; }
                if(actor.BaseStats.DefinitionId=="hymay")
                {if(!ResolveWhipContact(actor,target))ContactEffects.PresentContact(actor,target,contact,null);return;}
                if (damage > 0 && actor.BaseStats.DefinitionId == "atong")
                { StartCoroutine(HammerGroundImpact(ContactPosition(target))); return; }
                if (damage > 0 && actor.BaseStats.DefinitionId == "popow")
                { StartCoroutine(PunchImpact(ContactPosition(target)+Vector3.up*.35f,actor.transform.position,ContactCellPixels(ContactPosition(target))*ContactCells(actor,contact!=null&&contact.IsAbility)/70f)); return; }
            }
            if (NaturalisOwns(actor?.BaseStats.DefinitionId)&&(contact==null||contact.IsBasic)) return;
            if(actor.BaseStats.DefinitionId=="jazar")
            {StartCoroutine(ToxicSplash(ContactPosition(target)+Vector3.up*.28f));return;}
            ContactEffects.PresentContact(actor,target,contact,projectiles?.Get(actor.BaseStats.DefinitionId));
        }

        private IEnumerator Pulse(Vector3 position, string word, Color color, float duration = .55f)
        {
            var simulation = match.Simulation;
            var root = Rect("ability-feedback", layer, new Vector2(32, 32));
            var sparks = new Image[4];
            for (int i = 0; i < 4; i++) sparks[i] = Box("spark", root, new Vector2(4, 4), color);
            Text label = word.Length > 0 ? Label(root, word, new Vector2(100, 18), 11) : null;
            if (label != null) label.color = color;
            float elapsed = 0;
            while (elapsed < duration && match.Simulation == simulation && match.Phase != MatchPhase.Menu)
            {
                elapsed += Time.deltaTime; float t = Mathf.Clamp01(elapsed / duration);
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
            DrawMartioFlights();
            expiredBadges.Clear();
            foreach (var pair in badges)
            {
                BattleUnit unit = pair.Key; Badge badge = pair.Value;
                if (unit == null) { if (badge.Root != null) Destroy(badge.Root.gameObject); expiredBadges.Add(pair.Key); continue; }
                bool visible = unit.IsAlive && unit.gameObject.activeInHierarchy &&
                    (match.Phase == MatchPhase.Preparation || match.Phase == MatchPhase.Combat);
                badge.Root.gameObject.SetActive(visible); if (!visible) continue;
                badge.Root.anchoredPosition = Round(Point(unit.transform.position + Vector3.up * 1.38f) + Vector2.down * 2);

            }
            foreach (var unit in expiredBadges)
            {
                badges.Remove(unit); volleys.Remove(unit); rollingFrames.Remove(unit);
            }
            expiredBadges.Clear();
            if (match.Simulation != visibleSimulation)
            {
                visibleSimulation = match.Simulation; groundRefresh = 0;
                volleys.Clear(); rollingFrames.Clear();
            }
            groundRefresh -= Time.unscaledDeltaTime;
            if (groundRefresh <= 0) { groundRefresh = .1f; RefreshGround(); RefreshGumTiles(); }
        }

        private void RefreshGround()
        {
            wantedGround.Clear();
            if (match.Phase == MatchPhase.Combat && match.Simulation != null)
                foreach (var effect in match.Simulation.GroundEffects)
                {
                    string key = effect.Kind + ":" + effect.Side + ":" + effect.Cell.X + ":" + effect.Cell.Y;
                    wantedGround.Add(key);
                    if (!ground.TryGetValue(key, out var icon))
                    {
                        icon = Box("ground-" + key, layer, effect.Kind == "cone" ? new Vector2(30, 30) : new Vector2(26, 26), new Color(1, 1, 1, effect.Kind == "cone" ? 1f : .8f));
                        icon.sprite = PixelSprite(effect.Kind); icon.transform.SetAsFirstSibling(); ground[key] = icon;
                    }
                    icon.rectTransform.anchoredPosition = Round(Point(match.Mapper.GetWorldPosition(effect.Cell)));
                }
            expiredGround.Clear();
            foreach (var pair in ground)
                if (!wantedGround.Contains(pair.Key))
                {
                    if (pair.Value != null) Destroy(pair.Value.gameObject);
                    expiredGround.Add(pair.Key);
                }
            foreach (var key in expiredGround) ground.Remove(key);
            expiredGround.Clear();
        }

        private bool Current(CombatSimulation simulation) => isActiveAndEnabled && match != null && match.Simulation == simulation && match.Phase == MatchPhase.Combat;
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

        private Sprite FlowerSprite()
        {
            if (authoredFlower == null)
                authoredFlower = Resources.LoadAll<Sprite>("MonsterPouch/Effects/Floral/flo-healing-bloom-v1").FirstOrDefault();
            // Imported art stays owned by Resources; only generated fallback sprites are disposed below.
            return authoredFlower != null ? authoredFlower : PixelSprite("flower");
        }

        // Small code-native pixel symbols: no character image is modified or redrawn here.
        private Sprite PixelSprite(string kind) => ProjectileProceduralArt.Pixel(kind, pixels);

        private void Unsubscribe()
        {
            ClearMartioVisuals();
            if (match == null) return;
            match.ActorCreated -= ActorCreated; match.Attacked -= Attacked; match.AbilityUsed -= AbilityUsed; match.Impacted -= Impacted;
            match.ProjectileBounced -= ContactProjectileBounced;
            match.FishShieldLaunched -= FishShieldLaunched;
            match.HealingRoseLaunched -= HealingRoseLaunched;
            match.BoulderRolled -= BoulderRolled;
            match.ReflectedMoonLaunched -= ReflectedMoonLaunched;
            match.KazuruRingLaunched -= KazuruRingLaunched;
            match.DashStarted -= DashStarted;
            match.FlowerCastStarted -= FlowerCastStarted;
            match.ConesLaunched -= ConesLaunched;
            match.ColoredGumLaunched -= ColoredGumLaunched;
            ClearGumTiles();
            match.AreaEffect -= AreaEffect;
            match.Displaced -= GeotharDisplaced;
        }
        private void OnDestroy()
        {
            Unsubscribe();
            ClearImpactVisuals();
            foreach (Sprite sprite in pixels.Values) if (sprite != null) { Destroy(sprite.texture); Destroy(sprite); }
            if (layer != null) Destroy(layer.gameObject);
        }
    }
}
