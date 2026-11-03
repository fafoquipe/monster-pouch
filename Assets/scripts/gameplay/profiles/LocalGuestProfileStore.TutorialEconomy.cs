using System;
using System.IO;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Machines;

namespace MonsterPouch.Gameplay.Profiles
{
    public sealed partial class LocalGuestProfileStore
    {
        LocalGuestProfile RequireTutorialEconomy(string module)
        {
            var profile = LoadExisting();
            if (NeedsRecovery) throw new InvalidDataException("Recover the profile before claiming tutorial rewards.");
            if (profile == null || profile.CollectionMode != GuestCollectionMode.StarterInventory || profile.HasLegacyAccess || profile.Starter == null ||
                profile.NextTutorialModuleId != module && !profile.CompletedTutorialModules.Contains(module))
                throw new InvalidOperationException("This tutorial module is not available.");
            return profile;
        }
        public LocalGuestProfile ClaimTutorialPouch(MatchConfig catalogue, GuestPouchRoomConfiguration configuration) =>
            ClaimTutorialPouchCore(catalogue, configuration, PouchRoll);
        internal LocalGuestProfile ClaimTutorialPouchCore(MatchConfig catalogue, GuestPouchRoomConfiguration configuration, Func<int,int> roll)
        {
            if (configuration == null || roll == null) throw new ArgumentException("An approved resource table is required.");
            var approved = configuration.Snapshot(); approved.ApprovalId = LocalGuestProfile.TutorialPouchApproval;
            using (Lock())
            {
                var profile = RequireTutorialEconomy(GuestTutorialModules.ProgressionCollection);
                if (!GuestAbilityCatalogue.KnownCollection(profile, catalogue)) throw new InvalidDataException("Invalid character collection.");
                if (profile.TutorialPouches.Count != 0) return profile;
                var next = ClonePouchProfile(profile);
                next.TutorialPouches.Add(new GuestTutorialPouch { Id = next.TutorialPouchId, ClaimedUtc = DateTime.UtcNow.ToString("O"),
                    ResourceRoll = CheckedPouchRoll(roll, 10000), Configuration = approved });
                CommitPouchProfile(next); return next;
            }
        }
        public LocalGuestProfile OpenTutorialPouch()
        {
            using (Lock())
            {
                var profile = RequireTutorialEconomy(GuestTutorialModules.ProgressionCollection);
                if (profile.TutorialPouches.Count != 1) throw new InvalidOperationException("Claim the tutorial pouch first.");
                if (!string.IsNullOrEmpty(profile.TutorialPouches[0].OpenedUtc)) return profile;
                var next = ClonePouchProfile(profile); var pouch = next.TutorialPouches[0];
                var resource = pouch.Configuration.At(pouch.ResourceRoll);
                if (next.WalletEvents.Any(e => e.Id == pouch.Id)) throw new InvalidDataException("Tutorial reward conflict.");
                DateTime claimed = DateTime.Parse(pouch.ClaimedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind);
                pouch.OpenedUtc = (DateTime.UtcNow < claimed ? claimed : DateTime.UtcNow).ToString("O");
                next.WalletEvents.Add(new GuestWalletEvent { Id = pouch.Id, SourceId = LocalGuestProfile.TutorialPouchApproval,
                    TimestampUtc = pouch.OpenedUtc, Kind = GuestWalletEventKind.FreeCredit,
                    Amounts = new[] { new PlayerMachineAmount { Currency = resource.Currency, Amount = resource.Amount } } });
                CommitPouchProfile(next); return next;
            }
        }
        public LocalGuestProfile RecordTutorialMenuVisit(string visit)
        {
            if (!LocalGuestProfile.CollectionVisitIds.Contains(visit)) throw new ArgumentException("Unknown tutorial view.");
            using (Lock())
            {
                var profile = RequireTutorialEconomy(GuestTutorialModules.ProgressionCollection);
                if (profile.TutorialMenuVisits.Contains(visit)) return profile;
                var next = ClonePouchProfile(profile); next.TutorialMenuVisits.Add(visit);
                CommitPouchProfile(next); return next;
            }
        }
        // Stable legacy module ID remains in saves; the lesson now visits the trophy road.
        public LocalGuestProfile RecordTutorialProgressionVisit()
        {
            using(Lock()) {
                var profile=RequireTutorialEconomy(GuestTutorialModules.FirstMachine);
                if(!string.IsNullOrEmpty(profile.ProgressionTutorialVisitedUtc))return profile;
                var next=ClonePouchProfile(profile);next.ProgressionTutorialVisitedUtc=DateTime.UtcNow.ToString("O");
                CommitPouchProfile(next);return next;
            }
        }
        // Replaying an already committed reward lets an early spender finish the
        // lesson without another charge or a forced fifth draw.
        public LocalGuestProfile RecordTutorialMachineReward(string walletReceiptId)
        {
            using (Lock())
            {
                var profile = RequireTutorialEconomy(GuestTutorialModules.FirstMachine);
                if (!profile.WalletEvents.Any(e => e.Id == walletReceiptId && e.Kind == GuestWalletEventKind.MachineDraw))
                    throw new ArgumentException("A real confirmed machine draw is required.");
                if (profile.TutorialMachineReady) return profile;
                var next = ClonePouchProfile(profile); next.TutorialMachineReceiptId = walletReceiptId;
                CommitPouchProfile(next); return next;
            }
        }
    }
}

