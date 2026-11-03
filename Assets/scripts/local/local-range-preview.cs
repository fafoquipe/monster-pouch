using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        AttackRangeGraphic attackRangePreview;
        OwnedUnit rangeDragUnit, rangeShownUnit;
        BoardCell rangeDragCell, rangeShownCell;
        bool rangePreviewDirty = true;
        Vector2Int? rangeTargetCell;
        int rangeShownRadius;
        BattleUnit selectedCombatActor;

        bool TrySelectCombatUnit(OwnedUnit owned)
        {
            if(Match.Phase!=MatchPhase.Combat||owned==null||!Match.Actors.TryGetValue(owned,out var actor)||
                actor==null||!actor.IsAlive||actor.IsReviving)return false;
            selectedCombatActor=actor;
            selectedOfferSlot=-1;
            CloseBankContents();
            PlaySound("button");
            rangePreviewDirty=true;
            return true;
        }

        void PreviewAttackRange(OwnedUnit owned, Vector2 screen)
        {
            if (!Match.Player.Owns(owned)&&Match.Player.Owned.TryGetValue(owned.Definition.Id,out var current)) owned = current;
            else if (!Match.Player.Owns(owned)&&rangeDragUnit != null && rangeDragUnit.Definition == owned.Definition)
                owned = rangeDragUnit; // Reuse an offer preview as the pointer moves within a cell.
            rangeDragUnit = owned;
            rangeDragCell = null;
            if (Contains(briefDrop,screen) || Contains(benchDrop,screen)) return;
            var world = GameCamera.ScreenToWorldPoint(new Vector3(screen.x,screen.y,-GameCamera.transform.position.z));
            if (!Match.Mapper.TryGetCell(world,out var cell) ||
                !Match.Config.IsDeploymentLegal(Match.LocalSide,cell.Coordinates)) return;
            var primary=Match.Player.FindOwned(owned.Definition.Id,false);
            if(owned.Location==UnitLocation.Bench&&primary!=owned&&primary?.Location==UnitLocation.Field)
            {
                // Fusion upgrades the existing figure without moving it or creating another attacker.
                rangeDragUnit=primary;
                if(!cell.IsBlocked)rangeDragCell=Match.Board.GetCell(primary.Deployment.x,primary.Deployment.y);
                return;
            }
            if (owned.IsPositionLocked(Match.Round) && cell.Coordinates != owned.Deployment) return;
            var occupant = Match.Player.Owned.Values.FirstOrDefault(u =>
                u.Location == UnitLocation.Field && u.Deployment == cell.Coordinates && u != owned);
            if (occupant != null && (owned.Location != UnitLocation.Field || occupant.IsPositionLocked(Match.Round))) return;
            rangeDragCell = cell;
        }

        void UpdateAttackRangePreview()
        {
            OwnedUnit owned = null;
            BoardCell cell = null;
            BattleUnit liveActor = null,liveTarget = null;
            if(Match.Phase!=MatchPhase.Combat)selectedCombatActor=null;
            if (Match.Phase == MatchPhase.Preparation && !Match.Paused && modal == null && !MapTransitionActive)
            {
                if (rangeDragUnit != null) { owned = rangeDragUnit; cell = rangeDragCell; }
                else if (SelectedOwned is OwnedUnit selected && selected.Location == UnitLocation.Field)
                { owned=selected;cell = Match.Board.GetCell(owned.Deployment.x,owned.Deployment.y); }
            }
            else if(Match.Phase==MatchPhase.Combat&&!Match.Paused&&modal==null&&!MapTransitionActive&&
                selectedCombatActor!=null&&selectedCombatActor.IsAlive&&!selectedCombatActor.IsReviving)
            {
                liveActor=selectedCombatActor;
                cell=liveActor.CurrentCell;
                liveTarget=Match.Simulation?.CurrentTarget(liveActor);
            }
            if ((owned == null && liveActor == null) || cell == null)
            {
                if (attackRangePreview != null) attackRangePreview.gameObject.SetActive(false);
                rangeShownUnit = null;
                return;
            }
            if (attackRangePreview == null)
            {
                var root = Panel(CanvasRoot,"Attack range preview",new Rect(0,0,540,960),Color.clear,false);
                // Board decoration stays below the Brief, HUD and inspection sheets.
                root.SetSiblingIndex(page.GetSiblingIndex());
                attackRangePreview = root.gameObject.AddComponent<AttackRangeGraphic>();
                attackRangePreview.raycastTarget = false;
            }
            attackRangePreview.gameObject.SetActive(true);
            bool selectionChanged = rangeShownUnit != owned || rangeShownCell != cell;
            if(liveActor!=null)
            {
                rangeShownRadius=liveActor.BaseStats.AttackRange;
                rangeTargetCell=liveTarget?.CurrentCell?.Coordinates;
            }
            else if (rangePreviewDirty || selectionChanged)
            {
                rangeShownUnit = owned;
                rangeShownCell = cell;
                rangePreviewDirty = false;
                rangeShownRadius = UnitStats.FromDefinition(owned,Match.Round).AttackRange;
                rangeTargetCell = PredictVisibleAttackTarget(owned,cell.Coordinates);
            }
            int x0 = Mathf.Max(0,cell.X-rangeShownRadius), x1 = Mathf.Min(BoardManager.Width-1,cell.X+rangeShownRadius);
            int y0 = Mathf.Max(0,cell.Y-rangeShownRadius), y1 = Mathf.Min(BoardManager.Height-1,cell.Y+rangeShownRadius);
            Vector3 a = Match.Mapper.GetWorldPosition(Match.Board.GetCell(x0,y0));
            Vector3 b = Match.Mapper.GetWorldPosition(Match.Board.GetCell(x1,y1));
            var half = new Vector3(Mathf.Abs(Match.Mapper.CellSize.x),Mathf.Abs(Match.Mapper.CellSize.y))*.5f;
            Vector2 cornerA = WorldToPortrait(Vector3.Min(a,b)-half);
            Vector2 cornerB = WorldToPortrait(Vector3.Max(a,b)+half);
            Rect bounds = Rect.MinMaxRect(Mathf.Min(cornerA.x,cornerB.x),Mathf.Min(cornerA.y,cornerB.y),
                Mathf.Max(cornerA.x,cornerB.x),Mathf.Max(cornerA.y,cornerB.y));
            Vector2 source = WorldToPortrait(liveActor!=null?liveActor.transform.position:Match.Mapper.GetWorldPosition(cell));
            Vector2? target = liveTarget!=null?WorldToPortrait(liveTarget.transform.position):rangeTargetCell.HasValue
                ? RangeTargetPortraitPoint(rangeTargetCell.Value)
                : (Vector2?)null;
            attackRangePreview.SetPreview(bounds,source,target,cell.Coordinates,rangeTargetCell,selectionChanged,liveActor!=null);
        }

        Vector2 RangeTargetPortraitPoint(Vector2Int cell) =>
            WorldToPortrait(Match.Mapper.GetWorldPosition(Match.Board.GetCell(cell.x,cell.y)));

        Vector2Int? PredictVisibleAttackTarget(OwnedUnit selected, Vector2Int destination)
        {
            // Resolve against a disposable copy of the revealed formation. Never move
            // live actors or consult the opponent's hidden preparation positions.
            var root = new GameObject("Range prediction") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var board = root.AddComponent<BoardManager>();
                var units = new List<BattleUnit>();
                BattleUnit Add(OwnedUnit owned, BoardSide side, Vector2Int at)
                {
                    var go = new GameObject(owned.Definition.Id);
                    go.transform.SetParent(root.transform,false);
                    BattleUnit actor = owned.Definition.IsMonster
                        ? (BattleUnit)go.AddComponent<MonsterUnit>() : go.AddComponent<WhelpUnit>();
                    actor.Initialize(side+"-"+owned.Definition.Id,side,UnitStats.FromDefinition(owned,Match.Round));
                    if (!board.TryOccupyCell(actor,at.x,at.y)) return null;
                    units.Add(actor);
                    return actor;
                }
                var source = Add(selected,Match.LocalSide,destination);
                foreach (var ally in Match.Player.Owned.Values)
                {
                    if (ally == selected || ally.Location != UnitLocation.Field) continue;
                    var at = ally.Deployment == destination ? selected.Deployment : ally.Deployment;
                    Add(ally,Match.LocalSide,at);
                }
                var echoes = Match.Round == 1 && Match.InitialOpponent != null
                    ? new[] { Match.InitialOpponent } : Match.PreviousOpponent.ToArray();
                foreach (var echo in echoes.GroupBy(e=>e.Position).Select(g=>g.First()))
                {
                    var definition = Match.Config.Get(echo.UnitId);
                    if (definition != null)
                        Add(echo.Inspection ?? new OwnedUnit(definition),Match.RivalSide,echo.Position);
                }
                return CombatTargetSelector.SelectTarget(board,source,units).Target?.CurrentCell?.Coordinates;
            }
            finally
            {
                root.SetActive(false);
                Destroy(root);
            }
        }
    }
}
