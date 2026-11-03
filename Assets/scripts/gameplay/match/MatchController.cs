using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    public enum MatchPhase { Menu, Preparation, Combat, RoundResult, MatchResult }

    public sealed partial class MatchController : MonoBehaviour
    {
        public MatchConfig Config { get; private set; }
        public BoardManager Board { get; private set; }
        public BoardWorldMapper Mapper { get; private set; }
        public PouchState Player { get; private set; }
        public PouchState Bot { get; private set; }
        public MatchPhase Phase { get; private set; }
        public string SessionId { get; private set; }
        public CombatSimulation Simulation { get; private set; }
        public int Round { get; private set; }
        public int PlayerWins { get; private set; }
        public int BotWins { get; private set; }
        public BoardSide? LastRoundWinner { get; private set; }
        public float RoundResultElapsed { get; private set; }
        // A frozen health snapshot also exists after elimination. Only an actual
        // timeout with standing characters on BOTH teams gets the health sequence.
        public bool UsesHealthTiebreaker => Simulation?.EronyaRoundCompared == true || (Simulation?.TimeoutResult != null
            && Simulation.HealthSummary.BlueTotal > 0 && Simulation.HealthSummary.RedTotal > 0);
        public float RoundHealthStartTime => UsesHealthTiebreaker ? Config.RoundUI.TimeoutBannerDuration : 0;
        public float RoundCardsStartTime => Config?.RoundUI?.CardsStartTime(UsesHealthTiebreaker) ?? .2f;
        public float RoundStampStartTime => Config?.RoundUI?.StampStartTimeFor(UsesHealthTiebreaker) ?? .42f;
        public float RoundTransitionDuration => Config?.RoundUI?.TransitionDurationFor(UsesHealthTiebreaker) ?? 1.74f;
        public int WinsToWin => Config?.RoundUI?.WinsToWin ?? 3;
        public bool Paused { get; private set; }
        public float Remaining { get; private set; }
        public string Result { get; private set; }
        public string Notice { get; private set; }
        public IReadOnlyDictionary<OwnedUnit, BattleUnit> Actors => actors;
        // Independent values captured at lock-in; never references to the live bot roster.
        public sealed class OpponentEcho
        {
            public string UnitId { get; }
            public Vector2Int Position { get; }
            public int Round { get; }
            public OwnedUnit Inspection { get; }
            public OpponentEcho(string id, Vector2Int position, int round)
            { UnitId=id; Position=position; Round=round; }
            public OpponentEcho(OwnedUnit unit,int round)
            {
                UnitId=unit.Definition.Id;Position=unit.Deployment;Round=round;
                Inspection=new OwnedUnit(unit.Definition){Location=UnitLocation.Field,Deployment=Position,
                    Copies=unit.Copies,UpgradePurchasedRound=unit.UpgradePurchasedRound,PersistentDamageBonus=unit.PersistentDamageBonus,
                    PersistentHealthBonus=unit.PersistentHealthBonus};
                Array.Copy(unit.Tricks,Inspection.Tricks,unit.Tricks.Length);
            }
        }
        readonly List<OpponentEcho> previousOpponent = new List<OpponentEcho>();
        public IReadOnlyList<OpponentEcho> PreviousOpponent => previousOpponent.AsReadOnly();
        public OpponentEcho InitialOpponent { get; private set; }
        public OwnedUnit InspectOpponent(string id)
        {
            if(Phase!=MatchPhase.Preparation)return null;
            var visible=Round==1?InitialOpponent:previousOpponent.FirstOrDefault(e=>e.UnitId==id);
            if(visible==null||visible.UnitId!=id)return null;
            var unit=Bot.Owned.TryGetValue(id,out var current)?new OpponentEcho(current,Round).Inspection:visible.Inspection;
            if(unit!=null)unit.Deployment=visible.Position;
            return unit;
        }
        public event Action Changed;
        public event Action<string> ActionRejected;
        public event Action<PlacementExchange> PlacementExchanged;
        public event Action<BattleUnit, OwnedUnit> ActorCreated;
        public event Action<BattleUnit, BoardCell, BoardCell> Moved;
        public event Action<CombatDisplacementSnapshot> Displaced;
        public CombatAttackPresentationSnapshot CurrentAttackPresentation => Simulation?.CurrentAttackPresentation;
        public BoardSide EronyaTiebreakSide => (BoardSide)EronyaRoundRules.TokenSide(seed,Round);
        public event Action<BattleUnit, BoardCell, BoardCell> Teleported;
        public event Action<BattleUnit, BattleUnit, bool> Attacked;
        public event Action<BattleUnit, BattleUnit, BattleUnit, float> ProjectileBounced;
        public event Action<BattleUnit, BattleUnit, float> ReflectedMoonLaunched;
        public event Action<BattleUnit, BattleUnit, float> KazuruRingLaunched;
        public event Action<BattleUnit, BattleUnit, int> Impacted;
        public event Action<BattleUnit, int> Healed;
        public event Action<BattleUnit, BattleUnit> CriticalImpacted;
        public event Action<BattleUnit> AttackReset;
        public event Action<BoardCell> HealingArea;
        public event Action<BattleUnit> Died;
        public event Action<BattleUnit, float> Reviving;
        public event Action<BattleUnit> Revived;
        public event Action<BattleUnit, BattleUnit> LethalImpacted;
        public event Action<BattleUnit, string> AbilityUsed;
        public event Action<BattleUnit, string, BoardCell[]> AreaEffect;
        public event Action<BattleUnit, BoardCell, BoardCell, float> BoulderRolled;
        public event Action<CombatSimulation.FishShieldFlight> FishShieldLaunched;
        public event Action<CombatSimulation.HealingRoseFlight> HealingRoseLaunched;
        public event Action<BattleUnit, BoardCell, BoardCell, BoardCell, float, float> DashStarted;
        public event Action<BattleUnit, BoardCell, float> FlowerCastStarted;
        public event Action<BattleUnit, BoardCell, BoardCell[], float> ConesLaunched;
        public event Action<BattleUnit, BattleUnit, GumBuff, float> ColoredGumLaunched;
        public event Action<string> Sound;
        readonly Dictionary<OwnedUnit, BattleUnit> actors = new Dictionary<OwnedUnit, BattleUnit>();
        float accumulator;
        int seed = 20260911;
        string chosenMonster = "bugaloo";
        string[] chosenWhelpIds;
        AbilityAccessSnapshot localAbilityAccess = AbilityAccessSnapshot.Unrestricted;
        public bool SetLocalAbilityAccess(AbilityAccessSnapshot snapshot)
        {
            if (snapshot == null || IsOnline || (Phase != MatchPhase.Menu && Phase != MatchPhase.MatchResult)) return false;
            try { snapshot.ValidateFor(Config); } catch (ArgumentException) { return false; }
            localAbilityAccess = snapshot; return true;
        }

        public void Configure(MatchConfig config, BoardManager board, BoardWorldMapper mapper)
        {
            if(ownsRuntimeConfig&&Config!=null&&Config!=config)Destroy(Config);
            Config = config!=null&&config.Characters!=null&&config.Characters.Length>0?config.CreateRuntimeConfig():config; ownsRuntimeConfig=Config!=config; Board = board; Mapper = mapper; Phase = MatchPhase.Menu;
            if (Config != null && Config.RoundUI == null) Config.RoundUI = new RoundPresentationSettings();
            LastRoundWinner = null; RoundResultElapsed = 0; ResetTrainingState();
        }
        public void StartMatch(string monsterId, int matchSeed = 0)
        {
            StartMatch(monsterId, null, matchSeed);
        }
        public bool StartMatch(string monsterId, IEnumerable<string> whelpIds, int matchSeed = 0,
            string localMap = "classic", string rivalMap = "classic")
        {
            if (IsOnline) return Reject("Sal de la sala antes de iniciar otra partida.");
            if(!ValidOnlineMap(localMap)||!ValidOnlineMap(rivalMap))return Reject("Elige un mapa válido.");
            Mapper.Flipped=false;
            if (Config == null) return Reject("No se cargó la configuración de la partida.");
            if (!Config.TryResolveWhelpSelection(whelpIds, out string[] selection, out string reason)) return Reject(reason);
            UnitDefinition selectedMonster = Config.Get(monsterId);
            if (selectedMonster == null || !selectedMonster.IsMonster) return Reject("Elige un Monster válido.");
            int resolvedSeed = matchSeed != 0 ? matchSeed : CreateRuntimeSeed(seed);
            PouchState nextPlayer, nextBot;
            try
            {
                nextPlayer = new PouchState(Config, resolvedSeed, monsterId, BoardSide.Blue, selection, localAbilityAccess);
                int botSeed = resolvedSeed ^ 0x45fa1;
                BotPouchSelection rival = BotPouchGenerator.Create(Config, botSeed, monsterId);
                nextBot = new PouchState(Config, botSeed ^ 0x6d2b79f5, rival.MonsterId, BoardSide.Red, rival.WhelpIds);
            }
            catch (ArgumentException error) { return Reject(error.Message); }
            CancelMoonkenDeliveries();Simulation?.Stop(); Simulation=null;
            previousOpponent.Clear(); InitialOpponent=null;
            ClearActors(); Board.BuildBoard(); Paused = false; Time.timeScale = 1;
            seed = resolvedSeed; chosenMonster = monsterId; Round = 0; PlayerWins = BotWins = 0;
            LastRoundWinner = null; RoundResultElapsed = 0;
            chosenWhelpIds = nextPlayer.SelectedWhelpIds.ToArray();
            ResetTrainingState();
            Player = nextPlayer;
            Bot = nextBot;
            HostMap=localMap;GuestMap=rivalMap;
            BeginRound();
            return true;
        }
        public void Rematch() { if(IsOnline){Menu();return;} if(IsTraining){RestartTraining();return;} StartMatch(chosenMonster, chosenWhelpIds, CreateRuntimeSeed(seed), HostMap, GuestMap); }

        private static int CreateRuntimeSeed(int previous)
        {
            int value;
            do value = Guid.NewGuid().GetHashCode(); while (value == 0 || value == previous);
            return value;
        }
        public void Menu()
        {
            CancelMoonkenDeliveries();
            bool wasOnline=IsOnline;IsOnline=false;ResetTrainingState();Mapper.Flipped=false;ResetOnlinePreparation();
            if(wasOnline)OnlineExited?.Invoke();
            Simulation?.Stop();
            Paused = false; Time.timeScale = 1; ClearActors(); Board.BuildBoard();
            previousOpponent.Clear(); InitialOpponent=null;
            Player = Bot = null; Simulation = null; Round = PlayerWins = BotWins = 0;
            LastRoundWinner = null; RoundResultElapsed = 0;
            Phase = MatchPhase.Menu; Notice = ""; Changed?.Invoke();
        }
        void BeginRound()
        {
            ResetOnlinePreparation();
            Simulation?.Stop();
            Simulation = null; ClearActors(); Board.BuildBoard(); Round++; accumulator = 0;
            if(Round==1)SessionId=Guid.NewGuid().ToString("N");
            LastRoundWinner = null; RoundResultElapsed = 0;
            Player.BeginPreparation(Round); Bot.BeginPreparation(Round);
            Phase = MatchPhase.Preparation; Remaining = 40; Notice = "";
            EnsureMonsterPosition(Player); EnsureMonsterPosition(Bot);
            // Each hero keeps its own role's default. FormationCells already rotates
            // that default for the rival side; it must not depend on our hero's role.
            if(Round==1)InitialOpponent=new OpponentEcho(Bot.Monster,0);
            SyncActors(); if(!IsOnline)RunBot(Bot); SyncActors(); Changed?.Invoke();
        }
        void EnsureMonsterPosition(PouchState owner)
        {
            OwnedUnit unit = owner.Monster;
            if (unit.Location == UnitLocation.Field && Config.IsDeploymentLegal(owner.Side, unit.Deployment)) return;
            var cell = FormationCell(owner, unit);
            if (cell != null) owner.TrySetLocation(unit.Definition.Id, UnitLocation.Field, new Vector2Int(cell.X, cell.Y), out _);
        }
        BoardCell FormationCell(PouchState owner, OwnedUnit unit)
        {
            return Config.FormationCells(unit.Definition, owner.Side).Select(p => Board.GetCell(p.x,p.y)).Where(c =>
                (c.OccupiedBy == null || (actors.TryGetValue(unit, out var a) && ReferenceEquals(c.OccupiedBy, a))) &&
                !owner.Owned.Values.Any(o => o != unit && o.Location == UnitLocation.Field && o.Deployment == new Vector2Int(c.X,c.Y)))
                .FirstOrDefault();
        }
        public void RunBot(PouchState owner)
        {
            if (IsTraining || Phase != MatchPhase.Preparation) return;
            // Uses only its own pouch, costs and roster; bounded even with no affordable offers.
            for (int pass = 0; pass < 5; pass++)
            {
                bool purchased = false;
                for (int i = 0; i < owner.Offers.Count; i++)
                {
                    var offer = owner.Offers[i];
                    if (offer != null && owner.TryBuy(i, offer.Token, out _)) purchased = true;
                }
                if (owner.Coins <= 0) break;
                if (!purchased && (owner.Rerolls <= 0 || !owner.TryReroll(owner.OfferVersion, out _))) break;
                if (owner.Owned.Values.Count(u => !u.Definition.IsMonster) >= 2 && !purchased) break;
            }
            foreach (var unit in owner.Owned.Values)
            {
                if (unit.Location == UnitLocation.Field) continue;
                var cell = FormationCell(owner, unit);
                if (cell != null) owner.TrySetLocation(unit.Definition.Id, UnitLocation.Field, new Vector2Int(cell.X, cell.Y), out _);
                else owner.TrySetLocation(unit.Definition.Id, UnitLocation.Bench, Vector2Int.zero, out _);
            }
            owner.TryUpgradeMonster(out _);
            SyncActors();
        }
        void SyncActors()
        {
            if (Player == null || Bot == null) return;
            var all = Player.AllOwned.Concat(Bot.AllOwned).Concat(trainingTargets).ToList();
            foreach (var pair in actors.ToList())
                if (!all.Contains(pair.Key) || pair.Key.Location != UnitLocation.Field) RemoveActor(pair.Key, pair.Value);
            foreach (var owner in Phase == MatchPhase.Preparation ? new[] { Player } : new[] { Player, Bot })
                foreach (var owned in owner.Owned.Values)
                {
                    if (owned.Location != UnitLocation.Field || actors.ContainsKey(owned)) continue;
                    GameObject go = new GameObject(owner.Side + "-" + owned.Definition.Id);
                    go.transform.SetParent(transform, false);
                    BattleUnit actor = owned.Definition.IsMonster ? (BattleUnit)go.AddComponent<MonsterUnit>() : go.AddComponent<WhelpUnit>();
                    actor.Initialize(owner.Side + "-" + owned.Definition.Id, owner.Side, UnitStats.FromDefinition(owned.Definition, owned, false));
                    if(IsTraining&&owner==Bot&&CurrentTrainingLesson==TrainingLesson.SimpleCombat)
                    {actor.TrainingEffectMultiplier=trainingCombatOptions.StatMultiplier;TrainingTarget=actor;}
                    if (!Board.TryOccupyCell(actor, owned.Deployment.x, owned.Deployment.y)) { Dispose(go); continue; }
                    go.transform.position = Mapper.GetWorldPosition(actor.CurrentCell);
                    actors.Add(owned, actor); ActorCreated?.Invoke(actor, owned);
                }
        }
        void RemoveActor(OwnedUnit owned, BattleUnit actor)
        {
            if (actor != null) { Board.ReleaseUnit(actor); actor.gameObject.SetActive(false); Dispose(actor.gameObject); }
            actors.Remove(owned);
        }
        void ClearActors()
        {
            foreach (var pair in actors.ToList()) RemoveActor(pair.Key, pair.Value);
            actors.Clear();
        }
        static void Dispose(GameObject go) { if(Application.isPlaying) Destroy(go);else DestroyImmediate(go); }
        public bool Buy(int slot, long token)
        {
            if ((IsTraining && !TrainingEconomyAllowed) || !CanPrepare()) return false;
            bool ok = Player.TryBuy(slot, token, out string reason);
            if (ok) { SyncActors(); Notice = ""; Sound?.Invoke("buy"); }
            else Reject(reason);
            if(ok)RecordOnline(new PreparationCommand{kind="buy",slot=slot}); Changed?.Invoke(); return ok;
        }
        public bool BuyAndPlace(int slot, long token, BoardCell cell, bool bench = false)
        {
            if ((IsTraining && !TrainingEconomyAllowed) || !CanPrepare()) return false;
            Vector2Int position = Vector2Int.zero;
            if (!bench)
            {
                if (cell == null || !Board.IsManagedCell(cell)) return Reject("La casilla no está disponible.");
                // An owned copy buys its pending Trick in place, just like tapping its offer.
                // The drop cell identifies the board; its occupant is never the upgrade target.
                if (slot >= 0 && slot < Player.Offers.Count && Player.Offers[slot] != null &&
                    Player.Owned.ContainsKey(Player.Offers[slot].UnitId)) return Buy(slot, token);
                if (cell.IsBlocked || cell.ReservedBy != null || cell.OccupiedBy != null)
                    return Reject("La casilla no está disponible.");
                position = cell.Coordinates;
            }
            bool ok = Player.TryBuyAndPlace(slot, token, bench ? UnitLocation.Bench : UnitLocation.Field, position,
                out string reason);
            if (ok)
            {
                SyncActors(); Notice = ""; Sound?.Invoke("buy");
            }
            else Reject(reason);
            if(ok)RecordOnline(new PreparationCommand{kind="buy-place",slot=slot,x=position.x,y=position.y,bench=bench}); Changed?.Invoke(); return ok;
        }
        public bool Reroll(int version)
        {
            if ((IsTraining && !TrainingEconomyAllowed) || !CanPrepare()) return false;
            bool ok = Player.TryReroll(version, out string reason);
            if (ok) { Notice = "Nuevas ofertas"; Sound?.Invoke("button"); } else Reject(reason);
            if(ok)RecordOnline(new PreparationCommand{kind="reroll"}); Changed?.Invoke(); return ok;
        }
        public bool UpgradeMonster()
        {
            if ((IsTraining && !TrainingEconomyAllowed) || !CanPrepare()) return false;
            bool ok = Player.TryUpgradeMonster(out string reason);
            if (ok) { Notice = "Mejora activa en el próximo combate; posición fija desde la siguiente ronda."; Sound?.Invoke("buy"); } else Reject(reason);
            if(ok)RecordOnline(new PreparationCommand{kind="upgrade",index=-1}); Changed?.Invoke(); return ok;
        }
        public bool UpgradeMonster(int index)
        {
            if ((IsTraining && !TrainingEconomyAllowed) || !CanPrepare()) return false;
            bool ok = Player.TryUpgradeMonster(index, out string reason);
            if (ok) { Notice = "Habilidad activada para el próximo combate."; Sound?.Invoke("buy"); }
            else Reject(reason);
            if(ok)RecordOnline(new PreparationCommand{kind="upgrade",index=index}); Changed?.Invoke(); return ok;
        }
        public bool ChooseTrick(string id, int index)
        {
            if ((IsTraining && !TrainingEconomyAllowed) || !CanPrepare()) return false;
            bool ok = Player.TrySelectTrick(id, index, out string reason);
            if (ok) Notice = "Trick elegido para la próxima copia."; else Reject(reason);
            if(ok)RecordOnline(new PreparationCommand{kind="choose",unit=id,index=index}); Changed?.Invoke(); return ok;
        }
        public bool BuyTrick(string id, int index, int slot, long token)
        {
            if ((IsTraining && !TrainingEconomyAllowed) || !CanPrepare()) return false;
            bool ok = Player.TryBuyTrick(id, index, slot, token, out string reason);
            if (ok) { SyncActors(); Notice = "Mejora comprada."; Sound?.Invoke("buy"); }
            else Reject(reason);
            if(ok)RecordOnline(new PreparationCommand{kind="trick",unit=id,index=index,slot=slot}); Changed?.Invoke(); return ok;
        }
        public bool Place(string id, BoardCell cell)
            => Place(Player?.FindOwned(id,false),cell);
        public bool Place(OwnedUnit owned, BoardCell cell)
        {
            if (!CanPrepare() || cell == null || !Player.Owns(owned)) return false;
            string id=owned.Definition.Id;bool sourceBench=owned.Location==UnitLocation.Bench;
            var primary=Player.FindOwned(id,false);
            if(sourceBench&&primary!=owned&&primary?.Location==UnitLocation.Field)
            {
                if(!Board.IsManagedCell(cell)||cell.IsBlocked||!Config.IsDeploymentLegal(LocalSide,cell.Coordinates))
                    return Reject("La casilla no está disponible.");
                if(!Player.TrySetLocation(owned,UnitLocation.Field,cell.Coordinates,out string mergeReason))return Reject(mergeReason);
                RecordOnline(new PreparationCommand{kind="place",unit=id,x=cell.X,y=cell.Y,sourceBench=true});
                SyncActors();Notice="";Sound?.Invoke("buy");Changed?.Invoke();return true;
            }
            actors.TryGetValue(owned, out var actor);
            if (!Board.IsManagedCell(cell) || cell.IsBlocked || (cell.ReservedBy != null && !ReferenceEquals(cell.ReservedBy, actor)))
                return Reject("La casilla no está disponible.");
            if (cell.OccupiedBy != null && !ReferenceEquals(cell.OccupiedBy, actor))
            {
                if (owned.Location != UnitLocation.Field) return Reject("La casilla está ocupada.");
                var other = Player.Owned.Values.FirstOrDefault(u => u != owned && u.Location == UnitLocation.Field && u.Deployment == cell.Coordinates);
                bool exchanged=other != null ? ExchangePlacement(owned, other) : Reject("Solo puedes intercambiar personajes propios.");
                if(exchanged)RecordOnline(new PreparationCommand{kind="place",unit=id,x=cell.X,y=cell.Y});
                return exchanged;
            }
            var pos = new Vector2Int(cell.X, cell.Y);
            UnitLocation previousLocation=owned.Location; Vector2Int previousCell=owned.Deployment;
            if (!Player.TrySetLocation(owned, UnitLocation.Field, pos, out string reason)) return Reject(reason);
            if (actor != null)
            {
                if(!Board.TryRepositionUnit(actor, cell))
                { Player.TrySetLocation(owned,previousLocation,previousCell,out _);return Reject("No se puede ocupar esa casilla."); }
                actor.transform.position = Mapper.GetWorldPosition(cell);
            }
            RecordOnline(new PreparationCommand{kind="place",unit=id,x=cell.X,y=cell.Y,sourceBench=sourceBench});
            SyncActors(); Notice = owned.Definition.DisplayName + " colocado"; Sound?.Invoke("button"); Changed?.Invoke(); return true;
        }
        public bool Store(string id, bool bench)
            => Store(Player?.FindOwned(id,false),bench);
        public bool Store(OwnedUnit owned, bool bench)
        {
            if ((IsTraining && !TrainingEconomyAllowed) || !CanPrepare() || !Player.Owns(owned)) return false;
            string id=owned.Definition.Id;bool sourceBench=owned.Location==UnitLocation.Bench;
            bool ok = Player.TrySetLocation(owned, bench ? UnitLocation.Bench : UnitLocation.Brief, Vector2Int.zero, out string reason);
            if (!ok) return Reject(reason);
            RecordOnline(new PreparationCommand{kind="store",unit=id,bench=bench,sourceBench=sourceBench});
            SyncActors(); Notice = bench ? "Whelp en el bench" : "Whelp en el Brief"; Changed?.Invoke(); return true;
        }

        bool ValidPlacementActor(OwnedUnit unit)
        {
            if (unit.Location != UnitLocation.Field) return false;
            var cell = Board.GetCell(unit.Deployment.x, unit.Deployment.y);
            return cell != null && !cell.IsBlocked && cell.ReservedBy == null &&
                actors.TryGetValue(unit, out var actor) && actor != null && actor.ReservedCell == null &&
                ReferenceEquals(actor.CurrentCell, cell) && ReferenceEquals(cell.OccupiedBy, actor);
        }

        bool ExchangePlacement(OwnedUnit first, OwnedUnit second)
        {
            if (!ValidPlacementActor(first) || !ValidPlacementActor(second)) return Reject("La casilla no está disponible.");
            var firstActor = actors[first]; var secondActor = actors[second];
            if (!Board.CanExchangeUnits(firstActor, secondActor)) return Reject("No se pueden intercambiar esas casillas.");
            if (!Player.TryExchange(first.Definition.Id, second.Definition.Id, out var exchange, out string reason)) return Reject(reason);
            // Both model and board endpoints have been checked; no callbacks run
            // until the two actors occupy their final cells.
            Board.TryExchangeUnits(firstActor, secondActor);
            firstActor.transform.position = Mapper.GetWorldPosition(firstActor.CurrentCell);
            secondActor.transform.position = Mapper.GetWorldPosition(secondActor.CurrentCell);
            SyncActors(); Notice = "Posiciones intercambiadas"; Sound?.Invoke("button");
            PlacementExchanged?.Invoke(exchange); Changed?.Invoke(); return true;
        }
        public bool Sell(string id)
            => Sell(Player?.FindOwned(id,false));
        public bool Sell(OwnedUnit owned)
        {
            if ((IsTraining && !TrainingEconomyAllowed) || !CanPrepare() || !Player.Owns(owned)) return false;
            string id=owned.Definition.Id;bool sourceBench=owned.Location==UnitLocation.Bench;
            if (!Player.TrySell(owned, out string reason)) return Reject(reason);
            RecordOnline(new PreparationCommand{kind="sell",unit=id,sourceBench=sourceBench});
            SyncActors(); Notice = "Venta completada."; Sound?.Invoke("buy"); Changed?.Invoke(); return true;
        }
        bool CanPrepare() => Phase == MatchPhase.Preparation && !Paused && !OnlineReady && !PreparationLocked && (!IsOnline || onlineCommands.Count<MaxPreparationCommands);
        public bool Reject(string text) { Notice = text; ActionRejected?.Invoke(text); Sound?.Invoke("reject"); Changed?.Invoke(); return false; }
        public void Ready()
        {
            if (IsTraining || Phase != MatchPhase.Preparation || Paused || IsOnline && OnlineReady) return;
            if(IsOnline){SetReady(true);return;}
            BeginCombat();
        }
        void BeginCombat()
        {
            if (IsTraining && !trainingCombatStarting) return;
            Player.EndPreparation(); Bot.EndPreparation(); Phase = MatchPhase.Combat; Remaining = IsTraining?0:40; accumulator = 0;
            SyncActors();
            previousOpponent.Clear(); InitialOpponent=null;
            foreach (var owned in Bot.Owned.Values)
                if (owned.Location == UnitLocation.Field)
                    previousOpponent.Add(new OpponentEcho(owned,Round));
            Simulation = new CombatSimulation(Board, seed ^ Round, Config.OpeningAbilityWindowSeconds);
            Simulation.EronyaTiebreakSide=EronyaTiebreakSide;
            if(IsTraining&&CurrentTrainingLesson==TrainingLesson.AttacksHealth)
                Simulation.TimeoutTickLimit=TrainingEnergyCycleTickBudget();
            Simulation.Displaced += snapshot => Displaced?.Invoke(snapshot);
            Simulation.Moved += (actor, from, to) => Moved?.Invoke(actor, from, to);
            Simulation.Attacked += (actor, target, projectile) => { if(IsTraining&&actor.Side==LocalSide)TrainingAttackCount++;Attacked?.Invoke(actor,target,projectile); };
            Simulation.ProjectileBounced += (actor, origin, target, duration) => ProjectileBounced?.Invoke(actor, origin, target, duration);
            Simulation.ReflectedMoonLaunched += (actor, target, duration) => ReflectedMoonLaunched?.Invoke(actor, target, duration);
            Simulation.KazuruRingLaunched += (actor, target, duration) => KazuruRingLaunched?.Invoke(actor, target, duration);
            Simulation.MoonkensProduced += QueueMoonkenDelivery;
            Simulation.Impacted += (actor, target, damage) => { if(IsTraining&&actor.Side==LocalSide&&target.Side==RivalSide)TrainingDamageDealt+=Mathf.Max(0,damage);Impacted?.Invoke(actor,target,damage); Sound?.Invoke("hit"); };
            Simulation.Healed += (actor,amount)=>Healed?.Invoke(actor,amount);
            Simulation.CriticalImpacted += (actor,target)=>CriticalImpacted?.Invoke(actor,target);
            Simulation.AttackReset += actor=>AttackReset?.Invoke(actor);
            Simulation.HealingArea += cell=>HealingArea?.Invoke(cell);
            Simulation.Died += actor => { Died?.Invoke(actor); Sound?.Invoke("death"); };
            Simulation.Reviving += (actor, duration) => Reviving?.Invoke(actor, duration);
            Simulation.Revived += actor => { Revived?.Invoke(actor); Sound?.Invoke("buy"); };
            Simulation.LethalImpacted += (actor, target) => LethalImpacted?.Invoke(actor, target);
            Simulation.Teleported += (actor, from, to) => Teleported?.Invoke(actor, from, to);
            Simulation.AbilityUsed += (actor, effect) => {if(IsTraining&&actor.Side==LocalSide)TrainingAbilityCount++;AbilityUsed?.Invoke(actor, effect);};
            Simulation.AreaEffect += (actor, effect, cells) => AreaEffect?.Invoke(actor, effect, cells);
            Simulation.FishShieldLaunched += flight => FishShieldLaunched?.Invoke(flight);
            Simulation.HealingRoseLaunched += flight => HealingRoseLaunched?.Invoke(flight);
            Simulation.BoulderRolled += (actor, from, to, duration) => BoulderRolled?.Invoke(actor, from, to, duration);
            Simulation.DashStarted += (actor, from, throughTarget, to, width, duration) => DashStarted?.Invoke(actor, from, throughTarget, to, width, duration);
            Simulation.ConesLaunched += (actor,origin,cells,duration)=>ConesLaunched?.Invoke(actor,origin,cells,duration);
            Simulation.ColoredGumLaunched += (actor,target,buff,duration)=>ColoredGumLaunched?.Invoke(actor,target,buff,duration);
            Simulation.FlowerCastStarted += (actor, destination, duration) => FlowerCastStarted?.Invoke(actor, destination, duration);
            Simulation.PermanentDamageEarned += (actor, amount) =>
            {
                foreach (var pair in actors) if (pair.Value == actor) { pair.Key.PersistentDamageBonus += amount; break; }
            };
            Simulation.PermanentHealthEarned += (actor, amount) =>
            {
                foreach (var pair in actors) if (pair.Value == actor) { pair.Key.PersistentHealthBonus += amount; break; }
            };
            Simulation.PositionMarked += actor =>
            {
                foreach (var pair in actors) if (pair.Value == actor) { if (pair.Key.MarkedPositionRound == 0) pair.Key.MarkedPositionRound = Round; break; }
            };
            // Blotan's earned currency is paid by the delivery clock when its Moonken reaches the HUD.
            Simulation.CurrencyStolen += (side, amount) =>
            {
                PouchState recipient = side == LocalSide ? Player : Bot;
                PouchState victim = side == LocalSide ? Bot : Player;
                recipient.AddCombatCoins(-victim.AddCombatCoins(-amount));
            };
            Simulation.Summoned += (actor, definition) =>
            {
                // This view record belongs only to the combat. It never enters a player's pouch or copy pool.
                var view = new OwnedUnit(definition) { Location = UnitLocation.Field, Deployment = actor.CurrentCell.Coordinates };
                actor.transform.SetParent(transform, false);
                actor.transform.position = Mapper.GetWorldPosition(actor.CurrentCell);
                actors.Add(view, actor);
                ActorCreated?.Invoke(actor, view);
            };
            foreach (var pair in actors)
            {
                var owner = pair.Value.Side == Player.Side ? Player : Bot;
                pair.Value.CombatTeamSlot = owner.CombatTeamSlot(pair.Key.Definition.Id);
            }
            Simulation.Begin(actors.Values.ToList(), actors.Where(p=>!trainingTargets.Contains(p.Key)).ToDictionary(p => p.Value, p => p.Key));
            if(Simulation.UsesEronyaCombat&&!IsTraining)Remaining=30;
            Notice = ""; Changed?.Invoke();
        }
        void Update() { Advance(Time.unscaledDeltaTime); }
        public void Advance(float dt)
        {
            if (Paused || dt <= 0 || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            if(IsTraining){AdvanceTraining(dt);return;}
            AdvanceMoonkenDeliveries(dt);
            if (Phase == MatchPhase.Preparation) { Remaining = Mathf.Max(0, Remaining - dt); if (Remaining <= 0) Ready(); else if(!IsOnline&&OnlineReady){readyCountdown-=dt;if(readyCountdown<=0)BeginCombat();} }
            else if (Phase == MatchPhase.Combat)
            {
                accumulator += dt;
                while (accumulator + .00001f >= .1f && Phase == MatchPhase.Combat)
                {
                    accumulator -= .1f; Simulation.Step(.1f); Remaining = Mathf.Max(0,(Simulation.UsesEronyaCombat?30:40) - Simulation.Elapsed);
                    if (Simulation.Finished) EndRound();
                }
            }
            else if (Phase == MatchPhase.RoundResult || Phase == MatchPhase.MatchResult)
            {
                RoundResultElapsed += dt;
                float transitionDuration = RoundTransitionDuration;
                if (!IsOnline && Phase == MatchPhase.RoundResult && RoundResultElapsed >= transitionDuration) BeginRound();
            }
        }
        void EndRound()
        {
            LastRoundWinner = Simulation.Winner;
            RoundResultElapsed = 0;
            if (LastRoundWinner == LocalSide) PlayerWins++;
            if (LastRoundWinner == RivalSide) BotWins++;
            string reason=Simulation.Reason;
            if(Simulation.Winner==RivalSide && reason=="El equipo rival perdió todas sus unidades")reason="Tu equipo perdió todas sus unidades";
            Result = (Simulation.Winner == null ? "Empate" : Simulation.Winner == LocalSide ? "¡Ronda ganada!" : "Ronda del rival") + "\n" + reason;
            Phase = PlayerWins >= WinsToWin || BotWins >= WinsToWin ? MatchPhase.MatchResult : MatchPhase.RoundResult;
            Notice = ""; Sound?.Invoke("result"); Changed?.Invoke();
            if(IsOnline)OnlineRoundFinished?.Invoke();
        }
        public void NextRound() { if (!IsOnline && Phase == MatchPhase.RoundResult && !Paused) BeginRound(); }
        public void Pause(bool paused) { if(IsOnline)return; Paused = paused; Time.timeScale = paused ? 0 : 1; Changed?.Invoke(); }
        bool ownsRuntimeConfig;
        void OnDestroy() { Time.timeScale = 1; ResetTrainingState();if(ownsRuntimeConfig&&Config!=null)Destroy(Config); }
    }
}
