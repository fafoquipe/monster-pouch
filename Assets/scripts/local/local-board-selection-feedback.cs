using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using UnityEngine;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        Sprite boardSelectionSurfaceSprite;
        SpriteRenderer rivalSelectionSurface;
        OwnedUnit boardFeedbackUnit, selectedOfferFeedbackUnit;
        long selectedOfferFeedbackToken;
        BoardSide boardFeedbackSide;
        bool boardFeedbackFlipped;

        OwnedUnit BoardSelectionFeedbackUnit()
        {
            if (!UnitHoldAllowed || Match.Phase != MatchPhase.Preparation ||
                Match.OnlineReady || Match.PreparationLocked || Match.Player == null) return null;
            // Offer dragging deliberately does not select or buy the offer. All
            // three drag sources already share this short-lived preview context.
            if (rangeDragUnit != null) return rangeDragUnit;
            if (SelectedId == null) return null;
            if (SelectedOwned is OwnedUnit owned) return owned;
            if (!CurrentOffer(selectedOfferSlot,selectedOfferToken,out var offer) || offer.UnitId != SelectedId) return null;
            if (selectedOfferFeedbackUnit == null || selectedOfferFeedbackToken != offer.Token ||
                selectedOfferFeedbackUnit.Definition.Id != offer.UnitId)
            {
                selectedOfferFeedbackUnit = new OwnedUnit(Match.Config.Get(offer.UnitId));
                selectedOfferFeedbackToken = offer.Token;
            }
            return selectedOfferFeedbackUnit;
        }

        void UpdateBoardSelectionFeedback()
        {
            var unit = BoardSelectionFeedbackUnit();
            if (unit != boardFeedbackUnit || (unit != null &&
                (boardFeedbackSide != Match.LocalSide || boardFeedbackFlipped != Match.Mapper.Flipped)))
            {
                boardFeedbackUnit = unit;
                if (unit != null) { boardFeedbackSide = Match.LocalSide; boardFeedbackFlipped = Match.Mapper.Flipped; }
                RefreshHighlights();
            }
            if (unit == null)
            {
                if (rivalSelectionSurface != null) rivalSelectionSurface.gameObject.SetActive(false);
                return;
            }
            if (boardSelectionSurfaceSprite == null)
                boardSelectionSurfaceSprite = Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),new Vector2(.5f,.5f),1);
            if (rivalSelectionSurface == null)
                rivalSelectionSurface = CreateBoardSelectionSurface("Rival placement surface",new Color(1,.18f,.18f,.30f));
            FitBoardSelectionSurface(rivalSelectionSurface,Match.RivalSide);
        }

        SpriteRenderer CreateBoardSelectionSurface(string name,Color tint)
        {
            var root = new GameObject(name);
            root.transform.SetParent(transform,false);
            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = boardSelectionSurfaceSprite;
            renderer.sharedMaterial = Resources.Load<Material>("MonsterPouch/UnitSpriteMaterial");
            renderer.color = tint;
            // Above the board art, below the cell outlines and all character bodies.
            renderer.sortingOrder = 29;
            return renderer;
        }

        void FitBoardSelectionSurface(SpriteRenderer surface,BoardSide side)
        {
            Vector3 min = new Vector3(float.PositiveInfinity,float.PositiveInfinity,0);
            Vector3 max = new Vector3(float.NegativeInfinity,float.NegativeInfinity,0);
            foreach (var cell in Match.Board.GetAllCells())
            {
                if (cell.Side != side) continue;
                Vector3 point = Match.Mapper.GetWorldPosition(cell);
                min = Vector3.Min(min,point); max = Vector3.Max(max,point);
            }
            var size = new Vector3(Mathf.Abs(Match.Mapper.CellSize.x),Mathf.Abs(Match.Mapper.CellSize.y),0);
            Vector3 center = (min+max)*.5f;
            center.z = Match.Mapper.GetWorldPosition(Match.Board.GetCell(0,0)).z-.04f;
            surface.transform.position = center;
            Vector3 parentScale = transform.lossyScale;
            surface.transform.localScale = new Vector3((max.x-min.x+size.x)/Mathf.Abs(parentScale.x),
                (max.y-min.y+size.y)/Mathf.Abs(parentScale.y),1);
            surface.gameObject.SetActive(true);
        }

        void HideBoardSelectionFeedback()
        {
            boardFeedbackUnit = null;
            if (rivalSelectionSurface != null) rivalSelectionSurface.gameObject.SetActive(false);
            foreach (var line in highlights) if (line != null) line.SetActive(false);
        }

        void DisposeBoardSelectionFeedback()
        {
            HideBoardSelectionFeedback();
            if (boardSelectionSurfaceSprite != null) Destroy(boardSelectionSurfaceSprite);
        }
    }
}
