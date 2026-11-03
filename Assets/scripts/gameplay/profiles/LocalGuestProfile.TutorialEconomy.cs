using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MonsterPouch.Gameplay.Profiles
{
    [Serializable] public sealed class GuestTutorialPouch
    {
        public string Id, ClaimedUtc, OpenedUtc;
        public int ResourceRoll;
        public GuestPouchRoomConfiguration Configuration;
    }
    public sealed partial class LocalGuestProfile
    {
        public List<GuestTutorialPouch> TutorialPouches = new List<GuestTutorialPouch>();
        public List<string> TutorialMenuVisits = new List<string>();
        public string TutorialMachineReceiptId;
        internal const string TutorialPouchApproval = "approved-tutorial-blue-pouch-2026-10-24";
        internal static readonly string[] CollectionVisitIds = { "deck", "collection", "ability", "wallet" };
        internal string TutorialPouchId => "tutorial-pouch:" + ProfileId;
        public bool TutorialCollectionReady => TutorialPouches.Count == 1 && !string.IsNullOrEmpty(TutorialPouches[0].OpenedUtc) &&
            CollectionVisitIds.All(TutorialMenuVisits.Contains);
        public bool TutorialMachineReady => !string.IsNullOrEmpty(ProgressionTutorialVisitedUtc) || !string.IsNullOrEmpty(TutorialMachineReceiptId) &&
            WalletEvents.Any(e => e.Id == TutorialMachineReceiptId && e.Kind == GuestWalletEventKind.MachineDraw);

        void ValidateTutorialEconomy()
        {
            if (TutorialPouches == null || TutorialMenuVisits == null || TutorialPouches.Count > 1 ||
                TutorialMenuVisits.Any(id => !CollectionVisitIds.Contains(id)) || TutorialMenuVisits.Distinct().Count() != TutorialMenuVisits.Count)
                throw new InvalidDataException("Invalid tutorial collection evidence.");
            if ((TutorialPouches.Count > 0 || TutorialMenuVisits.Count > 0 || !string.IsNullOrEmpty(TutorialMachineReceiptId)) &&
                (CollectionMode != GuestCollectionMode.StarterInventory || HasLegacyAccess || Starter == null ||
                 !CompletedTutorialModules.Contains(GuestTutorialModules.TacticalBank)))
                throw new InvalidDataException("Tutorial economy requires completion of the six practice modules.");
            foreach (var pouch in TutorialPouches)
            {
                if (pouch == null || pouch.Id != TutorialPouchId || pouch.ResourceRoll < 0 || pouch.ResourceRoll >= 10000 ||
                    !GuestPouchRules.Timestamp(pouch.ClaimedUtc, out var claimed) || pouch.Configuration == null ||
                    pouch.Configuration.ApprovalId != TutorialPouchApproval)
                    throw new InvalidDataException("Invalid tutorial pouch acquisition.");
                pouch.Configuration.Validate();
                var wallet = WalletEvents.SingleOrDefault(e => e.Id == pouch.Id);
                if (string.IsNullOrEmpty(pouch.OpenedUtc))
                {
                    if (wallet != null) throw new InvalidDataException("An unopened tutorial pouch cannot grant currency.");
                }
                else
                {
                    if (!GuestPouchRules.Timestamp(pouch.OpenedUtc, out var opened) || opened < claimed)
                        throw new InvalidDataException("Invalid tutorial pouch opening time.");
                    var reward = pouch.Configuration.At(pouch.ResourceRoll);
                    if (wallet == null || wallet.Kind != GuestWalletEventKind.FreeCredit || wallet.SourceId != TutorialPouchApproval ||
                        wallet.TimestampUtc != pouch.OpenedUtc || wallet.Amounts.Length != 1 || wallet.Amounts[0].Currency != reward.Currency ||
                        wallet.Amounts[0].Amount != reward.Amount)
                        throw new InvalidDataException("The tutorial pouch and earned currency must commit together.");
                }
            }
            if (WalletEvents.Any(e => (e.Id.StartsWith("tutorial-pouch:", StringComparison.Ordinal) || e.SourceId == TutorialPouchApproval) &&
                !TutorialPouches.Any(p => p.Id == e.Id && !string.IsNullOrEmpty(p.OpenedUtc))))
                throw new InvalidDataException("Orphaned tutorial pouch reward.");
            if (!string.IsNullOrEmpty(TutorialMachineReceiptId) && (!TutorialMachineReady ||
                !CompletedTutorialModules.Contains(GuestTutorialModules.ProgressionCollection)))
                throw new InvalidDataException("A tutorial machine observation requires a committed draw after the collection module.");
            if (CompletedTutorialModules.Contains(GuestTutorialModules.ProgressionCollection) && !TutorialCollectionReady ||
                CompletedTutorialModules.Contains(GuestTutorialModules.FirstMachine) && !TutorialMachineReady)
                throw new InvalidDataException("Tutorial completion requires its actual inventory evidence.");
        }
    }
}

