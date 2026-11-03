using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        const float PlacementExchangeDuration = .36f;
        RectTransform placementExchangeRoot;
        Coroutine placementExchangeRoutine;
        PouchState placementExchangePlayer;
        ExchangePiece[] placementExchangePieces;
        float placementExchangeBend;
        readonly Dictionary<Renderer, bool> exchangeRenderers = new Dictionary<Renderer, bool>();
        readonly Dictionary<CanvasGroup, ExchangeGroupState> exchangeGroups = new Dictionary<CanvasGroup, ExchangeGroupState>();
        public bool IsPlacementExchanging => placementExchangeRoot != null;

        struct ExchangeGroupState { public float Alpha; public bool Raycasts; }
        struct ExchangeEndpoint
        {
            public UnitLocation Location;
            public Vector2Int Cell;
            public int BriefSlot;
            public Sprite Sprite;
            public bool FlipX;
            public Vector2 Foot, Size, Pivot;
        }
        sealed class ExchangePiece
        {
            public OwnedUnit Unit;
            public ExchangeEndpoint From, To;
            public RectTransform Root, Shadow;
            public Image Source, Destination;
            public PlacementExchangeShadow ShadowGraphic;
        }

        void HandlePlacementMatchChanged()
        {
            if(IsPlacementExchanging && !PlacementExchangeStillCurrent())
                CancelPlacementExchange();
            rebuild = true;
        }

        bool PlacementExchangeStillCurrent()
        {
            if(Match.Phase != MatchPhase.Preparation || Match.Player != placementExchangePlayer) return false;
            return placementExchangePieces != null && placementExchangePieces.All(p =>
                Match.Player.Owned.TryGetValue(p.Unit.Definition.Id,out var current) && ReferenceEquals(current,p.Unit)
                && p.Unit.Location == p.To.Location
                && (p.To.Location != UnitLocation.Field || p.Unit.Deployment == p.To.Cell)
                && (p.To.Location != UnitLocation.Brief || p.Unit.BriefSlot == p.To.BriefSlot));
        }

        void AnimatePlacementExchange(PlacementExchange exchange)
        {
            CancelPlacementExchange();
            if(!isActiveAndEnabled || Match.Phase != MatchPhase.Preparation || effects == null) return;
            if(exchange.FirstLocation != UnitLocation.Field || exchange.SecondLocation != UnitLocation.Field
                || exchange.First.Location != UnitLocation.Field || exchange.Second.Location != UnitLocation.Field) return;
            placementExchangePlayer = Match.Player;
            placementExchangeRoot = Panel(effects,"Placement exchange",new Rect(0,0,540,960),Color.clear,false);
            placementExchangePieces = new[] {
                BuildExchangePiece(exchange.First,exchange.FirstLocation,exchange.FirstCell,exchange.FirstBriefSlot),
                BuildExchangePiece(exchange.Second,exchange.SecondLocation,exchange.SecondCell,exchange.SecondBriefSlot)
            };
            if(placementExchangePieces.Any(p => p == null)) { CancelPlacementExchange(); return; }
            UpdatePlacementExchangeBend();
            // The model has already exchanged the cells. Hide their destination
            // renderers synchronously, before the next rendered frame can show them.
            RefreshPlacementExchangeVisibility();
            foreach(var piece in placementExchangePieces) PoseExchangePiece(piece,0);
            placementExchangeRoutine = StartCoroutine(PlayPlacementExchange());
        }

        ExchangePiece BuildExchangePiece(OwnedUnit unit,UnitLocation oldLocation,Vector2Int oldCell,int oldSlot)
        {
            var unitArt = art?.Get(unit.Definition.Id);
            if(unitArt == null) return null;
            var piece = new ExchangePiece {
                Unit = unit,
                From = ExchangeGeometry(unit,unitArt,oldLocation,oldCell,oldSlot,true),
                To = ExchangeGeometry(unit,unitArt,unit.Location,unit.Deployment,unit.BriefSlot,false)
            };
            if(piece.From.Sprite == null || piece.To.Sprite == null) return null;
            piece.Root = Panel(placementExchangeRoot,"exchange-piece-"+unit.Definition.Id,new Rect(),Color.clear,false);
            piece.Shadow = Panel(piece.Root,"Exchange shadow",new Rect(),Color.clear,false);
            piece.ShadowGraphic = piece.Shadow.gameObject.AddComponent<PlacementExchangeShadow>();
            piece.ShadowGraphic.raycastTarget = false;
            piece.Source = ExchangeImage(piece.Root,"Source pose",piece.From.Sprite,piece.From.FlipX);
            piece.Destination = ExchangeImage(piece.Root,"Destination pose",piece.To.Sprite,piece.To.FlipX);
            return piece;
        }

        Image ExchangeImage(Transform parent,string name,Sprite sprite,bool flip)
        {
            var rect = Panel(parent,name,new Rect(),Color.clear,false);
            rect.pivot = new Vector2(.5f,.5f);
            rect.localScale = new Vector3(flip ? -1 : 1,1,1);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite; image.raycastTarget = false;
            return image;
        }

        ExchangeEndpoint ExchangeGeometry(OwnedUnit unit,UnitArt unitArt,UnitLocation location,Vector2Int cell,int slot,bool source)
        {
            bool field = location == UnitLocation.Field;
            Sprite sprite = field ? unitArt.Idle(UnitFacing.North) : unitArt.Portrait;
            if(sprite == null) sprite = unitArt.Portrait ?? unitArt.Idle(UnitFacing.North);
            var result = new ExchangeEndpoint { Location=location,Cell=cell,BriefSlot=slot,Sprite=sprite,
                FlipX=field && (unitArt.Animation(UnitFacing.North)?.FlipX ?? false) };
            if(sprite == null) return result;
            result.Pivot = new Vector2(sprite.pivot.x/sprite.rect.width,sprite.pivot.y/sprite.rect.height);
            if(result.FlipX) result.Pivot.x = 1-result.Pivot.x;
            if(field)
            {
                Vector3 world = Match.Mapper.GetWorldPosition(Match.Board.GetCell(cell.x,cell.y));
                result.Foot = WorldToPortrait(world);
                float scale = UnitPresentation.PresentationScale(unitArt, Match.Mapper.CellSize.x) / sprite.pixelsPerUnit;
                var right = WorldToPortrait(world+Vector3.right*sprite.rect.width*scale);
                var up = WorldToPortrait(world+Vector3.up*sprite.rect.height*scale);
                result.Size = new Vector2(Mathf.Abs(right.x-result.Foot.x),Mathf.Abs(up.y-result.Foot.y));
            }
            else
            {
                Rect fit = location == UnitLocation.Bench ? new Rect(422,825,54,46) : new Rect(52+slot*97,781,80,82);
                // Source cards still belong to the previous page; destination cards
                // are measured after RenderPage has built the new storage contents.
                var card = StorageExchangeCard(unit,location,slot,source);
                var portrait = card == null ? null : card.GetComponentsInChildren<Image>().FirstOrDefault(i => i.name == "Art");
                if(portrait != null)
                {
                    var corners = new Vector3[4]; portrait.rectTransform.GetWorldCorners(corners);
                    Vector2 topLeft = ToCanvas(RectTransformUtility.WorldToScreenPoint(null,corners[1]));
                    Vector2 bottomRight = ToCanvas(RectTransformUtility.WorldToScreenPoint(null,corners[3]));
                    fit = new Rect(topLeft,bottomRight-topLeft);
                }
                float fitScale = Mathf.Min(fit.width/sprite.rect.width,fit.height/sprite.rect.height);
                result.Size = sprite.rect.size*fitScale;
                Vector2 top = fit.center-result.Size*.5f;
                result.Foot = top+Vector2.Scale(result.Size,new Vector2(result.Pivot.x,1-result.Pivot.y));
            }
            return result;
        }

        RectTransform StorageExchangeCard(OwnedUnit unit,UnitLocation location,int slot,bool allowOffer)
        {
            if(location == UnitLocation.Field || page == null) return null;
            if(location == UnitLocation.Bench) return benchDrop;
            var cards = page.GetComponentsInChildren<RectTransform>();
            return cards.FirstOrDefault(r => r.name == "brief-"+unit.Definition.Id)
                ?? (allowOffer ? cards.FirstOrDefault(r => r.name == "offer-"+slot
                    && r.GetComponent<BriefOfferGesture>()?.UnitId == unit.Definition.Id) : null);
        }

        void RefreshPlacementExchangeVisibility()
        {
            if(!IsPlacementExchanging || placementExchangePieces == null) return;
            foreach(var piece in placementExchangePieces)
            {
                if(piece == null) continue;
                // A field piece moved into storage already lost its actor, but the
                // previous page can still contain its bar until the next rebuild.
                foreach(var oldBar in bars.Values)
                    if(oldBar != null && oldBar.name == "health-Blue-"+piece.Unit.Definition.Id)
                        ConcealExchangeGroup(oldBar);
                if(Match.Actors.TryGetValue(piece.Unit,out var actor) && actor != null)
                {
                    foreach(var renderer in actor.GetComponentsInChildren<Renderer>(true))
                    {
                        if(!exchangeRenderers.ContainsKey(renderer)) exchangeRenderers.Add(renderer,renderer.forceRenderingOff);
                        renderer.forceRenderingOff = true;
                    }
                    if(bars.TryGetValue(actor,out var bar)) ConcealExchangeGroup(bar);
                }
                ConcealExchangeGroup(StorageExchangeCard(piece.Unit,piece.From.Location,piece.From.BriefSlot,true));
                ConcealExchangeGroup(StorageExchangeCard(piece.Unit,piece.To.Location,piece.To.BriefSlot,false));
            }
        }

        void ConcealExchangeGroup(RectTransform target)
        {
            if(target == null) return;
            var group = target.GetComponent<CanvasGroup>();
            if(group == null) group = target.gameObject.AddComponent<CanvasGroup>();
            if(!exchangeGroups.ContainsKey(group))
                exchangeGroups.Add(group,new ExchangeGroupState { Alpha=group.alpha,Raycasts=group.blocksRaycasts });
            group.alpha = 0; group.blocksRaycasts = false;
        }

        IEnumerator PlayPlacementExchange()
        {
            yield return null;
            if(!IsPlacementExchanging) yield break;
            Canvas.ForceUpdateCanvases();
            foreach(var piece in placementExchangePieces)
                piece.To = ExchangeGeometry(piece.Unit,art.Get(piece.Unit.Definition.Id),piece.To.Location,piece.To.Cell,piece.To.BriefSlot,false);
            UpdatePlacementExchangeBend();
            RefreshPlacementExchangeVisibility();
            float elapsed = 0;
            while(elapsed < PlacementExchangeDuration)
            {
                if(!PlacementExchangeStillCurrent())
                { FinishPlacementExchange(); yield break; }
                elapsed += Time.unscaledDeltaTime;
                foreach(var piece in placementExchangePieces) PoseExchangePiece(piece,Mathf.Clamp01(elapsed/PlacementExchangeDuration));
                yield return null;
            }
            FinishPlacementExchange();
        }

        void PoseExchangePiece(ExchangePiece piece,float progress)
        {
            float t = Mathf.SmoothStep(0,1,progress);
            Vector2 delta = piece.To.Foot-piece.From.Foot;
            Vector2 side = delta.sqrMagnitude > .001f ? new Vector2(-delta.y,delta.x).normalized : Vector2.zero;
            Vector2 point = Vector2.Lerp(piece.From.Foot,piece.To.Foot,t)
                + side*(Mathf.Sin(t*Mathf.PI)*placementExchangeBend);
            piece.Root.anchoredPosition = new Vector2(point.x,-point.y);
            Vector2 size = Vector2.Lerp(piece.From.Size,piece.To.Size,t);
            Vector2 pivot = Vector2.Lerp(piece.From.Pivot,piece.To.Pivot,t);
            var bodyRect = new Rect(-size.x*pivot.x,-size.y*(1-pivot.y),size.x,size.y);
            foreach(var image in new[] { piece.Source,piece.Destination })
            {
                image.rectTransform.anchoredPosition = new Vector2(bodyRect.center.x,-bodyRect.center.y);
                image.rectTransform.sizeDelta = bodyRect.size;
            }
            float crossfade = piece.From.Sprite == piece.To.Sprite && piece.From.FlipX == piece.To.FlipX
                ? 0 : Mathf.SmoothStep(0,1,Mathf.InverseLerp(.58f,.92f,t));
            piece.Source.color = new Color(1,1,1,1-crossfade);
            piece.Destination.color = new Color(1,1,1,crossfade);
            float onBoard = Mathf.Lerp(piece.From.Location == UnitLocation.Field ? 1 : 0,piece.To.Location == UnitLocation.Field ? 1 : 0,t);
            float shadowWidth = Mathf.Clamp(size.x*.52f,18,50);
            SetRect(piece.Shadow,new Rect(-shadowWidth*.5f,-3,shadowWidth,6));
            piece.ShadowGraphic.color = new Color(.02f,.04f,.08f,.24f*onBoard);
        }

        void UpdatePlacementExchangeBend()
        {
            float width=placementExchangePieces.Sum(p => Mathf.Max(p.From.Size.x,p.To.Size.x));
            float distance=placementExchangePieces.Min(p => Vector2.Distance(p.From.Foot,p.To.Foot));
            // Both pieces use the same bend in opposite directions, so their
            // silhouettes separate even when exchanging adjacent board cells.
            placementExchangeBend=Mathf.Min(Mathf.Clamp(width*.28f,18,44),distance*.85f);
        }

        void CancelPlacementExchange()
        {
            if(placementExchangeRoutine != null) StopCoroutine(placementExchangeRoutine);
            FinishPlacementExchange();
        }

        void FinishPlacementExchange()
        {
            foreach(var pair in exchangeRenderers) if(pair.Key != null) pair.Key.forceRenderingOff = pair.Value;
            foreach(var pair in exchangeGroups) if(pair.Key != null)
            { pair.Key.alpha=pair.Value.Alpha; pair.Key.blocksRaycasts=pair.Value.Raycasts; }
            exchangeRenderers.Clear(); exchangeGroups.Clear();
            if(placementExchangeRoot != null)
            { placementExchangeRoot.gameObject.SetActive(false); Destroy(placementExchangeRoot.gameObject); }
            placementExchangeRoot=null; placementExchangePieces=null; placementExchangeRoutine=null; placementExchangePlayer=null;
        }

        void OnDisable()
        {
            HideBoardSelectionFeedback();
            CancelPlacementExchange();
            if(attackRangePreview != null) attackRangePreview.gameObject.SetActive(false);
        }
    }

    public sealed class PlacementExchangeShadow : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = rectTransform.rect;
            mesh.AddVert(rect.center,color,Vector2.zero);
            const int segments = 24;
            for(int i=0;i<=segments;i++)
            {
                float angle=i*Mathf.PI*2/segments;
                mesh.AddVert(rect.center+Vector2.Scale(rect.size*.5f,new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))),color,Vector2.zero);
                if(i>0) mesh.AddTriangle(0,i,i+1);
            }
        }
    }
}
