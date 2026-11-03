using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MonsterPouch.Gameplay.Profiles
{
    // Moon-Ken remains exclusively in PouchState and is deliberately absent here.
    public enum PermanentCurrency { Earthcoins, Suncoins, Saturncoins }
    public enum CreditOrigin { InternalQA, FreePlayer, Purchased }
    public enum ProfileEnvironment { InternalQA, Player }

    [Serializable] public sealed class CurrencyBalance
    {
        public PermanentCurrency Currency;
        public CreditOrigin Origin;
        public long Amount;
    }
    [Serializable] public sealed class EconomicTransaction
    {
        public string Id, Source, TimestampUtc;
        public PermanentCurrency Currency;
        public CreditOrigin Origin;
        public long Delta, Before, After;
    }
    [Serializable] public sealed partial class LocalEconomyProfile
    {
        public const int CurrentVersion = 4;
        public int Version = CurrentVersion;
        public string ProfileId;
        public ProfileEnvironment Environment;
        public long Revision;
        // A copy of legacy progress is preserved; this does not remove its PlayerPrefs.
        public string LegacyProgressJson;
        public List<string> OwnedCharacterIds = new List<string>();
        public List<string> CompletedTutorialSteps = new List<string>();
        public List<CurrencyBalance> Balances = new List<CurrencyBalance>();
        public List<EconomicTransaction> Transactions = new List<EconomicTransaction>();
        // Unity serializes an inline null class as a default object. An empty list
        // preserves "not chosen" across JSON round trips without a phantom receipt.
        public List<StarterSelection> StarterSelections = new List<StarterSelection>();
        public StarterSelection Starter => StarterSelections?.FirstOrDefault();

        public long Balance(PermanentCurrency currency, CreditOrigin origin) =>
            Balances.Where(b => b.Currency == currency && b.Origin == origin).Sum(b => b.Amount);

        public static LocalEconomyProfile CreateQA(string legacyProgress)
        {
            return new LocalEconomyProfile {
                ProfileId = Guid.NewGuid().ToString("N"),
                Environment = ProfileEnvironment.InternalQA,
                LegacyProgressJson = legacyProgress ?? ""
            };
        }

        public void Validate()
        {
            if (Version < 1 || Version > CurrentVersion)
                throw new NotSupportedException("Unsupported profile schema; original save is preserved.");
            if (Version == 1 && Starter != null)
                throw new InvalidDataException("A v1 profile cannot contain a starter receipt.");
            if (!Guid.TryParseExact(ProfileId, "N", out _) || Revision < 0 ||
                !Enum.IsDefined(typeof(ProfileEnvironment), Environment))
                throw new InvalidDataException("Invalid profile identity or revision.");
            if (OwnedCharacterIds == null || CompletedTutorialSteps == null || Balances == null || Transactions == null ||
                StarterSelections == null || StarterSelections.Count > 1 || StarterSelections.Any(s=>s==null))
                throw new InvalidDataException("Missing profile records.");
            if (OwnedCharacterIds.Any(string.IsNullOrWhiteSpace) || CompletedTutorialSteps.Any(string.IsNullOrWhiteSpace) ||
                OwnedCharacterIds.Distinct(StringComparer.Ordinal).Count() != OwnedCharacterIds.Count ||
                CompletedTutorialSteps.Distinct(StringComparer.Ordinal).Count() != CompletedTutorialSteps.Count)
                throw new InvalidDataException("Invalid inventory or tutorial IDs.");
            if (Starter != null)
            {
                var deck = Starter.Deck;
                if (Version<4&&Starter.ReceiptId!=StarterRoster.LegacyReceiptId || !StarterRoster.IsValidSelection(Starter) ||
                    !OwnedCharacterIds.Contains(deck.Monster) || deck.Whelps.Any(id => !OwnedCharacterIds.Contains(id)))
                    throw new InvalidDataException("Incomplete starter selection or missing ownership.");
            }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var totals = new Dictionary<string, long>();
            foreach (var transaction in Transactions)
            {
                if (transaction == null || string.IsNullOrWhiteSpace(transaction.Id) || !ids.Add(transaction.Id) ||
                    string.IsNullOrWhiteSpace(transaction.Source) || transaction.Delta == 0 ||
                    !Valid(transaction.Currency, transaction.Origin) ||
                    !DateTime.TryParse(transaction.TimestampUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out _))
                    throw new InvalidDataException("Invalid transaction.");
                string key = Key(transaction.Currency, transaction.Origin);
                totals.TryGetValue(key, out long previous);
                long after;
                try { after = checked(previous + transaction.Delta); }
                catch (OverflowException) { throw new InvalidDataException("Balance overflow."); }
                if (after < 0 || transaction.Before != previous || transaction.After != after)
                    throw new InvalidDataException("Inconsistent transaction balance.");
                totals[key] = after;
            }
            var buckets = new HashSet<string>();
            foreach (var balance in Balances)
            {
                if (balance == null || !Valid(balance.Currency, balance.Origin) || balance.Amount < 0)
                    throw new InvalidDataException("Invalid balance.");
                string key = Key(balance.Currency, balance.Origin);
                totals.TryGetValue(key, out long expected);
                if (!buckets.Add(key) || expected != balance.Amount)
                    throw new InvalidDataException("Balance does not match transaction history.");
            }
            if (totals.Keys.Any(key => !buckets.Contains(key)))
                throw new InvalidDataException("Missing balance bucket.");
            if (Environment == ProfileEnvironment.InternalQA &&
                (Balances.Any(b => b.Origin != CreditOrigin.InternalQA) ||
                 Transactions.Any(t => t.Origin != CreditOrigin.InternalQA)))
                throw new InvalidDataException("QA credits cannot become player or purchased credits.");
            if (Environment == ProfileEnvironment.Player &&
                (Balances.Any(b => b.Origin == CreditOrigin.InternalQA) ||
                 Transactions.Any(t => t.Origin == CreditOrigin.InternalQA)))
                throw new InvalidDataException("Player profiles cannot contain internal QA credits.");
            ValidateInventory();
        }
        static string Key(PermanentCurrency currency, CreditOrigin origin) => (int)currency + ":" + (int)origin;
        static bool Valid(PermanentCurrency currency, CreditOrigin origin) =>
            Enum.IsDefined(typeof(PermanentCurrency), currency) && Enum.IsDefined(typeof(CreditOrigin), origin);
    }

}
