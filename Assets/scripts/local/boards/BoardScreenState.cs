using System;

namespace MonsterPouch.Local.Boards
{
    public enum BoardOptionStatus { Unspecified, Available, LockedByTrophies, OwnedCosmetic, PaidCosmetic }

    [Serializable] public sealed class BoardSelectionOption
    {
        public string BoardId;
        public bool CanSelect;
        public BoardOptionStatus Status;
        public long? UnlockAt;
    }

    // Presentation input only. Account authority, ownership and eligibility belong
    // to the caller; this view never infers them from a balance or saved preference.
    [Serializable] public sealed class BoardScreenState
    {
        public bool Spanish;
        public long Trophies;
        public string CurrentBoardId;
        public long StageMinimum;
        public long? NextStageAt;
        public string AdjacentBoardId;
        public long AdjacentStageMinimum;
        public bool AdjacentIsNext;
        public long VictoryGain;
        public long DefeatLoss;
        public string SelectedBoardId;
        public bool SelectionDefined;
        public BoardSelectionOption[] Options = Array.Empty<BoardSelectionOption>();
    }
}
