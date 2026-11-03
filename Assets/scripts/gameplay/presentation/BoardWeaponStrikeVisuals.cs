using System;
using UnityEngine;

namespace MonsterPouch.Gameplay.Presentation
{
    [Serializable]
    public sealed class BoardWeaponStrikeVisual
    {
        public string Id;
        public Sprite Sprite;
        public float TipPixels;
        [Min(.1f)] public float LengthCells = .95f;
        [Range(.5f, 2)] public float ThicknessScale = 1.15f;
        [Range(.01f, .15f)] public float ForwardSeconds = .08f;
        [Range(0, .15f)] public float ReturnSeconds = .06f;
    }

    [CreateAssetMenu(menuName = "Monster Pouch/Board weapon strikes")]
    public sealed class BoardWeaponStrikeVisuals : ScriptableObject
    {
        public BoardWeaponStrikeVisual[] Units = Array.Empty<BoardWeaponStrikeVisual>();
        public BoardWeaponStrikeVisual Get(string id)
        {
            foreach (var entry in Units) if (entry != null && entry.Id == id) return entry;
            return null;
        }
    }

    // This clock interpolates a visual inside an already queued attack. It never
    // advances simulation, applies damage or confirms contact on its own.
    public sealed class BoardWeaponStrikePhase
    {
        public readonly float Due, Forward, Return;
        // Materialize the boundary as a float once, just like stored Elapsed.
        // Mono may otherwise compare stored float time to a wider subtraction.
        readonly float forwardStart;
        public float Elapsed { get; private set; }
        public float Returning { get; private set; }
        public bool Contact { get; private set; }
        public BoardWeaponStrikePhase(float due, float forward, float retreat)
        { Due = Mathf.Max(0, due); Forward = Mathf.Min(Due, Mathf.Max(.001f, forward)); Return = Mathf.Max(0, retreat); forwardStart = Due - Forward; }
        public void Advance(float delta, float simulationElapsed, float tick)
        {
            delta = float.IsNaN(delta) || float.IsInfinity(delta) ? 0 : Mathf.Max(0, delta);
            if (Contact) Returning += delta;
            else Elapsed = Mathf.Min(Elapsed + delta, Mathf.Max(0, simulationElapsed) + tick);
        }
        public void ConfirmContact() { if (Contact) return; Contact = true; Returning = 0; }
        public bool Visible => Contact ? Return <= 0 ? Returning <= 0 : Returning < Return : Forward > 0 && Elapsed > forwardStart;
        public bool Finished => Contact && (Return <= 0 ? Returning > 0 : Returning >= Return);
        public float Progress => Contact ? 1 - Mathf.SmoothStep(0, 1, Return <= 0 ? 0 : Returning / Return)
            : Mathf.Min(.985f, Mathf.SmoothStep(0, 1, Forward <= 0 ? 0 : (Elapsed - forwardStart) / Forward));
        public float Alpha => Contact ? 1 - (Return <= 0 ? 0 : Mathf.Clamp01(Returning / Return))
            : Mathf.Clamp01(Progress * 6);
    }

    public static class BoardWeaponStrikeRendering
    {
        // The parent carries the board's XY projection. The child rotates in cell
        // coordinates, so north/diagonal strokes lie on the board, like east/west.
        public static void Apply(Transform plane, SpriteRenderer renderer, BoardWeaponStrikeVisual art,
            Vector3 source, Vector3 target, Vector2 cellSize, float progress, float alpha)
        {
            float sx = Mathf.Max(.001f, Mathf.Abs(cellSize.x)), sy = Mathf.Max(.001f, Mathf.Abs(cellSize.y));
            Vector2 delta = new Vector2((target.x-source.x)/sx, (target.y-source.y)/sy);
            float distance = delta.magnitude;
            if (distance < .001f || art?.Sprite == null || art.TipPixels <= 0 || progress <= 0 || alpha <= 0)
            { renderer.enabled = false; return; }
            Vector2 direction = delta / distance;
            float reached = distance * Mathf.Clamp01(progress);
            float length = Mathf.Min(art.LengthCells, reached);
            float scale = length * art.Sprite.pixelsPerUnit / art.TipPixels;
            plane.position = source;
            plane.rotation = Quaternion.identity;
            plane.localScale = new Vector3(sx, sy, 1);
            renderer.sprite = art.Sprite;
            renderer.transform.localPosition = direction * Mathf.Max(0, reached - length);
            renderer.transform.localRotation = Quaternion.Euler(0,0,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg);
            renderer.transform.localScale = new Vector3(scale, scale * art.ThicknessScale, 1);
            renderer.color = new Color(1,1,1,Mathf.Clamp01(alpha));
            // Keep the short stroke readable over the feet/capes in north-facing
            // attacks. Sorting changes visibility, never the effect's board height.
            renderer.sortingOrder = Mathf.Max(20, Mathf.Max(UnitPresentation.BodySortingOrder(source.y),
                UnitPresentation.BodySortingOrder(target.y)) + 1);
            renderer.enabled = true;
        }
    }
}
