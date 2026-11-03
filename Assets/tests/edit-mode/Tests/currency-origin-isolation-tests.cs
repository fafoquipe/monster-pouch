using System;
using System.IO;
using MonsterPouch.Gameplay.Profiles;
using NUnit.Framework;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class CurrencyOriginIsolationTests
    {
        [TestCase(ProfileEnvironment.InternalQA, CreditOrigin.FreePlayer)]
        [TestCase(ProfileEnvironment.InternalQA, CreditOrigin.Purchased)]
        [TestCase(ProfileEnvironment.Player, CreditOrigin.InternalQA)]
        public void ChangingEnvironmentCannotMakeForeignCreditsValid(ProfileEnvironment destination, CreditOrigin origin)
        {
            var profile = NewProfile(origin == CreditOrigin.InternalQA ?
                ProfileEnvironment.InternalQA : ProfileEnvironment.Player);
            AddCredit(profile, origin);
            Assert.DoesNotThrow(() => profile.Validate(), "The original ledger is internally consistent.");
            profile.Environment = destination;
            Assert.Throws<InvalidDataException>(() => profile.Validate());
        }

        [Test] public void PlayerProfilesAcceptEmptyAndDistinctFreeAndPurchasedBalances()
        {
            var profile = NewProfile(ProfileEnvironment.Player);
            Assert.DoesNotThrow(() => profile.Validate());
            AddCredit(profile, CreditOrigin.FreePlayer);
            AddCredit(profile, CreditOrigin.Purchased);
            Assert.DoesNotThrow(() => profile.Validate());
            Assert.AreEqual(1, profile.Balance(PermanentCurrency.Saturncoins, CreditOrigin.FreePlayer));
            Assert.AreEqual(1, profile.Balance(PermanentCurrency.Saturncoins, CreditOrigin.Purchased));
            Assert.AreEqual(0, profile.Balance(PermanentCurrency.Saturncoins, CreditOrigin.InternalQA));
        }

        [Test] public void SpendingQaBalanceToZeroStillCannotTurnItsHistoryIntoPlayerData()
        {
            var profile = NewProfile(ProfileEnvironment.InternalQA);
            AddCredit(profile, CreditOrigin.InternalQA);
            profile.Transactions.Add(new EconomicTransaction {
                Id = "spent-qa", Source = "validation-fixture", TimestampUtc = DateTime.UtcNow.ToString("O"),
                Currency = PermanentCurrency.Saturncoins, Origin = CreditOrigin.InternalQA,
                Delta = -1, Before = 1, After = 0
            });
            profile.Balances[0].Amount = 0;
            Assert.DoesNotThrow(() => profile.Validate());
            profile.Environment = ProfileEnvironment.Player;
            Assert.Throws<InvalidDataException>(() => profile.Validate());
        }

        static LocalEconomyProfile NewProfile(ProfileEnvironment environment) =>
            new LocalEconomyProfile { ProfileId = Guid.NewGuid().ToString("N"), Environment = environment };

        static void AddCredit(LocalEconomyProfile profile, CreditOrigin origin)
        {
            profile.Balances.Add(new CurrencyBalance {
                Currency = PermanentCurrency.Saturncoins, Origin = origin, Amount = 1
            });
            profile.Transactions.Add(new EconomicTransaction {
                Id = "credit:" + origin, Source = "validation-fixture", TimestampUtc = DateTime.UtcNow.ToString("O"),
                Currency = PermanentCurrency.Saturncoins, Origin = origin, Delta = 1, Before = 0, After = 1
            });
        }
    }
}
