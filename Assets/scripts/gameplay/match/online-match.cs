using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MonsterPouch.Gameplay.Board;
using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    [Serializable] public sealed class PreparationCommand
    {
        public string kind, unit;
        public int slot, index, x, y;
        public bool bench, sourceBench;
    }

    public sealed partial class MatchController
    {
        public bool IsOnline { get; private set; }
        public string HostMap { get; private set; }="classic";
        public string GuestMap { get; private set; }="classic";
        public string RoundMap => (Mathf.Max(1,Round)&1)==1?HostMap:GuestMap;
        // Player identity, not the arena selected for this round.
        public string MapForSide(BoardSide side) => side==BoardSide.Blue?HostMap:GuestMap;
        public static bool ValidOnlineMap(string id)=>id=="classic"||id=="lounge"||id=="casino-night"||id=="royal"||id=="naturalis"||id=="aquarion"||id=="geothar"||id=="volkanar"||id=="aireola";
        public bool OnlineReady { get; private set; }
        public bool OpponentReady { get; private set; }
        public bool PreparationLocked { get; private set; }
        float readyCountdown;
        public bool ToggleReady()=>SetReady(!OnlineReady);
        public bool SetReady(bool value)
        {
            if(IsTraining||Phase!=MatchPhase.Preparation||Paused||PreparationLocked||(!value&&Remaining<=0))return false;
            if(OnlineReady==value)return true;
            OnlineReady=value;readyCountdown=value?1.2f:0;Notice=value?(IsOnline?"Esperando al rival…":""):"";
            Changed?.Invoke();if(IsOnline)OnlineReadyRequested?.Invoke();return true;
        }
        public void LockPreparation(bool locked){PreparationLocked=locked;Changed?.Invoke();}
        public BoardSide LocalSide => Player?.Side ?? BoardSide.Blue;
        public BoardSide RivalSide => LocalSide==BoardSide.Blue?BoardSide.Red:BoardSide.Blue;
        public event Action OnlineReadyRequested;
        public event Action OnlineRoundFinished;
        public event Action OnlineExited;
        readonly List<PreparationCommand> onlineCommands=new List<PreparationCommand>();
        bool remoteRevealed;
        public PreparationCommand[] PreparationCommands => onlineCommands.ToArray();
        public const int MaxPreparationCommands=256;

        void RecordOnline(PreparationCommand command)
        {
            if(IsOnline)onlineCommands.Add(command);
        }

        public bool StartOnlineMatch(BoardSide localSide, string localMonster, string[] localWhelps,
            string rivalMonster, string[] rivalWhelps, int matchSeed, string localMap="classic", string rivalMap="classic",
            AbilityAccessSnapshot localAccess=null, AbilityAccessSnapshot rivalAccess=null)
        {
            if(IsOnline||Config==null||matchSeed==0||!ValidOnlineMap(localMap)||!ValidOnlineMap(rivalMap))return Reject("No se puede iniciar esta partida.");
            PouchState own,rival;
            try
            {
                int redSeed=matchSeed^0x45fa1;
                own=new PouchState(Config,localSide==BoardSide.Blue?matchSeed:redSeed,localMonster,localSide,localWhelps,localAccess??localAbilityAccess);
                var other=localSide==BoardSide.Blue?BoardSide.Red:BoardSide.Blue;
                rival=new PouchState(Config,other==BoardSide.Blue?matchSeed:redSeed,rivalMonster,other,rivalWhelps,rivalAccess??AbilityAccessSnapshot.Unrestricted);
            }
            catch(ArgumentException error){return Reject(error.Message);}
            Simulation?.Stop();Simulation=null;ClearActors();Board.BuildBoard();
            previousOpponent.Clear();InitialOpponent=null;
            IsOnline=true;ResetTrainingState();Paused=false;Time.timeScale=1;Mapper.Flipped=localSide==BoardSide.Red;
            HostMap=localSide==BoardSide.Blue?localMap:rivalMap;
            GuestMap=localSide==BoardSide.Blue?rivalMap:localMap;
            Player=own;Bot=rival;seed=matchSeed;Round=PlayerWins=BotWins=0;
            BeginRound();return true;
        }

        void ResetOnlinePreparation()
        {
            onlineCommands.Clear();OnlineReady=OpponentReady=remoteRevealed=PreparationLocked=false;readyCountdown=0;
        }
        public void SetOpponentReady(bool value=true){if(IsOnline&&Phase==MatchPhase.Preparation){OpponentReady=value;Changed?.Invoke();}}

        // Replay only legal decisions through the same finite economy. No client-supplied
        // health, damage, money or arbitrary state is accepted. Ability access is the frozen handshake snapshot.
        public bool RevealOpponent(PreparationCommand[] commands, int round)
        {
            if(!IsOnline||!OnlineReady||!OpponentReady||remoteRevealed||round!=Round||
                Phase!=MatchPhase.Preparation||commands==null||commands.Length>MaxPreparationCommands)return false;
            // Validate the complete journal privately. A bad tail cannot leave
            // earlier purchases, bank merges or random rerolls in the live pouch.
            if(!Bot.TryApplyPreparationJournal(candidate=>
            {
                foreach(var command in commands)
                    if(!ApplyOpponentDecision(candidate,command))return false;
                return true;
            }))return false;
            remoteRevealed=true;return true;
        }

        bool ApplyOpponentDecision(PouchState candidate,PreparationCommand c)
        {
            if(c==null||c.unit?.Length>64)return false;
            long token=c.slot>=0&&c.slot<candidate.Offers.Count?candidate.Offers[c.slot]?.Token??0:0;
            var position=new Vector2Int(c.x,c.y);
            switch(c.kind)
            {
                case "buy":return candidate.TryBuy(c.slot,token,out _);
                case "buy-place":
                    if(!c.bench&&(Board.GetCell(c.x,c.y)==null||Board.GetCell(c.x,c.y).IsBlocked))return false;
                    return candidate.TryBuyAndPlace(c.slot,token,c.bench?UnitLocation.Bench:UnitLocation.Field,position,out _);
                case "reroll":return candidate.TryReroll(candidate.OfferVersion,out _);
                case "upgrade":return c.index<0?candidate.TryUpgradeMonster(out _):candidate.TryUpgradeMonster(c.index,out _);
                case "choose":return candidate.TrySelectTrick(c.unit,c.index,out _);
                case "trick":return candidate.TryBuyTrick(c.unit,c.index,c.slot,token,out _);
                case "place":
                    var cell=Board.GetCell(c.x,c.y);
                    var own=candidate.FindOwned(c.unit,c.sourceBench);
                    if(cell==null||cell.IsBlocked||own==null)return false;
                    var primary=candidate.FindOwned(c.unit,false);
                    if(c.sourceBench&&primary!=own&&primary?.Location==UnitLocation.Field)
                        return Config.IsDeploymentLegal(candidate.Side,position)&&candidate.TrySetLocation(own,UnitLocation.Field,position,out _);
                    var other=candidate.Owned.Values.FirstOrDefault(u=>u!=own&&u.Location==UnitLocation.Field&&u.Deployment==position);
                    if(other!=null)return own.Location==UnitLocation.Field&&candidate.TryExchange(c.unit,other.Definition.Id,out _,out _);
                    return candidate.TrySetLocation(own,UnitLocation.Field,position,out _);
                case "store":return candidate.TrySetLocation(candidate.FindOwned(c.unit,c.sourceBench),c.bench?UnitLocation.Bench:UnitLocation.Brief,Vector2Int.zero,out _);
                case "sell":return candidate.TrySell(candidate.FindOwned(c.unit,c.sourceBench),out _);
                default:return false;
            }
        }
        public bool StartOnlineCombat(int round)
        {
            if(!IsOnline||round!=Round||!OnlineReady||!OpponentReady||!remoteRevealed||Phase!=MatchPhase.Preparation)return false;
            BeginCombat();return true;
        }
        public bool NextOnlineRound(int previousRound)
        {
            if(!IsOnline||Round!=previousRound||Phase!=MatchPhase.RoundResult)return false;
            BeginRound();return true;
        }
        public string OnlineDigest()
        {
            var b=new StringBuilder();b.Append(EronyaMechanics.Version).Append('|').Append(EronyaAbilityParameters.Signature).Append('|');b.Append(Round).Append('|').Append((int)Phase).Append('|').Append(LastRoundWinner?.ToString()??"draw");
            b.Append('|').Append(HostMap).Append('|').Append(GuestMap).Append('|').Append(RoundMap);
            // Canonical side order: both clients must confirm the same BO5 score.
            b.Append('|').Append(LocalSide==BoardSide.Blue?PlayerWins:BotWins)
                .Append(':').Append(LocalSide==BoardSide.Blue?BotWins:PlayerWins);
            foreach(var pouch in new[]{Player,Bot}.OrderBy(p=>(int)p.Side))
            {
                b.Append('|').Append(pouch.Side).Append(':').Append(pouch.Coins).Append(':').Append(pouch.PoolCount);
                b.Append(':').Append(pouch.AbilityAccess.Canonical);
                foreach(var u in pouch.AllOwned.OrderBy(u=>u.Definition.Id,StringComparer.Ordinal).ThenBy(u=>(int)u.Location))
                {
                    b.Append('|').Append(u.Definition.Id).Append(':').Append(u.Copies).Append(':').Append(u.Paid)
                        .Append(':').Append(u.BankRefunded).Append(':').Append(u.BankReservedCost)
                        .Append(':').Append((int)u.Location).Append(':').Append(u.Deployment.x).Append(',').Append(u.Deployment.y)
                        .Append(':').Append(u.PersistentDamageBonus).Append(':').Append(u.PersistentHealthBonus).Append(':').Append(u.MarkedPositionRound).Append(':').Append(u.UpgradePurchasedRound);
                    foreach(bool trick in u.Tricks)b.Append(trick?'1':'0');
                }
            }
            foreach(var actor in actors.Values.Where(a=>Phase!=MatchPhase.Preparation).OrderBy(a=>a.UnitId,StringComparer.Ordinal))
                b.Append('|').Append(actor.UnitId).Append(':').Append(actor.PreciseHealth.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(':').Append(actor.Shield)
                    .Append(':').Append(actor.CurrentCell?.X??-1).Append(',').Append(actor.CurrentCell?.Y??-1);
            b.Append('|').Append(EronyaTiebreakSide);
            return Hash(b.ToString());
        }
        public static string Hash(string value)
        {
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","");
        }
    }
}
