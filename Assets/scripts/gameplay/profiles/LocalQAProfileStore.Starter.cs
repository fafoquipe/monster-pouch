using System;
using MonsterPouch.Gameplay.Match;

namespace MonsterPouch.Gameplay.Profiles
{
    public sealed partial class LocalQAProfileStore
    {
        public Result ChooseStarter(EconomyDevelopmentConfig config, MatchConfig catalogue, string monsterId)
        {
            if (!Allowed(config)) return Result.Disabled;
            if (!StarterRoster.IsChoice(monsterId)) return Result.InvalidStarter;
            if (!StarterRoster.TryBuild(monsterId, catalogue, out var deck)) return Result.InvalidCatalogue;
            using (Lock())
            {
                var profile = LoadExisting();
                if (profile == null) throw new InvalidOperationException("Open the QA profile before choosing a starter.");
                if (NeedsRecovery) return Result.RecoveryRequired;
                if (profile.Starter != null)
                    return profile.Starter.Deck.Monster == monsterId ? Result.AlreadyApplied : Result.Conflict;
                if (!profile.OwnedCharacterIds.Contains(monsterId)) profile.OwnedCharacterIds.Add(monsterId);
                foreach (var id in deck.Whelps)
                    if (!profile.OwnedCharacterIds.Contains(id)) profile.OwnedCharacterIds.Add(id);
                profile.StarterSelections.Add(new StarterSelection {
                    ReceiptId = StarterRoster.CurrentReceiptId, TimestampUtc = DateTime.UtcNow.ToString("O"), Deck = deck
                });
                profile.Revision = checked(profile.Revision + 1);
                Save(profile);
                return Result.Applied;
            }
        }
    }
}
