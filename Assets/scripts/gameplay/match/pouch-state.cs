using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MonsterPouch.Gameplay.Board;
using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    public enum UnitLocation { Brief, Bench, Field }

    public sealed class OwnedUnit
    {
        public UnitDefinition Definition { get; }
        public int Copies { get; internal set; }
        public bool[] Tricks { get; } = new bool[3];
        public int NextTrick { get; internal set; } = -1;
        public int Paid { get; internal set; }
        public UnitLocation Location { get; internal set; } = UnitLocation.Brief;
        public int BriefSlot { get; internal set; } = -1;
        public Vector2Int Deployment { get; internal set; }
        public int UpgradePurchasedRound { get; internal set; }
        public int PersistentDamageBonus { get; internal set; }
        public int MarkedPositionRound { get; internal set; }
        public bool HasMonsterUpgrade => UpgradePurchasedRound > 0;
        public int TrickCount => (Tricks[0] ? 1 : 0) + (Tricks[1] ? 1 : 0) + (Tricks[2] ? 1 : 0);
        public bool IsPositionLocked(int round) => (HasMonsterUpgrade && round > UpgradePurchasedRound) || (MarkedPositionRound > 0 && round > MarkedPositionRound);
        public bool IsUpgradeActive(int combatRound) => HasMonsterUpgrade && combatRound >= UpgradePurchasedRound;

        public OwnedUnit(UnitDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        public int PendingTrickIndex
        {
            get
            {
                if (NextTrick >= 0 && NextTrick < Tricks.Length && !Tricks[NextTrick]) return NextTrick;
                for (int i = 0; i < Tricks.Length; i++) if (!Tricks[i]) return i;
                return -1;
            }
        }
    }

    public sealed class PouchOffer
    {
        public string UnitId { get; }
        public long Token { get; }
        internal PouchOffer(string unitId, long token) { UnitId = unitId; Token = token; }
    }

    /// <summary>
    /// A player's persistent roster and finite copy pool. Mutations validate first, then commit once.
    /// Combat consumes snapshots of this state; it never writes health into the definitions or roster.
    /// </summary>
    public sealed class PouchState
    {
        private readonly System.Random random;
        private readonly List<string> available = new List<string>();
        private readonly Dictionary<string, OwnedUnit> owned = new Dictionary<string, OwnedUnit>(StringComparer.Ordinal);
        private readonly PouchOffer[] offers = new PouchOffer[MatchConfig.OfferSlots];
        // Tokens identify commands across players and rematches as well as within a single pouch.
        // They are not part of random generation or combat determinism.
        private static long nextToken;
        private static int nextVersion;

        public MatchConfig Config { get; }
        public BoardSide Side { get; }
        public int Round { get; private set; }
        public int Coins { get; private set; }
        public int Rerolls { get; private set; }
        public int OfferVersion { get; private set; }
        public bool IsPreparation { get; private set; }
        public IReadOnlyDictionary<string, OwnedUnit> Owned { get; }
        public IReadOnlyList<PouchOffer> Offers { get; }
        public IReadOnlyList<string> SelectedWhelpIds { get; }
        public OwnedUnit Monster { get; }
        public int PoolCount => available.Count;

        public PouchState(MatchConfig config, int seed, string monsterId, BoardSide side, IEnumerable<string> whelpIds = null)
        {
            Config = config != null ? config : throw new ArgumentNullException(nameof(config));
            Side = side;
            random = new System.Random(seed);
            UnitDefinition monster = config.Get(monsterId);
            if (monster == null || !monster.IsMonster) throw new ArgumentException("Elige un Monster válido.", nameof(monsterId));
            if (!config.TryResolveWhelpSelection(whelpIds, out string[] selection, out string reason))
                throw new ArgumentException(reason, nameof(whelpIds));
            SelectedWhelpIds = Array.AsReadOnly(selection);
            var selected = new HashSet<string>(selection, StringComparer.Ordinal);
            foreach (UnitDefinition definition in config.Units)
            {
                if (definition.IsMonster) continue;
                if (definition.Tricks == null || definition.Tricks.Length != 3)
                    throw new ArgumentException("Cada Whelp necesita tres Tricks.", nameof(config));
                foreach (TrickDefinition trick in definition.Tricks)
                    if (trick == null) throw new ArgumentException("Falta un Trick del Whelp.", nameof(config));
                if (!selected.Contains(definition.Id)) continue;
                for (int copy = 0; copy < MatchConfig.CopiesPerWhelp; copy++) available.Add(definition.Id);
            }
            Monster = new OwnedUnit(monster) { Copies = 1, Location = UnitLocation.Field };
            List<Vector2Int> placement = config.FormationCells(monster, side);
            if (placement.Count == 0) throw new ArgumentException("La máscara de despliegue del Monster está vacía.", nameof(config));
            Monster.Deployment = placement[0];
            owned.Add(monster.Id, Monster);
            Owned = new ReadOnlyDictionary<string, OwnedUnit>(owned);
            Offers = Array.AsReadOnly(offers);
        }

        /// <summary>Round advancement is strictly sequential and idempotent for the current round.</summary>
        public bool BeginPreparation(int round)
        {
            if (round != Round + 1 || (Round > 0 && IsPreparation)) return false;
            Round = round;
            IsPreparation = true;
            Coins += Config.IncomeForRound(round);
            Rerolls += MatchConfig.FreeRerollsPerRound;
            FillEmptyOffers();
            AdvanceOfferVersion();
            return true;
        }

        public void EndPreparation() => IsPreparation = false;

        public int GetOfferCost(int slot)
        {
            if (!ValidSlot(slot) || offers[slot] == null) return -1;
            UnitDefinition definition = Config.Get(offers[slot].UnitId);
            if (definition == null) return -1;
            if (!owned.TryGetValue(definition.Id, out OwnedUnit unit)) return Mathf.Max(0, definition.BaseCost);
            int index = unit.PendingTrickIndex;
            return index < 0 || unit.Copies >= MatchConfig.CopiesPerWhelp ? -1 : Mathf.Max(0, definition.Tricks[index].Cost);
        }

        public string GetOfferEffect(int slot)
        {
            if (!ValidSlot(slot) || offers[slot] == null) return "Sin oferta";
            UnitDefinition definition = Config.Get(offers[slot].UnitId);
            if (definition == null) return "No disponible";
            if (!owned.TryGetValue(definition.Id, out OwnedUnit unit)) return "Desbloquear " + definition.DisplayName;
            int index = unit.PendingTrickIndex;
            return index < 0 ? "Tricks completos" : definition.Tricks[index].Name;
        }

        public bool TryBuy(int slot, long token, out string reason)
        {
            if (!RequirePreparation(out reason)) return false;
            if (!ValidatePurchase(slot, token, out UnitDefinition definition, out int price, out reason)) return false;
            PouchOffer offer = offers[slot];

            // No calls or events between validation and commit: a second command sees a spent token.
            if (!owned.TryGetValue(offer.UnitId, out OwnedUnit unit))
            {
                unit = new OwnedUnit(definition) { BriefSlot = slot };
                owned.Add(offer.UnitId, unit);
            }
            else
            {
                unit.Tricks[unit.PendingTrickIndex] = true;
                unit.NextTrick = -1;
            }
            unit.Copies++;
            unit.Paid += price;
            Coins -= price;
            offers[slot] = null;
            AdvanceOfferVersion();
            reason = string.Empty;
            return true;
        }

        /// <summary>A new Whelp buys and deploys once; a repeat copy upgrades its existing unit in place.</summary>
        public bool TryBuyTrick(string unitId, int trickIndex, int slot, long token, out string reason)
        {
            if (!RequirePreparation(out reason)) return false;
            if (!ValidSlot(slot) || offers[slot] == null || offers[slot].Token != token || offers[slot].UnitId != unitId)
                return Reject("Esa oferta ya no está disponible.", out reason);
            if (!owned.TryGetValue(unitId, out OwnedUnit unit) || unit.Definition.IsMonster ||
                trickIndex < 0 || trickIndex >= 3 || unit.Tricks[trickIndex])
                return Reject("Ese Trick no está pendiente.", out reason);
            int previous = unit.NextTrick;
            unit.NextTrick = trickIndex;
            if (TryBuy(slot, token, out reason)) return true;
            unit.NextTrick = previous;
            return false;
        }

        public bool TryBuyAndPlace(int slot, long token, UnitLocation location, Vector2Int cell, out string reason)
        {
            if (!RequirePreparation(out reason)) return false;
            if (!ValidatePurchase(slot, token, out UnitDefinition definition, out _, out reason)) return false;
            if (location != UnitLocation.Field && location != UnitLocation.Bench)
                return Reject("Arrastra la oferta al campo o al bench.", out reason);
            bool alreadyOwned = owned.ContainsKey(definition.Id);
            if (!ValidateDestination(definition.Id, location, cell, !alreadyOwned, out reason)) return false;
            if (!TryBuy(slot, token, out reason)) return false;
            if (!alreadyOwned)
            {
                OwnedUnit unit = owned[definition.Id];
                unit.Location = location;
                unit.BriefSlot = -1;
                if (location == UnitLocation.Field) unit.Deployment = cell;
            }
            return true;
        }

        private bool ValidatePurchase(int slot, long token, out UnitDefinition definition, out int price, out string reason)
        {
            definition = null;
            price = -1;
            if (!ValidSlot(slot) || offers[slot] == null || offers[slot].Token != token)
                return Reject("Esa oferta ya no está disponible.", out reason);
            PouchOffer offer = offers[slot];
            definition = Config.Get(offer.UnitId);
            price = GetOfferCost(slot);
            if (definition == null || definition.IsMonster || price < 0)
                return Reject("Este Whelp ya tiene sus tres Tricks.", out reason);
            if (Coins < price) return Reject("No tienes suficientes Moon Tokens.", out reason);

            reason = string.Empty;
            return true;
        }

        public bool TryReroll(int expectedVersion, out string reason)
        {
            if (!RequirePreparation(out reason)) return false;
            if (expectedVersion != OfferVersion) return Reject("La Pouch ya cambió.", out reason);
            if (Rerolls <= 0) return Reject("No quedan rerolls.", out reason);
            bool freeSlot = false;
            for (int i = 0; i < offers.Length; i++) freeSlot |= !IsBriefSlotOccupied(i);
            if (!freeSlot) return Reject("Libera un hueco del Brief para recibir ofertas.", out reason);
            bool any = available.Count > 0;
            foreach (PouchOffer offer in offers) any |= offer != null;
            if (!any) return Reject("Ya adquiriste todas las copias.", out reason);
            for (int i = 0; i < offers.Length; i++)
            {
                if (offers[i] != null) available.Add(offers[i].UnitId);
                offers[i] = null;
            }
            Rerolls--;
            FillEmptyOffers();
            AdvanceOfferVersion();
            reason = string.Empty;
            return true;
        }

        public bool TrySelectTrick(string unitId, int trickIndex, out string reason)
        {
            if (!RequirePreparation(out reason)) return false;
            if (!owned.TryGetValue(unitId ?? string.Empty, out OwnedUnit unit) || unit.Definition.IsMonster)
                return Reject("Selecciona un Whelp propio.", out reason);
            if (trickIndex < 0 || trickIndex >= 3 || unit.Tricks[trickIndex])
                return Reject("Ese Trick no está pendiente.", out reason);
            bool copyShown = false;
            foreach (PouchOffer offer in offers) copyShown |= offer != null && offer.UnitId == unitId;
            if (!copyShown) return Reject("Necesitas una copia de este Whelp en la Pouch.", out reason);
            unit.NextTrick = trickIndex;
            AdvanceOfferVersion();
            reason = string.Empty;
            return true;
        }

        public bool TryUpgradeMonster(out string reason)
        {
            if (Monster.Definition.UsesDocumentedRules)
                return TryUpgradeMonster(Monster.PendingTrickIndex, out reason);
            if (!RequirePreparation(out reason)) return false;
            TrickDefinition upgrade = Monster.Definition.MonsterUpgrade;
            if (upgrade == null || Monster.HasMonsterUpgrade) return Reject("Mejora ya adquirida o no disponible.", out reason);
            int price = Mathf.Max(0, upgrade.Cost);
            if (Coins < price) return Reject("No tienes suficientes Moon Tokens.", out reason);
            Coins -= price;
            Monster.Paid += price;
            Monster.UpgradePurchasedRound = Round;
            reason = string.Empty;
            return true;
        }

        public bool TryUpgradeMonster(int index, out string reason)
        {
            if (!RequirePreparation(out reason)) return false;
            if (!Monster.Definition.UsesDocumentedRules) return TryUpgradeMonster(out reason);
            if (index < 0 || index >= 3 || Monster.Tricks[index] || Monster.Definition.Tricks[index] == null)
                return Reject("Mejora ya adquirida o no disponible.", out reason);
            int price = Mathf.Max(0, Monster.Definition.Tricks[index].Cost);
            if (Coins < price) return Reject("No tienes suficientes Moon-Ken.", out reason);
            Coins -= price; Monster.Paid += price; Monster.Tricks[index] = true;
            if (!Monster.HasMonsterUpgrade) Monster.UpgradePurchasedRound = Round;
            reason = string.Empty; return true;
        }

        public int AddCombatCoins(int amount)
        {
            int before = Coins; Coins = Mathf.Max(0, Coins + amount); return Coins - before;
        }

        public bool TrySell(string unitId, out string reason)
        {
            if (!RequirePreparation(out reason)) return false;
            if (!owned.TryGetValue(unitId ?? string.Empty, out OwnedUnit unit) || unit.Definition.IsMonster)
                return Reject("Solo puedes vender tus Whelps.", out reason);
            Coins += unit.Paid;
            if (Config.ReturnSoldCopies)
                for (int i = 0; i < unit.Copies; i++) available.Add(unitId);
            owned.Remove(unitId);
            AdvanceOfferVersion();
            reason = string.Empty;
            return true;
        }

        public bool TrySetLocation(string unitId, UnitLocation location, Vector2Int cell, out string reason)
        {
            if (!RequirePreparation(out reason)) return false;
            if (!owned.TryGetValue(unitId ?? string.Empty, out OwnedUnit unit))
                return Reject("Primero desbloquea este Whelp.", out reason);
            if (location != UnitLocation.Brief && location != UnitLocation.Bench && location != UnitLocation.Field)
                return Reject("Destino inválido.", out reason);
            if (unit.Definition.IsMonster && location != UnitLocation.Field)
                return Reject("Tu Monster permanece en el campo.", out reason);
            if (unit.IsPositionLocked(Round) && (location != unit.Location || cell != unit.Deployment))
                return Reject(unit.MarkedPositionRound > 0 && Round > unit.MarkedPositionRound ? "Tauris marcó esta unidad: su posición está fija." : "La mejora del Monster fija su posición desde esta ronda.", out reason);
            int briefSlot = -1;
            if (location == UnitLocation.Brief)
            {
                if (unit.Location == UnitLocation.Brief && ValidSlot(unit.BriefSlot) &&
                    offers[unit.BriefSlot] == null && !IsBriefSlotOccupied(unit.BriefSlot, unit)) briefSlot = unit.BriefSlot;
                else
                    for (int i = 0; i < offers.Length; i++)
                        if (offers[i] == null && !IsBriefSlotOccupied(i, unit)) { briefSlot = i; break; }
                if (briefSlot < 0) return Reject("El Brief no tiene un hueco libre.", out reason);
            }
            else if (!ValidateDestination(unit.Definition.Id, location, cell, !unit.Definition.IsMonster, out reason)) return false;
            unit.Location = location;
            unit.BriefSlot = briefSlot;
            if (location == UnitLocation.Field) unit.Deployment = cell;
            reason = string.Empty;
            return true;
        }

        private bool ValidateDestination(string unitId, UnitLocation location, Vector2Int cell, bool requireFieldCapacity, out string reason)
        {
            if (location == UnitLocation.Field)
            {
                if (!Config.IsDeploymentLegal(Side, cell)) return Reject("Elige una casilla de despliegue válida.", out reason);
                int whelps = 0;
                foreach (OwnedUnit other in owned.Values)
                {
                    if (other.Definition.Id == unitId || other.Location != UnitLocation.Field) continue;
                    if (other.Deployment == cell) return Reject("La casilla está ocupada.", out reason);
                    if (!other.Definition.IsMonster) whelps++;
                }
                if (requireFieldCapacity && whelps >= MatchConfig.MaxFieldWhelps)
                    return Reject("El campo admite cinco Whelps distintos.", out reason);
            }
            else if (location == UnitLocation.Bench)
            {
                foreach (OwnedUnit other in owned.Values)
                    if (other.Definition.Id != unitId && other.Location == UnitLocation.Bench)
                        return Reject("El bench está ocupado.", out reason);
            }
            reason = string.Empty;
            return true;
        }

        private bool IsBriefSlotOccupied(int slot, OwnedUnit except = null)
        {
            foreach (OwnedUnit unit in owned.Values)
                if (unit != except && unit.Location == UnitLocation.Brief && unit.BriefSlot == slot) return true;
            return false;
        }

        /// <summary>All unpurchased copies, including displayed offers.</summary>
        public int RemainingCopies(string unitId)
        {
            int count = 0;
            foreach (string id in available) if (id == unitId) count++;
            foreach (PouchOffer offer in offers) if (offer != null && offer.UnitId == unitId) count++;
            return count;
        }

        private void FillEmptyOffers()
        {
            for (int i = 0; i < offers.Length && available.Count > 0; i++)
            {
                if (offers[i] != null || IsBriefSlotOccupied(i)) continue;
                int index = random.Next(available.Count);
                string id = available[index];
                available.RemoveAt(index);
                offers[i] = new PouchOffer(id, System.Threading.Interlocked.Increment(ref nextToken));
            }
        }

        private bool RequirePreparation(out string reason)
        {
            reason = IsPreparation ? string.Empty : "Esta acción solo está disponible durante la preparación.";
            return IsPreparation;
        }

        private void AdvanceOfferVersion() => OfferVersion = System.Threading.Interlocked.Increment(ref nextVersion);

        private static bool ValidSlot(int slot) => slot >= 0 && slot < MatchConfig.OfferSlots;
        private static bool Reject(string message, out string reason) { reason = message; return false; }
    }
}
