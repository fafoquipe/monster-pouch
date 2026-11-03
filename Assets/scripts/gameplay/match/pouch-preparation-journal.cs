using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MonsterPouch.Gameplay.Match
{
    public sealed partial class PouchState
    {
        private readonly int randomSeed;
        private readonly List<int> randomDrawBounds = new List<int>();

        private int NextAvailableIndex(int upperBound)
        {
            // Preserve the existing seeded Random.Next algorithm exactly. Recording
            // its arguments lets a private replay reconstruct its state without
            // reflection, shared RNG mutation or a different random generator.
            int result = random.Next(upperBound);
            randomDrawBounds.Add(upperBound);
            return result;
        }

        internal bool TryApplyPreparationJournal(Func<PouchState, bool> apply)
        {
            if (apply == null) throw new ArgumentNullException(nameof(apply));
            if (!IsPreparation) return false;
            var originals = new Dictionary<OwnedUnit, OwnedUnit>();
            var candidate = new PouchState(this, originals);
            if (!apply(candidate)) return false;

            // Keep existing OwnedUnit, collection and offer-view references. A
            // bank fusion can promote an existing reserve to the primary slot,
            // so preserve identity by object rather than by character ID.
            OwnedUnit Resolve(OwnedUnit staged)
            {
                if (!originals.TryGetValue(staged, out OwnedUnit original)) return staged;
                CopyOwnedState(staged, original);
                return original;
            }
            owned.Clear();
            foreach (var pair in candidate.owned) owned.Add(pair.Key, Resolve(pair.Value));
            bankReserves.Clear();
            foreach (var pair in candidate.bankReserves) bankReserves.Add(pair.Key, Resolve(pair.Value));
            available.Clear(); available.AddRange(candidate.available);
            Array.Copy(candidate.offers, offers, offers.Length);
            Round = candidate.Round; Coins = candidate.Coins; Rerolls = candidate.Rerolls;
            OfferVersion = candidate.OfferVersion; IsPreparation = candidate.IsPreparation;
            random = candidate.random;
            randomDrawBounds.Clear(); randomDrawBounds.AddRange(candidate.randomDrawBounds);
            return true;
        }

        private PouchState(PouchState source, Dictionary<OwnedUnit, OwnedUnit> originals)
        {
            Config = source.Config; Side = source.Side; SelectedWhelpIds = source.SelectedWhelpIds; AbilityAccess = source.AbilityAccess;
            Round = source.Round; Coins = source.Coins; Rerolls = source.Rerolls;
            OfferVersion = source.OfferVersion; IsPreparation = source.IsPreparation;
            randomSeed = source.randomSeed;
            random = new Random(randomSeed);
            foreach (int bound in source.randomDrawBounds) random.Next(bound);
            randomDrawBounds.AddRange(source.randomDrawBounds);

            OwnedUnit Copy(OwnedUnit original)
            {
                var copy = new OwnedUnit(original.Definition, original.AbilityAccess);
                CopyOwnedState(original, copy);
                originals.Add(copy, original);
                return copy;
            }
            foreach (var pair in source.owned) owned.Add(pair.Key, Copy(pair.Value));
            foreach (var pair in source.bankReserves) bankReserves.Add(pair.Key, Copy(pair.Value));
            Monster = owned[source.Monster.Definition.Id];
            available.AddRange(source.available);
            // Offers are immutable; unchanged entries retain their tokens and identity.
            Array.Copy(source.offers, offers, offers.Length);
            Owned = new ReadOnlyDictionary<string, OwnedUnit>(owned);
            BankReserves = new ReadOnlyDictionary<string, OwnedUnit>(bankReserves);
            Offers = Array.AsReadOnly(offers);
        }

        private static void CopyOwnedState(OwnedUnit source, OwnedUnit destination)
        {
            destination.Copies = source.Copies;
            Array.Copy(source.Tricks, destination.Tricks, source.Tricks.Length);
            destination.NextTrick = source.NextTrick; destination.Paid = source.Paid;
            destination.BankRefunded = source.BankRefunded; destination.BankReservedCost = source.BankReservedCost;
            destination.Location = source.Location; destination.BriefSlot = source.BriefSlot;
            destination.Deployment = source.Deployment; destination.UpgradePurchasedRound = source.UpgradePurchasedRound;
            destination.PersistentDamageBonus = source.PersistentDamageBonus;
            destination.PersistentHealthBonus = source.PersistentHealthBonus;
            destination.MarkedPositionRound = source.MarkedPositionRound;
        }
    }
}
