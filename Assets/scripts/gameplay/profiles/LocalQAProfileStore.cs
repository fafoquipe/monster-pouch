using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace MonsterPouch.Gameplay.Profiles
{
    /// <summary>
    /// Offline QA persistence. Checksums detect corruption, not cheating.
    /// No production, purchased-currency or client-to-server credit API is provided.
    /// </summary>
    public sealed partial class LocalQAProfileStore
    {
        [Serializable] sealed class Envelope
        {
            public int Version = 1;
            public string Payload, Sha256;
        }
        public enum Result { Applied, AlreadyApplied, InsufficientFunds, Conflict, Disabled, RecoveryRequired, InvalidStarter, InvalidCatalogue }
        public bool NeedsRecovery { get; private set; }
        readonly string directory, mainPath, backupPath;
        public LocalQAProfileStore(string qaDirectory)
        {
            directory = Path.GetFullPath(qaDirectory);
            mainPath = Path.Combine(directory, "qa-profile.json");
            backupPath = Path.Combine(directory, "qa-profile.backup.json");
        }

        public LocalEconomyProfile OpenOrCreate(string legacyProgressJson = "")
        {
            using (Lock())
            {
                var profile = LoadExisting();
                if (profile != null) return profile;
                profile = LocalEconomyProfile.CreateQA(legacyProgressJson);
                Save(profile);
                return profile;
            }
        }

        public Result GrantWelcome(EconomyDevelopmentConfig config)
        {
            if (!Allowed(config)) return Result.Disabled;
            return Apply("welcome:v1", PermanentCurrency.Saturncoins, config.StarterAmount, "welcome", true);
        }

        public Result Spend(EconomyDevelopmentConfig config, string operationId, PermanentCurrency currency, long amount)
        {
            if (!Allowed(config)) return Result.Disabled;
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            return Apply(operationId, currency, -amount, "qa-spend", false);
        }

        static bool Allowed(EconomyDevelopmentConfig config) =>
            config != null && config.EnableLocalQA && (Application.isEditor || Debug.isDebugBuild);

        Result Apply(string operationId, PermanentCurrency currency, long delta, string source, bool welcome)
        {
            if (string.IsNullOrWhiteSpace(operationId) || operationId.Length > 160 ||
                !Enum.IsDefined(typeof(PermanentCurrency), currency))
                throw new ArgumentException("Invalid operation or currency.");
            using (Lock())
            {
                var profile = LoadExisting();
                if (profile == null) throw new InvalidOperationException("Open the QA profile before applying transactions.");
                if (NeedsRecovery) return Result.RecoveryRequired;
                if (profile.Environment != ProfileEnvironment.InternalQA) return Result.Disabled;
                var previous = profile.Transactions.FirstOrDefault(t => t.Id == operationId);
                if (previous != null)
                {
                    // A welcome amount change never grants a second welcome or rewrites the first.
                    return previous.Source == source && previous.Currency == currency &&
                        (welcome || previous.Delta == delta) ? Result.AlreadyApplied : Result.Conflict;
                }
                long before = profile.Balance(currency, CreditOrigin.InternalQA);
                long after = checked(before + delta);
                if (after < 0) return Result.InsufficientFunds;
                var bucket = profile.Balances.SingleOrDefault(b => b.Currency == currency && b.Origin == CreditOrigin.InternalQA);
                if (bucket == null)
                {
                    bucket = new CurrencyBalance { Currency = currency, Origin = CreditOrigin.InternalQA };
                    profile.Balances.Add(bucket);
                }
                bucket.Amount = after;
                profile.Transactions.Add(new EconomicTransaction {
                    Id = operationId, Currency = currency, Origin = CreditOrigin.InternalQA,
                    Delta = delta, Before = before, After = after, Source = source,
                    TimestampUtc = DateTime.UtcNow.ToString("O")
                });
                profile.Revision = checked(profile.Revision + 1);
                Save(profile);
                return Result.Applied;
            }
        }

        FileStream Lock()
        {
            Directory.CreateDirectory(directory);
            // Across sessions/processes. A competing writer fails without partial mutation.
            return new FileStream(Path.Combine(directory, "qa-profile.lock"), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None);
        }

        LocalEconomyProfile LoadExisting()
        {
            NeedsRecovery = false;
            if (!File.Exists(mainPath) && !File.Exists(backupPath)) return null;
            try { return Read(mainPath); }
            catch (NotSupportedException) { throw; } // Never downgrade a future schema.
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is ArgumentException)
            {
                if (!File.Exists(backupPath))
                    throw new InvalidDataException("QA profile cannot be read. Original files were preserved.", error);
                var recovered = Read(backupPath);
                // Recovery is read-only. A previous generation may lack a committed debit
                // or welcome receipt, so automatic spending/granting must remain disabled.
                NeedsRecovery = true;
                return recovered;
            }
        }

        static LocalEconomyProfile Read(string path)
        {
            var envelope = JsonUtility.FromJson<Envelope>(File.ReadAllText(path, Encoding.UTF8));
            if (envelope == null) throw new InvalidDataException("Empty profile.");
            if (envelope.Version != 1) throw new NotSupportedException("Unsupported envelope version.");
            if (string.IsNullOrEmpty(envelope.Payload) || envelope.Sha256 != Hash(envelope.Payload))
                throw new InvalidDataException("Profile checksum mismatch.");
            var profile = JsonUtility.FromJson<LocalEconomyProfile>(envelope.Payload);
            if (profile == null) throw new InvalidDataException("Empty profile data.");
            profile.UpgradeSchema();
            if (profile.Environment != ProfileEnvironment.InternalQA)
                throw new InvalidDataException("Player data cannot be opened as a QA profile.");
            // Schema upgrades happen in memory; the original stays intact until
            // a successful operation commits and preserves the previous backup.
            return profile;
        }

        void Save(LocalEconomyProfile profile)
        {
            profile.Validate();
            string payload = JsonUtility.ToJson(profile);
            string encoded = JsonUtility.ToJson(new Envelope { Payload = payload, Sha256 = Hash(payload) }, true);
            string temporary = Path.Combine(directory, "qa-profile." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var bytes = Encoding.UTF8.GetBytes(encoded);
                    file.Write(bytes, 0, bytes.Length);
                    file.Flush(true);
                }
                if (File.Exists(mainPath))
                {
                    // Only keep a valid prior generation as backup.
                    bool valid = true;
                    try { Read(mainPath); } catch (Exception e) when (e is IOException || e is InvalidDataException || e is ArgumentException) { valid = false; }
                    File.Replace(temporary, mainPath, valid ? backupPath : null);
                }
                else File.Move(temporary, mainPath);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        static string Hash(string value)
        {
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "");
        }
    }
}
