namespace MonsterPouch.Gameplay.Progression
{
    /// <summary>
    /// Explicit trial approved on 2026-10-22. Calling this factory only returns
    /// detached configuration; it never starts a session or changes a profile.
    /// </summary>
    public static class TrophyLadderTrialConfiguration
    {
        // Frozen QA catalogue: this historical trial never follows the live account ladder.
        public static TrophyBoardCatalogue CreateCatalogue() => new TrophyBoardCatalogue {
            CatalogueId = "internal-qa.historical-boards.2026-10-22", Version = 1,
            Boards = new[] {
                new TrophyBoardDefinition { BoardId = "classic", Role = TrophyBoardRole.InitialProgression },
                new TrophyBoardDefinition { BoardId = "royal", Role = TrophyBoardRole.Progression },
                new TrophyBoardDefinition { BoardId = "lounge", Role = TrophyBoardRole.PaidCosmetic }
            }
        };
        public static TrophyLadderPolicy CreatePolicy() => new TrophyLadderPolicy {
            PolicyId = "internal-qa.ladder-trial.2026-10-22", Version = 1,
            InitialTrophies = 0, MinimumTrophies = 0, MinimumRule = TrophyMinimumRule.ClampToMinimum,
            DifferenceBands = new[] { new TrophyDifferenceBand { WinGain = 30, LossAmount = 5 } },
            Stages = new[] {
                new TrophyLadderStage { StageId = "trial-ball-pit", BoardId = "classic", MinimumTrophies = 0, DefeatLossOverride = 5 },
                new TrophyLadderStage { StageId = "trial-royal-court", BoardId = "royal", MinimumTrophies = 150, DefeatLossOverride = 30 }
            }
        };
    }
}
