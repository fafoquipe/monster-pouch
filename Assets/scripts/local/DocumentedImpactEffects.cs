using System.Collections;
using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    // These effects follow combat events. They never schedule hits, alter cells or deal damage.
    public sealed partial class DocumentedBattleEffects
    {
        private sealed class WhipCast
        {
            public BattleUnit Actor, Target;
            public bool Super, Contact;
            public Vector3 ContactPoint;
            public int ResetVersion;
        }
        private readonly HashSet<RectTransform> impactVisuals = new HashSet<RectTransform>();
        private readonly List<WhipCast> whipCasts = new List<WhipCast>();
        private static readonly Color Ember = new Color(1f, .28f, .07f);
        private static readonly Color Flame = new Color(1f, .75f, .18f);
        private float EffectDelta => match != null && !match.Paused ? Time.deltaTime : 0;

        private RectTransform EffectRoot(string name)
        {
            var root = Rect(name, layer, Vector2.zero);
            impactVisuals.Add(root);
            return root;
        }
        private void ReleaseEffect(RectTransform root)
        {
            impactVisuals.Remove(root);
            if (root != null) Destroy(root.gameObject);
        }
        private void ClearImpactVisuals()
        {
            ClearBoardWeaponStrikes();
            ClearAreaVisuals();
            foreach (var root in impactVisuals) if (root != null) Destroy(root.gameObject);
            impactVisuals.Clear(); whipCasts.Clear();
        }
        private void OnDisable() { ClearImpactVisuals(); }
        private static void DrawSegment(Image segment, Vector2 start, Vector2 end, float width)
        {
            Vector2 delta = end - start;
            segment.rectTransform.anchoredPosition = Round((start + end) * .5f);
            segment.rectTransform.sizeDelta = new Vector2(Mathf.Max(1, delta.magnitude + 1), width);
            segment.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }
        private static Color Alpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);
        private Vector3 Hand(BattleUnit actor)
        {
            var view = actor != null ? actor.GetComponent<UnitPresentation>() : null;
            return (view != null && view.VisualRoot != null ? view.VisualRoot.position : actor.transform.position) + Vector3.up * .42f;
        }
        private float CellPixels(BoardCell cell)
        {
            var world = match.Mapper.GetWorldPosition(cell);
            BoardCell next = match.Board.GetCell(cell.X < BoardManager.Width - 1 ? cell.X + 1 : cell.X - 1, cell.Y);
            return next == null ? 42f : Vector2.Distance(Point(world), Point(match.Mapper.GetWorldPosition(next)));
        }

        private void StartWhip(BattleUnit actor, BattleUnit target)
        {
            if (!isActiveAndEnabled) return;
            var cast = new WhipCast { Actor = actor, Target = target, ResetVersion = actor.AttackResetVersion };
            whipCasts.Add(cast); StartCoroutine(Whip(cast));
        }
        private void MarkWhipSuper(BattleUnit actor)
        {
            for (int i = whipCasts.Count - 1; i >= 0; i--)
                if (whipCasts[i].Actor == actor && !whipCasts[i].Contact) { whipCasts[i].Super = true; break; }
        }
        private bool ResolveWhipContact(BattleUnit actor, BattleUnit target)
        {
            foreach (var cast in whipCasts)
                if (cast.Actor == actor && cast.Target == target && !cast.Contact)
                {
                    cast.Contact = true; cast.ContactPoint = ContactPosition(target) + Vector3.up * .35f;
                    StartCoroutine(PunchImpact(cast.ContactPoint, actor.transform.position, ContactCellPixels(cast.ContactPoint)*ContactCells(actor,cast.Super)/70f));
                    return true;
                }
            return false;
        }

        private IEnumerator Whip(WhipCast cast)
        {
            var simulation = match.Simulation;
            float windup = CombatSimulation.GetAttackWindup(cast.Actor);
            float contactTime = CombatSimulation.GetImpactDelay(cast.Actor, cast.Target);
            float elapsed = 0;
            while (elapsed < windup && Current(simulation) && cast.Actor != null && cast.Actor.IsAlive &&
                   cast.Actor.AttackResetVersion == cast.ResetVersion)
            { elapsed += EffectDelta; yield return null; }
            if (!Current(simulation) || cast.Actor == null || !cast.Actor.IsAlive || cast.Actor.AttackResetVersion != cast.ResetVersion)
            { whipCasts.Remove(cast); yield break; }
            var root = EffectRoot(cast.Super ? "hymay-super-whip" : "hymay-whip");
            const int count = 16;
            var outline = new Image[count]; var rope = new Image[count];
            for (int i = 0; i < count; i++) outline[i] = Box("whip-outline", root, Vector2.zero, new Color(.28f, .09f, .045f));
            for (int i = 0; i < count; i++) rope[i] = Box("whip-strand", root, Vector2.zero, new Color(1f, .69f, .23f));
            var tip = Box("whip-tip", root, new Vector2(7, 7), new Color(1, .9f, .5f));
            float recoil = 0, extension = 0;
            Vector3 lastTarget = cast.Target != null ? cast.Target.transform.position + Vector3.up * .45f : Hand(cast.Actor);
            while (root != null && Current(simulation) && cast.Actor != null)
            {
                elapsed += EffectDelta;
                if (cast.Target != null) lastTarget = cast.Target.transform.position + Vector3.up * .45f;
                // After release the strand remains attached even if its owner is reset, just like an in-flight attack.
                bool pulling=cast.Super&&cast.Target!=null&&cast.Target.IsBeingPulled;
                bool retract = !pulling&&(cast.Contact || elapsed >= contactTime + .2f);
                if (retract) recoil += EffectDelta;
                float recovery = cast.Super ? .27f : .17f;
                if (recoil >= recovery) break;
                extension = Mathf.MoveTowards(extension, 1, EffectDelta / Mathf.Max(.08f, contactTime - windup));
                float reach = pulling ? 1 : retract ? 1 - Mathf.Pow(recoil / recovery, 1.8f) : extension;
                Vector2 source = Point(Hand(cast.Actor));
                Vector2 target = Point(cast.Contact && !cast.Super ? cast.ContactPoint : lastTarget);
                Vector2 delta = target - source;
                Vector2 normal = delta.sqrMagnitude > .01f ? new Vector2(-delta.y, delta.x).normalized : Vector2.up;
                float amplitude = Mathf.Min(30, delta.magnitude * .2f) * (retract ? .7f : 1 - extension * .88f);
                Vector2 previous = source;
                for (int i = 0; i < count; i++)
                {
                    float t = (i + 1f) / count;
                    Vector2 point = source + delta * (t * reach) + normal * Mathf.Sin(t * Mathf.PI * 2 - extension * 2) * Mathf.Sin(t * Mathf.PI) * amplitude;
                    point = Round(point);
                    DrawSegment(outline[i], previous, point, cast.Super ? 7 : 5);
                    DrawSegment(rope[i], previous, point, cast.Super ? 3 : 2);
                    rope[i].color = cast.Super ? new Color(1f, .49f + .28f * (i % 2), .13f) : new Color(1f, .74f, .37f);
                    previous = point;
                }
                tip.rectTransform.anchoredPosition = Round(previous);
                tip.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
                yield return null;
            }
            ReleaseEffect(root); whipCasts.Remove(cast);
        }

        private IEnumerator PunchImpact(Vector3 position, Vector3 source, float scale = 1)
        {
            var simulation = match.Simulation;
            var root = EffectRoot("punch-contact"); root.anchoredPosition = Round(Point(position));
            Vector2 direction = (Point(position) - Point(source)).normalized;
            float angle = Mathf.Atan2(direction.y, direction.x);
            var rays = new Image[7];
            for (int i = 0; i < rays.Length; i++) rays[i] = Box("impact-spike", root, Vector2.zero, i % 2 == 0 ? new Color(1, .95f, .68f) : Flame);
            var flash = Box("impact-core", root, Vector2.one * (10 * scale), Color.white);
            float elapsed = 0;
            while (root != null && elapsed < .23f && Current(simulation))
            {
                elapsed += EffectDelta; float t = Mathf.Clamp01(elapsed / .23f);
                flash.rectTransform.sizeDelta = Vector2.one * Mathf.Round((1 - t) * 13 * scale);
                flash.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
                flash.color = Alpha(Color.white, 1 - t);
                for (int i = 0; i < rays.Length; i++)
                {
                    float theta = angle + i * Mathf.PI * 2 / rays.Length;
                    var d = new Vector2(Mathf.Cos(theta), Mathf.Sin(theta));
                    float radius = (4 + t * 18) * scale;
                    DrawSegment(rays[i], d * radius, d * (radius + (1 - t) * 13 * scale), 3);
                    rays[i].color = Alpha(i % 2 == 0 ? new Color(1, .95f, .68f) : Flame, 1 - t);
                }
                yield return null;
            }
            ReleaseEffect(root);
        }

        private IEnumerator HammerGroundImpact(Vector3 position)
        {
            var simulation = match.Simulation;
            var root = EffectRoot("atong-hammer-ground-impact"); root.anchoredPosition = Round(Point(position));
            root.localScale = Vector3.one * (ContactCellPixels(position)*MonsterPouch.Gameplay.Presentation.ImpactPresentationSettings.ContactCells(false,false)/96f);
            var cracks = new Image[12]; var wave = new Image[16]; var debris = new Image[12];
            for (int i = 0; i < cracks.Length; i++) cracks[i] = Box("ground-fracture", root, Vector2.zero, new Color(.11f, .08f, .025f, .9f));
            for (int i = 0; i < wave.Length; i++) wave[i] = Box("hammer-shockwave", root, new Vector2(6, 3), new Color(1, .86f, .38f));
            for (int i = 0; i < debris.Length; i++) debris[i] = Box("ground-chip", root, Vector2.one * (i % 2 == 0 ? 5 : 3), new Color(.88f, .64f, .26f));
            var flash = Box("hammer-contact", root, new Vector2(24, 12), Color.white);
            float elapsed = 0;
            while (root != null && elapsed < .65f && Current(simulation))
            {
                elapsed += EffectDelta; float t = Mathf.Clamp01(elapsed / .65f);
                flash.color = Alpha(new Color(1, .93f, .66f), Mathf.Clamp01(1 - t * 5));
                flash.rectTransform.sizeDelta = new Vector2(24, 12) * (1 + t * 2);
                for (int i = 0; i < cracks.Length; i++)
                {
                    float angle = (i / 2) * Mathf.PI / 3;
                    Vector2 d = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * .48f);
                    Vector2 bend = new Vector2(-d.y, d.x) * (i % 4 < 2 ? 3 : -3);
                    Vector2 middle = d * 11 + bend;
                    DrawSegment(cracks[i], i % 2 == 0 ? d * 3 : middle, i % 2 == 0 ? middle : d * 29, 3);
                    cracks[i].color = Alpha(new Color(.11f, .08f, .025f), (1 - t) * .9f);
                }
                for (int i = 0; i < wave.Length; i++)
                {
                    float angle = i * Mathf.PI * 2 / wave.Length;
                    wave[i].rectTransform.anchoredPosition = Round(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * .48f) * (10 + t * 35));
                    wave[i].color = Alpha(new Color(1, .86f, .38f), Mathf.Max(0, 1 - t * 1.7f));
                }
                for (int i = 0; i < debris.Length; i++)
                {
                    float angle = i * Mathf.PI * 2 / debris.Length;
                    debris[i].rectTransform.anchoredPosition = Round(new Vector2(Mathf.Cos(angle) * t * 33, Mathf.Sin(angle) * t * 15 + Mathf.Sin(t * Mathf.PI) * (10 + i % 3 * 5)));
                    debris[i].color = Alpha(new Color(.88f, .64f, .26f), 1 - t);
                    debris[i].rectTransform.localRotation = Quaternion.Euler(0, 0, t * 180 + i * 30);
                }
                yield return null;
            }
            ReleaseEffect(root);
        }

        private void FlowerCastStarted(BattleUnit actor, BoardCell cell, float duration)
        {
            if (isActiveAndEnabled && actor != null && cell != null && layer != null) StartCoroutine(PlantFlower(actor, cell, duration));
        }
        private IEnumerator PlantFlower(BattleUnit actor, BoardCell cell, float duration)
        {
            var simulation = match.Simulation; int version = actor.AttackResetVersion;
            var root = EffectRoot("flo-flower-planting");
            var bloom = Box("flower-in-hands", root, new Vector2(26, 26), Color.white);
            bloom.sprite = FlowerSprite(); bloom.preserveAspect = true;
            var leaves = new Image[4];
            for (int i = 0; i < leaves.Length; i++) leaves[i] = Box("plant-spark", root, new Vector2(3, 3), new Color(.65f, 1, .4f));
            float elapsed = 0;
            while (root != null && elapsed < duration && Current(simulation) && actor != null && actor.IsAlive && actor.AttackResetVersion == version)
            {
                elapsed += EffectDelta; float t = Mathf.Clamp01(elapsed / Mathf.Max(.05f, duration));
                Vector2 from = Point(Hand(actor)); Vector2 to = Point(match.Mapper.GetWorldPosition(cell)) + Vector2.up * 9;
                // First reveal the flower in Flo's hands, then lift it and plant it on the chosen tile.
                float throwT = Mathf.Clamp01((t - .5f) / .5f);
                Vector2 point = t < .5f ? from + Vector2.up * Mathf.SmoothStep(0, 12, t * 2) :
                    Vector2.Lerp(from + Vector2.up * 12, to, throwT) + Vector2.up * Mathf.Sin(throwT * Mathf.PI) * 16;
                bloom.rectTransform.anchoredPosition = Round(point);
                bloom.rectTransform.localScale = Vector3.one * Mathf.SmoothStep(.15f, 1, Mathf.Clamp01(t * 4));
                bloom.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(throwT * Mathf.PI) * -20);
                for (int i = 0; i < leaves.Length; i++)
                {
                    float angle = i * Mathf.PI * .5f + t * 2;
                    leaves[i].rectTransform.anchoredPosition = Round(point + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (7 + 4 * t));
                    leaves[i].color = Alpha(new Color(.65f, 1, .4f), Mathf.Sin(t * Mathf.PI) * .75f);
                }
                yield return null;
            }
            ReleaseEffect(root);
        }

        private void DashStarted(BattleUnit actor, BoardCell origin, BoardCell through, BoardCell destination, float widthCells, float duration)
        {
            if (isActiveAndEnabled && actor != null && origin != null && through != null && destination != null)
                StartCoroutine(FlamingDash(actor, origin, through, destination, widthCells, duration));
        }
        private IEnumerator FlamingDash(BattleUnit actor, BoardCell origin, BoardCell through, BoardCell destination, float widthCells, float duration)
        {
            var simulation = match.Simulation;
            int resetVersion=actor.AttackResetVersion;
            Vector2 from = Point(match.Mapper.GetWorldPosition(origin));
            Vector2 middle = Point(match.Mapper.GetWorldPosition(through));
            Vector2 to = Point(match.Mapper.GetWorldPosition(destination));
            float width = CellPixels(origin) * widthCells;
            var root = EffectRoot("popow-flaming-dash"); root.SetAsFirstSibling();
            var laneA = Box("dash-affected-lane", root, Vector2.zero, Alpha(Ember, .14f));
            var laneB = Box("dash-affected-lane", root, Vector2.zero, Alpha(Ember, .14f));
            DrawSegment(laneA, from, middle, width); DrawSegment(laneB, middle, to, width);
            var borders = new Image[4];
            for (int i = 0; i < borders.Length; i++) borders[i] = Box("dash-area-edge", root, Vector2.zero, Alpha(Flame, .65f));
            Vector2 nA = new Vector2(-(middle - from).y, (middle - from).x).normalized * width * .5f;
            Vector2 nB = new Vector2(-(to - middle).y, (to - middle).x).normalized * width * .5f;
            DrawSegment(borders[0], from + nA, middle + nA, 2); DrawSegment(borders[1], from - nA, middle - nA, 2);
            DrawSegment(borders[2], middle + nB, to + nB, 2); DrawSegment(borders[3], middle - nB, to - nB, 2);
            var flameRoot = EffectRoot("popow-dash-flames");
            var aura=Box("dash-contact-aura",flameRoot,Vector2.one*width,Flame);
            aura.sprite=PixelSprite("dash-aura");
            var hookOutline=new Image[14];var hookTrail=new Image[14];
            for(int i=0;i<hookTrail.Length;i++)
            {
                hookOutline[i]=Box("hook-trail-ember",flameRoot,Vector2.zero,Ember);
                hookTrail[i]=Box("hook-trail-core",flameRoot,Vector2.zero,new Color(1,.96f,.75f));
            }
            var flames = new Image[9]; var cores = new Image[9];
            for (int i = 0; i < flames.Length; i++)
            {
                flames[i] = Box("dash-flame", flameRoot, new Vector2(12, 22), Ember); flames[i].sprite = PixelSprite("dash-flame");
                cores[i] = Box("flame-core", flameRoot, new Vector2(6, 13), Flame); cores[i].sprite = PixelSprite("dash-flame");
            }
            var embers = new Image[12];
            for (int i = 0; i < embers.Length; i++) embers[i] = Box("dash-ember", flameRoot, Vector2.one * (i % 2 == 0 ? 4 : 3), Flame);
            float firstLength = Vector2.Distance(from, middle), total = firstLength + Vector2.Distance(middle, to);
            float corner = total > 0 ? firstLength / total : .5f;
            Vector2 Path(float t) => t <= corner ? Vector2.Lerp(from, middle, corner > 0 ? t / corner : 1) :
                Vector2.Lerp(middle, to, corner < 1 ? (t - corner) / (1 - corner) : 1);
            float elapsed = 0;
            while (root != null && flameRoot != null && elapsed < duration + .24f && Current(simulation) &&
                   actor!=null && actor.IsAlive && actor.isActiveAndEnabled && actor.AttackResetVersion==resetVersion)
            {
                elapsed += EffectDelta; float t = Mathf.Clamp01(elapsed / Mathf.Max(.1f, duration));
                float fade = 1 - Mathf.Clamp01((elapsed - duration) / .24f);
                Vector2 current=Path(t);
                // Paint only the wake already travelled. The bright disc shows the
                // moving contact radius; future cells are not presented as stunned.
                Vector2 firstEnd=t<=corner?current:middle;
                DrawSegment(laneA,from,firstEnd,width);
                laneB.gameObject.SetActive(t>corner);
                if(t>corner)DrawSegment(laneB,middle,current,width);
                DrawSegment(borders[0],from+nA,firstEnd+nA,2);DrawSegment(borders[1],from-nA,firstEnd-nA,2);
                borders[2].gameObject.SetActive(t>corner);borders[3].gameObject.SetActive(t>corner);
                if(t>corner){DrawSegment(borders[2],middle+nB,current+nB,2);DrawSegment(borders[3],middle-nB,current-nB,2);}
                aura.rectTransform.anchoredPosition=Round(current);
                aura.color=Alpha(Flame,.85f*fade);
                laneA.color = laneB.color = Alpha(Ember, .14f * fade);
                foreach (var edge in borders) edge.color = Alpha(Flame, .65f * fade);
                Vector2 ahead=Path(Mathf.Min(1,t+.02f))-Path(Mathf.Max(0,t-.02f));
                float heading=Mathf.Atan2(ahead.y,ahead.x);
                Vector2 fist=Path(t)+Vector2.up*24;
                // A broad hooked punch arc leads the rush. The curved white core and orange
                // rim describe the fist's sweep instead of hiding it in a cloud of particles.
                for(int i=0;i<hookTrail.Length;i++)
                {
                    float u=i/(float)hookTrail.Length,v=(i+1f)/hookTrail.Length;
                    float turn=heading-1.5f+Mathf.Sin(Mathf.Min(1,t*2)*Mathf.PI)*.55f;
                    Vector2 a=fist+new Vector2(Mathf.Cos(turn+u*3.2f),Mathf.Sin(turn+u*3.2f))*(27-u*10);
                    Vector2 b=fist+new Vector2(Mathf.Cos(turn+v*3.2f),Mathf.Sin(turn+v*3.2f))*(27-v*10);
                    DrawSegment(hookOutline[i],a,b,9-u*4);
                    DrawSegment(hookTrail[i],a,b,5-u*3);
                    hookOutline[i].color=Alpha(Ember,fade*(1-u*.65f));
                    hookTrail[i].color=Alpha(new Color(1,.96f,.75f),fade*(1-u*.7f));
                }
                for (int i = 0; i < flames.Length; i++)
                {
                    float tailT = Mathf.Max(0, t - i * .035f);
                    Vector2 p = Path(tailT) + Vector2.up * (12 + i % 3 * 9);
                    p.x += (i % 3 - 1) * 11;
                    float flicker = Mathf.FloorToInt(elapsed * 22 + i) % 3;
                    flames[i].rectTransform.anchoredPosition = Round(p);
                    flames[i].rectTransform.sizeDelta = new Vector2(15 - i * .4f, 23 + flicker * 5);
                    flames[i].color = Alpha(Ember, fade * (.9f - i * .045f));
                    cores[i].rectTransform.anchoredPosition = Round(p - Vector2.up * 3);
                    cores[i].rectTransform.sizeDelta = new Vector2(7, 15 + flicker * 2);
                    cores[i].color = Alpha(Flame, fade * .9f);
                }
                for (int i = 0; i < embers.Length; i++)
                {
                    float age = Mathf.Repeat(elapsed * 2.5f + i / 12f, 1);
                    Vector2 p = Path(Mathf.Max(0, t - age * .24f));
                    embers[i].rectTransform.anchoredPosition = Round(p + new Vector2((i % 3 - 1) * age * width * .4f, 12 + age * 24));
                    embers[i].color = Alpha(Flame, (1 - age) * fade);
                }
                yield return null;
            }
            ReleaseEffect(root); ReleaseEffect(flameRoot);
        }
    }
}
