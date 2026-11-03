using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    /// <summary>Captured once at the end of combat. Presentation never reads changing health.</summary>
    public sealed class RoundHealthContribution
    {
        public BattleUnit Unit { get; }
        public string UnitId { get; }
        public BoardSide Side { get; }
        public bool IsMonster { get; }
        public int CurrentHealth { get; }
        public float Amount { get; }
        public Vector2Int Cell { get; }
        public int Order { get; internal set; }
        internal RoundHealthContribution(BattleUnit unit)
        {
            Unit=unit; UnitId=unit.UnitId; Side=unit.Side;
            IsMonster=unit.Category==UnitCategory.Monster;
            CurrentHealth=unit.CurrentHealth;
            // Round each living Whelp up before summing, including a Whelp at 1 HP.
            Amount=IsMonster?CurrentHealth:CurrentHealth/2+CurrentHealth%2;
            Cell=unit.CurrentCell!=null?unit.CurrentCell.Coordinates:Vector2Int.zero;
        }
    }
    public sealed class RoundHealthSummary
    {
        public IReadOnlyList<RoundHealthContribution> Contributions { get; }
        public float BlueTotal { get; }
        public float RedTotal { get; }
        public float Maximum => Mathf.Max(BlueTotal,RedTotal);
        public int SlotCount { get; }
        public float Total(BoardSide side)=>side==BoardSide.Blue?BlueTotal:RedTotal;

        public static bool Eligible(BattleUnit unit) => unit!=null && unit.IsAlive &&
            !unit.IsReviving && !unit.IsCombatSummon &&
            (unit.Category==UnitCategory.Monster || unit.Category==UnitCategory.Whelp);

        public RoundHealthSummary(IEnumerable<BattleUnit> units)
        {
            var list=(units??Enumerable.Empty<BattleUnit>()).Where(Eligible).Distinct()
                .Select(u=>new RoundHealthContribution(u))
                .OrderBy(c=>c.Side).ThenByDescending(c=>c.IsMonster).ThenBy(c=>c.UnitId,StringComparer.Ordinal).ToList();
            foreach(var side in new[]{BoardSide.Blue,BoardSide.Red})
            {
                // Reserve slot zero for the Monster even when it has died.
                int nextWhelp=1;
                foreach(var item in list.Where(c=>c.Side==side)) item.Order=item.IsMonster?0:nextWhelp++;
            }
            Contributions=new ReadOnlyCollection<RoundHealthContribution>(list);
            BlueTotal=list.Where(c=>c.Side==BoardSide.Blue).Sum(c=>c.Amount);
            RedTotal=list.Where(c=>c.Side==BoardSide.Red).Sum(c=>c.Amount);
            SlotCount=list.Count==0?1:list.Max(c=>c.Order)+1;
        }

        public float SlotProgress(RoundHealthContribution contribution,float progress)
            => Mathf.Clamp01(Mathf.Clamp01(progress)*SlotCount-contribution.Order);

        public static float TransferProgress(float slotProgress)
            => Mathf.SmoothStep(0,1,Mathf.InverseLerp(.35f,1,slotProgress));

        public float PresentedTotal(BoardSide side,float progress)
            => Contributions.Where(c=>c.Side==side).Sum(c=>c.Amount*TransferProgress(SlotProgress(c,progress)));
    }
}
