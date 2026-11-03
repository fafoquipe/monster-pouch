using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        public const float RoundStampContactProgress = .42f;
        RectTransform roundTransitionRoot, roundTransitionPanel, roundEnemyCard, roundLocalCard;
        CanvasGroup roundCardsGroup, roundActionsGroup, roundSparkGroup;
        RectTransform roundTimeoutBanner;
        CanvasGroup roundTimeoutGroup;
        Image roundDimImage;
        RoundFlagIcon roundStampFlag;
        RectTransform roundSparkOrigin;
        readonly List<RectTransform> roundSparks = new List<RectTransform>();
        readonly List<RectTransform> victoryConfetti = new List<RectTransform>();
        CanvasGroup victoryConfettiGroup;
        float roundStampProgress;

        public float RoundTransitionAlpha => roundCardsGroup != null ? roundCardsGroup.alpha : 0;
        public float RoundStampProgress => roundStampProgress;
        public bool RoundFinalActionsVisible => roundActionsGroup != null && roundActionsGroup.interactable;

        int VisibleRoundWins(BoardSide side)
        {
            int wins = side == Match.LocalSide ? Match.PlayerWins : Match.BotWins;
            bool result = Match.Phase == MatchPhase.RoundResult || Match.Phase == MatchPhase.MatchResult;
            RoundPresentationSettings settings = RoundHudSettings;
            float contact = Match.RoundStampStartTime + settings.StampDuration * RoundStampContactProgress;
            if (result && Match.LastRoundWinner == side && Match.RoundResultElapsed < contact) wins--;
            return Mathf.Max(0, wins);
        }

        // RenderPage calls this when it clears the old page. No local timer or coroutine
        // survives a rebuild: the entire pose is derived from the controller's clock.
        void ResetRoundTransitionReferences()
        {
            roundTransitionRoot = roundTransitionPanel = roundEnemyCard = roundLocalCard = null;
            roundCardsGroup = roundActionsGroup = roundSparkGroup = null;
            roundDimImage = null;
            roundStampFlag = null;
            roundSparkOrigin = null;
            roundSparks.Clear();
            victoryConfetti.Clear();
            victoryConfettiGroup = null;
            roundStampProgress = 0;
            roundTimeoutBanner = null; roundTimeoutGroup = null;
            ResetRoundHealth();
        }

        void RenderRoundTransition()
        {
            if (roundTransitionRoot != null)
            {
                roundTransitionRoot.gameObject.SetActive(false);
                Destroy(roundTransitionRoot.gameObject);
            }
            ResetRoundTransitionReferences();
            if (!Match.Paused && !BattleMenuOpen) CloseModal();
            CancelBoardPointer();
            EndDragPreview();

            // The clear hit surface blocks the battle even during the opening delay.
            roundTransitionRoot = HitArea(page, "Round transition", new Rect(0, 0, 540, 960));
            roundDimImage = roundTransitionRoot.GetComponent<Image>();
            RenderRoundTimeout();
            RenderRoundHealth();
            roundTransitionPanel = Panel(roundTransitionRoot, "Round cards", new Rect(28, 305, 484, 310), Color.clear, false);
            roundCardsGroup = roundTransitionPanel.gameObject.AddComponent<CanvasGroup>();
            roundCardsGroup.interactable = false;
            roundCardsGroup.blocksRaycasts = false;

            bool final = Match.Phase == MatchPhase.MatchResult;
            bool playerWon = final ? Match.PlayerWins >= Match.WinsToWin : Match.LastRoundWinner == Match.LocalSide;
            string title = !Match.LastRoundWinner.HasValue ? "ROUND DRAW"
                : final ? (playerWon ? "VICTORYYY" : "YOU LOSE") : "ROUND " + Match.Round + " COMPLETE";
            Label(roundTransitionPanel, title, new Rect(0, 0, 484, 36), 26,
                Match.LastRoundWinner.HasValue ? gold : Color.white, FontStyle.Bold);

            string playerName = LocalDisplayName;
            roundEnemyCard = RenderRoundCard(roundTransitionPanel, Match.RivalSide, RivalDisplayName, Match.Bot.Monster.Definition.Id,
                Match.BotWins, 48, new Color32(95, 42, 53, 255), new Color32(198, 83, 91, 255));
            roundLocalCard = RenderRoundCard(roundTransitionPanel, Match.LocalSide, playerName, Match.Player.Monster.Definition.Id,
                Match.PlayerWins, 176, new Color32(32, 65, 99, 255), new Color32(86, 156, 206, 255));

            if (final)
            {
                if (playerWon) CreateVictoryConfetti();
                var actions = Panel(roundTransitionRoot, "Match actions", new Rect(52, 625, 436, 48), Color.clear, false);
                roundActionsGroup = actions.gameObject.AddComponent<CanvasGroup>();
                var rematch = Button(actions, "REMATCH", new Rect(0, 0, 278, 48), () =>
                {
                    SelectedId = null; CloseModal(); Match.Rematch();
                }, gold, ink, 17);
                rematch.name = "REVANCHA";
                Button(actions, "MENU", new Rect(292, 0, 144, 48), () =>
                {
                    SelectedId = null; CloseModal(); Match.Menu();
                }, new Color(.2f, .29f, .33f), Color.white, 16);
            }
            UpdateRoundTransition();
        }

        RectTransform RenderRoundCard(Transform parent, BoardSide side, string displayName, string monsterId,
            int wins, float y, Color background, Color accent)
        {
            bool winner = Match.LastRoundWinner == side;
            var card = Panel(parent, side == Match.RivalSide ? "Enemy round card" : "Local round card",
                new Rect(0, y, 484, 112), background, false);
            PixelBorder(card, winner ? gold : accent, winner ? 3 : 2);
            Panel(card, "Team stripe", new Rect(5, 6, 4, 100), accent, false);
            var portrait = Panel(card, "Monster portrait", new Rect(20, 18, 76, 76), new Color(0, 0, 0, .26f), false);
            PixelBorder(portrait, winner ? gold : accent, 2);
            Portrait(portrait, monsterId, new Rect(4, 4, 68, 68));

            var name = Label(card, displayName, new Rect(112, 28, 202, 30), 22,
                winner ? gold : Color.white, FontStyle.Bold, TextAnchor.MiddleLeft);
            name.supportRichText = false;
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 12;
            name.resizeTextMaxSize = 22;
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            Label(card, winner ? "WINNER" : side == Match.LocalSide ? "YOU" : "OPPONENT",
                new Rect(113, 61, 188, 22), 12, winner ? gold : new Color(.78f, .81f, .86f), FontStyle.Bold, TextAnchor.MiddleLeft);

            int count = Mathf.Max(1, Match.WinsToWin);
            float gap = RoundHudSettings.spacingBetweenFlags;
            if(float.IsNaN(gap) || float.IsInfinity(gap)) gap = 7;
            gap = Mathf.Min(Mathf.Max(0, gap), 152f / (count * 2));
            float flagWidth = Mathf.Min(42, (152 - gap * (count - 1)) / count);
            float flagHeight = flagWidth * RoundFlagIcon.PixelHeight / RoundFlagIcon.PixelWidth;
            float start = 468 - count * flagWidth - (count - 1) * gap;
            int previouslyWon = winner ? Mathf.Max(0, wins - 1) : wins;
            for (int i = 0; i < count; i++)
            {
                var slot = Panel(card, "Round flag " + (i + 1),
                    new Rect(start + i * (flagWidth + gap), (112 - flagHeight) * .5f, flagWidth, flagHeight), Color.clear, false);
                // Stamp around the center without moving its resting layout.
                Vector2 position = slot.anchoredPosition;
                slot.pivot = new Vector2(.5f, .5f);
                slot.anchoredPosition = position + new Vector2(flagWidth * .5f, -flagHeight * .5f);
                var flag = slot.gameObject.AddComponent<RoundFlagIcon>();
                flag.Won = i < previouslyWon;
                if (winner && i == wins - 1)
                {
                    roundStampFlag = flag;
                    roundSparkOrigin = Panel(card, "Flag sparkle origin",
                        new Rect(start + i * (flagWidth + gap) + flagWidth * .5f, 56, 0, 0), Color.clear, false);
                    roundSparkGroup = roundSparkOrigin.gameObject.AddComponent<CanvasGroup>();
                    roundSparkGroup.blocksRaycasts = false;
                    for (int s = 0; s < 8; s++)
                    {
                        var spark = Panel(roundSparkOrigin, "Pixel sparkle", new Rect(0, 0, 4, 4), gold, false);
                        Panel(spark, "Shine", new Rect(1, -2, 2, 8), new Color(1, .94f, .65f), false);
                        Panel(spark, "Shine", new Rect(-2, 1, 8, 2), new Color(1, .94f, .65f), false);
                        roundSparks.Add(spark);
                    }
                }
            }
            // Draw the winner's sparkles above all flags, even when it is flag one.
            if (winner && roundSparkOrigin != null) roundSparkOrigin.SetAsLastSibling();
            return card;
        }

        void UpdateRoundTransition()
        {
            if (roundTransitionRoot == null || !roundTransitionRoot.gameObject.activeInHierarchy) return;
            if (Match.Phase != MatchPhase.RoundResult && Match.Phase != MatchPhase.MatchResult) return;
            RoundPresentationSettings settings = RoundHudSettings;
            float elapsed = Match.RoundResultElapsed;
            UpdateRoundTimeout();
            UpdateRoundHealth();
            float entrance = SegmentProgress(elapsed - Match.RoundCardsStartTime, settings.PanelDuration);
            float eased = Mathf.SmoothStep(0, 1, entrance);
            float exitStart = Match.RoundStampStartTime + settings.StampDuration + settings.AfterStampDelay;
            float exit = Match.Phase == MatchPhase.RoundResult
                ? Mathf.SmoothStep(0, 1, SegmentProgress(elapsed - exitStart, settings.NextRoundDelay)) : 0;
            roundCardsGroup.alpha = eased * (1-exit);
            bool lostMatch = Match.Phase == MatchPhase.MatchResult && Match.BotWins >= Match.WinsToWin;
            roundDimImage.color = lostMatch
                ? new Color(.20f, .055f, .075f, .70f * eased)
                : new Color(.025f, .035f, .06f, .65f * eased * (1-exit));
            roundTransitionPanel.anchoredPosition = new Vector2(28, -305 - (1 - eased) * 24 + exit * 18);
            roundTransitionPanel.localScale = Vector3.one * (1 - (1-eased)*.045f - exit*.025f);
            roundEnemyCard.anchoredPosition = new Vector2(Mathf.Round((1 - eased) * 36), -48);
            roundLocalCard.anchoredPosition = new Vector2(-Mathf.Round((1 - eased) * 36), -176);

            bool stampStarted = elapsed >= Match.RoundStampStartTime;
            roundStampProgress = SegmentProgress(elapsed - Match.RoundStampStartTime, settings.StampDuration);
            if (roundStampFlag != null)
            {
                roundStampFlag.Won = stampStarted && roundStampProgress >= RoundStampContactProgress;
                roundStampFlag.rectTransform.localScale = stampStarted ? RoundStampScale(roundStampProgress) : Vector3.one;
            }
            UpdateRoundSparkles(stampStarted ? roundStampProgress : 0);
            UpdateVictoryConfetti(elapsed - Match.RoundStampStartTime - settings.StampDuration, settings.ConfettiDuration);

            if (roundActionsGroup != null)
            {
                float actionStart = Match.RoundStampStartTime + settings.StampDuration + settings.AfterStampDelay;
                bool ready = elapsed >= actionStart;
                roundActionsGroup.alpha = Mathf.SmoothStep(0,1,(elapsed-actionStart)/.22f);
                roundActionsGroup.interactable = ready && !Match.Paused;
                roundActionsGroup.blocksRaycasts = ready && !Match.Paused;
            }
        }

        void RenderRoundTimeout()
        {
            if (!Match.UsesHealthTiebreaker) return;
            roundTimeoutBanner = Panel(roundTransitionRoot, "Timeout banner",
                new Rect(62, 350, 416, 140), new Color32(28, 31, 43, 248), false);
            roundTimeoutBanner.pivot = new Vector2(.5f, .5f);
            roundTimeoutBanner.anchoredPosition = new Vector2(270, -420);
            PixelBorder(roundTimeoutBanner, gold, 4);
            Panel(roundTimeoutBanner, "Timeout divider", new Rect(100, 85, 216, 3), gold, false);
            Label(roundTimeoutBanner, Match.Simulation?.EronyaRoundCompared==true&&!Match.Simulation.EronyaRoundTimedOut?GameText.Results:GameText.TimeoutTitle, new Rect(8, 12, 400, 64), 40, gold, FontStyle.Bold);
            Label(roundTimeoutBanner, GameText.Tiebreaker, new Rect(8, 96, 400, 30), 22, Color.white, FontStyle.Bold);
            roundTimeoutGroup = roundTimeoutBanner.gameObject.AddComponent<CanvasGroup>();
            roundTimeoutGroup.interactable = roundTimeoutGroup.blocksRaycasts = false;
        }

        void UpdateRoundTimeout()
        {
            if (roundTimeoutBanner == null) return;
            float age = Match.RoundResultElapsed, duration = RoundHudSettings.TimeoutBannerDuration;
            bool visible = age < duration;
            roundTimeoutBanner.gameObject.SetActive(visible);
            if (!visible) return;
            roundTimeoutGroup.alpha = Mathf.Clamp01((duration - age) / .15f);
            roundTimeoutBanner.localScale = Vector3.one * Mathf.Lerp(.94f, 1, Mathf.SmoothStep(0, 1, age / .16f));
        }

        void CreateVictoryConfetti()
        {
            var layer = Panel(roundTransitionRoot, "Victory confetti", new Rect(0,0,540,960), Color.clear, false);
            victoryConfettiGroup = layer.gameObject.AddComponent<CanvasGroup>();
            victoryConfettiGroup.interactable = victoryConfettiGroup.blocksRaycasts = false;
            victoryConfettiGroup.alpha = 0;
            Color[] colors = { gold, Color.white, new Color32(86,156,206,255), new Color32(102,182,133,255) };
            for (int i=0;i<32;i++)
                victoryConfetti.Add(Panel(layer, "Confetti pixel", new Rect(0,0,4+i%3,7+i%4), colors[i%colors.Length], false));
        }
        void UpdateVictoryConfetti(float age, float duration)
        {
            if (victoryConfettiGroup == null) return;
            bool active = duration > 0 && age >= 0 && age < duration;
            victoryConfettiGroup.alpha = active ? Mathf.Min(1,(duration-age)/.5f) : 0;
            if (!active) return;
            for (int i=0;i<victoryConfetti.Count;i++)
            {
                float progress = Mathf.Clamp01((age/duration-i%5*.025f)/.9f);
                float x = 18+(i*137)%504 + Mathf.Sin(progress*9+i)*14;
                float y = -30+progress*(990+i%4*18);
                victoryConfetti[i].anchoredPosition = new Vector2(Mathf.Round(x),-Mathf.Round(y));
                victoryConfetti[i].localRotation = Quaternion.Euler(0,0,i*29+progress*240);
            }
        }

        void UpdateRoundSparkles(float progress)
        {
            if (roundSparkGroup == null) return;
            float burst = Mathf.Clamp01((progress - RoundStampContactProgress) / .55f);
            roundSparkGroup.alpha = progress >= RoundStampContactProgress && progress < .97f ? 1 - burst : 0;
            for (int i = 0; i < roundSparks.Count; i++)
            {
                float angle = i * Mathf.PI * .25f + Mathf.PI * .125f;
                float radius = Mathf.Lerp(18, 45, burst);
                roundSparks[i].anchoredPosition = new Vector2(Mathf.Round(Mathf.Cos(angle) * radius), Mathf.Round(Mathf.Sin(angle) * radius));
                roundSparks[i].localScale = Vector3.one * (burst < .55f ? 1 : .5f);
            }
        }

        static float SegmentProgress(float elapsed, float duration)
        {
            return duration <= 0 ? (elapsed >= 0 ? 1 : 0) : Mathf.Clamp01(elapsed / duration);
        }

        static Vector3 RoundStampScale(float progress)
        {
            // Oversize approach, a brief squash on impact, rebound, then settle.
            if (progress < RoundStampContactProgress)
                return Vector3.Lerp(new Vector3(1.6f, 1.6f, 1), new Vector3(.98f, .82f, 1),
                    Mathf.SmoothStep(0, 1, progress / RoundStampContactProgress));
            if (progress < .72f)
                return Vector3.Lerp(new Vector3(.98f, .82f, 1), new Vector3(1.04f, 1.12f, 1),
                    Mathf.SmoothStep(0, 1, (progress - RoundStampContactProgress) / .3f));
            return Vector3.Lerp(new Vector3(1.04f, 1.12f, 1), Vector3.one,
                Mathf.SmoothStep(0, 1, (progress - .72f) / .28f));
        }
    }
}
