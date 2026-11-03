namespace MonsterPouch.Gameplay.Profiles
{
    /// <summary>Approved local, earned-currency rules. No purchased currency or match Moonkens.</summary>
    public static class GuestPermanentEconomy
    {
        internal const long AbilityV1EarthCost = 100, AbilityV1SunCost = 100, WelcomeV1Saturnkens = 20;
        public const long AbilityEarthCost = AbilityV1EarthCost;
        public const long AbilitySunCost = AbilityV1SunCost;
        public const long WelcomeSaturnkens = WelcomeV1Saturnkens;
        internal const int CurrentPolicyVersion = 1;
        internal const string AbilityV1ApprovalId = "approved-ability-purchase-2026-10-24";
        public const string AbilityPurchaseApprovalId = AbilityV1ApprovalId;
        internal const string WelcomeV1ApprovalId = "approved-new-account-welcome-2026-10-24";
        public const string WelcomeApprovalId = WelcomeV1ApprovalId;
        internal const string PurchasePrefix = "ability-buy:";
        internal const string WelcomePrefix = "welcome:";
        internal static string PurchaseId(string requestId) => PurchasePrefix + requestId;
        internal static string WelcomeId(string profileId) => WelcomePrefix + profileId;
        internal static bool ReservedId(string id) => id != null &&
            (id.StartsWith(PurchasePrefix, System.StringComparison.Ordinal) ||
             id.StartsWith(WelcomePrefix, System.StringComparison.Ordinal));
    }

    public enum GuestAbilityPurchaseStatus
    {
        Ready, Purchased, AlreadyApplied, AlreadyOwned, InsufficientFunds, CharacterLocked,
        InvalidRequest, InvalidCatalogue, MissingProfile, StarterRequired, LegacyUnavailable,
        RecoveryRequired, Conflict
    }

    public sealed class GuestAbilityPurchaseResult
    {
        public GuestAbilityPurchaseStatus Status;
        public LocalGuestProfile Profile;
        public GuestWalletEvent WalletReceipt;
        public GuestAbilityUnlockReceipt UnlockReceipt;
    }
}
