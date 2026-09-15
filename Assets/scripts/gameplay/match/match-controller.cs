using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    public enum MatchPhase { Menu, Preparation, Combat, RoundResult, MatchResult }

    public sealed class MatchController : MonoBehaviour
    {
        public MatchConfig Config { get; private set; }
        public BoardManager Board { get; private set; }
        public BoardWorldMapper Mapper { get; private set; }
        public PouchState Player { get; private set; }
        public PouchState Bot { get; private set; }
        public MatchPhase Phase { get; private set; }
        public CombatSimulation Simulation { get; private set; }
        public int Round { get; private set; }
        public int PlayerWins { get; private set; }
        public int BotWins { get; private set; }
        public bool Paused { get; private set; }
        public float Remaining { get; private set; }
        public string Result { get; private set; }
        public string Notice { get; private set; }
        public IReadOnlyDictionary<OwnedUnit, BattleUnit> Actors => actors;
        public event Action Changed;
        public event Action<BattleUnit, OwnedUnit> ActorCreated;
        public event Action<BattleUnit, BoardCell, BoardCell> Moved;
        public event Action<BattleUnit, BattleUnit, bool> Attacked;
        public event Action<BattleUnit, BattleUnit, int> Impacted;
        public event Action<BattleUnit> Died;
        public event Action<BattleUnit, float> Reviving;
        public event Action<BattleUnit> Revived;
        public event Action<BattleUnit, BattleUnit> LethalImpacted;
        public event Action<BattleUnit, string> AbilityUsed;
        public event Action<BattleUnit, BoardCell, BoardCell, float> BoulderRolled;
        public event Action<string> Sound;
        readonly Dictionary<OwnedUnit, BattleUnit> actors = new Dictionary<OwnedUnit, BattleUnit>();
        float accumulator;
        int seed = 20260911;
        string chosenMonster = "bugaloo";
        string[] chosenWhelpIds;

        public void Configure(MatchConfig config, BoardManager board, BoardWorldMapper mapper)
        {
            Config = config; Board = board; Mapper = mapper; Phase = MatchPhase.Menu;
        }
        public void StartMatch(string monsterId, int matchSeed = 20260911)
        {
            StartMatch(monsterId, null, matchSeed);
        }
        public bool StartMatch(string monsterId, IEnumerable<string> whelpIds, int matchSeed = 20260911)
        {
            if (Config == null) return Reject("No se cargó la configuración de la partida.");
            if (!Config.TryResolveWhelpSelection(whelpIds, out string[] selection, out string reason)) return Reject(reason);
            UnitDefinition selectedMonster = Config.Get(monsterId);
            if (selectedMonster == null || !selectedMonster.IsMonster) return Reject("Elige un Monster válido.");
            PouchState nextPlayer, nextBot;
            try
            {
                nextPlayer = new PouchState(Config, matchSeed, monsterId, BoardSide.Blue, selection);
                string rivalMonster = Config.Units.FirstOrDefault(d => d.IsMonster && d.Id != monsterId)?.Id ?? monsterId;
                nextBot = new PouchState(Config, matchSeed ^ 0x45fa1, rivalMonster, BoardSide.Red, nextPlayer.SelectedWhelpIds);
            }
            catch (ArgumentException error) { return Reject(error.Message); }
            Simulation?.Stop(); Simulation=null;
            ClearActors(); Board.BuildBoard(); Paused = false; Time.timeScale = 1;
            seed = matchSeed; chosenMonster = monsterId; Round = 0; PlayerWins = BotWins = 0;
            chosenWhelpIds = nextPlayer.SelectedWhelpIds.ToArray();
            Player = nextPlayer;
            Bot = nextBot;
            BeginRound();
            return true;
        }
        public void Rematch() { StartMatch(chosenMonster, chosenWhelpIds, seed); }
        public void Menu()
        {
            Simulation?.Stop();
            Paused = false; Time.timeScale = 1; ClearActors(); Board.BuildBoard();
            Player = Bot = null; Simulation = null; Round = PlayerWins = BotWins = 0;
            Phase = MatchPhase.Menu; Notice = ""; Changed?.Invoke();
        }
        void BeginRound()
        {
            Simulation?.Stop();
            Simulation = null; ClearActors(); Board.BuildBoard(); Round++; accumulator = 0;
            Player.BeginPreparation(Round); Bot.BeginPreparation(Round);
            Phase = MatchPhase.Preparation; Remaining = 40; Notice = "";
            EnsureMonsterPosition(Player); EnsureMonsterPosition(Bot);
            SyncActors(); RunBot(Bot); SyncActors(); Changed?.Invoke();
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
                (c.OccupiedBy == null || (actors.TryGetValue(unit, out var a) && c.OccupiedBy == a)) &&
                !owner.Owned.Values.Any(o => o != unit && o.Location == UnitLocation.Field && o.Deployment == new Vector2Int(c.X,c.Y)))
                .FirstOrDefault();
        }
        public void RunBot(PouchState owner)
        {
            if (Phase != MatchPhase.Preparation) return;
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
            var all = Player.Owned.Values.Concat(Bot.Owned.Values).ToList();
            foreach (var pair in actors.ToList())
                if (!all.Contains(pair.Key) || pair.Key.Location != UnitLocation.Field) RemoveActor(pair.Key, pair.Value);
            foreach (var owner in new[] { Player, Bot })
                foreach (var owned in owner.Owned.Values)
                {
                    if (owned.Location != UnitLocation.Field || actors.ContainsKey(owned)) continue;
                    GameObject go = new GameObject(owner.Side + "-" + owned.Definition.Id);
                    go.transform.SetParent(transform, false);
                    BattleUnit actor = owned.Definition.IsMonster ? (BattleUnit)go.AddComponent<MonsterUnit>() : go.AddComponent<WhelpUnit>();
                    actor.Initialize(owner.Side + "-" + owned.Definition.Id, owner.Side, UnitStats.FromDefinition(owned.Definition, owned, false));
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
            if (!CanPrepare()) return false;
            bool ok = Player.TryBuy(slot, token, out string reason);
            if (ok) { SyncActors(); Notice = "Copia adquirida. Selecciona el Whelp del Brief y toca una casilla."; Sound?.Invoke("buy"); }
            else Reject(reason);
            Changed?.Invoke(); return ok;
        }
        public bool BuyAndPlace(int slot, long token, BoardCell cell, bool bench = false)
        {
            if (!CanPrepare()) return false;
            Vector2Int position = Vector2Int.zero;
            if (!bench)
            {
                BattleUnit existingActor = null;
                if (slot >= 0 && slot < Player.Offers.Count && Player.Offers[slot] != null &&
                    Player.Owned.TryGetValue(Player.Offers[slot].UnitId, out var existing)) actors.TryGetValue(existing, out existingActor);
                if (cell == null || !Board.IsManagedCell(cell) || cell.IsBlocked ||
                    (cell.ReservedBy != null && cell.ReservedBy != existingActor)) return Reject("La casilla no está disponible.");
                if (cell.OccupiedBy != null && cell.OccupiedBy != existingActor) return Reject("La casilla está ocupada.");
                position = cell.Coordinates;
            }
            bool ok = Player.TryBuyAndPlace(slot, token, bench ? UnitLocation.Bench : UnitLocation.Field, position, out string reason);
            if (ok) { SyncActors(); Notice = "Copia adquirida; las mejoras conservan la posición de tu Whelp."; Sound?.Invoke("buy"); }
            else Reject(reason);
            Changed?.Invoke(); return ok;
        }
        public bool Reroll(int version)
        {
            if (!CanPrepare()) return false;
            bool ok = Player.TryReroll(version, out string reason);
            if (ok) { Notice = "Nuevas ofertas"; Sound?.Invoke("button"); } else Reject(reason);
            Changed?.Invoke(); return ok;
        }
        public bool UpgradeMonster()
        {
            if (!CanPrepare()) return false;
            bool ok = Player.TryUpgradeMonster(out string reason);
            if (ok) { Notice = "Mejora activa en el próximo combate; posición fija desde la siguiente ronda."; Sound?.Invoke("buy"); } else Reject(reason);
            Changed?.Invoke(); return ok;
        }
        public bool UpgradeMonster(int index)
        {
            if (!CanPrepare()) return false;
            bool ok = Player.TryUpgradeMonster(index, out string reason);
            if (ok) { Notice = "Habilidad activada para el próximo combate."; Sound?.Invoke("buy"); }
            else Reject(reason);
            Changed?.Invoke(); return ok;
        }
        public bool ChooseTrick(string id, int index)
        {
            if (!CanPrepare()) return false;
            bool ok = Player.TrySelectTrick(id, index, out string reason);
            if (ok) Notice = "Trick elegido para la próxima copia."; else Reject(reason);
            Changed?.Invoke(); return ok;
        }
        public bool BuyTrick(string id, int index, int slot, long token)
        {
            if (!CanPrepare()) return false;
            bool ok = Player.TryBuyTrick(id, index, slot, token, out string reason);
            if (ok) { SyncActors(); Notice = "Mejora comprada."; Sound?.Invoke("buy"); }
            else Reject(reason);
            Changed?.Invoke(); return ok;
        }
        public bool Place(string id, BoardCell cell)
        {
            if (!CanPrepare() || cell == null || !Player.Owned.TryGetValue(id, out var owned)) return false;
            actors.TryGetValue(owned, out var actor);
            if (!Board.IsManagedCell(cell) || cell.IsBlocked || (cell.ReservedBy != null && cell.ReservedBy != actor))
                return Reject("La casilla no está disponible.");
            if (cell.OccupiedBy != null && cell.OccupiedBy != actor) return Reject("La casilla está ocupada.");
            var pos = new Vector2Int(cell.X, cell.Y);
            UnitLocation previousLocation=owned.Location; Vector2Int previousCell=owned.Deployment;
            if (!Player.TrySetLocation(id, UnitLocation.Field, pos, out string reason)) return Reject(reason);
            if (actor != null)
            {
                if(!Board.TryRepositionUnit(actor, cell))
                { Player.TrySetLocation(id,previousLocation,previousCell,out _);return Reject("No se puede ocupar esa casilla."); }
                actor.transform.position = Mapper.GetWorldPosition(cell);
            }
            SyncActors(); Notice = owned.Definition.DisplayName + " colocado"; Sound?.Invoke("button"); Changed?.Invoke(); return true;
        }
        public bool Store(string id, bool bench)
        {
            if (!CanPrepare()) return false;
            bool ok = Player.TrySetLocation(id, bench ? UnitLocation.Bench : UnitLocation.Brief, Vector2Int.zero, out string reason);
            if (!ok) return Reject(reason);
            SyncActors(); Notice = bench ? "Whelp en el bench" : "Whelp en el Brief"; Changed?.Invoke(); return true;
        }
        public bool Sell(string id)
        {
            if (!CanPrepare()) return false;
            if (!Player.TrySell(id, out string reason)) return Reject(reason);
            SyncActors(); Notice = "Venta completada: recuperaste lo pagado."; Sound?.Invoke("buy"); Changed?.Invoke(); return true;
        }
        bool CanPrepare() => Phase == MatchPhase.Preparation && !Paused;
        public bool Reject(string text) { Notice = text; Sound?.Invoke("reject"); Changed?.Invoke(); return false; }
        public void Ready()
        {
            if (!CanPrepare()) return;
            Player.EndPreparation(); Bot.EndPreparation(); Phase = MatchPhase.Combat; Remaining = 40; accumulator = 0;
            Simulation = new CombatSimulation(Board);
            Simulation.Moved += (actor, from, to) => Moved?.Invoke(actor, from, to);
            Simulation.Attacked += (actor, target, projectile) => { Attacked?.Invoke(actor,target,projectile); };
            Simulation.Impacted += (actor, target, damage) => { Impacted?.Invoke(actor,target,damage); Sound?.Invoke("hit"); };
            Simulation.Died += actor => { Died?.Invoke(actor); Sound?.Invoke("death"); };
            Simulation.Reviving += (actor, duration) => Reviving?.Invoke(actor, duration);
            Simulation.Revived += actor => { Revived?.Invoke(actor); Sound?.Invoke("buy"); };
            Simulation.LethalImpacted += (actor, target) => LethalImpacted?.Invoke(actor, target);
            Simulation.AbilityUsed += (actor, effect) => AbilityUsed?.Invoke(actor, effect);
            Simulation.BoulderRolled += (actor, from, to, duration) => BoulderRolled?.Invoke(actor, from, to, duration);
            Simulation.PermanentDamageEarned += (actor, amount) =>
            {
                foreach (var pair in actors) if (pair.Value == actor) { pair.Key.PersistentDamageBonus += amount; break; }
            };
            Simulation.PositionMarked += actor =>
            {
                foreach (var pair in actors) if (pair.Value == actor) { if (pair.Key.MarkedPositionRound == 0) pair.Key.MarkedPositionRound = Round; break; }
            };
            Simulation.CurrencyEarned += (side, amount) => (side == BoardSide.Blue ? Player : Bot).AddCombatCoins(amount);
            Simulation.CurrencyStolen += (side, amount) =>
            {
                PouchState recipient = side == BoardSide.Blue ? Player : Bot;
                PouchState victim = side == BoardSide.Blue ? Bot : Player;
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
            Simulation.Begin(actors.Values.ToList(), actors.ToDictionary(p => p.Value, p => p.Key));
            Notice = ""; Changed?.Invoke();
        }
        void Update() { Advance(Time.unscaledDeltaTime); }
        public void Advance(float dt)
        {
            if (Paused || dt <= 0) return;
            if (Phase == MatchPhase.Preparation) { Remaining = Mathf.Max(0, Remaining - dt); if (Remaining <= 0) Ready(); }
            else if (Phase == MatchPhase.Combat)
            {
                accumulator += dt;
                while (accumulator + .00001f >= .1f && Phase == MatchPhase.Combat)
                {
                    accumulator -= .1f; Simulation.Step(.1f); Remaining = Mathf.Max(0,40 - Simulation.Elapsed);
                    if (Simulation.Finished) EndRound();
                }
            }
        }
        void EndRound()
        {
            if (Simulation.Winner == BoardSide.Blue) PlayerWins++;
            if (Simulation.Winner == BoardSide.Red) BotWins++;
            string reason=Simulation.Reason;
            if(Simulation.Winner==BoardSide.Red && reason=="El equipo rival perdió todas sus unidades")reason="Tu equipo perdió todas sus unidades";
            Result = (Simulation.Winner == null ? "Empate" : Simulation.Winner == BoardSide.Blue ? "¡Ronda ganada!" : "Ronda del rival") + "\n" + reason;
            Phase = PlayerWins >= 3 || BotWins >= 3 ? MatchPhase.MatchResult : MatchPhase.RoundResult;
            Notice = ""; Sound?.Invoke("result"); Changed?.Invoke();
        }
        public void NextRound() { if (Phase == MatchPhase.RoundResult && !Paused) BeginRound(); }
        public void Pause(bool paused) { Paused = paused; Time.timeScale = paused ? 0 : 1; Changed?.Invoke(); }
        void OnDestroy() { Time.timeScale = 1; }
    }
}
