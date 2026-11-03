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
        public AbilityAccessSnapshot AbilityAccess { get; }
        public int Copies { get; internal set; }
        public bool[] Tricks { get; } = new bool[3];
        public int NextTrick { get; internal set; } = -1;
        public int Paid { get; internal set; }
        // Paid records acquisition/upgrades, not repeated deployment payments.
        public int BankRefunded { get; internal set; }
        public int BankReservedCost { get; internal set; }
        public int BankRedeployCost => BankRefunded + BankReservedCost;
        public int SaleRefund => Mathf.Max(0, Paid - BankRefunded);
        public UnitLocation Location { get; internal set; } = UnitLocation.Brief;
        public int BriefSlot { get; internal set; } = -1;
        public Vector2Int Deployment { get; internal set; }
        public int UpgradePurchasedRound { get; internal set; }
        public int PersistentDamageBonus { get; internal set; }
        public int PersistentHealthBonus { get; internal set; }
        public int MarkedPositionRound { get; internal set; }
        public int HeroStarCount => Definition.Ruleset == CombatRuleset.Eronya && Definition.IsMonster ? TrickCount : 0;
        public bool HasMonsterUpgrade => Definition.Ruleset == CombatRuleset.Eronya && Definition.IsMonster ? HeroStarCount > 0 : UpgradePurchasedRound > 0;
        public int TrickCount => (Tricks[0] ? 1 : 0) + (Tricks[1] ? 1 : 0) + (Tricks[2] ? 1 : 0);
        // Historical free unlocks have no purchase timestamp; do not invent one while retaining their flags.
        public bool IsPositionLocked(int round) => ((HasMonsterUpgrade && UpgradePurchasedRound > 0 && round > UpgradePurchasedRound) || (MarkedPositionRound > 0 && round > MarkedPositionRound));
        public bool IsUpgradeActive(int combatRound) => HasMonsterUpgrade && combatRound >= UpgradePurchasedRound;

        public OwnedUnit(UnitDefinition definition, AbilityAccessSnapshot abilityAccess = null)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            AbilityAccess = abilityAccess ?? AbilityAccessSnapshot.Unrestricted;
        }

        public int PendingTrickIndex
        {
            get
            {
                if (NextTrick >= 0 && NextTrick < Tricks.Length && !Tricks[NextTrick] && AbilityAccess.Allows(Definition, NextTrick)) return NextTrick;
                for (int i = 0; i < Tricks.Length; i++) if (!Tricks[i] && AbilityAccess.Allows(Definition, i)) return i;
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
    public sealed partial class PouchState
    {
        private System.Random random;
        private readonly List<string> available = new List<string>();
        private readonly Dictionary<string, OwnedUnit> owned = new Dictionary<string, OwnedUnit>(StringComparer.Ordinal);
        private readonly Dictionary<string, OwnedUnit> bankReserves = new Dictionary<string, OwnedUnit>(StringComparer.Ordinal);
        private readonly PouchOffer[] offers = new PouchOffer[MatchConfig.OfferSlots];
        // Tokens identify commands across players and rematches as well as within a single pouch.
        // They are not part of random generation or combat determinism.
        private static long nextToken;
        private static int nextVersion;

        public MatchConfig Config { get; }
        public AbilityAccessSnapshot AbilityAccess { get; }
        public int CopyLimit(string id) => AbilityAccess.CopyLimit(Config.Get(id));
        public BoardSide Side { get; }
        public int Round { get; private set; }
        public int Coins { get; private set; }
        public int Rerolls { get; private set; }
        public int OfferVersion { get; private set; }
        public bool IsPreparation { get; private set; }
        public IReadOnlyDictionary<string, OwnedUnit> Owned { get; }
        public IReadOnlyDictionary<string, OwnedUnit> BankReserves { get; }
        public IEnumerable<OwnedUnit> AllOwned
        {
            get
            {
                foreach (OwnedUnit unit in owned.Values) yield return unit;
                foreach (OwnedUnit unit in bankReserves.Values) yield return unit;
            }
        }
        public IEnumerable<OwnedUnit> BankedUnits
        {
            get
            {
                foreach (OwnedUnit unit in owned.Values) if (unit.Location == UnitLocation.Bench) yield return unit;
                foreach (OwnedUnit unit in bankReserves.Values) yield return unit;
            }
        }
        public IReadOnlyList<PouchOffer> Offers { get; }
        public IReadOnlyList<string> SelectedWhelpIds { get; }
        private readonly Dictionary<string,int> combatTeamSlots = new Dictionary<string,int>(StringComparer.Ordinal);
        public int CombatTeamSlot(string id) => combatTeamSlots.TryGetValue(id ?? "",out var slot) ? slot : int.MaxValue;
        public OwnedUnit Monster { get; }
        public int PoolCount => available.Count;

        public PouchState(MatchConfig config, int seed, string monsterId, BoardSide side, IEnumerable<string> whelpIds = null, AbilityAccessSnapshot abilityAccess = null)
        {
            Config = config != null ? config : throw new ArgumentNullException(nameof(config));
            Side = side;
            AbilityAccess = abilityAccess ?? AbilityAccessSnapshot.Unrestricted;
            AbilityAccess.ValidateFor(Config);
            randomSeed = seed;
            random = new System.Random(seed);
            UnitDefinition monster = config.Get(monsterId);
            if (monster == null || !monster.IsMonster) throw new ArgumentException("Elige un Monster válido.", nameof(monsterId));
            var requestedTeam = whelpIds == null ? null : new List<string>(whelpIds);
            if (!config.TryResolveWhelpSelection(requestedTeam, out string[] selection, out string reason))
                throw new ArgumentException(reason, nameof(whelpIds));
            SelectedWhelpIds = Array.AsReadOnly(selection);
            combatTeamSlots[monsterId] = 0;
            int nextTeamSlot = 1;
            foreach (var selectedId in requestedTeam ?? new List<string>(selection))
                if (!combatTeamSlots.ContainsKey(selectedId)) combatTeamSlots[selectedId] = nextTeamSlot++;
            var selected = new HashSet<string>(selection, StringComparer.Ordinal);
            foreach (UnitDefinition definition in config.Units)
            {
                if (definition.IsMonster) continue;
                if (definition.Tricks == null || definition.Tricks.Length != 3)
                    throw new ArgumentException("Cada Whelp necesita tres Tricks.", nameof(config));
                foreach (TrickDefinition trick in definition.Tricks)
                    if (trick == null) throw new ArgumentException("Falta un Trick del Whelp.", nameof(config));
                if (!selected.Contains(definition.Id)) continue;
                for (int copy = 0; copy < CopyLimit(definition.Id); copy++) available.Add(definition.Id);
            }
            Monster = new OwnedUnit(monster, AbilityAccess) { Copies = 1, Location = UnitLocation.Field };
            List<Vector2Int> placement = config.FormationCells(monster, side);
            if (placement.Count == 0) throw new ArgumentException("La máscara de despliegue del Monster está vacía.", nameof(config));
            Monster.Deployment = placement[0];
            owned.Add(monster.Id, Monster);
            Owned = new ReadOnlyDictionary<string, OwnedUnit>(owned);
            BankReserves = new ReadOnlyDictionary<string, OwnedUnit>(bankReserves);
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

        // Only a newly constructed training pouch uses this entry. Normal rounds
        // still receive their configured income, offers and deployed Monster.
        internal void BeginTrainingPreparation()
        {
            Round = 1;
            IsPreparation = true;
            Monster.Location = UnitLocation.Brief;
            Monster.BriefSlot = 0;
        }

        internal OwnedUnit AddTrainingWhelp(string id)
        {
            if(!IsPreparation||owned.ContainsKey(id)||!available.Remove(id))throw new ArgumentException("Invalid prepared training Whelp.");
            var unit=new OwnedUnit(Config.Get(id), AbilityAccess){Copies=1,Location=UnitLocation.Brief,BriefSlot=0,Paid=0};
            owned.Add(id,unit);return unit;
        }

        internal void PrepareTrainingOffers(bool withBudget)
        {
            // The practice pouch selects one existing Whelp, retaining its real
            // four-copy pool, costs, purchases, upgrades and bank accounting.
            Coins=0;
            if(withBudget)
                foreach(var id in SelectedWhelpIds)
                {
                    var definition=Config.Get(id);
                    Coins=checked(Coins+Mathf.Max(0,definition.BaseCost));
                    foreach(var trick in definition.Tricks)Coins=checked(Coins+Mathf.Max(0,trick.Cost));
                }
            Rerolls=MatchConfig.FreeRerollsPerRound;
            FillEmptyOffers();AdvanceOfferVersion();
        }

        public void EndPreparation() => IsPreparation = false;

        public int GetOfferCost(int slot)
            => GetOfferCost(slot, -1);

        private int GetOfferCost(int slot, int selectedTrick)
        {
            if (!ValidSlot(slot) || offers[slot] == null) return -1;
            UnitDefinition definition = Config.Get(offers[slot].UnitId);
            if (definition == null) return -1;
            if (!owned.TryGetValue(definition.Id, out OwnedUnit unit)) return Mathf.Max(0, definition.BaseCost);
            int index = selectedTrick >= 0 ? selectedTrick : unit.PendingTrickIndex;
            return index < 0 || !AbilityAccess.Allows(definition, index) || CountOwnedCopies(definition.Id) >= CopyLimit(definition.Id) ? -1 : Mathf.Max(0, definition.Tricks[index].Cost);
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
                unit = new OwnedUnit(definition, AbilityAccess) { BriefSlot = slot };
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
                trickIndex < 0 || trickIndex >= 3 || unit.Tricks[trickIndex] || !AbilityAccess.Allows(unit.Definition, trickIndex))
                return Reject("Ese Trick no está pendiente.", out reason);
            int previous = unit.NextTrick;
            unit.NextTrick = trickIndex;
            if (TryBuy(slot, token, out reason)) return true;
            unit.NextTrick = previous;
            return false;
        }

        /// <summary>Read-only preview of the same purchase, without selecting a Trick or spending its offer.</summary>
        public bool CanBuyTrick(string unitId, int trickIndex, int slot, long token, out string reason)
        {
            if (!RequirePreparation(out reason)) return false;
            if (!ValidSlot(slot) || offers[slot] == null || offers[slot].Token != token || offers[slot].UnitId != unitId)
                return Reject("Esa oferta ya no está disponible.", out reason);
            if (!owned.TryGetValue(unitId, out OwnedUnit unit) || unit.Definition.IsMonster ||
                trickIndex < 0 || trickIndex >= 3 || unit.Tricks[trickIndex] || !AbilityAccess.Allows(unit.Definition, trickIndex))
                return Reject("Ese Trick no está pendiente.", out reason);
            return ValidatePurchase(slot, token, out _, out _, out reason, trickIndex);
        }

        public bool TryBuyAndPlace(int slot, long token, UnitLocation location, Vector2Int cell, out string reason)
        {
            if (!RequirePreparation(out reason)) return false;
            if (location == UnitLocation.Bench) return TryReserveInBank(slot, token, out reason);
            if (location != UnitLocation.Field)
                return Reject("Arrastra la oferta al campo o al banco.", out reason);
            if (!ValidatePurchase(slot, token, out UnitDefinition definition, out _, out reason)) return false;
            bool alreadyOwned = owned.ContainsKey(definition.Id);
            if (!ValidateDestination(definition.Id, location, cell, !alreadyOwned, out reason)) return false;
            if (!TryBuy(slot, token, out reason)) return false;
            if (!alreadyOwned)
            {
                OwnedUnit unit = owned[definition.Id];
                unit.Location = UnitLocation.Field;
                unit.BriefSlot = -1;
                unit.Deployment = cell;
            }
            return true;
        }

        private bool TryReserveInBank(int slot, long token, out string reason)
        {
            if (!ValidSlot(slot) || offers[slot] == null || offers[slot].Token != token)
                return Reject("Esa oferta ya no está disponible.", out reason);
            string id = offers[slot].UnitId;
            UnitDefinition definition = Config.Get(id);
            if (definition == null || definition.IsMonster || CountOwnedCopies(id) >= CopyLimit(id))
                return Reject("No quedan copias disponibles de este Whelp.", out reason);
            if (FindOwned(id, true) != null) return Reject("Este Whelp ya está en el banco.", out reason);
            if (owned.TryGetValue(id, out OwnedUnit primary) && primary.Location != UnitLocation.Field)
                return Reject("Coloca primero este Whelp en el campo.", out reason);
            if (!ValidateDestination(id, UnitLocation.Bench, Vector2Int.zero, false, out reason)) return false;
            var reserve = new OwnedUnit(definition, AbilityAccess)
            {
                Copies = 1, Location = UnitLocation.Bench, BankReservedCost = Mathf.Max(0, definition.BaseCost)
            };
            if (primary == null) owned.Add(id, reserve);
            else bankReserves.Add(id, reserve);
            offers[slot] = null;
            SuppressBenchOffers(id);
            AdvanceOfferVersion();
            reason = string.Empty;
            return true;
        }

        public bool TryExchange(string firstId, string secondId, out PlacementExchange exchange, out string reason)
        {
            exchange = null;
            if (!RequirePreparation(out reason)) return false;
            if (!owned.TryGetValue(firstId ?? string.Empty, out OwnedUnit first) ||
                !owned.TryGetValue(secondId ?? string.Empty, out OwnedUnit second) || first == second)
                return Reject("Selecciona dos personajes propios distintos.", out reason);
            if (!ValidateExchange(first, second, out reason)) return false;
            exchange = new PlacementExchange(first, second);
            CommitExchange(exchange);
            return true;
        }

        private bool ValidateExchange(OwnedUnit first, OwnedUnit second, out string reason)
        {
            if (first.Location != UnitLocation.Field || second.Location != UnitLocation.Field)
                return Reject("El intercambio solo está disponible entre personajes del tablero.", out reason);
            if (first.IsPositionLocked(Round) || second.IsPositionLocked(Round))
                return Reject("Uno de los personajes tiene su posición fija.", out reason);
            foreach (OwnedUnit destination in new[] { first, second })
            {
                if (!Config.IsDeploymentLegal(Side, destination.Deployment))
                    return Reject("Elige una casilla de despliegue válida.", out reason);
                foreach (OwnedUnit other in owned.Values)
                {
                    if (other == first || other == second || other.Location != destination.Location) continue;
                    if (other.Deployment == destination.Deployment)
                        return Reject("El destino ya está ocupado.", out reason);
                }
            }
            reason = string.Empty;
            return true;
        }

        private void CommitExchange(PlacementExchange exchange)
        {
            exchange.First.Deployment = exchange.SecondCell;
            exchange.Second.Deployment = exchange.FirstCell;
        }

        private bool ValidatePurchase(int slot, long token, out UnitDefinition definition, out int price, out string reason,
            int selectedTrick = -1)
        {
            definition = null;
            price = -1;
            if (!ValidSlot(slot) || offers[slot] == null || offers[slot].Token != token)
                return Reject("Esa oferta ya no está disponible.", out reason);
            PouchOffer offer = offers[slot];
            definition = Config.Get(offer.UnitId);
            price = GetOfferCost(slot, selectedTrick);
            if (definition == null || definition.IsMonster || price < 0)
                return Reject("Este Whelp ya tiene sus tres Tricks.", out reason);
            if (Coins < price) return Reject("No tienes suficientes Moonkens.", out reason);

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
            bool any = HasEligibleAvailableCopy();
            foreach (PouchOffer offer in offers) any |= offer != null;
            if (!any) return Reject("No hay Whelps elegibles para esta rotación.", out reason);
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
            if (trickIndex < 0 || trickIndex >= 3 || unit.Tricks[trickIndex] || !AbilityAccess.Allows(unit.Definition, trickIndex))
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
            if (upgrade == null || Monster.HasMonsterUpgrade || !CanAccessLegacyMonsterUpgrade(upgrade)) return Reject("Mejora ya adquirida o no disponible.", out reason);
            int price = Mathf.Max(0, upgrade.Cost);
            if (Coins < price) return Reject("No tienes suficientes Moonkens.", out reason);
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
            if (Monster.TrickCount > 0 || Monster.HasMonsterUpgrade)
                return Reject("Tu héroe ya eligió su única mejora para esta partida.", out reason);
            if (index < 0 || index >= 3 || Monster.Tricks[index] || Monster.Definition.Tricks[index] == null || !AbilityAccess.Allows(Monster.Definition, index))
                return Reject("Mejora ya adquirida o no disponible.", out reason);
            int price = Mathf.Max(0, Monster.Definition.Tricks[index].Cost);
            if (Coins < price) return Reject("No tienes suficientes Moonkens.", out reason);
            Coins -= price; Monster.Paid += price; Monster.Tricks[index] = true;
            if (Monster.UpgradePurchasedRound <= 0) Monster.UpgradePurchasedRound = Round;
            reason = string.Empty; return true;
        }

        /// <summary>Read-only affordability and availability; purchasing still revalidates independently.</summary>
        public bool CanUpgradeMonster(int index)
        {
            if (!IsPreparation) return false;
            if (!Monster.Definition.UsesDocumentedRules)
            {
                var upgrade = Monster.Definition.MonsterUpgrade;
                return upgrade != null && CanAccessLegacyMonsterUpgrade(upgrade) && !Monster.HasMonsterUpgrade && Coins >= Mathf.Max(0, upgrade.Cost);
            }
            if (Monster.TrickCount > 0 || Monster.HasMonsterUpgrade || index < 0 || index >= 3 ||
                Monster.Tricks[index] || Monster.Definition.Tricks[index] == null || !AbilityAccess.Allows(Monster.Definition, index)) return false;
            return Coins >= Mathf.Max(0, Monster.Definition.Tricks[index].Cost);
        }

        bool CanAccessLegacyMonsterUpgrade(TrickDefinition upgrade) => AbilityAccess.IsUnrestricted ||
            Monster.Definition.Tricks != null && AbilityAccess.Allows(Monster.Definition, Array.IndexOf(Monster.Definition.Tricks, upgrade));

        public int AddCombatCoins(int amount)
        {
            int before = Coins; Coins = Mathf.Max(0, Coins + amount); return Coins - before;
        }

        public bool Owns(OwnedUnit unit)
        {
            if (unit == null) return false;
            string id = unit.Definition.Id;
            return (owned.TryGetValue(id, out OwnedUnit primary) && ReferenceEquals(primary, unit)) ||
                   (bankReserves.TryGetValue(id, out OwnedUnit reserve) && ReferenceEquals(reserve, unit));
        }

        public OwnedUnit FindOwned(string id, bool bank)
        {
            id = id ?? string.Empty;
            if (bank && bankReserves.TryGetValue(id, out OwnedUnit reserve)) return reserve;
            if (!owned.TryGetValue(id, out OwnedUnit primary)) return null;
            return !bank || primary.Location == UnitLocation.Bench ? primary : null;
        }

        public int CountOwnedCopies(string id)
        {
            int copies = owned.TryGetValue(id ?? string.Empty, out OwnedUnit primary) ? primary.Copies : 0;
            if (bankReserves.TryGetValue(id ?? string.Empty, out OwnedUnit reserve)) copies += reserve.Copies;
            return copies;
        }

        public int GetDeploymentCost(OwnedUnit unit)
        {
            if (!Owns(unit) || unit.Location != UnitLocation.Bench) return 0;
            if (owned.TryGetValue(unit.Definition.Id, out OwnedUnit field) &&
                field != unit && field.Location == UnitLocation.Field)
            {
                int trick = NextFusionTrick(field, unit);
                return trick < 0 ? -1 : Mathf.Max(0, unit.Definition.Tricks[trick].Cost);
            }
            return unit.BankRedeployCost;
        }

        public bool TrySell(string unitId, out string reason)
            => TrySell(FindOwned(unitId, false), out reason);

        public bool TrySell(OwnedUnit unit, out string reason)
        {
            if (!RequirePreparation(out reason)) return false;
            if (!Owns(unit) || unit.Definition.IsMonster)
                return Reject("Solo puedes vender tus Whelps.", out reason);
            long saleBalance = (long)Coins + unit.SaleRefund;
            if (saleBalance > int.MaxValue) return Reject("El saldo de Moonkens no admite esta devolución.", out reason);
            Coins = (int)saleBalance;
            string id = unit.Definition.Id;
            if (Config.ReturnSoldCopies)
                for (int i = 0; i < unit.Copies; i++) available.Add(id);
            RemoveOwned(unit);
            AdvanceOfferVersion();
            reason = string.Empty;
            return true;
        }

        private void RemoveOwned(OwnedUnit unit)
        {
            string id = unit.Definition.Id;
            if (bankReserves.TryGetValue(id, out OwnedUnit reserve))
            {
                bankReserves.Remove(id);
                if (unit != reserve) owned[id] = reserve;
            }
            else owned.Remove(id);
        }

        public bool TrySetLocation(string unitId, UnitLocation location, Vector2Int cell, out string reason)
            => TrySetLocation(FindOwned(unitId, false), location, cell, out reason);

        public bool TrySetLocation(OwnedUnit unit, UnitLocation location, Vector2Int cell, out string reason)
        {
            if (!RequirePreparation(out reason)) return false;
            if (!Owns(unit)) return Reject("Primero desbloquea este Whelp.", out reason);
            if (location != UnitLocation.Brief && location != UnitLocation.Bench && location != UnitLocation.Field)
                return Reject("Destino inválido.", out reason);
            if (unit.Definition.IsMonster && location != UnitLocation.Field)
                return Reject("Tu Monster permanece en el campo.", out reason);
            if (location == UnitLocation.Brief && unit.Location != UnitLocation.Brief)
                return Reject("No puedes devolver tropas al Brief. Guárdalas en el banco o véndelas.", out reason);
            if (unit.IsPositionLocked(Round) && (location != unit.Location || cell != unit.Deployment))
                return Reject(unit.MarkedPositionRound > 0 && Round > unit.MarkedPositionRound ?
                    (Config.Get("tauris")?.DisplayName??"El personaje")+" marcó esta unidad: su posición está fija." :
                    "La mejora del Monster fija su posición desde esta ronda.", out reason);
            string id = unit.Definition.Id;
            if (location == UnitLocation.Bench && unit.Location == UnitLocation.Field &&
                bankReserves.TryGetValue(id, out OwnedUnit reserve))
                return TryFuseToBank(unit, reserve, out reason);
            if (location == UnitLocation.Field && unit.Location == UnitLocation.Bench &&
                owned.TryGetValue(id, out OwnedUnit field) && field != unit && field.Location == UnitLocation.Field)
                return TryFuseToField(field, unit, out reason);
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
            else if (!ValidateDestination(id, location, cell, !unit.Definition.IsMonster, out reason)) return false;
            bool enteringBank = location == UnitLocation.Bench && unit.Location != UnitLocation.Bench;
            bool leavingBank = unit.Location == UnitLocation.Bench && location == UnitLocation.Field;
            int refund = enteringBank ? unit.SaleRefund : 0;
            int charge = leavingBank ? unit.BankRedeployCost : 0;
            if (Coins < charge) return Reject("Necesitas " + charge + " Moonkens para sacar este Whelp del banco.", out reason);
            long nextBalance = (long)Coins + refund - charge;
            if (nextBalance > int.MaxValue) return Reject("El saldo de Moonkens no admite esta devolución.", out reason);
            Coins = (int)nextBalance;
            if (enteringBank) unit.BankRefunded += refund;
            if (leavingBank)
            {
                unit.Paid += unit.BankReservedCost;
                unit.BankReservedCost = unit.BankRefunded = 0;
            }
            unit.Location = location;
            unit.BriefSlot = briefSlot;
            if (location == UnitLocation.Field) unit.Deployment = cell;
            if (location == UnitLocation.Bench && SuppressBenchOffers(id)) AdvanceOfferVersion();
            reason = string.Empty;
            return true;
        }

        private int NextFusionTrick(OwnedUnit first, OwnedUnit second)
        {
            for (int i = 0; i < first.Tricks.Length; i++)
                if (!first.Tricks[i] && !second.Tricks[i] && AbilityAccess.Allows(first.Definition, i)) return i;
            return -1;
        }

        private bool ValidateFusion(OwnedUnit field, OwnedUnit reserve, out int nextTrick, out string reason)
        {
            nextTrick = NextFusionTrick(field, reserve);
            if (field.Copies + reserve.Copies > CopyLimit(field.Definition.Id) || nextTrick < 0)
                return Reject("Este Whelp ya tiene sus tres Tricks.", out reason);
            reason = string.Empty;
            return true;
        }

        private bool TryFuseToBank(OwnedUnit field, OwnedUnit reserve, out string reason)
        {
            if (!ValidateFusion(field, reserve, out int nextTrick, out reason)) return false;
            long cost = Mathf.Max(0, field.Definition.BaseCost);
            for (int i = 0; i < field.Tricks.Length; i++)
                if (field.Tricks[i] || reserve.Tricks[i] || i == nextTrick)
                    cost += Mathf.Max(0, field.Definition.Tricks[i].Cost);
            long nextBalance = (long)Coins + field.SaleRefund;
            if (cost > int.MaxValue || nextBalance > int.MaxValue)
                return Reject("El saldo de Moonkens no admite esta devolución.", out reason);
            Coins = (int)nextBalance;
            reserve.Copies += field.Copies;
            for (int i = 0; i < field.Tricks.Length; i++) reserve.Tricks[i] |= field.Tricks[i] || i == nextTrick;
            reserve.NextTrick = field.NextTrick >= 0 && !reserve.Tricks[field.NextTrick] ? field.NextTrick : -1;
            reserve.PersistentDamageBonus = field.PersistentDamageBonus;
            reserve.PersistentHealthBonus = field.PersistentHealthBonus;
            reserve.MarkedPositionRound = field.MarkedPositionRound;
            reserve.UpgradePurchasedRound = field.UpgradePurchasedRound;
            // All field investment was returned. The fused bank unit is wholly
            // unpaid until its current base and inherited/new Tricks are deployed.
            reserve.Paid = reserve.BankRefunded = 0;
            reserve.BankReservedCost = (int)cost;
            bankReserves.Remove(field.Definition.Id);
            owned[field.Definition.Id] = reserve;
            SuppressBenchOffers(field.Definition.Id);
            AdvanceOfferVersion();
            reason = string.Empty;
            return true;
        }

        private bool TryFuseToField(OwnedUnit field, OwnedUnit reserve, out string reason)
        {
            if (!ValidateFusion(field, reserve, out int nextTrick, out reason)) return false;
            int price = Mathf.Max(0, field.Definition.Tricks[nextTrick].Cost);
            if (Coins < price) return Reject("No tienes suficientes Moonkens para esta mejora.", out reason);
            if ((long)field.Paid + price > int.MaxValue)
                return Reject("La inversión de este Whelp no admite esta mejora.", out reason);
            Coins -= price;
            field.Paid += price;
            field.Copies += reserve.Copies;
            for (int i = 0; i < field.Tricks.Length; i++) field.Tricks[i] |= reserve.Tricks[i] || i == nextTrick;
            if (field.NextTrick >= 0 && field.Tricks[field.NextTrick]) field.NextTrick = -1;
            bankReserves.Remove(field.Definition.Id);
            AdvanceOfferVersion();
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
                int stored = 0;
                foreach (OwnedUnit other in BankedUnits)
                    if (other.Definition.Id != unitId) stored++;
                if (stored >= MatchConfig.MaxBenchUnits)
                    return Reject("El banco está lleno (3/3).", out reason);
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
            for (int i = 0; i < offers.Length; i++)
            {
                if (offers[i] != null || IsBriefSlotOccupied(i)) continue;
                int index = RandomEligibleAvailableIndex();
                if (index < 0) break;
                string id = available[index];
                available.RemoveAt(index);
                offers[i] = new PouchOffer(id, System.Threading.Interlocked.Increment(ref nextToken));
            }
        }

        private bool IsBenchBlocked(string unitId)
        {
            return FindOwned(unitId, true) != null;
        }

        private bool HasEligibleAvailableCopy()
        {
            foreach (string id in available)
                if (!IsBenchBlocked(id)) return true;
            return false;
        }

        private int RandomEligibleAvailableIndex()
        {
            int eligible = 0;
            foreach (string id in available)
                if (!IsBenchBlocked(id)) eligible++;
            if (eligible == 0) return -1;

            int selected = NextAvailableIndex(eligible);
            for (int i = 0; i < available.Count; i++)
                if (!IsBenchBlocked(available[i]) && selected-- == 0) return i;
            return -1;
        }

        /// <summary>
        /// A bench unit hides its whole character type from the shop without consuming
        /// or deleting any unpurchased copy. Hidden offers return to the finite pool.
        /// </summary>
        private bool SuppressBenchOffers(string unitId)
        {
            bool changed = false;
            for (int i = 0; i < offers.Length; i++)
            {
                if (offers[i] == null || !string.Equals(offers[i].UnitId, unitId, StringComparison.Ordinal)) continue;
                available.Add(offers[i].UnitId);
                offers[i] = null;
                changed = true;
            }
            return changed;
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
