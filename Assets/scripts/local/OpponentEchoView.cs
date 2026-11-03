using System.Collections.Generic;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Match;
using UnityEngine;

namespace MonsterPouch.Local
{
    /// <summary>A stationary revealed formation, independent of the bot's live actors.</summary>
    public sealed class OpponentEchoView : MonoBehaviour
    {
        public string UnitId { get; private set; }
        public Vector2Int SnapshotPosition { get; private set; }
        public bool IsHologram { get; private set; }
        public SpriteRenderer Renderer { get; private set; }
        public Vector3 FootPosition => transform.position;

        public void Configure(string id, Vector2Int cell, Vector3 foot, UnitArt art, bool hologram, float tileWidth = UnitPresentation.ReferenceTileWidth)
        {
            UnitId = id;
            SnapshotPosition = cell;
            IsHologram = hologram;
            transform.position = foot;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            Renderer = UnitPresentation.CreateSpriteRenderer("revealed-pose", transform);
            UnitPresentation.ApplyCharacterMaterial(Renderer,id);
            Renderer.sprite = art?.Idle(UnitFacing.South);
            Renderer.flipX = art?.Animation(UnitFacing.South)?.FlipX ?? false;
            Renderer.transform.localScale = Vector3.one * UnitPresentation.PresentationScale(art, tileWidth);
            Renderer.sortingOrder = UnitPresentation.BodySortingOrder(foot.y);
            Renderer.color = hologram ? new Color(.30f, .91f, 1, .63f) : Color.white;

            var shadow = UnitPresentation.CreateSpriteRenderer("revealed-ground-shadow", transform);
            shadow.sprite = UnitPresentation.ContactShadowSprite;
            shadow.transform.localPosition = new Vector3(0, -.035f, .02f);
            shadow.transform.localScale = UnitPresentation.ContactShadowScale(art, tileWidth);
            shadow.sortingOrder = 19;
            shadow.color = hologram ? new Color(.10f, .72f, .83f, .24f) : new Color(.015f, .025f, .035f, .55f);
        }
    }

    // The world sprites cannot inherit a screen-space Canvas transform. Their
    // lifetime still belongs to its page, including immediate hide on refresh.
    public sealed class OpponentEchoOwner : MonoBehaviour
    {
        readonly List<OpponentEchoView> views = new List<OpponentEchoView>();
        MatchController match;
        public void Configure(MatchController owner)
        {
            match = owner;
            match.Changed += OnMatchChanged;
        }
        void OnMatchChanged()
        {
            if (match == null || match.Phase != MatchPhase.Preparation) Clear();
        }
        public void Add(OpponentEchoView view) => views.Add(view);
        public void Clear()
        {
            foreach (var view in views)
            {
                if (view == null) continue;
                view.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(view.gameObject);
                else DestroyImmediate(view.gameObject);
            }
            views.Clear();
        }
        void OnDisable()
        {
            if (match != null) match.Changed -= OnMatchChanged;
            Clear();
        }
        void OnDestroy() => Clear();
    }
}
