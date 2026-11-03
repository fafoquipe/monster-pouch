using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        readonly List<RoundFlagIcon> enemyRoundFlags = new List<RoundFlagIcon>();
        readonly List<RoundFlagIcon> playerRoundFlags = new List<RoundFlagIcon>();
        RectTransform enemyRoundHud, playerRoundHud, roundLabelRect, roundClockRect;
        Text roundLabel,enemyNameLabel,eronyaTokenLabel;
        Text cachedRoundLabel,cachedClockLabel;
        int cachedRoundNumber=int.MinValue,cachedClockSecond=int.MinValue;
        bool cachedRoundSpanish;
        OpponentReadyCheck opponentCheck;float opponentReadyProgress;int readyRound=-1;object readyPouch;
        string RivalDisplayName=>Match.IsOnline?(online!=null?online.OpponentName:"RIVAL"):"BOT";
        string LocalDisplayName=>OnlineRoom.DisplayName(PlayerPrefs.GetString("monster-pouch.player-name","Jugador"));
        readonly RoundPresentationSettings roundHudDefaults = new RoundPresentationSettings();
        RoundPresentationSettings RoundHudSettings => Match.Config.RoundUI ?? roundHudDefaults;

        void RenderRoundHud()
        {
            enemyRoundFlags.Clear();
            playerRoundFlags.Clear();
            var settings = RoundHudSettings;
            enemyRoundHud = CreateRoundHud("Enemy round HUD", RivalDisplayName, settings.enemyPanelPosition,
                202, false, enemyRoundFlags);
            playerRoundHud = CreateRoundHud("Player round HUD",
                LocalDisplayName,
                PlayerRoundHudPosition(settings.playerPanelPosition), 184, true, playerRoundFlags);
            enemyNameLabel=enemyRoundHud.GetComponentInChildren<Text>();enemyNameLabel.name="Rival name";
            if(Match.IsOnline)
            {
                SetRect(enemyNameLabel.rectTransform,new Rect(9,40,153,21));
                var check=Panel(enemyRoundHud,"Opponent ready check",new Rect(172,38,24,24),Color.clear,false);
                opponentCheck=check.gameObject.AddComponent<OpponentReadyCheck>();
            }
            else opponentCheck=null;
            // The brief's currency and reroll controls occupy this corner in preparation.
            playerRoundHud.gameObject.SetActive(Match.Phase != MatchPhase.Preparation);
            roundLabel = Label(page, "", new Rect(374, 13, 148, 28), 21, Color.white, FontStyle.Bold, TextAnchor.MiddleRight);
            roundLabelRect = roundLabel.rectTransform;
            roundLabelRect.name = "Round label";
            roundLabel.gameObject.AddComponent<Outline>().effectColor = ink;
            roundClockRect = Panel(page, "Clock", new Rect(399, 44, 123, 44), ink, false);
            PixelBorder(roundClockRect, new Color(.40f,.64f,.65f), 2);
            clockText = Label(roundClockRect, "", new Rect(6, 1, 80, 42), 31, Color.white, FontStyle.Bold, TextAnchor.MiddleRight);
            // A small physical clock face remains readable without a symbol font.
            var face = Panel(roundClockRect, "Clock face", new Rect(92, 13, 22, 22), cyan, false);
            Panel(face, "Inset", new Rect(3, 3, 16, 16), new Color(.09f,.26f,.31f), false);
            Panel(face, "Minute hand", new Rect(10, 4, 2, 9), Color.white, false);
            Panel(face, "Hour hand", new Rect(10, 11, 7, 2), Color.white, false);
            Panel(roundClockRect, "Clock crown", new Rect(99, 8, 8, 4), gold, false);
            eronyaTokenLabel=Label(page,"",new Rect(344,94,178,20),12,gold,FontStyle.Bold,TextAnchor.MiddleRight);
            eronyaTokenLabel.name="Eronya tiebreak token";
            UpdateRoundHud();
        }

        RectTransform CreateRoundHud(string name, string playerName, Vector2 position, float width,
            bool alignRight, List<RoundFlagIcon> flags)
        {
            var root = Panel(page, name, new Rect(position.x, position.y, width, 64), new Color(.03f,.06f,.08f,.63f), false);
            var label = Label(root, playerName, new Rect(9, 40, width-18, 21), 17, Color.white, FontStyle.Bold,
                alignRight ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft);
            label.resizeTextForBestFit = true;
            label.supportRichText = false;
            label.resizeTextMinSize = 12;
            label.resizeTextMaxSize = 17;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            for(int i = 0; i < Match.WinsToWin; i++)
            {
                var slot = Panel(root, "Round flag " + (i+1), new Rect(0, 3, 28, 36), Color.clear, false);
                flags.Add(slot.gameObject.AddComponent<RoundFlagIcon>());
            }
            LayoutRoundHudFlags(flags, width, alignRight);
            return root;
        }

        void LayoutRoundHudFlags(List<RoundFlagIcon> flags, float width, bool right)
        {
            float requested = RoundHudSettings.spacingBetweenFlags;
            float gap = float.IsNaN(requested) || float.IsInfinity(requested) ? 7 : Mathf.Max(0, requested);
            if(flags.Count > 1)gap = Mathf.Min(gap, (width-18-28*flags.Count)/(flags.Count-1));
            float span = flags.Count*28 + Mathf.Max(0,flags.Count-1)*gap;
            float left = right ? width-9-span : 9;
            for(int i=0;i<flags.Count;i++)
                if(flags[i]!=null)SetRect(flags[i].rectTransform,new Rect(left+i*(28+gap),3,28,36));
        }

        void UpdateRoundHud()
        {
            if(Match.Phase == MatchPhase.Menu || roundLabel == null)return;
            if(enemyNameLabel!=null)enemyNameLabel.text=RivalDisplayName;
            if(readyRound!=Match.Round||!ReferenceEquals(readyPouch,Match.Player)){readyRound=Match.Round;readyPouch=Match.Player;opponentReadyProgress=0;}
            bool ready=Match.IsOnline&&Match.Phase==MatchPhase.Preparation&&Match.OpponentReady;
            opponentReadyProgress=Mathf.MoveTowards(opponentReadyProgress,ready?1:0,Time.unscaledDeltaTime/.32f);
            if(opponentCheck!=null)
            {
                opponentCheck.SetProgress(opponentReadyProgress);
                var rect=opponentCheck.rectTransform;
                float x=9+Mathf.Min(153,enemyNameLabel.preferredWidth)+8;
                SetRect(rect,new Rect(Mathf.Min(172,x),38,24,24));
                rect.localScale=Vector3.one*(1+.20f*Mathf.Sin(opponentReadyProgress*Mathf.PI));
            }
            if(eronyaTokenLabel!=null)
            {
                bool active=Match.Phase!=MatchPhase.Preparation&&Match.Simulation?.UsesEronyaCombat==true;
                if(Match.Phase==MatchPhase.Preparation)
                    foreach(var pouch in new[]{Match.Player,Match.Bot})
                        if(pouch!=null)foreach(var unit in pouch.AllOwned)
                            if(unit.Location==UnitLocation.Field&&unit.Definition.Ruleset==CombatRuleset.Eronya)active=true;
                eronyaTokenLabel.gameObject.SetActive(active);
                eronyaTokenLabel.text=EronyaRoundPresentation.TokenLabel(Match.EronyaTiebreakSide,Match.LocalSide,GameText.Spanish);
            }
            var settings = RoundHudSettings;
            SetRect(enemyRoundHud, new Rect(settings.enemyPanelPosition.x,settings.enemyPanelPosition.y,202,64));
            Vector2 playerPosition=PlayerRoundHudPosition(settings.playerPanelPosition);
            SetRect(playerRoundHud, new Rect(playerPosition.x,playerPosition.y,184,64));
            SetRect(roundLabelRect,new Rect(374+settings.roundLabelPositionOffset.x,13+settings.roundLabelPositionOffset.y,148,28));
            SetRect(roundClockRect,new Rect(399+settings.timerPositionOffset.x,44+settings.timerPositionOffset.y,123,44));
            bool spanish=GameText.Spanish;
            if(cachedRoundLabel!=roundLabel||cachedRoundNumber!=Match.Round||cachedRoundSpanish!=spanish)
            {
                cachedRoundLabel=roundLabel;cachedRoundNumber=Match.Round;cachedRoundSpanish=spanish;
                roundLabel.text=(spanish?"Ronda ":"Round ")+Match.Round;
            }
            if(clockText!=null)
            {
                int second=Mathf.CeilToInt(Mathf.Max(0,Match.Remaining));
                if(cachedClockLabel!=clockText||cachedClockSecond!=second)
                {cachedClockLabel=clockText;cachedClockSecond=second;clockText.text=second.ToString();}
                clockText.color = Match.Remaining <= 10 ? new Color(1,.65f,.33f) : Color.white;
            }
            LayoutRoundHudFlags(enemyRoundFlags,202,false);
            LayoutRoundHudFlags(playerRoundFlags,184,true);
            int enemyWins = VisibleRoundWins(Match.RivalSide), localWins = VisibleRoundWins(Match.LocalSide);
            for(int i=0;i<enemyRoundFlags.Count;i++)if(enemyRoundFlags[i]!=null)enemyRoundFlags[i].SetWon(i<enemyWins);
            for(int i=0;i<playerRoundFlags.Count;i++)if(playerRoundFlags[i]!=null)playerRoundFlags[i].SetWon(i<localWins);
        }

        Vector2 PlayerRoundHudPosition(Vector2 configured)
        {
            // Framing can change without rewriting the balance/presentation asset.
            var arena=Match.Mapper!=null?Match.Mapper.GetComponent<CardTableArena>():null;
            if(arena!=null&&arena.IsApplied)configured.y=Mathf.Max(configured.y,CardTableArena.BriefTop);
            return configured;
        }
    }
}
